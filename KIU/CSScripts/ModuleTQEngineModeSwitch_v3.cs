using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KCLV_CustomPlugins
{
    // RF uses one engine whose configuration changes in place; stock uses four engine modules.
    // Only the RF backend deliberately avoids Activate() and Shutdown() during a mode change.
    public class ModuleTQEngineModeSwitch_v3 : PartModule, IEngineStatus
    {
        private enum EngineBackend
        {
            Unavailable,
            RealFuels,
            Stock
        }

        [KSPField]
        public string engineID = "TQ12A_V3";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Engine Mode")]
        public string modeDisplay = "9 Engines";

        [KSPField(isPersistant = true)]
        public int currentMode = 9;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Thrust Rating")]
        public string thrustDisplay = "101%";

        [KSPField(isPersistant = true)]
        public int currentThrustLevel = 101;

        private ModuleEnginesFX engine;
        private PartModule engineConfigs;
        private MethodInfo setConfiguration;
        private FieldInfo throttleResponseRate;
        private readonly Dictionary<int, ModuleEnginesFX> stockEngines = new Dictionary<int, ModuleEnginesFX>();
        private readonly List<WaterfallTarget> waterfallTargets = new List<WaterfallTarget>();
        private EngineBackend backend = EngineBackend.Unavailable;
        private bool initialized;
        private bool missingConfigReported;

        private sealed class WaterfallTarget
        {
            public int Mode;
            public PartModule Module;
            public MethodInfo SetControllerValue;
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "▶ 1 Engine (Center)")]
        public void Mode1Event() => SetMode(1);

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "▶ 3 Engines")]
        public void Mode3Event() => SetMode(3);

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "▶ 5 Engines")]
        public void Mode5Event() => SetMode(5);

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "▶ 9 Engines (All)")]
        public void Mode9Event() => SetMode(9);

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Thrust: 100%")]
        public void Thrust100Event() => SetThrustLevel(100);

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Thrust: 101%")]
        public void Thrust101Event() => SetThrustLevel(101);

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Thrust: 110%")]
        public void Thrust110Event() => SetThrustLevel(110);

        [KSPAction(guiName = "1 Engine Mode")]
        public void Mode1Action(KSPActionParam _) => SetMode(1);

        [KSPAction(guiName = "3 Engine Mode")]
        public void Mode3Action(KSPActionParam _) => SetMode(3);

        [KSPAction(guiName = "5 Engine Mode")]
        public void Mode5Action(KSPActionParam _) => SetMode(5);

        [KSPAction(guiName = "9 Engine Mode")]
        public void Mode9Action(KSPActionParam _) => SetMode(9);

        [KSPAction(guiName = "Thrust Rating: 100%")]
        public void Thrust100Action(KSPActionParam _) => SetThrustLevel(100);

        [KSPAction(guiName = "Thrust Rating: 101%")]
        public void Thrust101Action(KSPActionParam _) => SetThrustLevel(101);

        [KSPAction(guiName = "Thrust Rating: 110%")]
        public void Thrust110Action(KSPActionParam _) => SetThrustLevel(110);

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
        }

        // Unity Start runs after every PartModule has completed OnStart, including optional ModuleEngineConfigs.
        public void Start()
        {
            FindEngine();
            FindEngineConfigs();
            FindStockEngines();
            FindWaterfallTargets();

            if (engine != null && engineConfigs != null && setConfiguration != null)
            {
                backend = EngineBackend.RealFuels;
                initialized = true;
                ConfigureThrustControls(true);
                ApplyConfiguration(currentMode, currentThrustLevel);
            }
            else if (stockEngines.Count == 4)
            {
                backend = EngineBackend.Stock;
                initialized = true;
                ConfigureThrustControls(false);
                SetStockMode(currentMode, true);
            }
            else if (!missingConfigReported)
            {
                ConfigureThrustControls(false);
                Debug.LogError("[KCLV] TQ-12A V3 requires either its RF configuration module or all four stock engine modules.");
                missingConfigReported = true;
            }
        }

        private void FindEngine()
        {
            foreach (ModuleEnginesFX candidate in part.FindModulesImplementing<ModuleEnginesFX>())
            {
                if (candidate.engineID == engineID)
                {
                    engine = candidate;
                    throttleResponseRate = candidate.GetType().GetField(
                        "throttleResponseRate",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    return;
                }
            }
        }

        private void FindEngineConfigs()
        {
            foreach (PartModule candidate in part.Modules)
            {
                if (candidate.moduleName != "ModuleEngineConfigs") continue;

                FieldInfo configEngineID = candidate.GetType().GetField("engineID");
                if (configEngineID == null || (string)configEngineID.GetValue(candidate) != engineID) continue;

                MethodInfo method = candidate.GetType().GetMethod(
                    "SetConfiguration",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(bool) },
                    null);
                if (method == null) continue;

                engineConfigs = candidate;
                setConfiguration = method;
                return;
            }
        }

        private void FindStockEngines()
        {
            stockEngines.Clear();
            foreach (ModuleEnginesFX candidate in part.FindModulesImplementing<ModuleEnginesFX>())
            {
                int mode = ModeForStockEngine(candidate.engineID);
                if (mode == 0) continue;

                stockEngines[mode] = candidate;
                HideAutomaticEngineEvents(candidate);
                candidate.manuallyOverridden = true;
                candidate.isEnabled = false;
            }
        }

        private static int ModeForStockEngine(string candidateEngineID)
        {
            switch (candidateEngineID)
            {
                case "TQ12A_V3_Stock_Mode1": return 1;
                case "TQ12A_V3_Stock_Mode3": return 3;
                case "TQ12A_V3_Stock_Mode5": return 5;
                case "TQ12A_V3_Stock_Mode9": return 9;
                default: return 0;
            }
        }

        private static void HideAutomaticEngineEvents(ModuleEnginesFX candidate)
        {
            for (int index = 0; index < candidate.Events.Count; index++)
            {
                BaseEvent engineEvent = candidate.Events[index];
                if (engineEvent == null || (engineEvent.name != "Activate" && engineEvent.name != "Shutdown")) continue;

                engineEvent.guiActive = false;
                engineEvent.guiActiveEditor = false;
            }
        }

        private void ConfigureThrustControls(bool isRealFuels)
        {
            Fields[nameof(thrustDisplay)].guiActive = isRealFuels;
            Fields[nameof(thrustDisplay)].guiActiveEditor = isRealFuels;

            Events[nameof(Thrust100Event)].guiActive = isRealFuels;
            Events[nameof(Thrust100Event)].guiActiveEditor = isRealFuels;
            Events[nameof(Thrust101Event)].guiActive = isRealFuels;
            Events[nameof(Thrust101Event)].guiActiveEditor = isRealFuels;
            Events[nameof(Thrust110Event)].guiActive = isRealFuels;
            Events[nameof(Thrust110Event)].guiActiveEditor = isRealFuels;

            Actions[nameof(Thrust100Action)].active = isRealFuels;
            Actions[nameof(Thrust101Action)].active = isRealFuels;
            Actions[nameof(Thrust110Action)].active = isRealFuels;
        }

        private void FindWaterfallTargets()
        {
            waterfallTargets.Clear();
            foreach (PartModule candidate in part.Modules)
            {
                if (candidate.moduleName != "ModuleWaterfallFX") continue;

                FieldInfo moduleIDField = candidate.GetType().GetField("moduleID");
                string moduleID = moduleIDField?.GetValue(candidate) as string;
                int mode = ModeForWaterfallModule(moduleID);
                if (mode == 0) continue;

                MethodInfo method = candidate.GetType().GetMethod(
                    "SetControllerValue",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(float) },
                    null);
                if (method != null)
                    waterfallTargets.Add(new WaterfallTarget { Mode = mode, Module = candidate, SetControllerValue = method });
            }
        }

        private static int ModeForWaterfallModule(string moduleID)
        {
            switch (moduleID)
            {
                case "TQ12A_V3_Mode1": return 1;
                case "TQ12A_V3_Mode3": return 3;
                case "TQ12A_V3_Mode5": return 5;
                case "TQ12A_V3_Mode9": return 9;
                default: return 0;
            }
        }

        private static string ConfigurationFor(int rating, int mode)
        {
            if (rating != 100 && rating != 101 && rating != 110) return null;
            switch (mode)
            {
                case 1:
                case 3:
                case 5:
                case 9:
                    return $"TQ12A_V3_{rating}_mode{mode}";
                default: return null;
            }
        }

        private void SetMode(int mode)
        {
            if (!initialized) return;
            if (backend == EngineBackend.Stock)
            {
                SetStockMode(mode, false);
                return;
            }
            if (backend != EngineBackend.RealFuels) return;
            if (ConfigurationFor(currentThrustLevel, mode) == null) return;
            ApplyConfiguration(mode, currentThrustLevel);
        }

        private void SetThrustLevel(int rating)
        {
            if (backend != EngineBackend.RealFuels) return;
            if (ConfigurationFor(rating, currentMode) == null) return;
            ApplyConfiguration(currentMode, rating);
        }

        private void SetStockMode(int mode, bool setup)
        {
            if (!stockEngines.TryGetValue(mode, out ModuleEnginesFX targetEngine)) return;

            ModuleEnginesFX oldEngine = engine;
            float previousLimiter = oldEngine != null ? oldEngine.thrustPercentage : targetEngine.thrustPercentage;
            bool wasIgnited = oldEngine != null && oldEngine.EngineIgnited;

            targetEngine.thrustPercentage = previousLimiter;
            targetEngine.manuallyOverridden = false;
            targetEngine.isEnabled = true;

            if (oldEngine != null && oldEngine != targetEngine)
            {
                if (wasIgnited && !setup)
                {
                    targetEngine.Activate();
                    targetEngine.currentThrottle = oldEngine.currentThrottle;
                    oldEngine.Shutdown();
                }

                oldEngine.manuallyOverridden = true;
                oldEngine.isEnabled = false;
            }

            foreach (KeyValuePair<int, ModuleEnginesFX> entry in stockEngines)
            {
                if (entry.Value == targetEngine) continue;
                entry.Value.manuallyOverridden = true;
                entry.Value.isEnabled = false;
            }

            engine = targetEngine;
            currentMode = mode;
            modeDisplay = $"{mode} Engine{(mode == 1 ? "" : "s")}";
        }

        private void ApplyConfiguration(int mode, int rating)
        {
            string configuration = ConfigurationFor(rating, mode);
            if (!initialized || backend != EngineBackend.RealFuels || configuration == null) return;

            try
            {
                // This is RF's public API. The same ModuleEnginesRF instance remains ignited.
                setConfiguration.Invoke(engineConfigs, new object[] { configuration, false });
                StartCoroutine(TemporarilyRemoveSpoolUp());
                currentMode = mode;
                currentThrustLevel = rating;
                modeDisplay = $"{mode} Engine{(mode == 1 ? "" : "s")}";
                thrustDisplay = $"{rating}%";
            }
            catch (Exception exception)
            {
                Debug.LogError($"[KCLV] TQ-12A V3 configuration change failed: {exception.Message}");
            }
        }

        // Mirrors RF's ModuleBimodalEngineConfigs behavior: configuration reload recalculates
        // the response rate, so keep the existing throttle continuous for one physics tick.
        private IEnumerator TemporarilyRemoveSpoolUp()
        {
            if (throttleResponseRate == null) yield break;
            if (!(throttleResponseRate.GetValue(engine) is float originalRate)) yield break;

            throttleResponseRate.SetValue(engine, 1000000f);
            yield return new WaitForFixedUpdate();
            throttleResponseRate.SetValue(engine, originalRate);
        }

        public void LateUpdate()
        {
            if (!initialized || !HighLogic.LoadedSceneIsFlight) return;

            float throttle = engine.isOperational ? engine.currentThrottle : 0f;
            foreach (WaterfallTarget target in waterfallTargets)
                target.SetControllerValue.Invoke(target.Module, new object[] { "throttle", target.Mode == currentMode ? throttle : 0f });
        }

        public bool isOperational => engine != null && engine.isOperational;
        public float normalizedOutput => engine != null ? engine.normalizedOutput : 0f;
        public float throttleSetting => engine != null ? engine.throttleSetting : 0f;
        public string engineName => engine != null ? engine.engineName : "N/A";
    }
}
