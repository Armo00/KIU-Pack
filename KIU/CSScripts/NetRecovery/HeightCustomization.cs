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
  readonly List<GameObject> heightBatches=new List<GameObject>();readonly List<Mesh> heightMeshes=new List<Mesh>();
  sealed class HeightLegCollider { public BoxCollider box; public Vector3 lower,upper; }
  readonly List<HeightLegCollider> heightLegColliders=new List<HeightLegCollider>();
  void InitializeHeight(){
   heightUpper=part.FindModelTransform("LHZHeightUpper");heightTemplate=part.FindModelTransform("LHZHeightTemplate");heightStays=part.FindModelTransform("LHZHeightStays");
   if(heightUpper==null||heightTemplate==null||heightStays==null){heightSegments=0;heightSelection=0;Fields["heightSelection"].guiActiveEditor=false;heightSpec="Height model groups missing";Debug.LogError("[KIUNetRecovery] Height model groups missing; extension disabled");return;}
   heightLegColliders.Clear();
   for(int i=0;i<4;i++){
    var t=part.FindModelTransform("COL_Leg_"+i+"_Continuous");if(t==null)continue;
    var box=t.GetComponent<BoxCollider>();var lower=t.Find("LHZCollisionLower");var upper=t.Find("LHZCollisionUpper");
    if(box!=null&&lower!=null&&upper!=null)heightLegColliders.Add(new HeightLegCollider{box=box,lower=lower.localPosition,upper=upper.localPosition});
   }
   var ui=Fields["heightSelection"].uiControlEditor as UI_FloatEdit;if(ui!=null){ui.minValue=0;ui.maxValue=15;ui.incrementLarge=16;ui.incrementSmall=ui.incrementSlide=1;ui.affectSymCounterparts=UI_Scene.None;ui.onFieldChanged=OnHeightChanged;}
   SetHeightSegments(heightSegments);
  }
  public void OnHeightChanged(BaseField field,object previous){SetHeightSegments(Mathf.RoundToInt(heightSelection));if(HighLogic.LoadedSceneIsEditor&&EditorLogic.fetch!=null)GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship);}
  public void SetHeightSegments(int count){
   if(HighLogic.LoadedSceneIsFlight&&lockedHook!=null)return;
   if(heightUpper==null||heightTemplate==null||heightStays==null){heightSegments=0;heightSelection=0;return;}
   heightSegments=Mathf.Clamp(count,0,15);heightSelection=heightSegments;
   heightSpec=Localizer.Format("#LHZ_HeightValue",heightSegments.ToString(),CapturePlaneHeight.ToString("F1"));
   // First initialization still repairs duplicated/saved geometry; later calls
   // at the same height neither allocate meshes nor refresh the Part caches.
   if(builtHeight==heightSegments)return;
   int before=builtHeight>=0?builtHeight:Mathf.RoundToInt(heightUpper.localPosition.y);
   heightTemplate.gameObject.SetActive(false);
   // Fixed authoring origins make editor duplication and saved-ship reload
   // idempotent; do not adopt an already raised transform as the new baseline.
   heightUpper.localPosition=Vector3.up*HeightOffset;heightStays.localScale=new Vector3(1,(48+HeightOffset)/48,1);
   foreach(var leg in heightLegColliders){
    var top=leg.upper+Vector3.up*HeightOffset;leg.box.center=(leg.lower+top)*.5f;
    var size=leg.box.size;size.y=top.y-leg.lower.y;leg.box.size=size;
   }
   // Loaded craft children already have their saved positions. Only an editor
   // change after initial restoration should move attached child assemblies.
   float attachedMove=builtHeight>=0?heightSegments-before:0;
   SetHeightNode("top",82+HeightOffset,attachedMove);SetHeightNode("lhzCapture",CapturePlaneHeight,attachedMove);
   BuildHeightBatches();builtHeight=heightSegments;if(ropes!=null)DrawNet();KIUModelRenderers.Refresh(part);
  }
  void BuildHeightBatches(){
   DestroyHeightBatches();
   // Editor duplication may copy generated children without copying our lists.
   // Detach before Destroy (which is deferred) so FindModelComponents(true)
   // cannot find inactive stale colliders/renderers in this same frame.
   var stale=new List<GameObject>();
   foreach(Transform t in heightTemplate.parent)if(t.name.StartsWith("LHZ_HeightCombined_")||t.name.StartsWith("LHZ_HeightCollider_")||t.name.StartsWith("LHZHeightSection_"))stale.Add(t.gameObject);
   foreach(var g in stale)RetireHeightObject(g);
   if(heightSegments==0)return;
   var groups=new Dictionary<Material,List<CombineInstance>>();var sizes=new Dictionary<Material,int>();
   foreach(var f in heightTemplate.GetComponentsInChildren<MeshFilter>(true)){
    var renderer=f.GetComponent<MeshRenderer>();if(renderer==null||f.sharedMesh==null)continue;var mat=renderer.sharedMaterial;
    if(!groups.ContainsKey(mat)){groups[mat]=new List<CombineInstance>();sizes[mat]=0;}
    for(int i=0;i<heightSegments;i++){
     if(sizes[mat]+f.sharedMesh.vertexCount>60000&&groups[mat].Count>0){AddHeightBatch(mat,groups[mat]);groups[mat].Clear();sizes[mat]=0;}
     groups[mat].Add(new CombineInstance{mesh=f.sharedMesh,transform=Matrix4x4.Translate(Vector3.up*i)*heightTemplate.parent.worldToLocalMatrix*f.transform.localToWorldMatrix});sizes[mat]+=f.sharedMesh.vertexCount;
    }
   }
   foreach(var entry in groups)if(entry.Value.Count>0)AddHeightBatch(entry.Key,entry.Value);
   foreach(var source in heightTemplate.GetComponentsInChildren<BoxCollider>(true)){
    // Older MU files retain their four extension templates. The continuous-leg
    // model needs no runtime collider objects, at any supported height.
    if(heightLegColliders.Count==4)break;
    var obj=new GameObject("LHZ_HeightCollider_"+heightBatches.Count);obj.layer=heightTemplate.gameObject.layer;obj.transform.SetParent(heightTemplate.parent,false);obj.transform.position=source.transform.position;obj.transform.rotation=source.transform.rotation;obj.transform.localScale=source.transform.localScale;
    var box=obj.AddComponent<BoxCollider>();box.center=source.center+Vector3.up*((heightSegments-1)*.5f);var size=source.size;size.y*=heightSegments;box.size=size;box.sharedMaterial=source.sharedMaterial;heightBatches.Add(obj);
   }
  }
  void AddHeightBatch(Material mat,List<CombineInstance> items){var mesh=new Mesh();mesh.name="LHZ_HeightCombined";mesh.CombineMeshes(items.ToArray(),true,true);heightMeshes.Add(mesh);var obj=new GameObject("LHZ_HeightCombined_"+heightBatches.Count);obj.layer=heightTemplate.gameObject.layer;obj.transform.SetParent(heightTemplate.parent,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=mat;heightBatches.Add(obj);}
  static void RetireHeightObject(GameObject g){if(g==null)return;g.SetActive(false);g.transform.SetParent(null,false);UnityEngine.Object.Destroy(g);}
  void DestroyHeightBatches(){
   foreach(var g in heightBatches)RetireHeightObject(g);heightBatches.Clear();
   // Only meshes allocated by this module are owned here. A copied object's
   // shared mesh may still belong to the original Part and must not be freed.
   foreach(var m in heightMeshes)if(m!=null)UnityEngine.Object.Destroy(m);heightMeshes.Clear();
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
