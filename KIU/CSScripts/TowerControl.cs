using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// Tower service policy. Generic dimension/capacity/rendering belongs to ConfigurableTank.
public class TowerControl : PartModule {
    [KSPField(guiActiveEditor=true,guiName="塔段高度（米）",guiFormat="F1")]
    [UI_FloatEdit(scene=UI_Scene.Editor,minValue=0,maxValue=200,incrementLarge=0,incrementSmall=.1f,incrementSlide=0,sigFigs=1,unit=" m")]
    public float extensionCount;
    [KSPField] public string launchRetractAnimations="";
    [KSPField(isPersistant=true)] public bool launchRetractionRequested;
    [KSPField(isPersistant=true)] public int stateVersion;
    [KSPField(isPersistant=true)] public bool legacyStateMigrated;
    private ConfigurableTank dimensions;
    private bool heightUIRefreshPending,launchEventsRegistered;
    private Vessel launchVessel;
    private readonly List<Part> launchClamps=new List<Part>();
    private float baseHeight { get { return dimensions.baseHeight; } }
    private float segmentLength { get { return dimensions.segmentLength; } }
    private int segments { get { return dimensions.segments; } }
    private bool upperOnly { get { return dimensions.upperOnly; } }
    private float CurrentHeight { get { return dimensions.CurrentModelHeight; } }
    private static readonly FieldInfo SliderBlock=typeof(UIPartActionFloatEdit).GetField("blockSliderUpdate",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
    public override void OnLoad(ConfigNode node) {
        base.OnLoad(node);if(!node.HasValue("stateVersion"))stateVersion=0;ReadServiceHeights(node);
    }
    public override void OnStart(StartState state) {
        base.OnStart(state);dimensions=part.FindModuleImplementing<ConfigurableTank>();
        if(dimensions==null)throw new InvalidOperationException("TowerControl requires ConfigurableTank dimensions");
        dimensions.DimensionsChanged+=OnDimensionsChanged;
        dimensions.showDimensionControls=false;Fields["extensionCount"].guiName=KSP.Localization.Localizer.Format(dimensions.extensionLabel);
        dimensions.Fields["extensionCount"].guiActiveEditor=false;
        StartCoroutine(InitializeAfterDimensions());
    }
    private IEnumerator InitializeAfterDimensions() {
        yield return null;
        if(!dimensions.ModelInitialized)dimensions.InitializeModel();
        MigrateLegacyState();ConfigureSegmentControl();ConfigureStructureControls();InitializeLaunchRetraction();
        ApplyServiceHeights(CurrentHeight,false);ResetHeightAnchors();QueueHeightUIRefresh();
    }
    private void MigrateLegacyState() {
        if(stateVersion>=1)return;
        var old=dimensions.GetLoadedStateSnapshot();bool migrated=false;
        if(old!=null) {
            string step=old.GetValue("heightStep");if(step=="0.1"||step=="0.5"||step=="1"){heightStep=step;migrated=true;}
            for(int i=0;i<3;i++){float v=-1;if(old.TryGetValue("serviceHeight"+i,ref v)&&v>=0){Fields["serviceHeight"+i].SetValue(v,this);migrated=true;}}
            bool released=false;if(old.TryGetValue("launchRetractionRequested",ref released)){launchRetractionRequested=released;migrated=true;}
        }
        legacyStateMigrated=migrated;stateVersion=1;
        if(migrated)Debug.Log("[TowerControl] Migrated legacy tower state on "+part.name);
    }
    public override void OnActive(){RetractLaunchArms();}
    public void OnDestroy(){if(dimensions!=null)dimensions.DimensionsChanged-=OnDimensionsChanged;if(launchEventsRegistered)GameEvents.onStageActivate.Remove(OnLaunchStage);}
    private void OnDimensionsChanged(ConfigurableTank source) {
        extensionCount=source.CurrentModelHeight;if(!servicesReady)return;
        ApplyServiceHeights(extensionCount,HighLogic.LoadedSceneIsEditor);ConfigureSegmentControl();QueueHeightUIRefresh();
    }
    private float ControlValue(int count){return baseHeight+count*segmentLength;}
    private void ConfigureSegmentControl() {
        extensionCount=CurrentHeight;var ui=Fields["extensionCount"].uiControlEditor as UI_FloatEdit;
        if(ui==null)return;ui.minValue=baseHeight;ui.maxValue=dimensions.MaximumModelHeight;
        ui.incrementLarge=0;ui.incrementSmall=HeightIncrement;ui.incrementSlide=0;ui.sigFigs=1;ui.useSI=false;ui.unit=" m";
        ui.affectSymCounterparts=UI_Scene.None;ui.onFieldChanged=OnSegmentControlChanged;
        SynchronizeWidget("extensionCount",ui.partActionItem as UIPartActionFloatEdit,ui);
    }
    public void OnSegmentControlChanged(BaseField field,object previous) {
        float requestedHeight=SnapHeight("extensionCount",extensionCount);
        int count=Mathf.Clamp(Mathf.RoundToInt((requestedHeight-baseHeight)/segmentLength),0,dimensions.maxSegments);
        dimensions.SetSegmentCount(count,true);extensionCount=CurrentHeight;
        ApplyServiceHeights(CurrentHeight,true);QueueHeightUIRefresh();
    }
    private void QueueHeightUIRefresh() {
        if(heightUIRefreshPending||!HighLogic.LoadedSceneIsEditor)return;
        heightUIRefreshPending=true;StartCoroutine(RefreshHeightUIAfterInput());
    }
    private IEnumerator RefreshHeightUIAfterInput() {
        yield return null;heightUIRefreshPending=false;
        ConfigureSegmentControl();ConfigureServiceUI(CurrentHeight);heightWidgets.Clear();MonoUtilities.RefreshContextWindows(part);
    }
    // Synchronize the actual PAW control as well as its BaseField descriptor.
    // Suppress slider callbacks while changing limits, so old range cannot write back.
    private void SynchronizeWidget(string name,UIPartActionFloatEdit item,UI_FloatEdit source) {
        if(item==null||source==null||item.slider==null)return;
        var shown=item.Control as UI_FloatEdit;
        if(shown!=null){shown.minValue=source.minValue;shown.maxValue=source.maxValue;shown.incrementLarge=0;shown.incrementSmall=HeightIncrement;shown.incrementSlide=0;
            if(name=="extensionCount")shown.onFieldChanged=OnSegmentControlChanged;else shown.onFieldChanged=OnServiceHeightChanged;}
        bool blocked=SliderBlock!=null&&(bool)SliderBlock.GetValue(item);
        if(SliderBlock!=null)SliderBlock.SetValue(item,true);
        try {
            float v=Convert.ToSingle(Fields[name].GetValue(this));
            item.slider.minValue=source.minValue;item.slider.maxValue=source.maxValue;item.slider.SetValueWithoutNotify(v);
            if(item.fieldValue!=null)item.fieldValue.text=KSPUtil.LocalizeNumber(v,"F1")+" m";
            var plus=item.incSmall==null?null:item.incSmall.GetComponent<Button>();var minus=item.decSmall==null?null:item.decSmall.GetComponent<Button>();
            if(plus!=null)plus.interactable=v<source.maxValue-.0001f;if(minus!=null)minus.interactable=v>source.minValue+.0001f;
        }finally{if(SliderBlock!=null)SliderBlock.SetValue(item,blocked);}
    }
    public Vector2 GetServiceHeightLimits(int slot) {
        var s=serviceHeights.Find(x=>x.slot==slot);if(s==null)throw new ArgumentOutOfRangeException("slot");
        float min,max;ServiceBounds(s,CurrentHeight,out min,out max);return new Vector2(min,max);
    }
    public void LateUpdate() {
        if(!servicesReady)return;
        if(Mathf.Abs(extensionCount-CurrentHeight)>.001f&&!heightUIRefreshPending)OnDimensionsChanged(dimensions);
        ConfigureSegmentControl();UpdateServiceVisibility();
        var ui=Fields["extensionCount"].uiControlEditor as UI_FloatEdit;var item=ui==null?null:ui.partActionItem as UIPartActionFloatEdit;
        LayoutHeightWidget("extensionCount",item);SynchronizeWidget("extensionCount",item,ui);
    }
private void InitializeLaunchRetraction()
    {
        if (string.IsNullOrEmpty(launchRetractAnimations)) return;
        foreach (PartModule module in part.Modules) {
            ModuleAnimateGeneric motion=module as ModuleAnimateGeneric;
            if (motion==null || Array.IndexOf(launchRetractAnimations.Split(','),motion.animationName)<0) continue;
            motion.allowDeployLimit=false;
            motion.deployPercent=100f;
            motion.Fields["deployPercent"].guiActive=false;
            motion.Fields["deployPercent"].guiActiveEditor=false;
        }
        if (!HighLogic.LoadedSceneIsFlight || launchEventsRegistered) return;
        launchVessel=part.vessel;
        launchClamps.Clear();
        if (launchVessel!=null)
            foreach (Part p in launchVessel.parts)
                foreach (PartModule m in p.Modules)
                    if (m is LaunchClamp && m.stagingEnabled) { launchClamps.Add(p); break; }
        GameEvents.onStageActivate.Add(OnLaunchStage);
        launchEventsRegistered=true;
    }
private void OnLaunchStage(int stage)
    {
        if (!HighLogic.LoadedSceneIsFlight || launchRetractionRequested) return;
        // Staging is global. Only the launch vessel that owns these service arms
        // may trigger them, including the frame in which its clamp detaches.
        bool ownsActive=FlightGlobals.ActiveVessel==launchVessel;
        foreach (Part clamp in launchClamps)
            if (clamp!=null && clamp.vessel==FlightGlobals.ActiveVessel) ownsActive=true;
        if (!ownsActive) return;
        foreach (Part clamp in launchClamps)
            if (clamp!=null && clamp.inverseStage==stage) { RetractLaunchArms(); return; }
    }
private void RetractLaunchArms()
    {
        if (!HighLogic.LoadedSceneIsFlight || string.IsNullOrEmpty(launchRetractAnimations) || launchRetractionRequested) return;
        launchRetractionRequested=true;
        foreach (string item in launchRetractAnimations.Split(','))
            foreach (PartModule module in part.Modules) {
                ModuleAnimateGeneric motion=module as ModuleAnimateGeneric;
                if (motion!=null && motion.animationName==item.Trim() && motion.animSwitch) motion.Toggle();
            }
        Debug.Log("[TowerControl] Launch service retraction: "+part.partInfo.name);
    }
    [KSPField] public bool selectableHeightStep = true;
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
        public bool relativeToTop;
        public string rootName,optionName,label;
        public float authored,minimum,topMargin,gap;
        public Transform root,option;
        public readonly List<ServiceHeightFollower> followers=new List<ServiceHeightFollower>();
        public string FieldName { get { return "serviceHeight"+slot; } }
    }
    private class ServiceHeightFollower {
        public string name;
        public Transform transform;
        public float positionFactor,positionOffset,scaleFactor,scaleOffset=1;
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
        if(specs.Length==0)return;
        serviceHeights.Clear();
        foreach(ConfigNode n in specs) {
            var s=new ServiceHeightSpec();
            n.TryGetValue("slot",ref s.slot);n.TryGetValue("authoredHeight",ref s.authored);
            n.TryGetValue("minimum",ref s.minimum);n.TryGetValue("topMargin",ref s.topMargin);
            n.TryGetValue("aboveSlot",ref s.below);n.TryGetValue("separation",ref s.gap);
            n.TryGetValue("relativeToTop",ref s.relativeToTop);
            s.rootName=n.GetValue("transform");s.optionName=n.GetValue("installedTransform");s.label=n.GetValue("label");
            if(s.slot<0||s.slot>2||s.minimum<0||s.topMargin<0||string.IsNullOrEmpty(s.rootName)||serviceHeights.Exists(v=>v.slot==s.slot))
                throw new ArgumentException("Invalid SERVICE_HEIGHT configuration");
            foreach(ConfigNode f in n.GetNodes("FOLLOWER")) {
                var follower=new ServiceHeightFollower();follower.name=f.GetValue("transform");
                f.TryGetValue("positionFactor",ref follower.positionFactor);f.TryGetValue("positionOffset",ref follower.positionOffset);
                f.TryGetValue("scaleFactor",ref follower.scaleFactor);f.TryGetValue("scaleOffset",ref follower.scaleOffset);
                if(string.IsNullOrEmpty(follower.name))throw new ArgumentException("Missing service follower transform");
                s.followers.Add(follower);
            }
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
        // KSP does not call OnLoad for every new editor clone. Re-read immutable
        // prefab config after partInfo exists, preserving craft height values.
        ReadServiceHeights(new ConfigNode());
        serviceRails.Clear();
        if(serviceHeights.Count==0 && !string.IsNullOrEmpty(heightRails))
            Debug.LogError("[TowerControl] Missing SERVICE_HEIGHT definitions on "+part.name);
        foreach(var s in serviceHeights) {
            s.root=part.FindModelTransform(s.rootName);s.option=part.FindModelTransform(s.optionName);
            if(s.root==null||s.option==null)throw new InvalidOperationException("Missing service height root: "+s.rootName);
            Fields[s.FieldName].guiName=s.label;
            if(GetServiceHeight(s)<0)SetServiceHeight(s,s.authored+(s.relativeToTop?segments*segmentLength:0));
            foreach(var f in s.followers) {
                f.transform=part.FindModelTransform(f.name);
                if(f.transform==null)throw new InvalidOperationException("Missing service height follower: "+f.name);
            }
            UI_FloatEdit ui=Fields[s.FieldName].uiControlEditor as UI_FloatEdit;
            if(ui!=null){ui.affectSymCounterparts=UI_Scene.None;ui.onFieldChanged=OnServiceHeightChanged;}
        }
        foreach(string name in heightRails.Split(','))if(!string.IsNullOrEmpty(name.Trim())) {
            Transform rail=part.FindModelTransform(name.Trim());
            if(rail==null)throw new InvalidOperationException("Missing service rail: "+name);
            serviceRails.Add(rail);
        }
        servicesReady=true;
        Debug.Log("[TowerControl] Service height controls initialized: "+part.name+" count="+serviceHeights.Count);
        ApplyServiceHeights(baseHeight+segments*segmentLength,false);
        ResetHeightAnchors();
    }
    private float GetServiceHeight(ServiceHeightSpec s){return Convert.ToSingle(Fields[s.FieldName].GetValue(this));}
    private void SetServiceHeight(ServiceHeightSpec s,float value){Fields[s.FieldName].SetValue(value,this);}

    private void OnHeightStepChanged(BaseField field,object previous) {
        ResetHeightAnchors();ConfigureSegmentControl();ConfigureServiceUI(CurrentHeight);
        heightWidgets.Clear();QueueHeightUIRefresh();
        if(HighLogic.LoadedSceneIsEditor&&EditorLogic.fetch!=null)GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);
    }

    private void ServiceBounds(ServiceHeightSpec s,float height,out float min,out float max) {
        min=s.minimum+(s.relativeToTop?height-baseHeight:0);max=Mathf.Max(min,height-s.topMargin);
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
            SynchronizeWidget(s.FieldName,ui==null?null:ui.partActionItem as UIPartActionFloatEdit,ui);
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
        QueueHeightUIRefresh();
    }
    private void ApplyServiceHeights(float height,bool notifyClamp) {
        if(!servicesReady)return;
        bool clamped=false;
        foreach(var s in serviceHeights) {
            float minimum=s.minimum+(s.relativeToTop?height-baseHeight:0);
            float old=GetServiceHeight(s),v=Mathf.Clamp(old,minimum,Mathf.Max(minimum,height-s.topMargin));
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
        foreach(var s in serviceHeights) {
            float relative=GetServiceHeight(s)-(s.relativeToTop?height-baseHeight:0);
            s.root.localPosition=Vector3.up*(relative-s.authored);
            foreach(var f in s.followers) {
                Vector3 p=f.transform.localPosition;p.y=relative*f.positionFactor+f.positionOffset;f.transform.localPosition=p;
                Vector3 scale=f.transform.localScale;scale.y=Mathf.Max(.001f,relative*f.scaleFactor+f.scaleOffset);f.transform.localScale=scale;
            }
        }
        foreach(var rail in serviceRails){Vector3 scale=rail.localScale;scale.y=height;rail.localScale=scale;}
        ConfigureServiceUI(height);
        if(clamped && notifyClamp && HighLogic.LoadedSceneIsEditor)
            ScreenMessages.PostScreenMessage("塔段高度改变：超出行程的摆臂已限位，请检查接口高度。",4f,ScreenMessageStyle.UPPER_CENTER);
    }
    private void UpdateServiceVisibility() {
        if(!servicesReady)return;
        ConfigureServiceUI(CurrentHeight);
        if(Time.unscaledTime<nextServiceCheck)return;
        nextServiceCheck=Time.unscaledTime+.25f;bool changed=false;
        foreach(var s in serviceHeights) {
            bool visible=s.option.gameObject.activeInHierarchy;
            if(Fields[s.FieldName].guiActiveEditor!=visible){SetServiceFieldVisible(Fields[s.FieldName],visible);changed=true;}
            UI_FloatEdit ui=Fields[s.FieldName].uiControlEditor as UI_FloatEdit;
            UIPartActionFloatEdit item=ui==null?null:ui.partActionItem as UIPartActionFloatEdit;
            LayoutHeightWidget(s.FieldName,item);
            SynchronizeWidget(s.FieldName,item,ui);
        }
        if(changed)MonoUtilities.RefreshContextWindows(part);
    }
    private static void SetServiceFieldVisible(BaseField field,bool visible) {
        field.guiActiveEditor=visible;field.guiActive=false;
        if(field.uiControlEditor!=null){field.uiControlEditor.scene=visible?UI_Scene.Editor:UI_Scene.None;field.uiControlEditor.controlEnabled=visible;}
        if(field.uiControlFlight!=null)field.uiControlFlight.scene=UI_Scene.None;
    }
}
