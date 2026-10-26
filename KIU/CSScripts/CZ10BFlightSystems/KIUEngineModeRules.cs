using System;
using System.Reflection;

// Pure calculations shared by the runtime adapter and its offline regression
// cases. RF owns its scale law; this adapter does not edit solver fuel flow.
public static class KIUEngineModeRules
{
    public const int NoSavedIgnitions = int.MinValue;

    public static MethodInfo ResolveMethod(object instance, string name, params Type[] parameters)
    {
        if (instance == null) return null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        for (Type type = instance.GetType(); type != null; type = type.BaseType)
        {
            MethodInfo method = type.GetMethod(name, flags, null, parameters, null);
            if (method != null) return method;
        }
        return null;
    }

    public static bool ValidScale(double ratio)
    {
        return ratio > 0.000001 && !double.IsNaN(ratio) && !double.IsInfinity(ratio);
    }

    public static bool NeedsScaleSync(double ratio, double rfScaleSquared)
    {
        if (!ValidScale(ratio)) return false;
        double expected = ratio * ratio;
        return double.IsNaN(rfScaleSquared) || double.IsInfinity(rfScaleSquared)
            || Math.Abs(expected - rfScaleSquared) > Math.Max(0.00001, expected * 0.00001);
    }

    public static float PriceCorrection(float baselineCost, double ratio, float exponent,
        float defaultCost, float otherManagedCost)
    {
        if (!ValidScale(ratio)) return 0;
        double target = baselineCost * Math.Pow(ratio, exponent);
        if (double.IsNaN(target) || double.IsInfinity(target) || target > float.MaxValue) return 0;
        return (float)target - defaultCost - otherManagedCost;
    }

    public static int PreserveIgnitions(int saved, int current)
    {
        if (saved == NoSavedIgnitions) return current;
        if (saved < 0) return saved; // RF's unlimited-ignition convention.
        if (current < 0) return saved;
        return Math.Min(saved, current); // Never refund a real ignition during startup.
    }
}
