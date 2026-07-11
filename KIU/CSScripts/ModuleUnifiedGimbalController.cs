using System.Collections.Generic;
using UnityEngine;

namespace KCLV_CustomPlugins
{
    // Generic controller for every ModuleGimbal attached to the same Part.
    public class ModuleUnifiedGimbalController : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "Gimbal Limiter"),
         UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f)]
        public float gimbalLimiter = 100f;

        [KSPField(isPersistant = true)]
        public bool gimbalLocked;

        private readonly List<ModuleGimbal> gimbals = new List<ModuleGimbal>();
        private float appliedLimiter = float.NaN;
        private bool appliedLockState;
        private bool hasGimbals;

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Lock Gimbals")]
        public void ToggleGimbalsEvent()
        {
            SetGimbalLock(!gimbalLocked);
        }

        [KSPAction(guiName = "Lock Gimbals")]
        public void LockGimbalsAction(KSPActionParam _)
        {
            SetGimbalLock(true);
        }

        [KSPAction(guiName = "Unlock Gimbals")]
        public void UnlockGimbalsAction(KSPActionParam _)
        {
            SetGimbalLock(false);
        }

        [KSPAction(guiName = "Toggle Gimbals")]
        public void ToggleGimbalsAction(KSPActionParam _)
        {
            SetGimbalLock(!gimbalLocked);
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            gimbals.Clear();
            gimbals.AddRange(part.FindModulesImplementing<ModuleGimbal>());
            hasGimbals = gimbals.Count > 0;
            ConfigureControllerUi();

            if (!hasGimbals)
            {
                Debug.LogWarning("[KCLV] ModuleUnifiedGimbalController found no ModuleGimbal on this part.");
                return;
            }

            ApplyControllerState();
        }

        // Run after every PartModule has completed OnStart, including ModuleGimbal.
        public void Start()
        {
            if (!hasGimbals) return;

            SuppressIndividualGimbalControls();
            ApplyControllerState();
        }

        public void Update()
        {
            if (!hasGimbals) return;

            if (!Mathf.Approximately(appliedLimiter, gimbalLimiter) || appliedLockState != gimbalLocked)
                ApplyControllerState();
        }

        // ModuleGimbal can restore its own PAW controls during its internal UI update.
        // Reassert suppression after its regular Update and FixedUpdate work has completed.
        public void LateUpdate()
        {
            if (!hasGimbals) return;

            SuppressIndividualGimbalControls();
        }

        private void ConfigureControllerUi()
        {
            Fields[nameof(gimbalLimiter)].guiActive = hasGimbals;
            Fields[nameof(gimbalLimiter)].guiActiveEditor = hasGimbals;
            Events[nameof(ToggleGimbalsEvent)].guiActive = hasGimbals;
            Events[nameof(ToggleGimbalsEvent)].guiActiveEditor = hasGimbals;
            Actions[nameof(LockGimbalsAction)].active = hasGimbals;
            Actions[nameof(UnlockGimbalsAction)].active = hasGimbals;
            Actions[nameof(ToggleGimbalsAction)].active = hasGimbals;
            UpdateToggleEventLabel();
        }

        private void SuppressIndividualGimbalControls()
        {
            foreach (ModuleGimbal gimbal in gimbals)
                HideIndividualGimbalControls(gimbal);
        }

        private static void HideIndividualGimbalControls(ModuleGimbal gimbal)
        {
            if (gimbal == null) return;

            // ModuleGimbal.UpdateToggles recreates its Show Actuation Toggles event
            // whenever showToggles is true, so disable that source state first.
            gimbal.showToggles = false;
            gimbal.currentShowToggles = false;
            BaseEvent toggleTogglesEvent = gimbal.Events["ToggleToggles"];
            if (toggleTogglesEvent != null)
            {
                toggleTogglesEvent.guiActive = false;
                toggleTogglesEvent.guiActiveEditor = false;
            }

            for (int index = 0; index < gimbal.Fields.Count; index++)
            {
                BaseField field = gimbal.Fields[index];
                if (field == null) continue;
                field.guiActive = false;
                field.guiActiveEditor = false;
            }

            for (int index = 0; index < gimbal.Events.Count; index++)
            {
                BaseEvent gimbalEvent = gimbal.Events[index];
                if (gimbalEvent == null) continue;
                gimbalEvent.guiActive = false;
                gimbalEvent.guiActiveEditor = false;
            }

            for (int index = 0; index < gimbal.Actions.Count; index++)
            {
                BaseAction gimbalAction = gimbal.Actions[index];
                if (gimbalAction == null) continue;
                gimbalAction.active = false;
            }
        }

        private void SetGimbalLock(bool locked)
        {
            if (!hasGimbals) return;

            gimbalLocked = locked;
            ApplyControllerState();
        }

        private void ApplyControllerState()
        {
            foreach (ModuleGimbal gimbal in gimbals)
            {
                if (gimbal == null) continue;
                gimbal.gimbalLimiter = gimbalLimiter;
                gimbal.gimbalLock = gimbalLocked;
            }

            appliedLimiter = gimbalLimiter;
            appliedLockState = gimbalLocked;
            UpdateToggleEventLabel();
        }

        private void UpdateToggleEventLabel()
        {
            Events[nameof(ToggleGimbalsEvent)].guiName = gimbalLocked ? "Unlock Gimbals" : "Lock Gimbals";
        }
    }
}
