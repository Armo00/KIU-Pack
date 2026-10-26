using System;
using System.Reflection;
using UnityEngine;

// Keeps the existing stock ModuleControlSurface and its saved user settings.
// The fold animation affects its child; the steering transform itself stays at
// the stock neutral rotation until deployment has completed.
[DefaultExecutionOrder(10000)]
public sealed class KIUGridFinInterlock : PartModule
{
    [KSPField] public string deploymentAnimation = "DeployGridFin";
    [KSPField] public string controlTransform = "ctrlSrf";
    [KSPField] public float deployedThreshold = 0.999f;
    [KSPField(isPersistant = true)] public bool baselineCaptured;
    [KSPField(isPersistant = true)] public bool baselineSurfaceEnabled = true;

    // Diagnostic state, not an extra editor control or a persisted override.
    [KSPField] public bool steeringLocked = true;
    [KSPField] public float deploymentFraction;
    [KSPField] public bool controllerEnabled;
    [KSPField] public float actualDeflectionDegrees;

    private ModuleAnimateGeneric deployment;
    private ModuleControlSurface surface;
    private bool originalComponentEnabled;
    private bool started;
    private bool warned;

    private static readonly BindingFlags ReflectionFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo NeutralField = typeof(ModuleControlSurface).GetField("neutral", ReflectionFlags);
    private static readonly FieldInfo SurfaceField = typeof(ModuleControlSurface).GetField("ctrlSurface", ReflectionFlags);
    private static readonly FieldInfo DeflectionField = typeof(ModuleControlSurface).GetField("deflection", ReflectionFlags);

    public static bool CanSteer(float fraction, bool moving, float threshold)
    {
        return !float.IsNaN(fraction) && !float.IsInfinity(fraction)
            && fraction >= threshold && !moving;
    }

    public override void OnStart(StartState state)
    {
        base.OnStart(state);
        if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight) return;
        foreach (ModuleAnimateGeneric candidate in part.FindModulesImplementing<ModuleAnimateGeneric>())
            if (candidate.animationName == deploymentAnimation) { deployment = candidate; break; }
        foreach (ModuleControlSurface candidate in part.FindModulesImplementing<ModuleControlSurface>())
            if (candidate.transformName == controlTransform) { surface = candidate; break; }
        if (surface == null) { Warn("No matching stock control surface was found."); return; }
        // Symmetry copies can inherit the disabled Behaviour from a closed fin.
        // Preserve the pre-interlock state across copies and save/load instead
        // of mistaking that inherited lock for a user-disabled control surface.
        if (!baselineCaptured)
        {
            baselineSurfaceEnabled = surface.enabled;
            baselineCaptured = true;
        }
        originalComponentEnabled = baselineSurfaceEnabled;
        started = true;
        ApplyState();
    }

    private void Update() { ApplyState(); }
    private void FixedUpdate() { ApplyState(); }
    private void LateUpdate()
    {
        ApplyState();
        if (started && steeringLocked) HoldNeutral();
    }

    private void ApplyState()
    {
        if (!started || surface == null) return;
        bool ready = false;
        try
        {
            if (deployment != null)
            {
                deploymentFraction = deployment.GetScalar;
                ready = CanSteer(deploymentFraction, deployment.IsMoving(), deployedThreshold);
            }
            else Warn("No matching deployment animation was found; steering remains locked.");
        }
        catch (Exception error)
        {
            Warn("Deployment animation is unavailable; steering remains locked. " + error.GetType().Name);
        }
        steeringLocked = !ready;
        // Behaviour.enabled is not a KSP persistent field. Do not write
        // isEnabled/moduleIsEnabled, authorityLimiter, axis preferences, lift
        // coefficients or the angle range: saves and TweakScale retain them.
        surface.enabled = ready && originalComponentEnabled && surface.isEnabled && surface.moduleIsEnabled;
        if (steeringLocked) HoldNeutral();
        controllerEnabled = surface.enabled;
        if (DeflectionField != null) actualDeflectionDegrees = (float)DeflectionField.GetValue(surface);
    }

    private void HoldNeutral()
    {
        if (surface == null) return;
        if (NeutralField == null || SurfaceField == null || DeflectionField == null)
        {
            Warn("The stock steering fields are unavailable; the control surface remains disabled.");
            return;
        }
        DeflectionField.SetValue(surface, 0f);
        Transform pivot = SurfaceField.GetValue(surface) as Transform;
        if (pivot != null) pivot.localRotation = (Quaternion)NeutralField.GetValue(surface);
    }

    private void Warn(string message)
    {
        if (warned) return;
        warned = true;
        Debug.LogWarning("[KIU GridFin] " + message);
    }

    private void OnDestroy()
    {
        if (started && surface != null) surface.enabled = originalComponentEnabled;
    }
}
