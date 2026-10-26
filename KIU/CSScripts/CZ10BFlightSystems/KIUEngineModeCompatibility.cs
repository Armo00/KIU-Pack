using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using UnityEngine;

// A bounded adapter for this Part's existing B9 geometry selector, RF config
// manager and optional TweakScale. No direct RF assembly dependency is needed,
// so the same assembly can still supply the grid-fin interlock without RF.
[DefaultExecutionOrder(10020)]
public sealed class KIUEngineModeCompatibility : PartModule, IPartCostModifier
{
    [KSPField] public string engineID = "YF219";
    [KSPField] public string seaLevelConfiguration = "YF-219E";
    [KSPField] public string vacuumConfiguration = "YF-219EV";
    [KSPField] public string seaLevelTransform = "YF219_Nozzle_SeaLevel";
    [KSPField] public string vacuumTransform = "YF219_Nozzle_Vacuum";
    [KSPField] public string b9ModuleID = "EngineMode";
    [KSPField] public float seaLevelCost = 2500;
    [KSPField] public float vacuumCost = 2600;
    [KSPField] public float costScaleExponent = 3;
    [KSPField(isPersistant = true)] public int savedIgnitions = KIUEngineModeRules.NoSavedIgnitions;

    // Read-only diagnostics. These are deliberately not extra PAW controls.
    [KSPField] public float modelScaleRatio = 1;
    [KSPField] public float rfScaleSquared = 1;
    [KSPField] public string selectedConfiguration = "";
    [KSPField] public bool synchronized;

    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private PartModule configs;
    private ModuleEngines engine;
    private MethodInfo setConfiguration;
    private MethodInfo onPartScaleChanged;
    private Transform seaLevel, vacuum;
    private bool initialized, applying, warned, computingCost;

    private static FieldInfo Field(object obj, string name)
    {
        if (obj == null) return null;
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        return null;
    }

    private static object Read(object obj, string name)
    {
        FieldInfo field = Field(obj, name);
        return field == null ? null : field.GetValue(obj);
    }

    private bool Bind()
    {
        if (part == null || part.Modules == null) return false;
        foreach (PartModule module in part.Modules)
        {
            if (module.moduleName == "ModuleEngineConfigs"
                && Convert.ToString(Read(module, "engineID"), CultureInfo.InvariantCulture) == engineID)
                configs = module;
            ModuleEngines candidate = module as ModuleEngines;
            if (candidate != null && candidate.engineID == engineID) engine = candidate;
        }
        if (configs == null || engine == null) return false;
        setConfiguration = KIUEngineModeRules.ResolveMethod(configs, "SetConfiguration", typeof(string), typeof(bool));
        // RF 15.15 declares this KSP event private on ModuleEngineConfigsBase.
        // GetMethod on the derived class alone cannot find that callback.
        onPartScaleChanged = KIUEngineModeRules.ResolveMethod(configs, "OnPartScaleChanged", typeof(BaseEventDetails));
        seaLevel = part.FindModelTransform(seaLevelTransform);
        vacuum = part.FindModelTransform(vacuumTransform);
        return setConfiguration != null && onPartScaleChanged != null && Field(configs, "scale") != null;
    }

    private static bool HasSelectedMesh(Transform transform)
    {
        return transform != null && transform.gameObject.activeSelf
            && transform.GetComponentsInChildren<MeshRenderer>(true).Length > 0;
    }

    private string GeometryConfiguration()
    {
        bool sea = HasSelectedMesh(seaLevel), vac = HasSelectedMesh(vacuum);
        if (sea == vac) return null; // B9 has not finished, or its geometry is ambiguous.
        return sea ? seaLevelConfiguration : vacuumConfiguration;
    }

    private double ModelRatio()
    {
        if (part == null) return 1;
        Transform model = part.FindModelTransform("model");
        Part prefab = part.partInfo == null ? null : part.partInfo.partPrefab;
        Transform reference = prefab == null ? null : prefab.FindModelTransform("model");
        if (model == null || reference == null) return 1;
        double current = part.transform.InverseTransformVector(model.TransformVector(Vector3.up)).magnitude;
        double baseline = prefab.transform.InverseTransformVector(reference.TransformVector(Vector3.up)).magnitude;
        return baseline > 0.000001 ? current / baseline : 1;
    }

    private int Ignitions()
    {
        object value = Read(engine, "ignitions");
        return value == null ? KIUEngineModeRules.NoSavedIgnitions : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private void RestoreIgnitions(int saved)
    {
        FieldInfo field = Field(engine, "ignitions");
        if (field == null || saved == KIUEngineModeRules.NoSavedIgnitions) return;
        field.SetValue(engine, KIUEngineModeRules.PreserveIgnitions(saved, Ignitions()));
    }

    private int ProtoIgnitions()
    {
        if (part == null || part.protoPartSnapshot == null) return KIUEngineModeRules.NoSavedIgnitions;
        foreach (ProtoPartModuleSnapshot module in part.protoPartSnapshot.modules)
        {
            if (module == null || module.moduleName != "ModuleEnginesRF" || module.moduleValues == null) continue;
            int value;
            if (int.TryParse(module.moduleValues.GetValue("ignitions"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value)) return value;
        }
        return KIUEngineModeRules.NoSavedIgnitions;
    }

    public override void OnStart(StartState state)
    {
        base.OnStart(state);
        if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;
        StartCoroutine(AfterModulesStarted());
    }

    private IEnumerator AfterModulesStarted()
    {
        int preserve = HighLogic.LoadedSceneIsFlight
            ? (savedIgnitions == KIUEngineModeRules.NoSavedIgnitions ? ProtoIgnitions() : savedIgnitions)
            : KIUEngineModeRules.NoSavedIgnitions;
        yield return null;
        yield return null;
        if (!Bind()) { Warn("Required RF interface or owning engine is unavailable."); yield break; }
        Synchronize(true);
        initialized = true;
        if (HighLogic.LoadedSceneIsFlight)
        {
            // Restore only during initialization, not during ordinary flight or
            // a legitimate maintenance/refuelling operation later in the mission.
            for (int frame = 0; frame < 3; frame++)
            {
                RestoreIgnitions(preserve);
                yield return null;
            }
        }
        else savedIgnitions = KIUEngineModeRules.NoSavedIgnitions;
    }

    private void LateUpdate()
    {
        if (initialized && HighLogic.LoadedSceneIsEditor) Synchronize(false);
    }

    private void Synchronize(bool initializing)
    {
        if (applying || configs == null || engine == null) return;
        if (!HighLogic.LoadedSceneIsEditor && !initializing) return;
        string desired = GeometryConfiguration();
        if (desired == null) return;
        double ratio = ModelRatio();
        if (!KIUEngineModeRules.ValidScale(ratio)) { Warn("Invalid model scale; no engine data changed."); return; }
        int preserve = HighLogic.LoadedSceneIsFlight ? Ignitions() : KIUEngineModeRules.NoSavedIgnitions;
        bool changed = false;
        applying = true;
        try
        {
            string current = Convert.ToString(Read(configs, "configuration"), CultureInfo.InvariantCulture);
            if (current != desired)
            {
                // B9 no longer reloads RF blindly on every flight load. Only an
                // actual mode mismatch calls RF's own configuration API here.
                setConfiguration.Invoke(configs, new object[] { desired, false });
                changed = true;
            }
            double rfScale = Convert.ToDouble(Read(configs, "scale"), CultureInfo.InvariantCulture);
            if (KIUEngineModeRules.NeedsScaleSync(ratio, rfScale))
            {
                BaseEventDetails details = new BaseEventDetails(BaseEventDetails.Sender.USER);
                details.Set<float>("factorAbsolute", (float)ratio);
                details.Set<float>("factorRelative", rfScale > 0 ? (float)(ratio / Math.Sqrt(rfScale)) : 1f);
                onPartScaleChanged.Invoke(configs, new object[] { details });
                changed = true;
            }
            if (HighLogic.LoadedSceneIsFlight) RestoreIgnitions(preserve);
            modelScaleRatio = (float)ratio;
            rfScaleSquared = Convert.ToSingle(Read(configs, "scale"), CultureInfo.InvariantCulture);
            selectedConfiguration = Convert.ToString(Read(configs, "configuration"), CultureInfo.InvariantCulture);
            synchronized = selectedConfiguration == desired && !KIUEngineModeRules.NeedsScaleSync(ratio, rfScaleSquared);
        }
        catch (Exception exception) { synchronized = false; Warn(exception.ToString()); }
        finally { applying = false; }
        if (changed && HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }

    public override void OnSave(ConfigNode node)
    {
        savedIgnitions = HighLogic.LoadedSceneIsFlight ? Ignitions() : KIUEngineModeRules.NoSavedIgnitions;
        base.OnSave(node);
        node.SetValue("savedIgnitions", savedIgnitions.ToString(CultureInfo.InvariantCulture), true);
    }

    public float GetModuleCost(float defaultCost, ModifierStagingSituation situation)
    {
        if (part == null || part.Modules == null || computingCost) return 0;
        computingCost = true;
        try
        {
            if (vacuum == null && seaLevel == null)
            {
                seaLevel = part.FindModelTransform(seaLevelTransform);
                vacuum = part.FindModelTransform(vacuumTransform);
            }
            string selection = GeometryConfiguration();
            if (selection == null) selection = selectedConfiguration;
            float cost = selection == seaLevelConfiguration ? seaLevelCost : vacuumCost;
            float managedCost = 0;
            foreach (PartModule module in part.Modules)
            {
                if (module == this) continue;
                bool owned = module.moduleName == "ModuleEngineConfigs" || module.moduleName == "TweakScale"
                    || (module.moduleName == "ModuleB9PartSwitch"
                        && Convert.ToString(Read(module, "moduleID"), CultureInfo.InvariantCulture) == b9ModuleID);
                IPartCostModifier modifier = module as IPartCostModifier;
                if (owned && modifier != null) managedCost += modifier.GetModuleCost(defaultCost, situation);
            }
            return KIUEngineModeRules.PriceCorrection(cost, ModelRatio(), costScaleExponent, defaultCost, managedCost);
        }
        finally { computingCost = false; }
    }

    public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }

    private void Warn(string message)
    {
        if (warned) return;
        warned = true;
        Debug.LogError("[KIUEngineModeCompatibility] " + message);
    }
}
