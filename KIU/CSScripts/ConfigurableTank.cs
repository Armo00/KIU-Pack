using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using UnityEngine;

// Generic, cfg-driven stepped tank. No CZ10B dimensions, fuel names or balance values.
public partial class ConfigurableTank : PartModule, IPartMassModifier, IPartCostModifier, IPartSizeModifier
{
    [KSPField(isPersistant=true)]
    public int segments;
    [KSPField] public int maxSegments=30;
    [KSPField(guiActiveEditor=true, guiName="Extension segments", guiFormat="F0")]
    [UI_FloatEdit(minValue=0, maxValue=30, incrementLarge=0, incrementSmall=1, incrementSlide=0)]
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
    // Optional authored per-end profiles. Empty keeps the existing shared model.
    [KSPField] public string lowerTemplateTransform="";
    [KSPField] public string upperTemplateTransform="";
    [KSPField] public bool useTerminalSurfaceVariants=false;
    [KSPField] public string surfaceSingleTransform="surfaceSingle";
    [KSPField] public string surfaceFirstTransform="surfaceFirst";
    [KSPField] public string surfaceMiddleTransform="surfaceMiddle";
    [KSPField] public string surfaceLastTransform="surfaceLast";
    [KSPField] public string bodyNormalTransform="";
    [KSPField] public string baseNormalTextureURL="";
    [KSPField] public string extendedNormalTextureURL="";
    [KSPField] public string bottomNode="bottom";
    [KSPField] public string topNode="top";
    // Optional additional nodes following the same lower/upper end.
    [KSPField] public string extraLowerNodes="";
    [KSPField] public string extraUpperNodes="";
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
    [KSPField] public string lastBayHiddenTransforms = "";
    [KSPField] public bool showResourceMode = true;
    [KSPField] public bool displayHeightInMeters = false;
    // Comma-separated authored-zero roots, each with a height-following fraction.
    // Example: "MidService:0.5". Top service geometry lives below topTransform.
    [KSPField] public string heightFollowers = "";
    private readonly List<KeyValuePair<Transform,float>> followers = new List<KeyValuePair<Transform,float>>();
    private readonly Dictionary<Transform,float> followerSteps = new Dictionary<Transform,float>();
    [KSPField] public string extensionLabel = "Extension segments";
    [KSPField] public string specLabel = "Tank Spec";
    [KSPField] public string modeLabel = "Tank mode";
    [KSPField] public string layoutLabel = "Layout (lower / upper)";
    [KSPField] public string addLabel = "Add extension";
    [KSPField] public string removeLabel = "Remove extension";
    [KSPField] public bool showDimensionControls = true;
    private ConfigNode loadedState;
    public event Action<ConfigurableTank> DimensionsChanged;
    public float CurrentModelHeight { get { return baseHeight+segments*segmentLength; } }
    public float MaximumModelHeight { get { return baseHeight+maxSegments*segmentLength; } }
    public bool ModelInitialized { get { return initialized; } }
    // Consumers moving or switching existing geometry use the same deferred
    // cache/highlight reconciliation as a dimension change.
    public void RequestRendererRefresh() { InvalidateRendererCaches();rendererRefreshPending=true; }
    // Generic load snapshot: consumers own interpretation of legacy values.
    public ConfigNode GetLoadedStateSnapshot() { return loadedState==null?null:loadedState.CreateCopy(); }
    private void PublishDimensionsChanged() {
        var changed=DimensionsChanged;if(changed==null)return;
        foreach(Action<ConfigurableTank> handler in changed.GetInvocationList())
            try { handler(this); } catch(Exception e) { Debug.LogError("[ConfigurableTank] Dimension consumer failed: "+e); }
    }
    private Transform lower,upper,body,template,lowerTemplate,upperTemplate;
    private Renderer bodyNormalRenderer;
    private Texture originalBodyNormal,extendedBodyNormal;
    private Vector3 originalLower,originalUpper;
    private bool initialized;
    private bool rendererRefreshPending;
    private float modelScale=1;
    private float authoredScale=1, scaleRatio=1;
    private bool modulesStarted, scalePending;
    private PartModule tweakScale;
    private float appliedDown,appliedUp;
    private readonly List<GameObject> copies=new List<GameObject>();
    // Structure mode keeps complete bays alive across 0.1 m height changes.
    // Ordinary alternating tank extensions retain their existing path.
    private sealed class StructureBayState {
        public readonly List<GameObject> endObjects=new List<GameObject>();
        public readonly List<bool> authoredActive=new List<bool>();
        public bool roleKnown,last;
    }
    private readonly Dictionary<GameObject,StructureBayState> structureBays=new Dictionary<GameObject,StructureBayState>();
    private bool structureAerodynamicsPending;
    private float structureAerodynamicsAfter;
    private readonly List<ResourceSpec> resources=new List<ResourceSpec>();
    private class ResourceSpec { public string name; public double baseline,increment; }
    public static int LowerCount(int count) { return (count+1)/2; }
    public static int UpperCount(int count) { return count/2; }




    public override void OnLoad(ConfigNode node)
    {
        base.OnLoad(node);
        ReadResources(node);
        loadedState=node.CreateCopy();
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
        Fields["layout"].guiActiveEditor=showLayout&&!upperOnly;
        Fields["tankSpec"].guiActiveEditor=showSpec;
        Fields["tankSpec"].guiActive=showSpec;
        maxSegments=Math.Max(0,maxSegments);
        Fields["extensionCount"].guiActiveEditor=showDimensionControls;
        ConfigureSegmentControl();
        InitializeModel();

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
        // FloatEdit uses incrementSmall as its moving slider interval whenever
        // incrementLarge is nonzero. Zero selects the complete min/max range.
        // Snap relative to baseHeight in our callback, not to global metre zero.
        float step=displayHeightInMeters?segmentLength:1f;
        ui.incrementLarge=0;ui.incrementSmall=step;ui.incrementSlide=0;
        if(displayHeightInMeters){Fields["extensionCount"].guiFormat="F1";ui.sigFigs=1;ui.useSI=false;ui.unit=" m";}

        ui.affectSymCounterparts=UI_Scene.None;
        ui.onFieldChanged=OnSegmentControlChanged;
        extensionCount=ControlValue(Math.Max(0,Math.Min(maxSegments,segments)));
    }
    public void OnSegmentControlChanged(BaseField field,object previous)
    {
        if(synchronizingSegmentWidget)return;
        int requested=Mathf.Clamp(Mathf.RoundToInt(displayHeightInMeters?(extensionCount-baseHeight)/segmentLength:extensionCount),0,maxSegments);
        if(requested!=segments)SetSegmentCount(requested,true);
        extensionCount=ControlValue(segments);

    }



    public void LateUpdate()
    {

        if(modulesStarted && scalePending) {
            scalePending=false;
            SynchronizeScale(HighLogic.LoadedSceneIsEditor);
        }
        if(rendererRefreshPending) {
            rendererRefreshPending=false;
            RefreshRenderers();
        }

        if(structureAerodynamicsPending && Time.unscaledTime>=structureAerodynamicsAfter) {
            structureAerodynamicsPending=false;
            RebuildAerodynamics();
        }
        SynchronizeSegmentWidget();
    }
    private UIPartActionFloatEdit segmentWidget;
    private bool synchronizingSegmentWidget;
    private static readonly FieldInfo SegmentSliderBlock=typeof(UIPartActionFloatEdit).GetField("blockSliderUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
    private void SynchronizeSegmentWidget()
    {
        UI_FloatEdit ui=Fields["extensionCount"].uiControlEditor as UI_FloatEdit;
        UIPartActionFloatEdit item=ui==null?null:ui.partActionItem as UIPartActionFloatEdit;
        if(item==null||item.slider==null)return;
        // The visible PAW can own a copy of the field's UI descriptor.
        UI_FloatEdit shown=item.Control as UI_FloatEdit;
        if(shown!=null) {
            shown.minValue=ui.minValue;shown.maxValue=ui.maxValue;
            shown.incrementLarge=0;shown.incrementSmall=ui.incrementSmall;shown.incrementSlide=0;
            shown.onFieldChanged=OnSegmentControlChanged;shown.affectSymCounterparts=UI_Scene.None;
        }
        bool blocked=SegmentSliderBlock!=null&&(bool)SegmentSliderBlock.GetValue(item);
        synchronizingSegmentWidget=true;
        if(SegmentSliderBlock!=null)SegmentSliderBlock.SetValue(item,true);
        try {
            extensionCount=ControlValue(segments);
            item.slider.minValue=ui.minValue;item.slider.maxValue=ui.maxValue;
            item.slider.SetValueWithoutNotify(extensionCount);
            item.slider.interactable=maxSegments>0;
            if(item.fieldValue!=null)item.fieldValue.text=KSPUtil.LocalizeNumber(extensionCount,displayHeightInMeters?"F1":"F0")+(displayHeightInMeters?" m":"");
        } finally {
            if(SegmentSliderBlock!=null)SegmentSliderBlock.SetValue(item,blocked);
            synchronizingSegmentWidget=false;
        }
        // Stock full-range mode hides all arrows. Restore only our single-step
        // pair; native FloatEdit arrow snapping is also anchored to zero and
        // cannot step a fractional base height reliably.
        if(item.incLarge!=null)item.incLarge.gameObject.SetActive(false);
        if(item.decLarge!=null)item.decLarge.gameObject.SetActive(false);
        if(item.incSmall!=null) {
            item.incSmall.gameObject.SetActive(true);
            if(item.incLarge!=null)PositionSegmentButton(item.incSmall.transform as RectTransform,item.incLarge.transform as RectTransform);
            var button=item.incSmall.GetComponent<UnityEngine.UI.Button>();if(button!=null)button.interactable=segments<maxSegments;
        }
        if(item.decSmall!=null) {
            item.decSmall.gameObject.SetActive(true);
            if(item.decLarge!=null)PositionSegmentButton(item.decSmall.transform as RectTransform,item.decLarge.transform as RectTransform);
            var button=item.decSmall.GetComponent<UnityEngine.UI.Button>();if(button!=null)button.interactable=segments>0;
        }
        item.slider.GetComponent<RectTransform>().sizeDelta=new Vector2(-44,0);
        if(segmentWidget==item)return;
        segmentWidget=item;
        if(item.incSmall!=null){item.incSmall.onToggle.RemoveListener(item.OnTap_incSmall);item.incSmall.onToggle.AddListener(StepSegmentUp);}
        if(item.decSmall!=null){item.decSmall.onToggle.RemoveListener(item.OnTap_decSmall);item.decSmall.onToggle.AddListener(StepSegmentDown);}
    }
    private static void PositionSegmentButton(RectTransform button,RectTransform slot)
    {
        if(button==null||slot==null)return;
        button.anchorMin=slot.anchorMin;button.anchorMax=slot.anchorMax;button.pivot=slot.pivot;
        button.sizeDelta=slot.sizeDelta;button.anchoredPosition=slot.anchoredPosition;
    }
    private void StepSegmentUp() { if(segments<maxSegments)SetSegmentCount(segments+1,true); }
    private void StepSegmentDown() { if(segments>0)SetSegmentCount(segments-1,true); }
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
        if(useFixedAnchorSpan) { InitializeFixedAnchorSpan(); return; }
        if(segmentLength<=0||baseHeight<=0||bottomCapHeight<=0||mainBodyHeight<=0)
            throw new ArgumentException("Invalid modular tank dimensions in cfg");
        lower=part.FindModelTransform(bottomTransform);upper=part.FindModelTransform(topTransform);
        body=part.FindModelTransform(bodyTransform);
        lowerTemplate=part.FindModelTransform(string.IsNullOrEmpty(lowerTemplateTransform)?templateTransform:lowerTemplateTransform);
        upperTemplate=part.FindModelTransform(string.IsNullOrEmpty(upperTemplateTransform)?templateTransform:upperTemplateTransform);
        template=lowerTemplate;
        if(lower==null||upper==null||body==null||lowerTemplate==null||upperTemplate==null)throw new InvalidOperationException("Missing modular tank model transforms");
        // Authored section roots are at zero; do not inherit an already extended symmetry clone's offsets.
        originalLower=Vector3.zero;originalUpper=Vector3.zero;
        template.gameObject.SetActive(false);
        upperTemplate.gameObject.SetActive(false);
        if(useTerminalSurfaceVariants){ValidateSurfaceVariants(lowerTemplate);if(upperTemplate!=lowerTemplate)ValidateSurfaceVariants(upperTemplate);}
        InitializeBodyNormal();
        foreach(PartModule module in part.Modules)if(module.moduleName=="TweakScale")tweakScale=module;
        // Read the authored model scale from the prefab, never from a scaled clone.
        Part prefab=part.partInfo==null?null:part.partInfo.partPrefab;
        Transform reference=prefab==null?null:prefab.FindModelTransform(string.IsNullOrEmpty(lowerTemplateTransform)?templateTransform:lowerTemplateTransform);
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
        InitializeContinuousColliders();
        initialized=true;
    }
    private void ValidateSurfaceVariants(Transform source)
    {
        string[] names={surfaceSingleTransform,surfaceFirstTransform,surfaceMiddleTransform,surfaceLastTransform};
        for(int i=0;i<names.Length;i++) {
            if(string.IsNullOrEmpty(names[i]))throw new ArgumentException("Empty terminal surface variant name");
            for(int j=0;j<i;j++)if(names[i]==names[j])throw new ArgumentException("Terminal surface variant names must be distinct");
            int matches=0;
            foreach(Transform child in source.GetComponentsInChildren<Transform>(true))if(child!=source&&child.name==names[i])matches++;
            if(matches!=1)throw new InvalidOperationException("Template "+source.name+" requires exactly one "+names[i]+" transform; found "+matches);
        }
    }
    private void SelectSurfaceVariant(GameObject instance,int spanIndex,int spanCount)
    {
        if(!useTerminalSurfaceVariants)return;
        string selected=spanCount==1?surfaceSingleTransform:spanIndex==0?surfaceFirstTransform:spanIndex==spanCount-1?surfaceLastTransform:surfaceMiddleTransform;
        foreach(Transform child in instance.GetComponentsInChildren<Transform>(true))
            if(child.name==surfaceSingleTransform||child.name==surfaceFirstTransform||child.name==surfaceMiddleTransform||child.name==surfaceLastTransform)
                child.gameObject.SetActive(child.name==selected);
    }
    private void InitializeBodyNormal()
    {
        if(string.IsNullOrEmpty(bodyNormalTransform))return;
        if(string.IsNullOrEmpty(baseNormalTextureURL)||string.IsNullOrEmpty(extendedNormalTextureURL))
            throw new ArgumentException("Body normal switching requires both baseNormalTextureURL and extendedNormalTextureURL");
        Transform target=part.FindModelTransform(bodyNormalTransform);
        bodyNormalRenderer=target==null?null:target.GetComponent<Renderer>();
        if(bodyNormalRenderer==null)throw new InvalidOperationException("Missing body normal renderer: "+bodyNormalTransform);
        originalBodyNormal=GameDatabase.Instance.GetTexture(baseNormalTextureURL,false);
        extendedBodyNormal=GameDatabase.Instance.GetTexture(extendedNormalTextureURL,false);
        if(originalBodyNormal==null||extendedBodyNormal==null)throw new InvalidOperationException("Missing body normal texture URL");
        bool found=false;
        foreach(Material material in bodyNormalRenderer.sharedMaterials)if(material!=null&&material.HasProperty("_BumpMap"))found=true;
        if(!found)throw new InvalidOperationException("Body normal renderer has no _BumpMap material: "+bodyNormalTransform);
    }
    private void ApplyBodyNormal(int upperCount)
    {
        if(bodyNormalRenderer==null)return;
        // Fetch current instances because visual switch modules may refresh their
        // own material references between geometry changes. The zero/low-only
        // states always restore the exact original GameDatabase texture.
        Texture normal=upperCount>0?extendedBodyNormal:originalBodyNormal;
        foreach(Material material in bodyNormalRenderer.materials)
            if(material!=null&&material.HasProperty("_BumpMap"))material.SetTexture("_BumpMap",normal);
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
        segments=count;
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
    private static bool ListedNode(string ids,AttachNode node)
    {
        if(node==null||string.IsNullOrEmpty(ids))return false;
        foreach(string id in ids.Split(','))if(id.Trim()==node.id)return true;
        return false;
    }
    private void UpdateExtraNodes(string ids,float offset,AttachNode bn,AttachNode tn)
    {
        if(string.IsNullOrEmpty(ids))return;
        Part prefab=part.partInfo==null?null:part.partInfo.partPrefab;
        if(prefab==null)throw new InvalidOperationException("Extra nodes require a prefab baseline");
        foreach(string token in ids.Split(',')) {
            string id=token.Trim();if(id.Length==0)continue;
            AttachNode node=part.FindAttachNode(id),reference=prefab.FindAttachNode(id);
            if(node==null||reference==null)throw new InvalidOperationException("Missing configured extra node: "+id);
            if(node==bn||node==tn)continue;
            // Absolute prefab-based restoration also repairs saved legacy caches;
            // never adopt an already extended craft node as a new baseline.
            Vector3 position=reference.position*scaleRatio;position.y+=offset;
            node.position=position;node.originalPosition=position;
            if(tweakScale!=null) {
                MethodInfo setNode=tweakScale.GetType().GetMethod("SetUnscaledAttachNodePosition",new[]{typeof(string),typeof(Vector3)});
                if(setNode!=null)setNode.Invoke(tweakScale,new object[]{id,position/scaleRatio});
            }
        }
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
        if(useFixedAnchorSpan) { ApplyFixedAnchorSpan(count); return; }
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
            AttachNode childNode=part.FindAttachNodeByPart(child);
            if(ListedNode(extraLowerNodes,childNode)){radial.Add(new KeyValuePair<Part,Vector3>(child,bd));continue;}
            if(ListedNode(extraUpperNodes,childNode)){radial.Add(new KeyValuePair<Part,Vector3>(child,td));continue;}
            float y=part.transform.InverseTransformPoint(child.transform.position).y;
            if(y<(baseBottom+bottomCapHeight-appliedDown)*modelScale)radial.Add(new KeyValuePair<Part,Vector3>(child,bd));
            else if(y>(baseBottom+bottomCapHeight+mainBodyHeight+appliedUp)*modelScale)radial.Add(new KeyValuePair<Part,Vector3>(child,td));
        }
        if(moveAttached) {
            Vector3 anchor=Vector3.zero;
            if(bn!=null&&bn.attachedPart==part.parent)anchor=bd;
            else if(tn!=null&&tn.attachedPart==part.parent)anchor=td;
            else if(part.parent!=null) {
                AttachNode parentNode=part.FindAttachNodeByPart(part.parent);
                if(ListedNode(extraLowerNodes,parentNode))anchor=bd;
                else if(ListedNode(extraUpperNodes,parentNode))anchor=td;
            }
            if(anchor!=Vector3.zero)MoveTree(part,-part.transform.TransformVector(anchor));
            if(bn!=null&&bn.attachedPart!=null&&bn.attachedPart!=part.parent)MoveTree(bn.attachedPart,part.transform.TransformVector(bd));
            if(tn!=null&&tn.attachedPart!=null&&tn.attachedPart!=part.parent)MoveTree(tn.attachedPart,part.transform.TransformVector(td));
            foreach(var pair in radial)MoveTree(pair.Key,part.transform.TransformVector(pair.Value));
        }
        if(bn!=null){bn.position=new Vector3(bn.position.x,newBottom,bn.position.z);bn.originalPosition=bn.position;}
        if(tn!=null){tn.position=new Vector3(tn.position.x,newTop,tn.position.z);tn.originalPosition=tn.position;}
        UpdateExtraNodes(extraLowerNodes,-down*modelScale,bn,tn);
        UpdateExtraNodes(extraUpperNodes,up*modelScale,bn,tn);
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
        bool structureTopologyChanged=false;
        if(!upperOnly) {
            foreach(GameObject old in copies)RetireExtension(old);
            copies.Clear();
        }
        if(upperOnly) {
            float total=count*segmentLength;
            structureTopologyChanged=UpdateStructureBays(total);
            foreach(var follower in followers) {
                float offset=total*follower.Value,step;
                if(followerSteps.TryGetValue(follower.Key,out step))offset=Mathf.Ceil((offset-.00001f)/step)*step;
                follower.Key.localPosition=Vector3.up*offset;
            }
        } else for(int i=0;i<count;i++) {
            bool below=(i%2)==0;int index=i/2;
            Transform source=below?lowerTemplate:upperTemplate;
            GameObject obj=Instantiate(source.gameObject);obj.name="TankExtension_"+i;
            obj.transform.SetParent(source.parent,false);obj.transform.localRotation=source.localRotation;
            obj.transform.localScale=Vector3.one;
            obj.transform.localPosition=Vector3.up*(below?baseBottom+bottomCapHeight-(index+1)*segmentLength:baseBottom+bottomCapHeight+mainBodyHeight+index*segmentLength);
            SelectSurfaceVariant(obj,below?low-1-index:index,below?low:high);
            StripExtensionColliders(obj);
            obj.SetActive(true);copies.Add(obj);
        }
        UpdateContinuousColliders(down,up);
        ApplyBodyNormal(high);

        appliedDown=down;appliedUp=up;
        float center=(-baseHeight*originFraction+(baseHeight+up-down)*.5f)*modelScale;
        part.CoMOffset=new Vector3(0,center,0);part.CoLOffset=part.CoMOffset;part.CoPOffset=part.CoMOffset;
        tankLength=(baseHeight+count*segmentLength)*modelScale;layout=low+" / "+high;
        tankDiameter=baseDiameter*modelScale;
        UpdateTankSpec(count);
        segments=count;
        extensionCount=ControlValue(count);
        Events["AddExtension"].active=false;Events["RemoveExtension"].active=false;
        // Pose changes update Unity bounds directly; rebuild renderer/highlight
        // membership only if a bay or its final header changed activation.
        if(!upperOnly || structureTopologyChanged) {
            InvalidateRendererCaches();
            rendererRefreshPending=true;
        }
        PublishDimensionsChanged();
    }

    private static void RetireExtension(GameObject old) {
        if(old==null)return;
        old.SetActive(false);
        // Destroy is deferred. Retired colliders/renderers must leave part
        // searches immediately, including multiple adjustments in one frame.
        old.transform.SetParent(null,false);
        UnityEngine.Object.Destroy(old);
    }
    private StructureBayState CaptureStructureBay(GameObject instance) {
        var state=new StructureBayState();
        if(!string.IsNullOrEmpty(lastBayHiddenTransforms)) {
            var names=new HashSet<string>();
            foreach(string token in lastBayHiddenTransforms.Split(','))if(token.Trim().Length>0)names.Add(token.Trim());
            foreach(Transform child in instance.GetComponentsInChildren<Transform>(true)) {
                if(child==instance.transform || !names.Contains(child.name))continue;
                string path=child.name;Transform parent=child.parent;
                while(parent!=instance.transform) { path=parent.name+"/"+path;parent=parent.parent; }
                Transform authored=template.Find(path);
                if(authored==null)throw new InvalidOperationException("Missing structure header template: "+path);
                state.endObjects.Add(child.gameObject);state.authoredActive.Add(authored.gameObject.activeSelf);
            }
        }
        structureBays.Add(instance,state);return state;
    }
    private bool UpdateStructureBays(float total) {
        int full=Mathf.FloorToInt((total+0.0001f)/structuralBayLength);
        float remainder=Mathf.Max(0,total-full*structuralBayLength);
        int needed=full+(remainder>0.0001f?1:0);
        bool changed=false;
        // Existing VAB symmetry clones can already contain generated bays.
        // Adopt them in numeric order instead of destroying/recreating them.
        if(structureBays.Count==0 && copies.Count>1)
            copies.Sort((a,b)=>StructureBayIndex(a).CompareTo(StructureBayIndex(b)));
        for(int i=copies.Count-1;i>=needed;i--) {
            GameObject old=copies[i];if(old!=null)structureBays.Remove(old);
            RetireExtension(old);copies.RemoveAt(i);changed=true;
        }
        for(int i=0;i<needed;i++) {
            if(i==copies.Count)copies.Add(null);
            GameObject obj=copies[i];
            if(obj==null) {
                obj=Instantiate(template.gameObject);obj.name="TankExtension_"+i;
                obj.transform.SetParent(template.parent,false);copies[i]=obj;changed=true;
            }
            StructureBayState state;
            if(!structureBays.TryGetValue(obj,out state))state=CaptureStructureBay(obj);
            bool last=i==needed-1;
            if(!state.roleKnown || state.last!=last) {
                for(int j=0;j<state.endObjects.Count;j++) {
                    bool active=!last && state.authoredActive[j];
                    if(state.endObjects[j].activeSelf!=active){state.endObjects[j].SetActive(active);changed=true;}
                }
                state.roleKnown=true;state.last=last;
            }
            float length=i<full?structuralBayLength:remainder;
            Vector3 scale=template.localScale;scale.y*=length/structuralBayLength;
            Vector3 position=Vector3.up*(extensionStart+i*structuralBayLength);
            if(obj.transform.localRotation!=template.localRotation)obj.transform.localRotation=template.localRotation;
            if(obj.transform.localScale!=scale)obj.transform.localScale=scale;
            if(obj.transform.localPosition!=position)obj.transform.localPosition=position;
            if(!obj.activeSelf){obj.SetActive(true);changed=true;}
        }
        return changed;
    }
    private static int StructureBayIndex(GameObject obj) {
        int index;
        return obj!=null && int.TryParse(obj.name.Substring("TankExtension_".Length),out index)?index:int.MaxValue;
    }
    private void InvalidateRendererCaches() { KIUModelRenderers.Invalidate(part); }

    private void NotifyModelChanged()
    {
        if(!notifyB9ModelChanges)return;
        // B9 material switches must discover newly instantiated extension renderers.
        // Use the public KSP event; no hard reference to the optional B9 assembly.
        foreach(PartModule module in part.Modules)
            if(module.moduleName=="ModuleB9PartSwitch" && module.Events.Contains("OnPartModelChanged"))
                module.Events["OnPartModelChanged"].Invoke();
    }

    private void RefreshRenderers() { KIUModelRenderers.Refresh(part); }

    private PartModule FuelModule()
    {
        foreach(PartModule module in part.Modules)if(module.moduleName=="ModuleFuelTanks")return module;
        return null;
    }
    public void UpdateCapacity(int count)
    {
        if(useFixedAnchorSpan) { UpdateTankSpec(count); return; }
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
        bool rf=!useFixedAnchorSpan && FuelModule()!=null;
        return (float)(((rf?realFuelsBaseDryMass:baseDryMass)+count*(rf?realFuelsDryMassPerSegment:dryMassPerSegment))*VolumeScale);
    }
    private void UpdateTankSpec(int count)
    {
        tankSpec=string.Format(CultureInfo.InvariantCulture,"{0:F1}m/{1:F1}t",(baseHeight+count*segmentLength)*modelScale,DryMassForCount(count));
    }
    private void RefreshAerodynamics()
    {
        // Continuous tower dragging must not synchronously render six drag-cube
        // faces for every 0.1 m tick. Geometry/nodes still update immediately.
        // Flight initialization and all ordinary tank behavior stay synchronous.
        if(upperOnly && HighLogic.LoadedSceneIsEditor) {
            structureAerodynamicsPending=true;
            structureAerodynamicsAfter=Time.unscaledTime+.15f;
            return;
        }
        RebuildAerodynamics();
    }
    private void RebuildAerodynamics()
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
    public Vector3 GetModuleSize(Vector3 defaultSize,ModifierStagingSituation sit) { return upperOnly?Vector3.zero:Vector3.up*((segments-(useFixedAnchorSpan?referenceSegments:0))*segmentLength*modelScale); }
    public ModifierChangeWhen GetModuleSizeChangeWhen() { return ModifierChangeWhen.CONSTANTLY; }
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
            // /L's standard scaler restores attachment nodes on its first
            // editor update only. KSP can reset them after flight OnLoad while
            // keeping the correctly scaled model and attached-part positions.
            Type scaleType=writer.Assembly.GetType("TweakScale.TweakScale",false);
            MethodInfo first=scaleType==null?null:scaleType.GetMethod("UpdateFirst",BindingFlags.Instance|BindingFlags.NonPublic);
            if(first!=null) {
                object nodePostfix=Activator.CreateInstance(harmonyMethod,new object[]{typeof(ConfigurableTankTweakScaleCompatibility).GetMethod("RestoreOptedInFlightNodes")});
                foreach(MethodInfo patch in harmony.GetMethods())if(patch.Name=="Patch") {
                    ParameterInfo[] parameters=patch.GetParameters();
                    if(parameters.Length<3||parameters[0].ParameterType!=typeof(MethodBase))continue;
                    object[] args=new object[parameters.Length];args[0]=first;bool found=false;
                    for(int i=1;i<parameters.Length;i++)if(parameters[i].Name=="postfix"){args[i]=nodePostfix;found=true;}
                    if(!found)continue;
                    patch.Invoke(instance,args);
                    Debug.Log("[ConfigurableTank] Original TweakScale opted-in flight node restoration installed.");
                    break;
                }
            }
        } catch(Exception e) {
            Debug.LogError("[ConfigurableTank] Could not install optional TweakScale/B9 compatibility: "+e);
        }
    }
    public static void RestoreOptedInFlightNodes(object __instance)
    {
        if(!HighLogic.LoadedSceneIsFlight)return;
        PartModule module=__instance as PartModule;Part p=module==null?null:module.part;
        if(p==null||p.partInfo==null||p.partInfo.partConfig==null||p.FindModuleImplementing<ConfigurableTank>()!=null)return;
        ConfigNode config=p.partInfo.partConfig;
        if(!String.Equals(config.GetValue("KIURestoreScaledNodes"),"true",StringComparison.OrdinalIgnoreCase))return;
        bool hasB9=false;foreach(ConfigNode m in config.GetNodes("MODULE"))if(m.GetValue("name")=="ModuleB9PartSwitch")hasB9=true;
        if(hasB9&&!IsVisualOnlyB9(config))return; // Never overwrite a functional node switch.
        try {
            FieldInfo scalerField=__instance.GetType().GetField("scaler",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            object scaler=scalerField==null?null:scalerField.GetValue(__instance);
            MethodInfo restore=scaler==null?null:scaler.GetType().GetMethod("MoveAttachmentNodes",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(bool),typeof(bool)},null);
            if(restore==null)return;
            // Use /L's own absolute-scale node calculation. No part movement,
            // fuel/mass/cost update, transform rewrite or joint manipulation.
            restore.Invoke(scaler,new object[]{false,true});
            Debug.Log("[ConfigurableTank] Restored scaled flight attachment nodes on "+p.partInfo.name);
        } catch(Exception e) {
            Debug.LogError("[ConfigurableTank] Could not restore opted-in flight nodes: "+e);
        }
    }
    public static void AllowVisualSwitches(Part p,ref string __result)
    {
        if(__result==null||!__result.StartsWith("having ModuleB9PartSwitch together ModuleFuelTanks"))return;
        if(p==null||p.partInfo==null||p.partInfo.partConfig==null)return;
        bool configurable=p.FindModuleImplementing<ConfigurableTank>()!=null;
        // Explicitly opted-in RF tanks can also have purely visual B9 switches.
        // This does not grant ConfigurableTank's volume/mass/cost handling to them;
        // RF and TweakScale retain their normal responsibilities on these Parts.
        bool visualRfOptIn=String.Equals(p.partInfo.partConfig.GetValue("KIUVisualB9RFCompatibility"),"true",StringComparison.OrdinalIgnoreCase);
        if(!configurable&&!visualRfOptIn)return;
        if(IsVisualOnlyB9(p.partInfo.partConfig)) {
            __result=null;
            Debug.Log("[ConfigurableTank] Allowed visual-only B9 with RF on "+p.partInfo.name+
                (configurable?"; ConfigurableTank owns volume/mass/cost.":"; explicit visual-RF opt-in; RF/TweakScale own volume/mass/cost."));
        }
    }
    // Keep the configuration guard independent of live Unity objects so the
    // opt-in cannot accidentally grow into a blanket bypass of /L's checks.
    internal static bool IsVisualOnlyB9(ConfigNode config)
    {
        if(config==null)return false;
        bool found=false;
        foreach(ConfigNode module in config.GetNodes("MODULE")) {
            if(module.GetValue("name")!="ModuleB9PartSwitch")continue;
            found=true;
            // Opt in only to material/visibility-only switches. Any B9 capacity,
            // resource, module, node or balance change keeps /L's rejection.
            foreach(ConfigNode child in module.nodes)if(child.name!="SUBTYPE")return false;
            foreach(ConfigNode.Value value in module.values) {
                if(value.name=="baseVolume"&&value.value.Trim()!="0")return false;
                if(Array.IndexOf(new[]{"name","moduleID","switcherDescription","switcherDescriptionPlural","baseVolume",
                    "affectDragCubes","affectFARVoxels","switchInFlight","uiGroupName","uiGroupDisplayName","advancedTweakablesOnly"},value.name)<0)return false;
            }
            foreach(ConfigNode subtype in module.GetNodes("SUBTYPE")) {
                foreach(ConfigNode child in subtype.nodes)if(child.name!="MATERIAL")return false;
                foreach(ConfigNode.Value value in subtype.values)
                    if(value.name!="name"&&value.name!="title"&&value.name!="descriptionSummary"&&value.name!="descriptionDetail"
                        &&value.name!="defaultSubtypePriority"&&value.name!="primaryColor"&&value.name!="secondaryColor"&&value.name!="transform")return false;
            }
        }
        return found;
    }
}
