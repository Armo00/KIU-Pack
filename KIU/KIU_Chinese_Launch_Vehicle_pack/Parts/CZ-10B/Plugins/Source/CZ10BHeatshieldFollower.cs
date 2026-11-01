using System;
using System.Collections.Generic;
using UnityEngine;

// Visual follower plus the user-authorized YF-100 travel cap (5 degrees,
// or the verified 3-degree fallback selected by the bay configuration).
// Thrust, vessel controls and graphics settings are never set.
[DefaultExecutionOrder(10000)]
public sealed class CZ10BHeatshieldFollower : PartModule
{
    [KSPField] public string nodePrefix = "engine";
    [KSPField] public string bonePrefix = "YF100_ShieldGimbal_";
    [KSPField] public string engineGimbalName = "gimbal";
    [KSPField] public string movableSlots = "0,1,4";
    [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#KCLV_CZ-10B_Shield_TVCLimit", guiFormat = "F0", guiUnits = "°")] public float maxEngineAngle = 5f;
    private readonly List<Slot> slots = new List<Slot>();
    private Vessel subscribedVessel;
    private sealed class Slot
    {
        public string nodeName;
        public Transform bone, source;
        public Part engine;
        public bool movable;
        public Vector3 restPosition;
        public Quaternion restRotation;
        public readonly List<Range> ranges = new List<Range>();
    }
    private sealed class Range
    {
        public ModuleGimbal module;
        public float total, xp, xn, yp, yn;
    }
    private void Restore(Slot slot)
    {
        foreach (Range r in slot.ranges)
        {
            if (r.module == null) continue;
            r.module.gimbalRange = r.total; r.module.gimbalRangeXP = r.xp;
            r.module.gimbalRangeXN = r.xn; r.module.gimbalRangeYP = r.yp;
            r.module.gimbalRangeYN = r.yn;
        }
        slot.ranges.Clear();
    }
    private void Cap(Slot slot, FlightCtrlState state = null)
    {
        if (slot.engine == null || slot.engine.partInfo == null || slot.engine.partInfo.name.Replace('_', '.') != "KCLV.YF100") return;
        if (slot.ranges.Count == 0)
            foreach (ModuleGimbal m in slot.engine.FindModulesImplementing<ModuleGimbal>())
                slot.ranges.Add(new Range { module = m, total = m.gimbalRange,
                    xp = m.gimbalRangeXP, xn = m.gimbalRangeXN,
                    yp = m.gimbalRangeYP, yn = m.gimbalRangeYN });
        Range primary = slot.ranges.Find(r => r.module != null && r.module.gimbalTransformName == engineGimbalName);
        if (primary == null || primary.total <= 0) return;
        float factor = slot.movable ? Mathf.Min(1f, Mathf.Clamp(maxEngineAngle, 0f, 5f) / primary.total) : 0f;
        float cone = 1f;
        ModuleGimbal g = primary.module;
        Vessel v = slot.engine.vessel;
        if (HighLogic.LoadedSceneIsFlight && v != null && v.ReferenceTransform != null &&
            g.gimbalTransforms != null && g.initRots != null && !g.gimbalLock)
        {
            FlightCtrlState input = state ?? v.ctrlState;
            Vector3 com = v.ReferenceTransform.InverseTransformPoint(v.CurrentCoM);
            for (int i = 0; i < g.gimbalTransforms.Count && i < g.initRots.Count; i++)
            {
                Transform t = g.gimbalTransforms[i];
                Vector3 pos = v.ReferenceTransform.InverseTransformPoint(t.position);
                float sign = com.y < pos.y ? -1f : 1f;
                Vector3 control = new Vector3(g.enablePitch ? input.pitch * sign : 0f,
                    g.enableRoll ? input.roll : 0f, g.enableYaw ? input.yaw * sign : 0f);
                if (g.enableRoll)
                {
                    if (pos.x > g.minRollOffset) control.x += control.y;
                    else if (pos.x < -g.minRollOffset) control.x -= control.y;
                    if (pos.z > g.minRollOffset) control.z += control.y;
                    else if (pos.z < -g.minRollOffset) control.z -= control.y;
                }
                Quaternion restWorld = (t.parent == null ? Quaternion.identity : t.parent.rotation) * g.initRots[i];
                Vector3 local = Quaternion.Inverse(restWorld) * v.ReferenceTransform.TransformDirection(control);
                float x = Mathf.Clamp(local.x, -1f, 1f) * (local.x > 0 ? primary.xp : primary.xn) * factor * g.gimbalLimiter * .01f;
                float y = Mathf.Clamp(local.y, -1f, 1f) * (local.y > 0 ? primary.yp : primary.yn) * factor * g.gimbalLimiter * .01f;
                Func<float, float> angle = delegate(float scale) {
                    Quaternion q = Quaternion.AngleAxis(x * scale, g.xMult * Vector3.right) *
                        Quaternion.AngleAxis(y * scale, g.yMult * (g.flipYZ ? Vector3.forward : Vector3.up));
                    return Quaternion.Angle(Quaternion.identity, q);
                };
                if (angle(cone) > maxEngineAngle)
                {
                    float low = 0f, high = cone;
                    for (int step = 0; step < 14; step++) { float mid = (low + high) * .5f; if (angle(mid) > maxEngineAngle) high = mid; else low = mid; }
                    cone = low;
                }
            }
        }
        foreach (Range r in slot.ranges)
        {
            if (r.module == null) continue;
            r.module.gimbalRange = r.total * factor;
            r.module.gimbalRangeXP = r.xp * factor * cone; r.module.gimbalRangeXN = r.xn * factor * cone;
            r.module.gimbalRangeYP = r.yp * factor * cone; r.module.gimbalRangeYN = r.yn * factor * cone;
        }
    }
    public override void OnStart(StartState state)
    {
        base.OnStart(state);
        slots.Clear();
        for (int i = 0; i < 7; i++)
        {
            Transform bone = part.FindModelTransform(bonePrefix + i);
            if (bone == null) continue;
            slots.Add(new Slot { nodeName = nodePrefix + i, bone = bone,
                movable = Array.Exists(movableSlots.Split(','), value => value.Trim() == i.ToString()),
                restPosition = bone.localPosition, restRotation = bone.localRotation });
        }
    }
    public void LateUpdate()
    {
        if (part == null || (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)) return;
        if (subscribedVessel != part.vessel)
        {
            if (subscribedVessel != null) {
                subscribedVessel.OnPostAutopilotUpdate -= AttachAfterControllers;
                subscribedVessel.OnFlyByWire -= LimitBeforeActuation;
            }
            subscribedVessel = part.vessel;
            if (subscribedVessel != null) subscribedVessel.OnPostAutopilotUpdate += AttachAfterControllers;
        }
        bool visible = slots.Exists(slot => slot.bone != null && slot.bone.gameObject.activeInHierarchy);
        Fields["maxEngineAngle"].guiActive = visible;
        Fields["maxEngineAngle"].guiActiveEditor = visible;
        foreach (Slot slot in slots)
        {
            if (slot.bone == null) { Restore(slot); slot.engine = null; slot.source = null; continue; }
            AttachNode attach = part.FindAttachNode(slot.nodeName);
            Part engine = attach == null ? null : attach.attachedPart;
            if (engine == part.parent || !slot.bone.gameObject.activeInHierarchy) engine = null;
            if (engine != slot.engine || (engine != null && slot.source == null))
            {
                Restore(slot);
                slot.engine = engine;
                slot.source = engine == null ? null : engine.FindModelTransform(engineGimbalName);
            }
            Cap(slot);
            if (slot.source != null && slot.bone.gameObject.activeInHierarchy)
            {
                slot.bone.position = slot.source.position;
                slot.bone.rotation = slot.source.rotation;
            }
            else
            {
                slot.bone.localPosition = slot.restPosition;
                slot.bone.localRotation = slot.restRotation;
            }
        }
    }
    // KSP calls OnPostAutopilotUpdate BEFORE OnFlyByWire. Append the limiter
    // after the existing input producers at that boundary, so Cap sees the
    // same final input that ModuleGimbal will consume, including autopilots.
    // Never change FlightCtrlState or subscribe/unsubscribe other handlers.
    private void AttachAfterControllers(FlightCtrlState state) {
        if (subscribedVessel == null) return;
        subscribedVessel.OnFlyByWire -= LimitBeforeActuation;
        subscribedVessel.OnFlyByWire += LimitBeforeActuation;
    }
    private void LimitBeforeActuation(FlightCtrlState state) { foreach (Slot slot in slots) Cap(slot, state); }
    public void OnDestroy()
    {
        if (subscribedVessel != null) {
            subscribedVessel.OnPostAutopilotUpdate -= AttachAfterControllers;
            subscribedVessel.OnFlyByWire -= LimitBeforeActuation;
        }
        foreach (Slot slot in slots) Restore(slot);
    }
}
