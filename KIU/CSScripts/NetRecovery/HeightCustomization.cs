using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.Localization;
namespace KIU.NetRecovery {
 public partial class LHZNetController : IPartSizeModifier {
  [KSPField(isPersistant=true)] public int heightSegments;
  [KSPField(guiActiveEditor=true,guiName="#LHZ_HeightSegments",guiFormat="F0")]
  [UI_FloatEdit(minValue=0,maxValue=15,incrementLarge=16,incrementSmall=1,incrementSlide=1)]
  public float heightSelection;
  [KSPField(guiActiveEditor=true,guiActive=true,guiName="#LHZ_HeightSpec")] public string heightSpec;
  public float HeightOffset {get{return heightSegments;}}
  public float CapturePlaneHeight {get{return BasePlane+HeightOffset;}}
  Transform heightUpper,heightTemplate,heightStays;int builtHeight=-1;
  readonly List<GameObject> heightSections=new List<GameObject>();
  void InitializeHeight(){
   heightUpper=part.FindModelTransform("LHZHeightUpper");heightTemplate=part.FindModelTransform("LHZHeightTemplate");heightStays=part.FindModelTransform("LHZHeightStays");
   if(heightUpper==null||heightTemplate==null||heightStays==null){heightSegments=0;heightSelection=0;Fields["heightSelection"].guiActiveEditor=false;heightSpec="Height model groups missing";Debug.LogError("[KIUNetRecovery] Height model groups missing; extension disabled");return;}
   heightSections.Clear();for(int i=0;i<15;i++){var t=heightTemplate.parent.Find("LHZHeightSection_"+i);if(t==null)break;heightSections.Add(t.gameObject);}
   var ui=Fields["heightSelection"].uiControlEditor as UI_FloatEdit;if(ui!=null){ui.minValue=0;ui.maxValue=15;ui.incrementLarge=16;ui.incrementSmall=ui.incrementSlide=1;ui.affectSymCounterparts=UI_Scene.None;ui.onFieldChanged=OnHeightChanged;}
   SetHeightSegments(heightSegments);
  }
  public void OnHeightChanged(BaseField field,object previous){SetHeightSegments(Mathf.RoundToInt(heightSelection));if(HighLogic.LoadedSceneIsEditor&&EditorLogic.fetch!=null)GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);}
  public void SetHeightSegments(int count){
   if(HighLogic.LoadedSceneIsFlight&&lockedHook!=null)return;
   if(heightUpper==null||heightTemplate==null||heightStays==null){heightSegments=0;heightSelection=0;return;}
   heightSegments=Mathf.Clamp(count,0,15);heightSelection=heightSegments;
   heightSpec=Localizer.Format("#LHZ_HeightValue",heightSegments.ToString(),CapturePlaneHeight.ToString("F1"));
   if(heightUpper==null||heightTemplate==null||heightStays==null)return;
   int before=builtHeight>=0?builtHeight:Mathf.RoundToInt(heightUpper.localPosition.y);
   for(int i=heightSections.Count;i<heightSegments;i++){
    var copy=UnityEngine.Object.Instantiate(heightTemplate.gameObject);copy.name="LHZHeightSection_"+i;copy.transform.SetParent(heightTemplate.parent,false);heightSections.Add(copy);
   }
   heightTemplate.gameObject.SetActive(false);
   for(int i=0;i<heightSections.Count;i++){
    var t=heightSections[i].transform;t.localPosition=heightTemplate.localPosition+Vector3.up*i;t.localRotation=heightTemplate.localRotation;t.localScale=Vector3.one;heightSections[i].SetActive(i<heightSegments);
   }
   // Fixed authoring origins make editor duplication and saved-ship reload
   // idempotent; do not adopt an already raised transform as the new baseline.
   heightUpper.localPosition=Vector3.up*HeightOffset;heightStays.localScale=new Vector3(1,(48+HeightOffset)/48,1);
   // Loaded craft children already have their saved positions. Only an editor
   // change after initial restoration should move attached child assemblies.
   float attachedMove=builtHeight>=0?heightSegments-before:0;
   SetHeightNode("top",82+HeightOffset,attachedMove);SetHeightNode("lhzCapture",CapturePlaneHeight,attachedMove);
   builtHeight=heightSegments;if(ropes!=null)DrawNet();
  }
  void SetHeightNode(string id,float y,float delta){
   var n=part.FindAttachNode(id);if(n==null)return;
   // A flight capture node is an actual saved native joint anchor.
   if(HighLogic.LoadedSceneIsFlight&&n.attachedPart!=null)return;
   n.position=new Vector3(n.position.x,y,n.position.z);n.originalPosition=n.position;
   if(HighLogic.LoadedSceneIsEditor&&n.attachedPart!=null&&n.attachedPart!=part.parent&&delta!=0)MoveHeightChildren(n.attachedPart,part.transform.TransformVector(Vector3.up*delta));
  }
  static void MoveHeightChildren(Part p,Vector3 move){var ps=Subtree(p).ToArray();var positions=ps.Select(q=>q.transform.position).ToArray();for(int i=0;i<ps.Length;i++)ps[i].transform.position=positions[i]+move;}
  public Vector3 GetModuleSize(Vector3 defaultSize,ModifierStagingSituation sit){return Vector3.up*HeightOffset;}
  public ModifierChangeWhen GetModuleSizeChangeWhen(){return ModifierChangeWhen.CONSTANTLY;}
 }
}
