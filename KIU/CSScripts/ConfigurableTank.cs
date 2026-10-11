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
    // Optional geometry metadata for consumers such as RP-1 ModuleToolingGeneric.
    [KSPField] public float baseDiameter;
    [KSPField] public float tankDiameter;
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
    [KSPField] public bool notifyB9ModelChanges = false;
    // Optional structure mode. Defaults preserve every existing tank contract.
    [KSPField] public bool upperOnly = false;
    [KSPField] public float extensionStart = 0f;
    [KSPField] public float structuralBayLength = 0f;
    [KSPField] public bool showResourceMode = true;
    [KSPField] public bool displayHeightInMeters = false;
    // Comma-separated authored-zero roots, each with a height-following fraction.
    // Example: "MidService:0.5". Top service geometry lives below topTransform.
    [KSPField] public string heightFollowers = "";
    private readonly List<KeyValuePair<Transform,float>> followers = new List<KeyValuePair<Transform,float>>();
    private readonly Dictionary<Transform,float> followerSteps = new Dictionary<Transform,float>();
    // Optional lift in a stacked structure. A target on the connected upper
    // sections supplies the endpoint; car dimensions remain unscaled.
    [KSPField] public string linkedLiftSpace = "";
    [KSPField] public string linkedLiftCar = "";
    [KSPField] public string linkedLiftTarget = "";
    [KSPField] public string linkedLiftAnimationName = "";
    [KSPField] public bool linkedLiftAvailable = false;
    [KSPField] public float linkedLiftGround = 0f;
    private float nextLiftCheck;

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
    private float authoredScale=1, scaleRatio=1;
    private bool modulesStarted, scalePending;
    private PartModule tweakScale;
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
        ReadServiceHeights(node);
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
        Fields["tankMode"].guiActiveEditor=showResourceMode;
        Fields["layout"].guiActiveEditor=!upperOnly;
        maxSegments=Math.Max(0,maxSegments);
        ConfigureSegmentControl();
        InitializeModel();
        ConfigureStructureControls();
        segments=Math.Max(0,Math.Min(maxSegments,segments));
        // Both TweakScale branches finish restoring their model/nodes in OnStart.
        // Do not give their restore code already-scaled extension nodes.
        if(tweakScale==null)ApplyGeometry(segments,false);
        // MFT's own OnStart must finish before updating its internal tank list.
        StartCoroutine(AfterModulesStarted());
    }
    private float ControlValue(int count) { return displayHeightInMeters ? baseHeight+count*segmentLength : count; }
    private void ConfigureSegmentControl()
    {
        UI_FloatEdit ui=Fields["extensionCount"].uiControlEditor as UI_FloatEdit;
        if(ui==null)return;
        ui.minValue=displayHeightInMeters?baseHeight:0;ui.maxValue=ControlValue(maxSegments);
        // One slider interval spans the entire configured range, including its end.
        float step=upperOnly && selectableHeightStep ? HeightIncrement : displayHeightInMeters?segmentLength:1f;
        ui.incrementLarge=upperOnly && selectableHeightStep ? 0 : ui.maxValue-ui.minValue+step;ui.incrementSmall=step;ui.incrementSlide=step;
        if(displayHeightInMeters){Fields["extensionCount"].guiFormat="F1";ui.sigFigs=1;ui.useSI=false;ui.unit=" m";}
        if(upperOnly && selectableHeightStep)ui.incrementSlide=0;
        ui.affectSymCounterparts=UI_Scene.None;
        ui.onFieldChanged=OnSegmentControlChanged;
        extensionCount=ControlValue(Math.Max(0,Math.Min(maxSegments,segments)));
    }
    public void OnSegmentControlChanged(BaseField field,object previous)
    {
        if(upperOnly && selectableHeightStep)extensionCount=SnapHeight("extensionCount",extensionCount);
        int requested=Mathf.Clamp(Mathf.RoundToInt(displayHeightInMeters?(extensionCount-baseHeight)/segmentLength:extensionCount),0,maxSegments);
        if(requested!=segments)SetSegmentCount(requested,true);
        extensionCount=ControlValue(segments);
    }
    private void SetLinkedLiftAvailable(Transform car,bool available)
    {
        if(linkedLiftAvailable==available && car.gameObject.activeSelf==available)return;
        linkedLiftAvailable=available;
        car.gameObject.SetActive(available);
        foreach(PartModule module in part.Modules) {
            ModuleAnimateGeneric animation=module as ModuleAnimateGeneric;
            if(animation==null || string.IsNullOrEmpty(linkedLiftAnimationName) || animation.animationName!=linkedLiftAnimationName)continue;
            if(animation.Events.Contains("Toggle"))animation.Events["Toggle"].active=available;
            if(animation.Actions["ToggleAction"]!=null)animation.Actions["ToggleAction"].active=available;
        }
        rendererRefreshPending=true;
    }
    private void UpdateLinkedLift()
    {
        if(string.IsNullOrEmpty(linkedLiftSpace) || Time.unscaledTime<nextLiftCheck)return;
        nextLiftCheck=Time.unscaledTime+.25f;
        Transform space=part.FindModelTransform(linkedLiftSpace),car=part.FindModelTransform(linkedLiftCar);
        if(space==null || car==null)return;
        Part current=part;Transform target=null;var visited=new HashSet<Part>();
        while(current!=null && visited.Add(current)) {
            target=current.FindModelTransform(linkedLiftTarget);
            if(target!=null)break;
            AttachNode next=current.FindAttachNode(topNode);
            current=next==null?null:next.attachedPart;
        }
        // A standalone lower tower has no passenger landing. Do not leave its
        // authored cabin suspended in space, or offer a nonfunctional lift.
        if(target==null) { SetLinkedLiftAvailable(car,false);return; }
        float travel=space.parent.InverseTransformPoint(target.position).y-linkedLiftGround;
        if(travel<=.1f || float.IsNaN(travel) || float.IsInfinity(travel)) { SetLinkedLiftAvailable(car,false);return; }
        Vector3 stretch=space.localScale;stretch.y=travel;space.localScale=stretch;
        Vector3 inverse=car.localScale;inverse.y=1f/travel;car.localScale=inverse;
        SetLinkedLiftAvailable(car,true);
    }
    public void LateUpdate()
    {
        if(modulesStarted && upperOnly)UpdateLinkedLift();
        if(modulesStarted && scalePending) {
            scalePending=false;
            SynchronizeScale(HighLogic.LoadedSceneIsEditor);
        }
        if(rendererRefreshPending) {
            rendererRefreshPending=false;
            RefreshRenderers();
        }
        UpdateServiceVisibility();
        // FloatEdit supplies a slider and single-step buttons. Hide its extra
        // interval-jump pair so both remaining arrow buttons adjust exactly one.
        UI_FloatEdit ui=Fields["extensionCount"].uiControlEditor as UI_FloatEdit;
        UIPartActionFloatEdit item=ui==null?null:ui.partActionItem as UIPartActionFloatEdit;
        if(item==null)return;
        if(item.incLarge!=null)item.incLarge.gameObject.SetActive(false);
        if(item.decLarge!=null)item.decLarge.gameObject.SetActive(false);
        if(upperOnly && selectableHeightStep)LayoutHeightWidget("extensionCount",item);
    }
    private IEnumerator AfterModulesStarted()
    {
        yield return null;
        modulesStarted=true;
        scalePending=false;
        ReadScale();
        ApplyGeometry(segments,false,false);
        UpdateCapacity(segments);
        NotifyModelChanged();
        RefreshAerodynamics();
        // Part and other modules may initialize their renderer lists after OnStart.
        rendererRefreshPending=true;
        // Editor's initial cost display is built before the deferred restore.
        // Publish the final capacity/mass/cost after all modules have started.
        part.UpdateMass();
        if(HighLogic.LoadedSceneIsEditor&&EditorLogic.fetch!=null&&EditorLogic.fetch.ship!=null)
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
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
        foreach(PartModule module in part.Modules)if(module.moduleName=="TweakScale")tweakScale=module;
        // Read the authored model scale from the prefab, never from a scaled clone.
        Part prefab=part.partInfo==null?null:part.partInfo.partPrefab;
        Transform reference=prefab==null?null:prefab.FindModelTransform(templateTransform);
        authoredScale=scaleWithModel
            ? (reference==null?part.transform.InverseTransformVector(template.parent.TransformVector(Vector3.up)).magnitude
              :prefab.transform.InverseTransformVector(reference.parent.TransformVector(Vector3.up)).magnitude)
            :1f;
        ReadScale();
        foreach(Transform child in template.parent)
            if(child.name.StartsWith("TankExtension_"))copies.Add(child.gameObject);
        if(upperOnly) {
            if(structuralBayLength<=0)throw new ArgumentException("upperOnly requires structuralBayLength");
            foreach(string entry in heightFollowers.Split(',')) {
                if(string.IsNullOrEmpty(entry.Trim()))continue;
                string[] values=entry.Trim().Split(':');float fraction;
                if((values.Length!=2 && values.Length!=3) || !float.TryParse(values[1],NumberStyles.Float,CultureInfo.InvariantCulture,out fraction) || fraction<0 || fraction>1)
                    throw new ArgumentException("Invalid heightFollowers entry: "+entry);
                Transform follower=part.FindModelTransform(values[0]);
                if(follower==null || follower==lower || follower==upper)throw new ArgumentException("Missing or conflicting height follower: "+entry);
                followers.Add(new KeyValuePair<Transform,float>(follower,fraction));
                if(values.Length==3) {
                    float step;
                    if(!float.TryParse(values[2],NumberStyles.Float,CultureInfo.InvariantCulture,out step) || step<=0)
                        throw new ArgumentException("Invalid height follower step: "+entry);
                    followerSteps[follower]=step;
                }
            }
        }
        initialized=true;
    }
    private void ReadScale()
    {
        scaleRatio=1f;
        if(tweakScale!=null) {
            // Rescaled persists a ratio; original TweakScale persists diameter.
            FieldInfo ratio=tweakScale.GetType().GetField("currentScaleFactor");
            if(ratio!=null)scaleRatio=Convert.ToSingle(ratio.GetValue(tweakScale));
            else {
                FieldInfo current=tweakScale.GetType().GetField("currentScale"),baseline=tweakScale.GetType().GetField("defaultScale");
                if(current!=null&&baseline!=null) {
                    float denominator=Convert.ToSingle(baseline.GetValue(tweakScale));
                    if(denominator>0)scaleRatio=Convert.ToSingle(current.GetValue(tweakScale))/denominator;
                }
            }
        }
        if(float.IsNaN(scaleRatio)||float.IsInfinity(scaleRatio)||scaleRatio<=0)scaleRatio=1f;
        modelScale=authoredScale*scaleRatio;
    }
    [KSPEvent(guiActive=false,guiActiveEditor=false,active=true)]
    public void OnPartScaleChanged(BaseEventDetails data)
    {
        // Rescaled sends this BEFORE its RF handler, /L AFTER it. Reconcile once
        // all handlers have completed so RF cannot erase the extension volume.
        scalePending=true;
    }
    private void SynchronizeScale(bool moveAttached)
    {
        ReadScale();
        // TweakScale already moved radial attachments with the model. Only fix
        // axial nodes which /L restores from the unextended prefab on each scale.
        ApplyGeometry(segments,moveAttached,false);
        UpdateCapacity(segments);
        NotifyModelChanged();
        RefreshAerodynamics();
        part.UpdateMass();
        if(HighLogic.LoadedSceneIsEditor&&EditorLogic.fetch!=null)
            GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }
    private double VolumeScale { get { return (double)scaleRatio*scaleRatio*scaleRatio; } }
    [KSPEvent(guiActiveEditor=false,guiName="Add extension",active=false)]
    public void AddExtension() { SetSegmentCount(segments+1,true); }
    [KSPEvent(guiActiveEditor=false,guiName="Remove extension",active=false)]
    public void RemoveExtension() { SetSegmentCount(segments-1,true); }
    public void SetSegmentCount(int count,bool symmetry)
    {
        if(HighLogic.LoadedSceneIsFlight)return; // No in-flight resizing/refueling.
        count=Math.Max(0,Math.Min(maxSegments,count));
        InitializeModel();
        ReadScale();
        // Validate/update MFT before committing geometry; missing API must not silently use stock fuel.
        UpdateCapacity(count);
        ApplyGeometry(count,HighLogic.LoadedSceneIsEditor);
        NotifyModelChanged();
        segments=count;
        extensionCount=ControlValue(count);
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
        ApplyGeometry(count,moveAttached,true);
    }
    private void ApplyGeometry(int count,bool moveAttached,bool moveRadial)
    {
        InitializeModel();
        int low=upperOnly?0:LowerCount(count),high=upperOnly?count:UpperCount(count);
        float down=low*segmentLength,up=high*segmentLength;
        float baseBottom=-baseHeight*originFraction,baseTop=baseBottom+baseHeight;
        float newBottom=(baseBottom-down)*modelScale,newTop=(baseTop+up)*modelScale;
        AttachNode bn=part.FindAttachNode(bottomNode),tn=part.FindAttachNode(topNode);
        Vector3 bd=bn==null?Vector3.zero:Vector3.up*(newBottom-bn.position.y);
        Vector3 td=tn==null?Vector3.zero:Vector3.up*(newTop-tn.position.y);
        // Classify radial attachments before moving the parent tree. Compare
        // against the current cap locations, not coordinates from another size.
        var radial=new List<KeyValuePair<Part,Vector3>>();
        if(moveAttached&&moveRadial) foreach(Part child in part.children) {
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
        // Rescaled has a public cache of unscaled node positions. Keep it in sync
        // when the length changes; /L has no such API and is corrected above.
        if(tweakScale!=null) {
            MethodInfo setNode=tweakScale.GetType().GetMethod("SetUnscaledAttachNodePosition",new[]{typeof(string),typeof(Vector3)});
            if(setNode!=null) {
                if(bn!=null)setNode.Invoke(tweakScale,new object[]{bn.id,bn.position/scaleRatio});
                if(tn!=null)setNode.Invoke(tweakScale,new object[]{tn.id,tn.position/scaleRatio});
            }
        }
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
        if(upperOnly) {
            float total=count*segmentLength;
            int bays=Mathf.FloorToInt((total+0.0001f)/structuralBayLength);
            float remainder=Mathf.Max(0,total-bays*structuralBayLength);
            int needed=bays+(remainder>0.0001f?1:0);
            for(int i=0;i<needed;i++) {
                float length=i<bays?structuralBayLength:remainder;
                GameObject obj=Instantiate(template.gameObject);obj.name="TankExtension_"+i;
                obj.transform.SetParent(template.parent,false);
                obj.transform.localRotation=template.localRotation;
                Vector3 scale=template.localScale;scale.y*=length/structuralBayLength;
                obj.transform.localScale=scale;
                obj.transform.localPosition=Vector3.up*(extensionStart+i*structuralBayLength);
                obj.SetActive(true);copies.Add(obj);
            }
            foreach(var follower in followers) {
                float offset=total*follower.Value,step;
                if(followerSteps.TryGetValue(follower.Key,out step))offset=Mathf.Ceil((offset-.00001f)/step)*step;
                follower.Key.localPosition=Vector3.up*offset;
            }
        } else for(int i=0;i<count;i++) {
            bool below=(i%2)==0;int index=i/2;
            GameObject obj=Instantiate(template.gameObject);obj.name="TankExtension_"+i;
            obj.transform.SetParent(template.parent,false);obj.transform.localRotation=template.localRotation;
            obj.transform.localScale=Vector3.one;
            obj.transform.localPosition=Vector3.up*(below?baseBottom+bottomCapHeight-(index+1)*segmentLength:baseBottom+bottomCapHeight+mainBodyHeight+index*segmentLength);
            obj.SetActive(true);copies.Add(obj);
        }
        ApplyServiceHeights(baseHeight+count*segmentLength,moveAttached);
        appliedDown=down;appliedUp=up;
        float center=(-baseHeight*originFraction+(baseHeight+up-down)*.5f)*modelScale;
        part.CoMOffset=new Vector3(0,center,0);part.CoLOffset=part.CoMOffset;part.CoPOffset=part.CoMOffset;
        tankLength=(baseHeight+count*segmentLength)*modelScale;layout=low+" / "+high;
        tankDiameter=baseDiameter*modelScale;
        UpdateTankSpec(count);
        extensionCount=ControlValue(count);
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
    private void NotifyModelChanged()
    {
        if(!notifyB9ModelChanges)return;
        // B9 material switches must discover newly instantiated extension renderers.
        // Use the public KSP event; no hard reference to the optional B9 assembly.
        foreach(PartModule module in part.Modules)
            if(module.moduleName=="ModuleB9PartSwitch" && module.Events.Contains("OnPartModelChanged"))
                module.Events["OnPartModelChanged"].Invoke();
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
            double target=(realFuelsBaseVolume+count*realFuelsVolumePerSegment)*VolumeScale;
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
                double capacity=(spec.baseline+count*spec.increment)*VolumeScale;
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
        return (float)(((rf?realFuelsBaseDryMass:baseDryMass)+count*(rf?realFuelsDryMassPerSegment:dryMassPerSegment))*VolumeScale);
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
    public float GetModuleCost(float defaultCost,ModifierStagingSituation sit)
    {
        // Our full-capacity cost follows the same cubic rule as tankage. The /L
        // cost modifier additionally prices resized resources even with exponent
        // zero; compensate only that module, leaving RF/B9/inventory costs alone.
        float scaleCost=0;
        IPartCostModifier modifier=tweakScale as IPartCostModifier;
        if(modifier!=null)scaleCost=modifier.GetModuleCost(defaultCost,sit);
        return (float)((defaultCost+segments*costPerSegment)*VolumeScale-defaultCost)-scaleCost;
    }
    public ModifierChangeWhen GetModuleCostChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
    public Vector3 GetModuleSize(Vector3 defaultSize,ModifierStagingSituation sit) { return upperOnly?Vector3.zero:Vector3.up*(segments*segmentLength*modelScale); }
    public ModifierChangeWhen GetModuleSizeChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }    // Optional tower controls. Tank configs opt out by default.
    [KSPField] public bool selectableHeightStep = false;
    [KSPField(isPersistant=true, guiActiveEditor=false, guiName="调节粒度")]
    [UI_ChooseOption(scene=UI_Scene.Editor,options=new string[]{"0.1","0.5","1"}, display=new string[]{"0.1 m","0.5 m","1 m"})]
    public string heightStep = "0.1";
    [KSPField(isPersistant=true, guiActiveEditor=false, guiName="Service 1",guiFormat="F1")]
    [UI_FloatEdit(scene=UI_Scene.Editor,minValue=0,maxValue=200,incrementLarge=0,incrementSmall=.1f,incrementSlide=.1f,sigFigs=5,unit="m")]
    public float serviceHeight0 = -1f;
    [KSPField(isPersistant=true, guiActiveEditor=false, guiName="Service 2",guiFormat="F1")]
    [UI_FloatEdit(scene=UI_Scene.Editor,minValue=0,maxValue=200,incrementLarge=0,incrementSmall=.1f,incrementSlide=.1f,sigFigs=5,unit="m")]
    public float serviceHeight1 = -1f;
    [KSPField(isPersistant=true, guiActiveEditor=false, guiName="Service 3",guiFormat="F1")]
    [UI_FloatEdit(scene=UI_Scene.Editor,minValue=0,maxValue=200,incrementLarge=0,incrementSmall=.1f,incrementSlide=.1f,sigFigs=5,unit="m")]
    public float serviceHeight2 = -1f;
    [KSPField] public string heightRails = "";
    private class ServiceHeightSpec {
        public int slot,below=-1;
        public string rootName,optionName,label;
        public float authored,minimum,topMargin,gap;
        public Transform root,option;
        public string FieldName { get { return "serviceHeight"+slot; } }
    }
    private readonly List<ServiceHeightSpec> serviceHeights=new List<ServiceHeightSpec>();
    private readonly List<Transform> serviceRails=new List<Transform>();
    private bool servicesReady;
    private float nextServiceCheck;
    private readonly Dictionary<string,float> heightAnchors=new Dictionary<string,float>();
    private readonly Dictionary<string,UIPartActionFloatEdit> heightWidgets=new Dictionary<string,UIPartActionFloatEdit>();
    private float SnapHeight(string name,float value) {
        float anchor; if(!heightAnchors.TryGetValue(name,out anchor))anchor=0;
        return Mathf.Round((anchor+Mathf.Round((value-anchor)/HeightIncrement)*HeightIncrement)*10f)/10f;
    }
    private void ResetHeightAnchors() {
        heightAnchors["extensionCount"]=ControlValue(segments);
        foreach(var s in serviceHeights)heightAnchors[s.FieldName]=GetServiceHeight(s);
        heightWidgets.Clear();
    }
    private void LayoutHeightWidget(string name,UIPartActionFloatEdit item) {
        if(item==null)return;
        item.incLarge.gameObject.SetActive(false);item.decLarge.gameObject.SetActive(false);
        item.incSmall.gameObject.SetActive(true);item.decSmall.gameObject.SetActive(true);
        item.incSmall.transform.localPosition=item.incLarge.transform.localPosition;
        item.decSmall.transform.localPosition=item.decLarge.transform.localPosition;
        item.slider.GetComponent<RectTransform>().sizeDelta=new Vector2(-44,0);
        UIPartActionFloatEdit previous;
        if(heightWidgets.TryGetValue(name,out previous)&&previous==item)return;
        heightWidgets[name]=item;
        item.incSmall.onToggle.RemoveAllListeners();item.decSmall.onToggle.RemoveAllListeners();
        item.incSmall.onToggle.AddListener(()=>StepHeight(name,1));
        item.decSmall.onToggle.AddListener(()=>StepHeight(name,-1));
    }
    private void StepHeight(string name,int direction) {
        float old=Convert.ToSingle(Fields[name].GetValue(this));
        Fields[name].SetValue(Mathf.Round((old+direction*HeightIncrement)*10f)/10f,this);
        if(name=="extensionCount")OnSegmentControlChanged(Fields[name],old);else OnServiceHeightChanged(Fields[name],old);
        heightWidgets.Clear();MonoUtilities.RefreshContextWindows(part);
    }
    private float HeightIncrement { get { return heightStep=="1"?1f:heightStep=="0.5"?.5f:.1f; } }
    private void ReadServiceHeights(ConfigNode node) {
        ConfigNode[] specs=node.GetNodes("SERVICE_HEIGHT");
        if(specs.Length==0 && part!=null && part.partInfo!=null && part.partInfo.partConfig!=null)
            foreach(ConfigNode module in part.partInfo.partConfig.GetNodes("MODULE"))
                if(module.GetValue("name")==GetType().Name){specs=module.GetNodes("SERVICE_HEIGHT");break;}
        serviceHeights.Clear();
        foreach(ConfigNode n in specs) {
            var s=new ServiceHeightSpec();
            n.TryGetValue("slot",ref s.slot);n.TryGetValue("authoredHeight",ref s.authored);
            n.TryGetValue("minimum",ref s.minimum);n.TryGetValue("topMargin",ref s.topMargin);
            n.TryGetValue("aboveSlot",ref s.below);n.TryGetValue("separation",ref s.gap);
            s.rootName=n.GetValue("transform");s.optionName=n.GetValue("installedTransform");s.label=n.GetValue("label");
            if(s.slot<0||s.slot>2||s.minimum<0||s.topMargin<0||string.IsNullOrEmpty(s.rootName)||serviceHeights.Exists(v=>v.slot==s.slot))
                throw new ArgumentException("Invalid SERVICE_HEIGHT configuration");
            serviceHeights.Add(s);
        }
        serviceHeights.Sort((a,b)=>a.slot.CompareTo(b.slot));
    }
    private void ConfigureStructureControls() {
        bool enabled=upperOnly && selectableHeightStep;
        Fields["heightStep"].guiActiveEditor=enabled;
        UI_ChooseOption choice=Fields["heightStep"].uiControlEditor as UI_ChooseOption;
        if(choice!=null){choice.scene=enabled?UI_Scene.Editor:UI_Scene.None;choice.affectSymCounterparts=UI_Scene.None;choice.onFieldChanged=OnHeightStepChanged;}
        for(int i=0;i<3;i++)SetServiceFieldVisible(Fields["serviceHeight"+i],false);
        if(heightStep!="0.1"&&heightStep!="0.5"&&heightStep!="1")heightStep="0.1";
        if(!enabled)return;
        foreach(var s in serviceHeights) {
            s.root=part.FindModelTransform(s.rootName);s.option=part.FindModelTransform(s.optionName);
            if(s.root==null||s.option==null)throw new InvalidOperationException("Missing service height root: "+s.rootName);
            Fields[s.FieldName].guiName=s.label;
            if(GetServiceHeight(s)<0)SetServiceHeight(s,s.authored);
            UI_FloatEdit ui=Fields[s.FieldName].uiControlEditor as UI_FloatEdit;
            if(ui!=null){ui.affectSymCounterparts=UI_Scene.None;ui.onFieldChanged=OnServiceHeightChanged;}
        }
        foreach(string name in heightRails.Split(','))if(!string.IsNullOrEmpty(name.Trim())) {
            Transform rail=part.FindModelTransform(name.Trim());
            if(rail==null)throw new InvalidOperationException("Missing service rail: "+name);
            serviceRails.Add(rail);
        }
        servicesReady=true;
        ApplyServiceHeights(baseHeight+segments*segmentLength,false);
        ResetHeightAnchors();
    }
    private float GetServiceHeight(ServiceHeightSpec s){return Convert.ToSingle(Fields[s.FieldName].GetValue(this));}
    private void SetServiceHeight(ServiceHeightSpec s,float value){Fields[s.FieldName].SetValue(value,this);}
    private void OnHeightStepChanged(BaseField field,object previous) {
        ResetHeightAnchors();
        ConfigureSegmentControl();
        ConfigureServiceUI(baseHeight+segments*segmentLength);
        heightWidgets.Clear();MonoUtilities.RefreshContextWindows(part);
        if(HighLogic.LoadedSceneIsEditor && EditorLogic.fetch!=null)GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }
    private void ServiceBounds(ServiceHeightSpec s,float height,out float min,out float max) {
        min=s.minimum;max=Mathf.Max(min,height-s.topMargin);
        if(s.below>=0){var b=serviceHeights.Find(x=>x.slot==s.below);if(b!=null)min=Mathf.Max(min,GetServiceHeight(b)+s.gap);}
        foreach(var above in serviceHeights)if(above.below==s.slot)max=Mathf.Min(max,GetServiceHeight(above)-above.gap);
        min=Mathf.Min(min,max);
    }
    private void ConfigureServiceUI(float height) {
        if(!servicesReady)return;
        foreach(var s in serviceHeights) {
            UI_FloatEdit ui=Fields[s.FieldName].uiControlEditor as UI_FloatEdit;
            float min,max;ServiceBounds(s,height,out min,out max);
            if(ui!=null){ui.minValue=min;ui.maxValue=max;ui.incrementLarge=0;ui.incrementSmall=HeightIncrement;ui.incrementSlide=0;ui.sigFigs=1;ui.useSI=false;ui.unit=" m";}
            SetServiceFieldVisible(Fields[s.FieldName],s.option.gameObject.activeInHierarchy);
        }
    }
    private void OnServiceHeightChanged(BaseField field,object previous) {
        if(!HighLogic.LoadedSceneIsEditor || !servicesReady)return;
        var s=serviceHeights.Find(x=>x.FieldName==field.name);if(s==null)return;
        float min,max;ServiceBounds(s,baseHeight+segments*segmentLength,out min,out max);
        SetServiceHeight(s,Mathf.Clamp(SnapHeight(s.FieldName,GetServiceHeight(s)),min,max));
        ApplyServiceHeights(baseHeight+segments*segmentLength,false);
        heightWidgets.Clear();MonoUtilities.RefreshContextWindows(part);
        if(EditorLogic.fetch!=null)GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }
    private void ApplyServiceHeights(float height,bool notifyClamp) {
        if(!servicesReady)return;
        bool clamped=false;
        foreach(var s in serviceHeights) {
            float old=GetServiceHeight(s),v=Mathf.Clamp(old,s.minimum,Mathf.Max(s.minimum,height-s.topMargin));
            SetServiceHeight(s,v);clamped|=Mathf.Abs(v-old)>.001f;
        }
        // Upper stations retain their bounded height; lower stations move down
        // only when shortening the tower makes their minimum separation invalid.
        for(int i=serviceHeights.Count-1;i>=0;i--) {
            var s=serviceHeights[i];if(s.below<0)continue;
            var lowerService=serviceHeights.Find(x=>x.slot==s.below);if(lowerService==null)continue;
            float old=GetServiceHeight(lowerService),v=Mathf.Min(old,GetServiceHeight(s)-s.gap);
            v=Mathf.Max(lowerService.minimum,v);SetServiceHeight(lowerService,v);clamped|=Mathf.Abs(v-old)>.001f;
        }
        foreach(var s in serviceHeights)s.root.localPosition=Vector3.up*(GetServiceHeight(s)-s.authored);
        foreach(var rail in serviceRails){Vector3 scale=rail.localScale;scale.y=height;rail.localScale=scale;}
        ConfigureServiceUI(height);
        if(clamped && notifyClamp && HighLogic.LoadedSceneIsEditor)
            ScreenMessages.PostScreenMessage("塔段缩短：超出行程的摆臂已限位，请检查接口高度。",4f,ScreenMessageStyle.UPPER_CENTER);
    }
    private void UpdateServiceVisibility() {
        if(!servicesReady || Time.unscaledTime<nextServiceCheck)return;
        nextServiceCheck=Time.unscaledTime+.25f;bool changed=false;
        foreach(var s in serviceHeights) {
            bool visible=s.option.gameObject.activeInHierarchy;
            if(Fields[s.FieldName].guiActiveEditor!=visible){SetServiceFieldVisible(Fields[s.FieldName],visible);changed=true;}
            UI_FloatEdit ui=Fields[s.FieldName].uiControlEditor as UI_FloatEdit;
            UIPartActionFloatEdit item=ui==null?null:ui.partActionItem as UIPartActionFloatEdit;
            LayoutHeightWidget(s.FieldName,item);
        }
        if(changed)MonoUtilities.RefreshContextWindows(part);
    }
    private static void SetServiceFieldVisible(BaseField field,bool visible) {
        field.guiActiveEditor=visible;field.guiActive=false;
        if(field.uiControlEditor!=null){field.uiControlEditor.scene=visible?UI_Scene.Editor:UI_Scene.None;field.uiControlEditor.controlEnabled=visible;}
        if(field.uiControlFlight!=null)field.uiControlFlight.scene=UI_Scene.None;
    }


}

// /L 2.4.x rejects every B9+MFT combination by module name, including switches
// that only replace paint. Keep its other checks and its genuine fuel-switch
// rejection intact. Use the host's optional Harmony 2 via reflection so neither
// TweakScale nor Harmony becomes a load-time dependency of ConfigurableTank.
[KSPAddon(KSPAddon.Startup.Instantly,true)]
public class ConfigurableTankTweakScaleCompatibility : MonoBehaviour
{
    public void Awake()
    {
        Type writer=null,harmony=null,harmonyMethod=null;
        foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            if(writer==null)writer=assembly.GetType("TweakScale.PrefabDryCostWriter",false);
            if(harmony==null)harmony=assembly.GetType("HarmonyLib.Harmony",false);
            if(harmonyMethod==null)harmonyMethod=assembly.GetType("HarmonyLib.HarmonyMethod",false);
        }
        if(writer==null)return; // Rescaled has no such blanket exclusion.
        if(harmony==null||harmonyMethod==null) {
            Debug.LogWarning("[ConfigurableTank] Original TweakScale with RF + visual B9 switches requires Harmony 2 (000_Harmony). Other tank modes are unaffected.");
            return;
        }
        try {
            MethodInfo original=writer.GetMethod("checkForSanity",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(Part)},null);
            if(original==null||original.ReturnType!=typeof(string))return;
            object instance=Activator.CreateInstance(harmony,new object[]{"KIU.ConfigurableTank.VisualB9"});
            object postfix=Activator.CreateInstance(harmonyMethod,new object[]{typeof(ConfigurableTankTweakScaleCompatibility).GetMethod("AllowVisualSwitches")});
            foreach(MethodInfo patch in harmony.GetMethods())if(patch.Name=="Patch") {
                ParameterInfo[] parameters=patch.GetParameters();
                if(parameters.Length<3||parameters[0].ParameterType!=typeof(MethodBase))continue;
                object[] args=new object[parameters.Length];args[0]=original;
                bool found=false;
                for(int i=1;i<parameters.Length;i++)if(parameters[i].Name=="postfix"){args[i]=postfix;found=true;}
                if(!found)continue;
                patch.Invoke(instance,args);
                Debug.Log("[ConfigurableTank] Original TweakScale visual-only B9/MFT compatibility installed.");
                break;
            }
        } catch(Exception e) {
            Debug.LogError("[ConfigurableTank] Could not install optional TweakScale/B9 compatibility: "+e);
        }
    }
    public static void AllowVisualSwitches(Part p,ref string __result)
    {
        if(__result==null||!__result.StartsWith("having ModuleB9PartSwitch together ModuleFuelTanks"))return;
        if(p==null||p.FindModuleImplementing<ConfigurableTank>()==null||p.partInfo==null||p.partInfo.partConfig==null)return;
        bool found=false;
        foreach(ConfigNode module in p.partInfo.partConfig.GetNodes("MODULE")) {
            if(module.GetValue("name")!="ModuleB9PartSwitch")continue;
            found=true;
            // Opt in only to material/visibility-only switches. Any B9 capacity,
            // resource, module, node or balance change keeps /L's rejection.
            foreach(ConfigNode child in module.nodes)if(child.name!="SUBTYPE")return;
            foreach(ConfigNode.Value value in module.values) {
                if(value.name=="baseVolume"&&value.value.Trim()!="0")return;
                if(Array.IndexOf(new[]{"name","moduleID","switcherDescription","switcherDescriptionPlural","baseVolume",
                    "affectDragCubes","affectFARVoxels","switchInFlight","uiGroupName","uiGroupDisplayName","advancedTweakablesOnly"},value.name)<0)return;
            }
            foreach(ConfigNode subtype in module.GetNodes("SUBTYPE")) {
                foreach(ConfigNode child in subtype.nodes)if(child.name!="MATERIAL")return;
                foreach(ConfigNode.Value value in subtype.values)
                    if(value.name!="name"&&value.name!="title"&&value.name!="descriptionSummary"&&value.name!="descriptionDetail"
                        &&value.name!="defaultSubtypePriority"&&value.name!="primaryColor"&&value.name!="secondaryColor"&&value.name!="transform")return;
            }
        }
        if(found) {
            __result=null;
            Debug.Log("[ConfigurableTank] Allowed visual-only B9 with RF on "+p.partInfo.name+"; ConfigurableTank owns volume/mass/cost.");
        }
    }
}
