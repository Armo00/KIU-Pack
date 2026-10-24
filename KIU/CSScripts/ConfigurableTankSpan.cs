using System;
using UnityEngine;

// Generic fixed-anchor structure mode. No Part names or separation logic.
public partial class ConfigurableTank
{
    [KSPField] public bool useFixedAnchorSpan=false;
    [KSPField] public int referenceSegments=0;
    [KSPField] public bool showLayout=true;
    [KSPField] public bool showSpec=true;
    private Vector3 spanBodyPosition,spanBodyScale;
    private bool spanRendererMembershipInitialized;

    private void InitializeFixedAnchorSpan()
    {
        FixedAnchorSpanGeometry.Calculate(0,referenceSegments,segmentLength,baseHeight);
        if(referenceSegments>maxSegments || upperOnly || useTerminalSurfaceVariants)
            throw new ArgumentException("Fixed-anchor span conflicts with template/upper-only settings");
        lower=part.FindModelTransform(bottomTransform);
        upper=part.FindModelTransform(topTransform);
        body=part.FindModelTransform(bodyTransform);
        if(lower==null || upper==null || body==null || lower==upper || lower==body || upper==body ||
           lower.parent!=upper.parent || lower.parent!=body.parent)
            throw new InvalidOperationException("Fixed-anchor span requires three distinct sibling transforms");
        Part prefab=part.partInfo==null?null:part.partInfo.partPrefab;
        Transform lo=prefab==null?lower:prefab.FindModelTransform(bottomTransform);
        Transform hi=prefab==null?upper:prefab.FindModelTransform(topTransform);
        Transform mid=prefab==null?body:prefab.FindModelTransform(bodyTransform);
        if(lo==null || hi==null || mid==null)
            throw new InvalidOperationException("Fixed-anchor span authored prefab roots missing");
        float expected=baseHeight+referenceSegments*segmentLength;
        if(Mathf.Abs((hi.localPosition.y-lo.localPosition.y)-expected)>Mathf.Max(.0001f,expected*.00001f))
            throw new InvalidOperationException("Fixed-anchor span reference length disagrees with authored ends");
        originalLower=lo.localPosition;originalUpper=hi.localPosition;
        spanBodyPosition=mid.localPosition;spanBodyScale=mid.localScale;
        if(spanBodyScale.y<=0)throw new ArgumentException("Fixed-anchor span needs positive axial scale");
        foreach(PartModule module in part.Modules)if(module.moduleName=="TweakScale")tweakScale=module;
        Transform referencePart=prefab==null?part.transform:prefab.transform;
        authoredScale=scaleWithModel?referencePart.InverseTransformVector(lo.parent.TransformVector(Vector3.up)).magnitude:1f;
        ReadScale();
        initialized=true;
    }

    private void ApplyFixedAnchorSpan(int count)
    {
        var pose=FixedAnchorSpanGeometry.Calculate(count,referenceSegments,segmentLength,baseHeight);
        lower.localPosition=originalLower+Vector3.up*pose.lowerDelta;
        upper.localPosition=originalUpper+Vector3.up*pose.upperDelta;
        body.localPosition=spanBodyPosition+Vector3.up*pose.centerDelta;
        body.localScale=new Vector3(spanBodyScale.x,spanBodyScale.y*pose.axialScale,spanBodyScale.z);
        // Anchors, attached Parts, mass distribution and resources are deliberately
        // unchanged. The visible end groups and span are the only moved objects.
        // No template objects/colliders are allocated at any segment count.
        appliedDown=LowerCount(count)*segmentLength;appliedUp=UpperCount(count)*segmentLength;
        segments=count;extensionCount=ControlValue(count);
        tankLength=pose.length*modelScale;tankDiameter=baseDiameter*modelScale;
        layout=LowerCount(count)+" / "+UpperCount(count);
        UpdateTankSpec(count);
        if(!spanRendererMembershipInitialized) {
            spanRendererMembershipInitialized=true;
            InvalidateRendererCaches();rendererRefreshPending=true;
        }
        PublishDimensionsChanged();
    }
}

// Pure arithmetic shared by runtime and offline regression checks.
internal static class FixedAnchorSpanGeometry
{
    internal struct Pose { internal float lowerDelta,upperDelta,centerDelta,axialScale,length; }
    private static bool Positive(float x) { return !float.IsNaN(x)&&!float.IsInfinity(x)&&x>0; }
    internal static Pose Calculate(int count,int reference,float step,float baseLength)
    {
        if(count<0 || reference<0 || count>100000 || reference>100000 || !Positive(step) || !Positive(baseLength))
            throw new ArgumentException("Invalid fixed-anchor span dimensions");
        float length=baseLength+count*step,authored=baseLength+reference*step;
        if(!Positive(length)||!Positive(authored))throw new ArgumentException("Nonfinite fixed-anchor span");
        float lo=((reference+1)/2-(count+1)/2)*step,hi=(count/2-reference/2)*step;
        return new Pose {lowerDelta=lo,upperDelta=hi,centerDelta=(lo+hi)*.5f,axialScale=length/authored,length=length};
    }
}
