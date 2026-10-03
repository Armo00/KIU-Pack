// Deterministic visual compliance. Native Part poses/joints remain fixed after capture.
// Replay the original DeployHook clip so its hydraulic mechanism stays synchronized.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 [Serializable] public class BufferEvidence {
  public float elapsed,visualSink,targetSink,guideDrop,hookAnimationTime,maxHookRetractionDegrees,maxRopeContactError;
  public int visualParts,modelRoots,hooks;public bool active;
  public uint[] hookIds;public float[][] guidePositions;public float[][] mouths,ropeContacts;public float[][][] ropePoints;public float[] ropeOffsets;
 }
 public partial class LHZNetController {
  public const float BufferDuration=2.6f;
  [KSPField(isPersistant=true)] public bool bufferActive;
  [KSPField(isPersistant=true)] public float bufferElapsed;
  [KSPField(isPersistant=true)] public string animatedExtraHookIds="";
  class ModelPose {public Part part;public Transform root;public Vector3 origin;}
  class HookPose {public Part part;public Transform hinge;public Quaternion deployed;public Vector3 throat;public int rope;public float targetTime;}
  class GuidePose {public Transform transform;public Vector3 origin;public int rope;}
  List<ModelPose> modelPoses;List<HookPose> hookPoses,extraHookPoses;List<GuidePose> guidePoses;int visualParts;
  float visualSink,targetSink,guideDrop,hookAnimationTime=1;float[] ropeOffsets=new float[4];
  // A short yield, one gentle rebound, then hold. Parameters are presentation choices.
  static float Smooth(float u){u=Mathf.Clamp01(u);return u*u*(3-2*u);}
  static float Depth(float t,float hold){if(t<.65f)return (hold+.15f)*Smooth(t/.65f);if(t<1.25f)return Mathf.Lerp(hold+.15f,hold-.10f,Smooth((t-.65f)/.6f));if(t<2.1f)return Mathf.Lerp(hold-.10f,hold,Smooth((t-1.25f)/.85f));return hold;}
  static IEnumerable<Part> Subtree(Part p){yield return p;foreach(var c in p.children)foreach(var q in Subtree(c))yield return q;}
  static void SampleHook(Part p,float time){
   if(p==null)return;foreach(var animation in p.GetComponentsInChildren<Animation>(true)){var st=animation["DeployHook"];if(st==null)continue;st.enabled=true;st.normalizedTime=time;animation.Sample();st.enabled=false;}
   var module=p.FindModuleImplementing<ModuleAnimateGeneric>();if(module!=null)module.animTime=time;
  }
  void PrepareVisualBuffer(){
   if(modelPoses!=null||lockedHook==null)return;targetSink=.65f;var recovered=Subtree(lockedHook.part).ToArray();RestorePairs(recovered.Where(p=>p.FindModuleImplementing<LHZHookProbe>()!=null&&Tip(p)!=null).ToArray());modelPoses=new List<ModelPose>();hookPoses=new List<HookPose>();extraHookPoses=new List<HookPose>();visualParts=recovered.Length;
   foreach(var p in recovered){
    var roots=new HashSet<Transform>();foreach(var r in p.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponentInParent<Part>()==p&&(r is MeshRenderer||r is SkinnedMeshRenderer))){var t=r.transform;while(t.parent!=null&&t.parent!=p.transform)t=t.parent;if(t.parent==p.transform)roots.Add(t);}
    foreach(var t in roots)modelPoses.Add(new ModelPose{part=p,root=t,origin=t.localPosition});
    if(p.FindModuleImplementing<LHZHookProbe>()==null||Tip(p)==null)continue;
    bool selected=savedPairs.ContainsKey(p.persistentId);if(!selected&&!Deployed(p)&&!(animatedExtraHookIds??"").Split(',').Contains(p.persistentId.ToString()))continue;
    SampleHook(p,1);var hinge=p.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="MainHinge");if(hinge==null)throw new Exception("Recovery-buffer MainHinge missing");
    var h=new HookPose{part=p,hinge=hinge,deployed=hinge.localRotation,throat=hinge.InverseTransformPoint(p.transform.TransformPoint(Mouth(p))),rope=selected?SavedRope(p):-1};var mouth=hinge.TransformPoint(h.throat);float low=0,high=1;
    for(int k=0;k<22;k++){float mid=(low+high)/2;SampleHook(p,mid);if(Quaternion.Angle(h.deployed,hinge.localRotation)>24)low=mid;else high=mid;}
    h.targetTime=(low+high)/2;SampleHook(p,h.targetTime);targetSink=Math.Max(targetSink,Vector3.Dot(hinge.TransformPoint(h.throat)-mouth,part.transform.up)+.65f);SampleHook(p,1);if(selected)hookPoses.Add(h);else extraHookPoses.Add(h);
   }
   if(hookPoses.Count!=4||hookPoses.Select(h=>h.rope).Distinct().Count()!=4)throw new Exception("Recovery-buffer requires four distinct hook/rope pairs");
   animatedExtraHookIds=String.Join(",",extraHookPoses.Select(h=>h.part.persistentId.ToString()).ToArray());
  }
  public void LateUpdate(){
   if(!HighLogic.LoadedSceneIsFlight||part==null||vessel==null||vessel.packed||lockedHook==null||lockedHook.part==null||lockedHook.part.parent!=part)return;
   PrepareVisualBuffer();if(!bufferActive)return;if(poweredShutdownPending){DrawNet();return;}bufferElapsed=Math.Min(BufferDuration,bufferElapsed+Time.deltaTime);visualSink=Depth(bufferElapsed,targetSink);guideDrop=.20f*Smooth(bufferElapsed/.8f);hookAnimationTime=hookPoses.Average(h=>Mathf.Lerp(1,h.targetTime,Smooth(bufferElapsed/.9f)));
   foreach(var m in modelPoses)if(m.root!=null&&m.part!=null)m.root.localPosition=m.origin+m.part.transform.InverseTransformVector(-part.transform.up*visualSink);
   foreach(var h in hookPoses.Concat(extraHookPoses))SampleHook(h.part,Mathf.Lerp(1,h.targetTime,Smooth(bufferElapsed/.9f)));DrawNet();
  }
  Vector3 BufferedMouth(HookPose h){return part.transform.InverseTransformPoint(h.hinge.TransformPoint(h.throat));}
  void PrepareGuides(){
   if(guidePoses!=null)return;guidePoses=new List<GuidePose>();
   foreach(var r in part.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.GetComponentInParent<Part>()==part&&r.name.StartsWith("RopeCarriage_"))){
    var f=r.GetComponent<MeshFilter>();if(f==null)continue;var c=part.transform.InverseTransformPoint(r.transform.TransformPoint(f.sharedMesh.bounds.center));float best=float.MaxValue;int index=0;
    for(int i=0;i<4;i++)foreach(float end in new[]{-Span,Span}){float offset=(i%2==0?-1:1)*Gap;var p=i<2?new Vector3(end,Plane,CenterZ+offset):new Vector3(offset,Plane,CenterZ+end);float d=(c-p).sqrMagnitude;if(d<best){best=d;index=i;}}
    guidePoses.Add(new GuidePose{transform=r.transform,origin=r.transform.localPosition,rope=index});
   }
  }
  void DrawBufferedRopes(){
   InitTracking();PrepareGuides();float endY=Plane-(lockedHook!=null?guideDrop:0);
   for(int i=0;i<4;i++){
    bool alongX=i<2;float offset=(float)axes[i].position;HookPose h=hookPoses==null?null:hookPoses.FirstOrDefault(x=>x.rope==i);Vector3 contact=Vector3.zero;
    if(lockedHook!=null&&h!=null){contact=BufferedMouth(h);offset=alongX?contact.z-CenterZ:contact.x;axes[i].position=offset;axes[i].velocity=0;}ropeOffsets[i]=offset;
    bool retained=false;if(lockedHook==null&&tracked!=null)for(int k=0;k<4;k++)if(tracked.rope[k]==i&&tracked.contacts.hit[k]){contact=Local(tracked.hooks[k]);retained=true;break;}
    for(int j=0;j<33;j++){
     float a=(j/16f-1)*Span,y=endY;
     if(lockedHook!=null&&h!=null||retained){float c=alongX?contact.x:contact.z-CenterZ;float u=j<=16?j/16f:(j-16)/16f;a=j<=16?Mathf.Lerp(-Span,c,u):Mathf.Lerp(c,Span,u);float height=Math.Max(0,endY-contact.y);float f=j<=16?1-(1-u)*(1-u):1-u*u;y=endY-height*f;}
     float crossOffset=offset;if(retained){float u=j/16f,f=1-(u-1)*(u-1);crossOffset=Mathf.Lerp(offset,alongX?contact.z-CenterZ:contact.x,f);}
     ropes[i].SetPosition(j,alongX?new Vector3(a,y,CenterZ+crossOffset):new Vector3(crossOffset,y,CenterZ+a));
    }
   }
   foreach(var g in guidePoses){if(g.transform==null)continue;float baseline=(g.rope%2==0?-1:1)*Gap,delta=ropeOffsets[g.rope]-baseline;var move=g.rope<2?new Vector3(0,endY-Plane,delta):new Vector3(delta,endY-Plane,0);g.transform.localPosition=g.origin+g.transform.parent.InverseTransformVector(part.transform.TransformVector(move));}
  }
  public BufferEvidence BufferState(){
   var mouths=hookPoses==null?new Vector3[0]:hookPoses.OrderBy(h=>h.rope).Select(BufferedMouth).ToArray();var contacts=ropes==null?new Vector3[0]:ropes.Select(r=>r.GetPosition(16)).ToArray();float error=0;for(int i=0;i<Math.Min(mouths.Length,contacts.Length);i++)error=Math.Max(error,Vector3.Distance(mouths[i],contacts[i]));
   return new BufferEvidence{hookIds=hookPoses==null?new uint[0]:hookPoses.OrderBy(h=>h.rope).Select(h=>h.part.persistentId).ToArray(),guidePositions=guidePoses==null?new float[0][]:guidePoses.Select(g=>{var p=part.transform.InverseTransformPoint(g.transform.position);return new[]{p.x,p.y,p.z};}).ToArray(),elapsed=bufferElapsed,visualSink=visualSink,targetSink=targetSink,guideDrop=guideDrop,hookAnimationTime=hookAnimationTime,maxHookRetractionDegrees=hookPoses==null?0:hookPoses.Max(h=>Quaternion.Angle(h.deployed,h.hinge.localRotation)),maxRopeContactError=error,visualParts=visualParts,modelRoots=modelPoses==null?0:modelPoses.Count,hooks=mouths.Length,active=bufferActive,mouths=mouths.Select(p=>new[]{p.x,p.y,p.z}).ToArray(),ropeContacts=contacts.Select(p=>new[]{p.x,p.y,p.z}).ToArray(),ropeOffsets=ropeOffsets.ToArray(),ropePoints=ropes==null?new float[0][][]:ropes.Select(r=>Enumerable.Range(0,33).Select(i=>{var p=r.GetPosition(i);return new[]{p.x,p.y,p.z};}).ToArray()).ToArray()};
  }
  void ResetVisualBuffer(){
   if(modelPoses!=null)foreach(var m in modelPoses)if(m.root!=null)m.root.localPosition=m.origin;
   if(hookPoses!=null)foreach(var h in hookPoses)SampleHook(h.part,1);
   if(extraHookPoses!=null)foreach(var h in extraHookPoses)SampleHook(h.part,1);
   modelPoses=null;hookPoses=null;extraHookPoses=null;animatedExtraHookIds="";visualParts=0;visualSink=guideDrop=bufferElapsed=0;hookAnimationTime=1;bufferActive=false;
  }
 }
}
