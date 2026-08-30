using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KCLV_CustomPlugins
{
    // Waterfall rebuilds all of its controllers when B9PartSwitch replaces a TEMPLATE in flight.
    // RemapController binds its source at the end of the frame, so it can miss the source's first
    // value change and remain at its zero-initialized value. This generic bridge detects controller
    // replacement and seeds the remap with its own mapping curve after binding has completed.
    public class ModuleWaterfallRemapControllerFix : PartModule
    {
        [KSPField]
        public string sourceController = "ActualAtmo";

        [KSPField]
        public string targetController = "atmosphereDepth";

        private readonly List<WaterfallTarget> waterfallTargets = new List<WaterfallTarget>();
        private bool reflectionFailureReported;

        private sealed class WaterfallTarget
        {
            public PartModule Module;
            public MethodInfo FindController;
            public MethodInfo SetControllerValue;
            public object RemapController;
            public int Revision;
            public float LastMappedValue;
            public bool HasMappedValue;
        }

        public void Start()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;

            FindWaterfallTargets();
            DetectControllerRebuilds();
        }

        public void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;

            if (waterfallTargets.Count == 0)
                FindWaterfallTargets();

            DetectControllerRebuilds();
        }

        private void FindWaterfallTargets()
        {
            waterfallTargets.Clear();

            foreach (PartModule candidate in part.Modules)
            {
                if (candidate.moduleName != "ModuleWaterfallFX") continue;

                Type moduleType = candidate.GetType();
                MethodInfo findController = moduleType.GetMethod(
                    "FindController",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null);
                MethodInfo setControllerValue = moduleType.GetMethod(
                    "SetControllerValue",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(float) },
                    null);

                if (findController == null || setControllerValue == null) continue;

                waterfallTargets.Add(new WaterfallTarget
                {
                    Module = candidate,
                    FindController = findController,
                    SetControllerValue = setControllerValue
                });
            }
        }

        private void DetectControllerRebuilds()
        {
            foreach (WaterfallTarget target in waterfallTargets)
            {
                object remapController;
                try
                {
                    remapController = target.FindController.Invoke(target.Module, new object[] { targetController });
                }
                catch (Exception exception)
                {
                    ReportReflectionFailure(exception);
                    continue;
                }

                if (remapController == null || ReferenceEquals(remapController, target.RemapController)) continue;

                try
                {
                    if (target.RemapController != null && TryReadControllerValue(target.RemapController, out float previousValue))
                    {
                        target.LastMappedValue = previousValue;
                        target.HasMappedValue = true;
                    }

                    target.RemapController = remapController;
                    target.Revision++;

                    // Carry the last valid mapped value across the rebuild immediately. The delayed
                    // pass below then replaces it with a fresh source/curve evaluation, so there is
                    // no one-frame vacuum plume while Waterfall reconnects the RemapController.
                    if (target.HasMappedValue)
                    {
                        target.SetControllerValue.Invoke(
                            target.Module,
                            new object[] { targetController, target.LastMappedValue });
                    }

                    StartCoroutine(ReseedAfterWaterfallInitialization(target, target.Revision));
                }
                catch (Exception exception)
                {
                    ReportReflectionFailure(exception);
                }
            }
        }

        private static bool TryReadControllerValue(object controller, out float value)
        {
            value = 0f;
            MethodInfo getValues = controller.GetType().GetMethod(
                "Get",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
            if (getValues == null) return false;

            float[] values = getValues.Invoke(controller, null) as float[];
            if (values == null || values.Length == 0) return false;

            value = values[0];
            return true;
        }

        private IEnumerator ReseedAfterWaterfallInitialization(WaterfallTarget target, int revision)
        {
            // Waiting through the following frame guarantees that Waterfall's own
            // WaitForEndOfFrame source-controller lookup has completed first.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (target.Revision != revision) yield break;

            try
            {
                object currentRemap = target.FindController.Invoke(target.Module, new object[] { targetController });
                if (currentRemap == null || !ReferenceEquals(currentRemap, target.RemapController)) yield break;

                object source = target.FindController.Invoke(target.Module, new object[] { sourceController });
                if (source == null) yield break;

                MethodInfo getValues = source.GetType().GetMethod(
                    "Get",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                FieldInfo mappingCurveField = currentRemap.GetType().GetField(
                    "mappingCurve",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (getValues == null || mappingCurveField == null) yield break;

                float[] sourceValues = getValues.Invoke(source, null) as float[];
                object mappingCurve = mappingCurveField.GetValue(currentRemap);
                if (sourceValues == null || sourceValues.Length == 0 || mappingCurve == null) yield break;

                MethodInfo evaluate = mappingCurve.GetType().GetMethod(
                    "Evaluate",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(float) },
                    null);
                if (evaluate == null) yield break;

                float mappedValue = Convert.ToSingle(evaluate.Invoke(mappingCurve, new object[] { sourceValues[0] }));

                // Use ModuleWaterfallFX's public setter rather than WaterfallController.Set().
                // Besides storing the value, this raises the controller's awake mask so the
                // replacement effects consume the corrected value on their next update.
                target.SetControllerValue.Invoke(
                    target.Module,
                    new object[] { targetController, mappedValue });
                target.LastMappedValue = mappedValue;
                target.HasMappedValue = true;
                Debug.Log($"[KCLV] Restored Waterfall remap controller {targetController} to {mappedValue:F3} on {part.partInfo.name}.");
            }
            catch (Exception exception)
            {
                ReportReflectionFailure(exception);
            }
        }

        private void ReportReflectionFailure(Exception exception)
        {
            if (reflectionFailureReported) return;

            reflectionFailureReported = true;
            Exception reported = exception is TargetInvocationException && exception.InnerException != null
                ? exception.InnerException
                : exception;
            Debug.LogError($"[KCLV] Waterfall remap controller refresh failed: {reported.Message}");
        }
    }
}
