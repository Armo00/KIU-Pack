using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

// The stock counterpart of the RF adapter. B9 owns geometry/mass/price and
// TweakScale owns the model. This module alone applies stock engine performance
// after both have initialized, so original TS and Rescaled use the same order.
[DefaultExecutionOrder(10020)]
public sealed class KIUStockEngineModeCompatibility : PartModule
{
    [KSPField] public string engineID = "YF219";
    [KSPField] public string seaLevelTransform = "YF219_Nozzle_SeaLevel";
    [KSPField] public string vacuumTransform = "YF219_Nozzle_Vacuum";
    [KSPField] public float thrustScaleExponent = 2.5f;
    [KSPField] public float seaLevelMaxThrust = 747.8382434f;
    [KSPField] public float seaLevelMinThrust = 189.4536099f;
    [KSPField] public float seaLevelVacuumIsp = 329;
    [KSPField] public float seaLevelSeaIsp = 302;
    [KSPField] public float vacuumMaxThrust = 794.6f;
    [KSPField] public float vacuumMinThrust = 201.3f;
    [KSPField] public float vacuumVacuumIsp = 355;
    [KSPField] public float vacuumSeaIsp = 250;
    [KSPField] public string selectedConfiguration = "";
    [KSPField] public float modelScaleRatio = 1;
    [KSPField] public float appliedMaxThrust;
    [KSPField] public bool synchronized;
    private ModuleEnginesFX engine;
    private Transform seaLevel, vacuum;
    private bool initialized, applying;

    private static bool Selected(Transform transform)
    {
        return transform != null && transform.gameObject.activeSelf
            && transform.GetComponentsInChildren<MeshRenderer>(true).Length > 0;
    }

    private double ModelRatio()
    {
        Transform model = part.FindModelTransform("model");
        Part prefab = part.partInfo == null ? null : part.partInfo.partPrefab;
        Transform reference = prefab == null ? null : prefab.FindModelTransform("model");
        if (model == null || reference == null) return 1;
        double current = part.transform.InverseTransformVector(model.TransformVector(Vector3.up)).magnitude;
        double baseline = prefab.transform.InverseTransformVector(reference.TransformVector(Vector3.up)).magnitude;
        return baseline > 0.000001 ? current / baseline : 1;
    }

    public override void OnStart(StartState state)
    {
        base.OnStart(state);
        if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;
        // Never operate on RF or another third-party engine solver.
        foreach (PartModule module in part.Modules)
        {
            if (module.moduleName == "ModuleEngineConfigs") return;
            if (module.GetType() == typeof(ModuleEnginesFX)
                && ((ModuleEnginesFX)module).engineID == engineID) engine = (ModuleEnginesFX)module;
        }
        if (engine == null) return;
        seaLevel = part.FindModelTransform(seaLevelTransform);
        vacuum = part.FindModelTransform(vacuumTransform);
        StartCoroutine(AfterModulesStarted());
    }

    private IEnumerator AfterModulesStarted()
    {
        yield return null;
        yield return null;
        initialized = true;
        // B9 and the original TS initialize in different lifecycle phases.
        for (int frame = 0; frame < 3; frame++) { Synchronize(); yield return null; }
    }

    private void LateUpdate()
    {
        if (initialized && HighLogic.LoadedSceneIsEditor) Synchronize();
    }

    private void Synchronize()
    {
        if (applying || engine == null) return;
        bool sea = Selected(seaLevel), vac = Selected(vacuum);
        if (sea == vac) return;
        double ratio = ModelRatio();
        if (!KIUEngineModeRules.ValidScale(ratio)) return;
        double multiplier = Math.Pow(ratio, thrustScaleExponent);
        float maximum = (float)((vac ? vacuumMaxThrust : seaLevelMaxThrust) * multiplier);
        float minimum = (float)((vac ? vacuumMinThrust : seaLevelMinThrust) * multiplier);
        float vacIsp = vac ? vacuumVacuumIsp : seaLevelVacuumIsp;
        float seaIsp = vac ? vacuumSeaIsp : seaLevelSeaIsp;
        if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0) return;
        string selection = vac ? "YF-219EV" : "YF-219E";
        bool changed = selection != selectedConfiguration || Math.Abs(ratio-modelScaleRatio) > .00001;
        bool needsLoad = changed || Math.Abs(engine.maxThrust-maximum) > .001
            || Math.Abs(engine.minThrust-minimum) > .001
            || Math.Abs(engine.atmosphereCurve.Evaluate(0)-vacIsp) > .001
            || Math.Abs(engine.atmosphereCurve.Evaluate(1)-seaIsp) > .001;
        applying = true;
        try
        {
            if (needsLoad)
            {
                FloatCurve curve = new FloatCurve();
                curve.Add(0, vacIsp); curve.Add(1, seaIsp); curve.Add(12, .001f);
                engine.atmosphereCurve = curve;
                ConfigNode data = new ConfigNode("MODULE");
                data.AddValue("name", "ModuleEnginesFX");
                data.AddValue("maxThrust", maximum.ToString("R", CultureInfo.InvariantCulture));
                data.AddValue("minThrust", minimum.ToString("R", CultureInfo.InvariantCulture));
                // Native ModuleEngines.OnLoad recomputes max/min fuel flow and
                // throttleMin from thrust, curve and its own gravity constant.
                // No ignition state, propellants or effect definitions replaced.
                engine.Load(data);
            }
            selectedConfiguration = selection;
            modelScaleRatio = (float)ratio;
            appliedMaxThrust = engine.maxThrust;
            synchronized = Math.Abs(engine.maxThrust-maximum) < .001;
        }
        finally { applying = false; }
        if (changed && HighLogic.LoadedSceneIsEditor && EditorLogic.fetch != null)
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }
}
