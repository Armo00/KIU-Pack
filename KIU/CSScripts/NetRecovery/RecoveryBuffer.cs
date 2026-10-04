// Deterministic visual compliance. Native Part poses/joints remain fixed after capture.
// Replay the original DeployHook clip so its hydraulic mechanism stays synchronized.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 [Serializable] public class BufferEvidence {
  public float elapsed,visualSink,targetSink,guideDrop,hookAnimationTime,maxHookRetractionDegrees,maxRopeContactError;
  public int visualParts,modelRoots,hooks,revision;public bool active,prepared;public string error;
  public uint[] hookIds;public float[][] guidePositions;public float[][] mouths,ropeContacts;public float[][][] ropePoints;public float[] ropeOffsets;
  public float[] cableLengths,requiredPayout,supportVerticalFractions;
 }
 public partial class LHZNetController {
  public const float BufferDuration=2.6f;public const int BufferRevision=3;
  [KSPField] public float hookRetractionDegrees=50,cableCenterDrop=8,guideDropDistance=.5f;
  [KSPField(isPersistant=true)] public bool bufferActive;
  [KSPField(isPersistant=true)] public float bufferElapsed;
  [KSPField(isPersistant=true)] public int bufferRevision;
  [KSPField(isPersistant=true)] public string animatedExtraHookIds="";
  class ModelPose {public Part part;public Transform root;public Vector3 origin;}
  class HookPose {public Part part;public Transform hinge;public Quaternion deployed;public Vector3 throat;public int rope;public float targetTime;}
  class GuidePose {public Transform transform;public Vector3 origin,meshCenter;public int rope;}
  List<ModelPose> modelPoses;List<HookPose> hookPoses,extraHookPoses;List<GuidePose> guidePoses;int visualParts;
  float visualSink,targetSink,guideDrop,hookAnimationTime=1;float[] ropeOffsets=new float[4];
  string bufferError="";float bufferRetryAt;bool bufferCompletionLogged;
  // A short yield, one gentle rebound, then hold. Parameters are presentation choices.
  static float Smooth(float u){u=Mathf.Clamp01(u);return u*u*(3-2*u);}
  static float Depth(float t,float hold){if(t<.65f)return (hold+.45f)*Smooth(t/.65f);if(t<1.25f)return Mathf.Lerp(hold+.45f,hold-.25f,Smooth((t-.65f)/.6f));if(t<2.1f)return Mathf.Lerp(hold-.25f,hold,Smooth((t-1.25f)/.85f));return hold;}
  static IEnumerable<Part> Subtree(Part p){yield return p;foreach(var c in p.children)foreach(var q in Subtree(c))yield return q;}
  static void SampleHook(Part p,float time){
   if(p==null)return;time=Mathf.Clamp01(time);bool sampled=false;
   // Sample the owned clip directly: the legacy mixer may have zero effective
   // weight or be culled even while its normalized-time field keeps advancing.
   foreach(var animation in p.GetComponentsInChildren<Animation>(true).Where(a=>a.GetComponentInParent<Part>()==p)){
    var st=animation["DeployHook"];if(st==null||st.clip==null||st.clip.length<=0)continue;
    animation.Stop("DeployHook");st.speed=0;st.weight=1;st.normalizedTime=time;st.enabled=false;
    st.clip.SampleAnimation(animation.gameObject,time*st.clip.length);sampled=true;
   }
   if(!sampled)throw new Exception("Owned DeployHook clip missing on hook "+p.persistentId);
   var module=p.FindModuleImplementing<ModuleAnimateGeneric>();if(module!=null){module.animTime=time;module.animSpeed=0;module.aniState=ModuleAnimateGeneric.animationStates.LOCKED;module.animSwitch=false;}
  }
  bool TryPrepareVisualBuffer(){
   if(modelPoses!=null)return true;if(Time.time<bufferRetryAt)return false;
   try{PrepareVisualBuffer();bufferError="";return modelPoses!=null;}
   catch(Exception e){modelPoses=null;hookPoses=null;extraHookPoses=null;bufferError=e.Message;bufferRetryAt=Time.time+1;Debug.LogError("[KIUNetRecovery] buffer preparation failed: "+bufferError);return false;}
  }
  void BeginVisualBuffer(){bufferActive=true;bufferElapsed=0;bufferRevision=BufferRevision;bufferError="";bufferRetryAt=0;bufferCompletionLogged=false;TryPrepareVisualBuffer();}
  [KSPEvent(guiActive=true,guiName="#LHZ_ReplayBuffer")]
  public void ReplayBuffer(){if(lockedHook==null||vessel==null||vessel.packed)return;ResetVisualBuffer();BeginVisualBuffer();DrawNet();}
  void PrepareVisualBuffer(){
   if(modelPoses!=null||lockedHook==null)return;targetSink=cableCenterDrop;var recovered=Subtree(lockedHook.part).ToArray();RestorePairs(recovered.Where(p=>p.FindModuleImplementing<LHZHookProbe>()!=null&&Tip(p)!=null).ToArray());modelPoses=new List<ModelPose>();hookPoses=new List<HookPose>();extraHookPoses=new List<HookPose>();visualParts=recovered.Length;
   foreach(var p in recovered){
    var roots=new HashSet<Transform>();foreach(var r in p.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponentInParent<Part>()==p&&(r is MeshRenderer||r is SkinnedMeshRenderer))){var t=r.transform;while(t.parent!=null&&t.parent!=p.transform)t=t.parent;if(t.parent==p.transform)roots.Add(t);}
    foreach(var t in roots)modelPoses.Add(new ModelPose{part=p,root=t,origin=t.localPosition});
    if(p.FindModuleImplementing<LHZHookProbe>()==null||Tip(p)==null)continue;
    bool selected=savedPairs.ContainsKey(p.persistentId);if(!selected&&!Deployed(p)&&!(animatedExtraHookIds??"").Split(',').Contains(p.persistentId.ToString()))continue;
    SampleHook(p,1);var hinge=p.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="MainHinge"&&t.GetComponentInParent<Part>()==p);if(hinge==null)throw new Exception("Recovery-buffer owned MainHinge missing");
    var h=new HookPose{part=p,hinge=hinge,deployed=hinge.localRotation,throat=hinge.InverseTransformPoint(p.transform.TransformPoint(Mouth(p))),rope=selected?SavedRope(p):-1};var mouth=hinge.TransformPoint(h.throat);float low=0,high=1;
    SampleHook(p,0);float range=Quaternion.Angle(h.deployed,hinge.localRotation);if(Single.IsNaN(range)||range<hookRetractionDegrees-.1f){SampleHook(p,1);throw new Exception("DeployHook calibration did not move owned hinge through the requested degrees on hook "+p.persistentId+"; measured "+range);}
    for(int k=0;k<22;k++){float mid=(low+high)/2;SampleHook(p,mid);if(Quaternion.Angle(h.deployed,hinge.localRotation)>hookRetractionDegrees)low=mid;else high=mid;}
    h.targetTime=(low+high)/2;SampleHook(p,h.targetTime);float angle=Quaternion.Angle(h.deployed,hinge.localRotation);if(Single.IsNaN(angle)||Math.Abs(angle-hookRetractionDegrees)>.2f){SampleHook(p,1);throw new Exception("DeployHook requested-angle calibration failed on hook "+p.persistentId+"; measured "+angle);}targetSink=Math.Max(targetSink,Vector3.Dot(hinge.TransformPoint(h.throat)-mouth,part.transform.up)+cableCenterDrop);SampleHook(p,1);if(selected)hookPoses.Add(h);else extraHookPoses.Add(h);
   }
   if(hookPoses.Count!=4||hookPoses.Select(h=>h.rope).Distinct().Count()!=4)throw new Exception("Recovery-buffer requires four distinct hook/rope pairs");
   animatedExtraHookIds=String.Join(",",extraHookPoses.Select(h=>h.part.persistentId.ToString()).ToArray());
   Debug.Log("[KIUNetRecovery] buffer prepared revision=3 hooks="+hookPoses.Count+" targetSink="+targetSink+" times="+String.Join(",",hookPoses.Select(h=>h.targetTime.ToString("R",System.Globalization.CultureInfo.InvariantCulture)).ToArray()));
  }
  public void LateUpdate(){
   TickCaptureCamera();
   if(!HighLogic.LoadedSceneIsFlight||part==null||vessel==null||vessel.packed||lockedHook==null||lockedHook.part==null||lockedHook.part.parent!=part)return;
   if(!TryPrepareVisualBuffer()||!bufferActive)return;if(poweredShutdownPending){DrawNet();return;}bufferElapsed=Math.Min(BufferDuration,bufferElapsed+Time.deltaTime);visualSink=Depth(bufferElapsed,targetSink);guideDrop=guideDropDistance*Smooth(bufferElapsed/.8f);hookAnimationTime=hookPoses.Average(h=>Mathf.Lerp(1,h.targetTime,Smooth(bufferElapsed/.9f)));
   foreach(var h in hookPoses.Concat(extraHookPoses))SampleHook(h.part,Mathf.Lerp(1,h.targetTime,Smooth(bufferElapsed/.9f)));
   // Clip curves may bind a model root. Apply the common sink after sampling.
   foreach(var m in modelPoses)if(m.root!=null&&m.part!=null)m.root.localPosition=m.origin+m.part.transform.InverseTransformVector(-part.transform.up*visualSink);
   DrawNet();if(!bufferCompletionLogged&&bufferElapsed>=BufferDuration){bufferCompletionLogged=true;Debug.Log("[KIUNetRecovery] buffer held revision=3 elapsed="+bufferElapsed+" sink="+visualSink+" hookDegrees="+String.Join(",",hookPoses.Select(h=>Quaternion.Angle(h.deployed,h.hinge.localRotation).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)).ToArray()));}
  }
  Vector3 BufferedMouth(HookPose h){return part.transform.InverseTransformPoint(h.hinge.TransformPoint(h.throat));}
  // A concentrated hook load needs an upward component of tension on both
  // sides. A smooth U with a horizontal tangent at the hook cannot represent
  // that load. Use taut spans with a small self-weight bow between endpoints.
  static Vector3 LoadedCablePoint(Vector3 a,Vector3 b,float u){var p=Vector3.Lerp(a,b,u);float slack=Math.Min(.12f,Math.Abs(b.y-a.y)*.08f);return p-Vector3.up*(4*slack*u*(1-u));}
  float CableLayer(int rope){return lockedHook==null?0:(rope<2?.3f:-.3f)*Smooth(bufferElapsed/.8f);}
  void PrepareGuides(){
   if(guidePoses!=null)return;guidePoses=new List<GuidePose>();
   foreach(var r in part.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.GetComponentInParent<Part>()==part&&r.name.StartsWith("RopeCarriage_"))){
    var f=r.GetComponent<MeshFilter>();if(f==null)continue;var c=part.transform.InverseTransformPoint(r.transform.TransformPoint(f.sharedMesh.bounds.center));float best=float.MaxValue;int index=0;
    for(int i=0;i<4;i++)foreach(float end in new[]{-Span,Span}){float offset=(i%2==0?-1:1)*Gap;var p=i<2?new Vector3(end,Plane,CenterZ+offset):new Vector3(offset,Plane,CenterZ+end);float d=(c-p).sqrMagnitude;if(d<best){best=d;index=i;}}
    guidePoses.Add(new GuidePose{transform=r.transform,origin=r.transform.localPosition,meshCenter=f.sharedMesh.bounds.center,rope=index});
   }
  }
  void DrawBufferedRopes(){
   InitTracking();PrepareGuides();float endY=Plane-(lockedHook!=null?guideDrop:0);
   for(int i=0;i<4;i++){
    bool alongX=i<2;float offset=(float)axes[i].position;HookPose h=hookPoses==null?null:hookPoses.FirstOrDefault(x=>x.rope==i);Vector3 contact=Vector3.zero;
    if(lockedHook!=null&&h!=null){contact=BufferedMouth(h);offset=alongX?contact.z-CenterZ:contact.x;axes[i].position=offset;axes[i].velocity=0;}ropeOffsets[i]=offset;
    bool retained=false;if(lockedHook==null&&tracked!=null)for(int k=0;k<4;k++)if(tracked.rope[k]==i&&tracked.contacts.hit[k]){contact=Local(tracked.hooks[k]);retained=true;break;}
    float anchorY=endY+CableLayer(i);
    var start=alongX?new Vector3(-Span,anchorY,CenterZ+offset):new Vector3(offset,anchorY,CenterZ-Span);
    var end=alongX?new Vector3(Span,anchorY,CenterZ+offset):new Vector3(offset,anchorY,CenterZ+Span);
    for(int j=0;j<33;j++)ropes[i].SetPosition(j,lockedHook!=null&&h!=null||retained?(j<=16?LoadedCablePoint(start,contact,j/16f):LoadedCablePoint(contact,end,(j-16)/16f)):Vector3.Lerp(start,end,j/32f));
   }
   foreach(var g in guidePoses){if(g.transform==null)continue;float baseline=(g.rope%2==0?-1:1)*Gap,delta=ropeOffsets[g.rope]-baseline;float drop=endY+CableLayer(g.rope)-Plane;var move=g.rope<2?new Vector3(0,drop,delta):new Vector3(delta,drop,0);g.transform.localPosition=g.origin+g.transform.parent.InverseTransformVector(part.transform.TransformVector(move));}
  }
  public BufferEvidence BufferState(){
   var mouths=hookPoses==null?new Vector3[0]:hookPoses.OrderBy(h=>h.rope).Select(BufferedMouth).ToArray();var contacts=ropes==null?new Vector3[0]:ropes.Select(r=>r.GetPosition(16)).ToArray();float error=0;for(int i=0;i<Math.Min(mouths.Length,contacts.Length);i++)error=Math.Max(error,Vector3.Distance(mouths[i],contacts[i]));
   var lengths=new float[contacts.Length];var payout=new float[contacts.Length];var support=new float[contacts.Length];for(int i=0;i<contacts.Length;i++){for(int j=1;j<33;j++)lengths[i]+=Vector3.Distance(ropes[i].GetPosition(j-1),ropes[i].GetPosition(j));payout[i]=Math.Max(0,lengths[i]-2*Span);support[i]=(ropes[i].GetPosition(15)-contacts[i]).normalized.y+(ropes[i].GetPosition(17)-contacts[i]).normalized.y;}
   return new BufferEvidence{cableLengths=lengths,requiredPayout=payout,supportVerticalFractions=support,revision=bufferRevision,prepared=hookPoses!=null&&hookPoses.Count==4,error=bufferError,hookIds=hookPoses==null?new uint[0]:hookPoses.OrderBy(h=>h.rope).Select(h=>h.part.persistentId).ToArray(),guidePositions=guidePoses==null?new float[0][]:guidePoses.Select(g=>{var p=part.transform.InverseTransformPoint(g.transform.TransformPoint(g.meshCenter));return new[]{p.x,p.y,p.z};}).ToArray(),elapsed=bufferElapsed,visualSink=visualSink,targetSink=targetSink,guideDrop=guideDrop,hookAnimationTime=hookAnimationTime,maxHookRetractionDegrees=hookPoses==null?0:hookPoses.Max(h=>Quaternion.Angle(h.deployed,h.hinge.localRotation)),maxRopeContactError=error,visualParts=visualParts,modelRoots=modelPoses==null?0:modelPoses.Count,hooks=mouths.Length,active=bufferActive,mouths=mouths.Select(p=>new[]{p.x,p.y,p.z}).ToArray(),ropeContacts=contacts.Select(p=>new[]{p.x,p.y,p.z}).ToArray(),ropeOffsets=ropeOffsets.ToArray(),ropePoints=ropes==null?new float[0][][]:ropes.Select(r=>Enumerable.Range(0,33).Select(i=>{var p=r.GetPosition(i);return new[]{p.x,p.y,p.z};}).ToArray()).ToArray()};
  }
  void ResetVisualBuffer(){
   if(modelPoses!=null)foreach(var m in modelPoses)if(m.root!=null)m.root.localPosition=m.origin;
   if(hookPoses!=null)foreach(var h in hookPoses)SampleHook(h.part,1);
   if(extraHookPoses!=null)foreach(var h in extraHookPoses)SampleHook(h.part,1);
   modelPoses=null;hookPoses=null;extraHookPoses=null;animatedExtraHookIds="";visualParts=0;visualSink=guideDrop=bufferElapsed=0;hookAnimationTime=1;bufferActive=false;bufferError="";bufferRetryAt=0;bufferCompletionLogged=false;
  }
 }
}
