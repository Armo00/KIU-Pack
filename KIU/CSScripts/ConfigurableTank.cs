using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEngine;

// Generic, cfg-driven stepped tank. No CZ10B dimensions, fuel names or balance values.
public class ConfigurableTank : PartModule, IPartMassModifier, IPartCostModifier, IPartSizeModifier
{
    [KSPField(isPersistant=true)]
    public int segments;
    [KSPField] public int maxSegments=30;
    [KSPField(guiActiveEditor=true, guiName="Extension segments", guiFormat="F0")]
    [UI_FloatEdit(minValue=0, maxValue=30, incrementLarge=31, incrementSmall=1, incrementSlide=1)]
    public float extensionCount;
    [KSPField] public float segmentLength;
    [KSPField] public float baseHeight;
    [KSPField] public float bottomCapHeight;
    [KSPField] public float mainBodyHeight;
    [KSPField] public float baseDryMass;
    [KSPField] public float dryMassPerSegment;
    [KSPField] public float realFuelsBaseDryMass;
    [KSPField] public float realFuelsDryMassPerSegment;
    [KSPField] public double realFuelsBaseVolume;
    [KSPField] public double realFuelsVolumePerSegment;
    [KSPField] public float costPerSegment;
    [KSPField] public string bottomTransform="tankBottom";
    [KSPField] public string topTransform="tankTop";
    [KSPField] public string bodyTransform="tankBody";
    [KSPField] public string templateTransform="extensionTemplate";
    [KSPField] public string bottomNode="bottom";
    [KSPField] public string topNode="top";
    [KSPField]
    public float tankLength;
    [KSPField(guiActiveEditor=true,guiActive=true,guiName="Tank Spec")]
    public string tankSpec;
    [KSPField(guiActiveEditor=true,guiName="Tank mode")]
    public string tankMode="Stock";
    [KSPField(guiActiveEditor=true,guiName="Layout (lower / upper)")]
    public string layout;
    // Defaults preserve the existing bottom-origin CZ10B contract.
    [KSPField] public float originFraction = 0f;
    [KSPField] public bool scaleWithModel = false;
    [KSPField] public bool realFuelsVolumeIsUsable = false;
    [KSPField] public string extensionLabel = "Extension segments";
    [KSPField] public string specLabel = "Tank Spec";
    [KSPField] public string modeLabel = "Tank mode";
    [KSPField] public string layoutLabel = "Layout (lower / upper)";
    [KSPField] public string addLabel = "Add extension";
    [KSPField] public string removeLabel = "Remove extension";
    private Transform lower,upper,body,template;
    private Vector3 originalLower,originalUpper;
    private bool initialized;
    private bool rendererRefreshPending;
    private float modelScale=1;
    private float appliedDown,appliedUp;
    private readonly List<GameObject> copies=new List<GameObject>();
    private readonly List<ResourceSpec> resources=new List<ResourceSpec>();
    private class ResourceSpec { public string name; public double baseline,increment; }
    public static int LowerCount(int count) { return (count+1)/2; }
    public static int UpperCount(int count) { return count/2; }

    public override void OnLoad(ConfigNode node)
    {
        base.OnLoad(node);
        ReadResources(node);
    }
    private void ReadResources(ConfigNode node)
    {
        ConfigNode[] specs=node.GetNodes("STOCK_RESOURCE");
        if(specs.Length==0 && part!=null && part.partInfo!=null && part.partInfo.partConfig!=null)
            foreach(ConfigNode module in part.partInfo.partConfig.GetNodes("MODULE"))
                if(module.GetValue("name")==GetType().Name) { specs=module.GetNodes("STOCK_RESOURCE");break; }
        if(specs.Length==0)return;
        resources.Clear();
        foreach(ConfigNode spec in specs) {
            ResourceSpec r=new ResourceSpec();r.name=spec.GetValue("name");
            spec.TryGetValue("baseAmount",ref r.baseline);spec.TryGetValue("amountPerSegment",ref r.increment);
            if(string.IsNullOrEmpty(r.name)||r.baseline<0||r.increment<0)throw new ArgumentException("Invalid STOCK_RESOURCE");
            resources.Add(r);
        }
    }
    public override void OnStart(StartState state)
    {
        base.OnStart(state);
        Fields["extensionCount"].guiName=KSP.Localization.Localizer.Format(extensionLabel);
        Fields["tankSpec"].guiName=KSP.Localization.Localizer.Format(specLabel);
        Fields["tankMode"].guiName=KSP.Localization.Localizer.Format(modeLabel);
        Fields["layout"].guiName=KSP.Localization.Localizer.Format(layoutLabel);
        Events["AddExtension"].guiName=KSP.Localization.Localizer.Format(addLabel);
        Events["RemoveExtension"].guiName=KSP.Localization.Localizer.Format(removeLabel);
        maxSegments=Math.Max(0,maxSegments);
        ConfigureSegmentControl();
        InitializeModel();
        segments=Math.Max(0,Math.Min(maxSegments,segments));
        ApplyGeometry(segments,false);
        // MFT's own OnStart must finish before updating its internal tank list.
        StartCoroutine(AfterModulesStarted());
    }
    private void ConfigureSegmentControl()
    {
        UI_FloatEdit ui=Fields["extensionCount"].uiControlEditor as UI_FloatEdit;
        if(ui==null)return;
        ui.minValue=0;ui.maxValue=maxSegments;
        // One slider interval spans the entire configured range, including its end.
        ui.incrementLarge=Math.Max(1,maxSegments+1);ui.incrementSmall=1;ui.incrementSlide=1;
        ui.affectSymCounterparts=UI_Scene.None;
        ui.onFieldChanged=OnSegmentControlChanged;
        extensionCount=Math.Max(0,Math.Min(maxSegments,segments));
    }
    public void OnSegmentControlChanged(BaseField field,object previous)
    {
        int requested=Mathf.Clamp(Mathf.RoundToInt(extensionCount),0,maxSegments);
        if(requested!=segments)SetSegmentCount(requested,true);
        extensionCount=segments;
    }
    public void LateUpdate()
    {
        if(rendererRefreshPending) {
            rendererRefreshPending=false;
            RefreshRenderers();
        }
        // FloatEdit supplies a slider and single-step buttons. Hide its extra
        // interval-jump pair so both remaining arrow buttons adjust exactly one.
        UI_FloatEdit ui=Fields["extensionCount"].uiControlEditor as UI_FloatEdit;
        UIPartActionFloatEdit item=ui==null?null:ui.partActionItem as UIPartActionFloatEdit;
        if(item==null)return;
        if(item.incLarge!=null)item.incLarge.gameObject.SetActive(false);
        if(item.decLarge!=null)item.decLarge.gameObject.SetActive(false);
    }
    private IEnumerator AfterModulesStarted()
    {
        yield return null;
        UpdateCapacity(segments);
        RefreshAerodynamics();
        // Part and other modules may initialize their renderer lists after OnStart.
        rendererRefreshPending=true;
    }
    public void InitializeModel()
    {
        if(initialized)return;
        if(segmentLength<=0||baseHeight<=0||bottomCapHeight<=0||mainBodyHeight<=0)
            throw new ArgumentException("Invalid modular tank dimensions in cfg");
        lower=part.FindModelTransform(bottomTransform);upper=part.FindModelTransform(topTransform);
        body=part.FindModelTransform(bodyTransform);template=part.FindModelTransform(templateTransform);
        if(lower==null||upper==null||body==null||template==null)throw new InvalidOperationException("Missing modular tank model transforms");
        // Authored section roots are at zero; do not inherit an already extended symmetry clone's offsets.
        originalLower=Vector3.zero;originalUpper=Vector3.zero;
        template.gameObject.SetActive(false);
        modelScale=scaleWithModel ? part.transform.InverseTransformVector(template.parent.TransformVector(Vector3.up)).magnitude : 1f;
        foreach(Transform child in template.parent)
            if(child.name.StartsWith("TankExtension_"))copies.Add(child.gameObject);
        initialized=true;
    }
    [KSPEvent(guiActiveEditor=false,guiName="Add extension",active=false)]
    public void AddExtension() { SetSegmentCount(segments+1,true); }
    [KSPEvent(guiActiveEditor=false,guiName="Remove extension",active=false)]
    public void RemoveExtension() { SetSegmentCount(segments-1,true); }
    public void SetSegmentCount(int count,bool symmetry)
    {
        if(HighLogic.LoadedSceneIsFlight)return; // No in-flight resizing/refueling.
        count=Math.Max(0,Math.Min(maxSegments,count));
        InitializeModel();
        // Validate/update MFT before committing geometry; missing API must not silently use stock fuel.
        UpdateCapacity(count);
        ApplyGeometry(count,HighLogic.LoadedSceneIsEditor);
        segments=count;
        extensionCount=count;
        if(symmetry)
            foreach(Part other in part.symmetryCounterparts) {
                ConfigurableTank m=other.FindModuleImplementing<ConfigurableTank>();
                if(m!=null)m.SetSegmentCount(count,false);
            }
        RefreshAerodynamics();
        if(HighLogic.LoadedSceneIsEditor && EditorLogic.fetch!=null)
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }
    private static void MoveTree(Part p,Vector3 delta)
    {
        List<Part> parts=new List<Part>();List<Vector3> positions=new List<Vector3>();
        CaptureTree(p,parts,positions);
        for(int i=0;i<parts.Count;i++)parts[i].transform.position=positions[i]+delta;
    }
    private static void CaptureTree(Part p,List<Part> parts,List<Vector3> positions)
    {
        parts.Add(p);positions.Add(p.transform.position);
        foreach(Part child in p.children)CaptureTree(child,parts,positions);
    }
    public void ApplyGeometry(int count,bool moveAttached)
    {
        InitializeModel();
        int low=LowerCount(count),high=UpperCount(count);
        float down=low*segmentLength,up=high*segmentLength;
        float baseBottom=-baseHeight*originFraction,baseTop=baseBottom+baseHeight;
        float newBottom=(baseBottom-down)*modelScale,newTop=(baseTop+up)*modelScale;
        AttachNode bn=part.FindAttachNode(bottomNode),tn=part.FindAttachNode(topNode);
        Vector3 bd=bn==null?Vector3.zero:Vector3.up*(newBottom-bn.position.y);
        Vector3 td=tn==null?Vector3.zero:Vector3.up*(newTop-tn.position.y);
        // Classify radial attachments before moving the parent tree. Compare
        // against the current cap locations, not coordinates from another size.
        var radial=new List<KeyValuePair<Part,Vector3>>();
        if(moveAttached) foreach(Part child in part.children) {
            if((bn!=null&&bn.attachedPart==child)||(tn!=null&&tn.attachedPart==child))continue;
            float y=part.transform.InverseTransformPoint(child.transform.position).y;
            if(y<(baseBottom+bottomCapHeight-appliedDown)*modelScale)radial.Add(new KeyValuePair<Part,Vector3>(child,bd));
            else if(y>(baseBottom+bottomCapHeight+mainBodyHeight+appliedUp)*modelScale)radial.Add(new KeyValuePair<Part,Vector3>(child,td));
        }
        if(moveAttached) {
            Vector3 anchor=Vector3.zero;
            if(bn!=null&&bn.attachedPart==part.parent)anchor=bd;
            else if(tn!=null&&tn.attachedPart==part.parent)anchor=td;
            if(anchor!=Vector3.zero)MoveTree(part,-part.transform.TransformVector(anchor));
            if(bn!=null&&bn.attachedPart!=null&&bn.attachedPart!=part.parent)MoveTree(bn.attachedPart,part.transform.TransformVector(bd));
            if(tn!=null&&tn.attachedPart!=null&&tn.attachedPart!=part.parent)MoveTree(tn.attachedPart,part.transform.TransformVector(td));
            foreach(var pair in radial)MoveTree(pair.Key,part.transform.TransformVector(pair.Value));
        }
        if(bn!=null){bn.position=new Vector3(bn.position.x,newBottom,bn.position.z);bn.originalPosition=bn.position;}
        if(tn!=null){tn.position=new Vector3(tn.position.x,newTop,tn.position.z);tn.originalPosition=tn.position;}
        lower.localPosition=originalLower-Vector3.up*down;
        upper.localPosition=originalUpper+Vector3.up*up;
        foreach(GameObject old in copies) {
            if(old==null)continue;
            old.SetActive(false);
            // Destroy is deferred. Remove retired meshes from model searches now.
            old.transform.SetParent(null,false);
            Destroy(old);
        }
        copies.Clear();
        for(int i=0;i<count;i++) {
            bool below=(i%2)==0;int index=i/2;
            GameObject obj=Instantiate(template.gameObject);obj.name="TankExtension_"+i;
            obj.transform.SetParent(template.parent,false);obj.transform.localRotation=template.localRotation;
            obj.transform.localScale=Vector3.one;
            obj.transform.localPosition=Vector3.up*(below?baseBottom+bottomCapHeight-(index+1)*segmentLength:baseBottom+bottomCapHeight+mainBodyHeight+index*segmentLength);
            obj.SetActive(true);copies.Add(obj);
        }
        appliedDown=down;appliedUp=up;
        float center=(-baseHeight*originFraction+(baseHeight+up-down)*.5f)*modelScale;
        part.CoMOffset=new Vector3(0,center,0);part.CoLOffset=part.CoMOffset;part.CoPOffset=part.CoMOffset;
        tankLength=(baseHeight+count*segmentLength)*modelScale;layout=low+" / "+high;
        UpdateTankSpec(count);
        extensionCount=count;
        Events["AddExtension"].active=false;Events["RemoveExtension"].active=false;
        InvalidateRendererCaches();
        rendererRefreshPending=true;
    }
    private void InvalidateRendererCaches()
    {
        // Consumers such as Vessel Viewer use these lists, not the scene hierarchy.
        part.ResetModelRenderersCache();
        part.ResetModelMeshRenderersCache();
        part.ResetModelSkinnedMeshRenderersCache();
    }
    private void RefreshRenderers()
    {
        if(part==null || (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight))return;
        InvalidateRendererCaches();
        var renderers=new List<Renderer>(part.FindModelComponents<Renderer>());
        renderers.RemoveAll(r=>r==null || !r.gameObject.activeInHierarchy || r.GetComponentInParent<Part>()!=part);
        KSPUtil.RemoveNonHighlightableRenderers(renderers);
        // The rim-highlight list and the outline Highlighter have separate caches.
        part.HighlightRenderer=renderers;
        part.RefreshHighlighter();
        if(part.highlighter!=null) {
            bool highlighted=part.HighlightActive,recursive=part.RecurseHighlight;
            // Highlight skips unchanged colors, so reapply an active selection to
            // new renderers without toggling any attached parts' highlight state.
            part.SetHighlight(false,false);
            if(highlighted)part.SetHighlight(true,false);
            part.RecurseHighlight=recursive;
        }
    }
    private PartModule FuelModule()
    {
        foreach(PartModule module in part.Modules)if(module.moduleName=="ModuleFuelTanks")return module;
        return null;
    }
    public void UpdateCapacity(int count)
    {
        PartModule mft=FuelModule();
        if(mft!=null) {
            tankMode="RealFuels / MFT";
            double target=realFuelsBaseVolume+count*realFuelsVolumePerSegment;
            if(target<=0)throw new InvalidOperationException("Missing realFuels volume cfg");
            MethodInfo method=mft.GetType().GetMethod("ChangeTotalVolume",new Type[]{typeof(double),typeof(bool)});
            if(method==null)throw new NotSupportedException("ModuleFuelTanks.ChangeTotalVolume(double,bool) is required");
            // The cfg contract is usable fuel volume, matching ModuleFuelTanks
            // "volume". ChangeTotalVolume expects geometric volume before its
            // utilization percentage; passing usable volume directly loses fuel.
            double volume=target;
            if(realFuelsVolumeIsUsable) {
                FieldInfo utilizationField=mft.GetType().GetField("utilization");
                if(utilizationField==null)throw new NotSupportedException("ModuleFuelTanks.utilization is required");
                double utilization=Convert.ToDouble(utilizationField.GetValue(mft),CultureInfo.InvariantCulture)*.01;
                if(utilization<=0||utilization>1)throw new InvalidOperationException("Invalid MFT utilization");
                volume=target/utilization;
            }
            method.Invoke(mft,new object[]{volume,false});
            MethodInfo mass=mft.GetType().GetMethod("CalculateMass",Type.EmptyTypes);
            if(mass!=null)mass.Invoke(mft,null);
        } else {
            tankMode="Stock";
            if(resources.Count==0)ReadResources(new ConfigNode());
            foreach(ResourceSpec spec in resources) {
                double capacity=spec.baseline+count*spec.increment;
                PartResource resource=part.Resources[spec.name];
                if(resource==null) {
                    ConfigNode node=new ConfigNode("RESOURCE");node.AddValue("name",spec.name);
                    node.AddValue("maxAmount",capacity);node.AddValue("amount",capacity);part.AddResource(node);
                } else {
                    double fraction=resource.maxAmount>0?resource.amount/resource.maxAmount:0;
                    resource.maxAmount=capacity;resource.amount=Math.Max(0,Math.Min(capacity,capacity*fraction));
                }
            }
        }
        part.ResetSimulationResources();GameEvents.onPartResourceListChange.Fire(part);
        UpdateTankSpec(count);
    }
    private float DryMassForCount(int count)
    {
        bool rf=FuelModule()!=null;
        return (rf?realFuelsBaseDryMass:baseDryMass)+count*(rf?realFuelsDryMassPerSegment:dryMassPerSegment);
    }
    private void UpdateTankSpec(int count)
    {
        tankSpec=string.Format(CultureInfo.InvariantCulture,"{0:F1}m/{1:F1}t",(baseHeight+count*segmentLength)*modelScale,DryMassForCount(count));
    }
    private void RefreshAerodynamics()
    {
        if(!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)return;
        if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
        if(DragCubeSystem.Instance==null)return;
        DragCube cube=DragCubeSystem.Instance.RenderProceduralDragCube(part);
        if(cube!=null) { part.DragCubes.ClearCubes();part.DragCubes.Cubes.Add(cube);part.DragCubes.ResetCubeWeights();part.DragCubes.ForceUpdate(true,true,true); }
    }
    public float GetModuleMass(float defaultMass,ModifierStagingSituation sit)
    {
        return DryMassForCount(segments)-defaultMass;
    }
    public ModifierChangeWhen GetModuleMassChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
    public float GetModuleCost(float defaultCost,ModifierStagingSituation sit) { return segments*costPerSegment; }
    public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
    public Vector3 GetModuleSize(Vector3 defaultSize,ModifierStagingSituation sit) { return Vector3.up*(segments*segmentLength*modelScale); }
    public ModifierChangeWhen GetModuleSizeChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
}
