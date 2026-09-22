using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.Localization;

// Keep the native ground joint, staging icon and detached-pad behaviour.
// Separate clip channels allow the bottom arm to operate independently.
public class CZ10BLaunchErector : LaunchClamp
{
    [KSPField(isPersistant = true)] public int heightSegments = 2;
    [KSPField] public int maxHeightSegments = 90;
    [KSPField] public float heightSegmentLength = .5f;
    [KSPField] public int modelHeightSegments = 2;
    [KSPField] public float baseTopHeight = 47.82f;
    [KSPField(guiActiveEditor = true, guiName = "#CZ10B_Erector_Extensions", guiFormat = "F0")]
    [UI_FloatEdit(minValue = 0, maxValue = 90, incrementLarge = 91, incrementSmall = 1, incrementSlide = 1)]
    public float heightSelection = 2;
    [KSPField(guiActiveEditor = true, guiActive = true, guiName = "#CZ10B_Erector_Height")]
    public string heightSpec;
    private Transform heightUpper, heightTemplate;
    private Transform[] heightTemplates;
    private Vector3 upperOrigin;
    private int builtSegments = -1;
    private Dictionary<int, List<GameObject>> heightPools = new Dictionary<int, List<GameObject>>();
    [KSPField] public float bottomDuration = 2f;
    [KSPField] public float topDuration = 2f;
    [KSPField] public float prepareDuration = 2f;
    [KSPField] public float releaseDuration = 4f;
    [KSPField(isPersistant = true)] public float bottomProgress;
    [KSPField(isPersistant = true)] public float topProgress;
    [KSPField(isPersistant = true)] public float prepareProgress;
    [KSPField(isPersistant = true)] public float releaseProgress;
    [KSPField(isPersistant = true)] public bool openingBottom;
    [KSPField(isPersistant = true)] public bool preparing;
    [KSPField(isPersistant = true)] public bool released;
    [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#CZ10B_Erector_Status")]
    public string sequenceStatus = "#CZ10B_Erector_Locked";
    private Animation modelAnimation;
    private bool initialized, wasAttached;
    public bool Ready { get { return bottomProgress >= 1f && topProgress >= 1f && prepareProgress >= 1f && !released; } }

    // ShipConstruction sends this message by name. The stock implementation
    // discards clamp geometry for an extensible tower. Our rigid platform must
    // instead keep its actual collider bottom in the launch-height query.
    public new void OnPutToGround(PartHeightQuery query) { }

    public override void OnStart(StartState state)
    {
        base.OnStart(state);
        InitializeAnimations();
        wasAttached = part.parent != null;
        if (HighLogic.LoadedSceneIsFlight && wasAttached && released)
        {
            // A released editor preview is not an actual detached flight state.
            released = openingBottom = preparing = false;
            bottomProgress = topProgress = prepareProgress = releaseProgress = 0f;
        }
        // Old saves may contain a detached native clamp but no persisted controller state.
        if (HighLogic.LoadedSceneIsFlight && !wasAttached && !released)
        { released = true; bottomProgress = topProgress = prepareProgress = releaseProgress = 1f; }
        ApplyPose();
    }

    public bool InitializeAnimations()
    {
        Transform root = part.FindModelTransform("ErectorAnimation");
        modelAnimation = root == null ? null : root.GetComponent<Animation>();
        initialized = modelAnimation != null;
        if (initialized)
        {
            foreach (string name in new[] { "BottomArm", "TopArms", "BackPrepare", "BackRelease", "HoldClamps" })
                initialized &= modelAnimation[name] != null;
            modelAnimation.Stop();
        }
        Transform helper = part.FindModelTransform("NativeStretch");
        if (helper != null) foreach (Renderer r in helper.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        if (!initialized) Debug.LogError("[CZ10B] Launch erector animation clips are missing.");
        InitializeHeight();
        bool hasVariantSwitcher = false;
        foreach (PartModule module in part.Modules)
            if (module.moduleName == "ModuleB9PartSwitch") hasVariantSwitcher = true;
        if (!hasVariantSwitcher)
            foreach (string variant in new[] { "Fairing75", "LowerLegR7" })
            {
                Transform tr = part.FindModelTransform(variant);
                if (tr != null) tr.gameObject.SetActive(false);
            }
        Events["OpenBottom"].guiName = Localizer.Format("#CZ10B_Erector_OpenBottom");
        Events["PrepareLaunch"].guiName = Localizer.Format("#CZ10B_Erector_Prepare");
        Events["LaunchRelease"].guiName = Localizer.Format("#CZ10B_Erector_Release");
        Events["ResetPreview"].guiName = Localizer.Format("#CZ10B_Erector_Reset");
        Actions["OpenBottomAction"].guiName = Localizer.Format("#CZ10B_Erector_OpenBottom");
        Actions["PrepareAction"].guiName = Localizer.Format("#CZ10B_Erector_PrepareAction");
        Actions["LaunchAction"].guiName = Localizer.Format("#CZ10B_Erector_ReleaseAction");
        Fields["heightSelection"].guiName = Localizer.Format("#CZ10B_Erector_Extensions");
        Fields["heightSpec"].guiName = Localizer.Format("#CZ10B_Erector_Height");
        Fields["sequenceStatus"].guiName = Localizer.Format("#CZ10B_Erector_Status");
        UpdateMenu();
        return initialized;
    }

    private void InitializeHeight()
    {
        if (heightUpper == null)
        {
            heightUpper = part.FindModelTransform("HeightAdjustableUpper");
            heightTemplate = part.FindModelTransform("HeightExtensionTemplate");
            if (heightUpper == null || heightTemplate == null) return;
            Transform origin = part.FindModelTransform("HeightUpperOrigin");
            upperOrigin = origin != null ? origin.localPosition : heightUpper.localPosition;
            heightTemplates = new Transform[7];
            heightPools = new Dictionary<int, List<GameObject>>();
            for (int units = 1; units <= 6; units++)
            {
                heightTemplates[units] = units == 1 ? heightTemplate : part.FindModelTransform(units == 6 ? "HeightBayTemplate" : "HeightRemainder" + units);
                heightPools[units] = new List<GameObject>();
                // Editor duplication includes inactive pooled bays. Adopt each
                // geometry type independently, keeping a fixed height origin.
                for (int i = 0; ; i++)
                {
                    Transform existing = heightTemplate.parent.Find("ErectorExtension_" + units + "_" + i);
                    if (existing == null) break;
                    heightPools[units].Add(existing.gameObject);
                }
            }
        }
        maxHeightSegments = Math.Max(0, maxHeightSegments);
        heightSegmentLength = Mathf.Max(.01f, heightSegmentLength);
        UI_FloatEdit ui = Fields["heightSelection"].uiControlEditor as UI_FloatEdit;
        if (ui != null)
        {
            ui.minValue = 0; ui.maxValue = maxHeightSegments;
            ui.incrementLarge = maxHeightSegments + 1; ui.incrementSmall = 1; ui.incrementSlide = 1;
            ui.affectSymCounterparts = UI_Scene.None; ui.onFieldChanged = OnHeightChanged;
        }
        SetHeightSegments(heightSegments);
    }
    public void OnHeightChanged(BaseField field, object previous)
    {
        SetHeightSegments(Mathf.RoundToInt(heightSelection));
        if (HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }
    public void SetHeightSegments(int count)
    {
        heightSegments = Mathf.Clamp(count, 0, maxHeightSegments); heightSelection = heightSegments;
        heightSpec = (baseTopHeight + heightSegments * heightSegmentLength).ToString("F2") + " m / " + heightSegments + " × " + heightSegmentLength.ToString("F1") + " m";
        if (heightTemplate == null || heightUpper == null) return;
        int fullBays = heightSegments / 6, remainder = heightSegments % 6;
        for (int units = 1; units <= 6; units++)
        {
            Transform template = heightTemplates[units];
            if (template == null) { Debug.LogError("[CZ10B] Missing extension template " + units); continue; }
            List<GameObject> pool = heightPools[units];
            int needed = units == 6 ? fullBays : units == remainder ? 1 : 0;
            for (int i = pool.Count; i < needed; i++)
            {
                GameObject copy = UnityEngine.Object.Instantiate(template.gameObject);
                copy.name = "ErectorExtension_" + units + "_" + i;
                copy.transform.SetParent(template.parent, false); pool.Add(copy);
            }
            template.gameObject.SetActive(false);
            for (int i = 0; i < pool.Count; i++)
            {
                Transform tr = pool[i].transform;
                int offset = units == 6 ? i * 6 : fullBays * 6;
                tr.localPosition = template.localPosition + Vector3.up * (offset * heightSegmentLength);
                tr.localRotation = template.localRotation; tr.localScale = Vector3.one;
                pool[i].SetActive(i < needed);
            }
        }
        heightUpper.localPosition = upperOrigin + Vector3.up * ((heightSegments - modelHeightSegments) * heightSegmentLength);
        builtSegments = heightSegments;
    }

    [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#CZ10B_Erector_OpenBottom", active = true)]
    public void OpenBottom() { if (!released) openingBottom = true; }
    [KSPAction("#CZ10B_Erector_OpenBottom")]
    public void OpenBottomAction(KSPActionParam p) { OpenBottom(); }

    [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#CZ10B_Erector_Prepare", active = true)]
    public void PrepareLaunch()
    {
        if (released) return;
        // Bottom restraint must clear before the back can move. If it is already
        // open, preparation immediately starts with the top restraint as requested.
        openingBottom = true; preparing = true;
    }
    [KSPAction("#CZ10B_Erector_PrepareAction")]
    public void PrepareAction(KSPActionParam p) { PrepareLaunch(); }

    [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "#CZ10B_Erector_Release", active = true)]
    public void LaunchRelease()
    {
        if (released) return;
        if (!Ready || !initialized)
        {
            if (HighLogic.LoadedSceneIsFlight) ScreenMessages.PostScreenMessage(Localizer.Format("#CZ10B_Erector_NotReady"), 4f, ScreenMessageStyle.UPPER_CENTER);
            return;
        }
        if (HighLogic.LoadedSceneIsFlight)
        {
            if (part.parent == null) return;
            // Disable only moving proxies before decoupling; keep ground/base collision.
            DisableMovingColliders();
            base.Release();
            if (part.parent != null) return;
        }
        released = true; preparing = false; releaseProgress = 0f;
        ApplyPose(); UpdateMenu();
    }
    [KSPAction("#CZ10B_Erector_ReleaseAction")]
    public void LaunchAction(KSPActionParam p) { LaunchRelease(); }
    public override void OnActive() { if (stagingEnabled) LaunchRelease(); }

    [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "#CZ10B_Erector_Reset", active = true)]
    public void ResetPreview()
    {
        if (HighLogic.LoadedSceneIsFlight) return;
        bottomProgress = topProgress = prepareProgress = releaseProgress = 0;
        openingBottom = preparing = released = false;
        foreach (Collider c in part.GetComponentsInChildren<Collider>(true))
            if (c.name.StartsWith("MovingCollision_")) c.enabled = true;
        ApplyPose(); UpdateMenu();
    }

    // Public deterministic advancement is also exercised in the native KSP QA.
    public void AdvanceSequence(float dt)
    {
        if (!initialized) return;
        if (released)
        {
            releaseProgress = Move(releaseProgress, dt, releaseDuration);
            // External native Release() calls still get an immediate clearing motion.
            bottomProgress = Move(bottomProgress, dt, releaseDuration);
            topProgress = Move(topProgress, dt, releaseDuration);
        }
        else
        {
            if (openingBottom) bottomProgress = Move(bottomProgress, dt, bottomDuration);
            if (preparing)
            {
                // Do not advance the back in the same tick that finishes the arms.
                bool armsWereClear = topProgress >= 1f && bottomProgress >= 1f;
                topProgress = Move(topProgress, dt, topDuration);
                if (armsWereClear) prepareProgress = Move(prepareProgress, dt, prepareDuration);
            }
        }
        ApplyPose(); UpdateMenu();
    }
    private static float Move(float value, float dt, float duration)
    { return Mathf.MoveTowards(value, 1f, Mathf.Max(0f, dt) / Mathf.Max(.05f, duration)); }

    private void LateUpdate()
    {
        if (!initialized || (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)) return;
        UI_FloatEdit ui = Fields["heightSelection"].uiControlEditor as UI_FloatEdit;
        UIPartActionFloatEdit item = ui == null ? null : ui.partActionItem as UIPartActionFloatEdit;
        if (item != null) { if (item.incLarge != null) item.incLarge.gameObject.SetActive(false); if (item.decLarge != null) item.decLarge.gameObject.SetActive(false); }
        if (HighLogic.LoadedSceneIsFlight && wasAttached && part.parent == null && !released)
        { released = true; preparing = false; DisableMovingColliders(); }
        if (HighLogic.LoadedSceneIsFlight && part.parent != null) wasAttached = true;
        if (HighLogic.LoadedSceneIsFlight && (vessel == null || vessel.packed)) { ApplyPose(); return; }
        AdvanceSequence(Time.deltaTime);
    }

    public void ApplyPose()
    {
        if (!initialized) return;
        if (builtSegments != heightSegments) SetHeightSegments(heightSegments);
        modelAnimation.Stop();
        Sample("BottomArm", bottomProgress);
        Sample("TopArms", topProgress);
        // BackPrepare and BackRelease deliberately share tracks, sampled exclusively.
        if (released) Sample("BackRelease", releaseProgress);
        else Sample("BackPrepare", prepareProgress);
        Sample("HoldClamps", released ? releaseProgress : 0f);
        if (released) DisableMovingColliders();
    }
    private void Sample(string name, float progress)
    {
        AnimationState s = modelAnimation[name];
        s.enabled = true; s.weight = 1f; s.speed = 0f; s.wrapMode = WrapMode.ClampForever;
        s.normalizedTime = Mathf.Clamp01(progress); modelAnimation.Sample(); s.enabled = false;
    }
    private void DisableMovingColliders()
    {
        foreach (Collider c in part.GetComponentsInChildren<Collider>(true))
            if (c.name.StartsWith("MovingCollision_")) c.enabled = false;
    }
    private void UpdateMenu()
    {
        // Hide stock UI entry points; stock release is used internally after readiness.
        if (Events.Contains("Release")) Events["Release"].active = false;
        if (Actions["ReleaseClamp"] != null) Actions["ReleaseClamp"].active = false;
        Events["OpenBottom"].active = !released && bottomProgress < 1f;
        Events["PrepareLaunch"].active = !released && !Ready;
        Events["LaunchRelease"].active = !released;
        sequenceStatus = Localizer.Format(released ? (releaseProgress >= 1 ? "#CZ10B_Erector_Released" : "#CZ10B_Erector_Releasing") : Ready ? "#CZ10B_Erector_Ready" : preparing ? (topProgress < 1 ? "#CZ10B_Erector_OpeningTop" : "#CZ10B_Erector_Preparing") : bottomProgress >= 1 ? "#CZ10B_Erector_BottomOpen" : openingBottom ? "#CZ10B_Erector_OpeningBottom" : "#CZ10B_Erector_Locked");
    }
}
