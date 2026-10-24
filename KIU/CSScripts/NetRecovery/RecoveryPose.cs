// Bounded recovery choreography on native Part/Rigidbody poses, not model-only
// rotation. No rope rigidbody chain. The ordinary KSP joint is rebuilt at rest.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 public partial class LHZNetController {
  [KSPField(isPersistant=true)] public bool recoveryPoseNative;
  [KSPField(isPersistant=true)] public float recoveryPivotX,recoveryPivotY,recoveryPivotZ;
  [KSPField(isPersistant=true)] public float recoveryAxisX,recoveryAxisY=1,recoveryAxisZ;
  [KSPField(isPersistant=true)] public float recoverySinkTarget;
  class RecoveryPartPose {public Part part;public Vector3 offset;public Quaternion rotation;}
  class RecoveryBodyPose {public Rigidbody body;public Vector3 offset;public Quaternion rotation;public bool kinematic;public RigidbodyConstraints constraints;}
  RecoveryPartPose[] recoveryParts;
  RecoveryBodyPose[] recoveryBodies;
  readonly HashSet<ConfigurableJoint> recoveryRetiredJoints=new HashSet<ConfigurableJoint>();
  readonly List<ConfigurableJoint> recoverySupports=new List<ConfigurableJoint>();
  HashSet<Rigidbody> recoveryBodySet;
  bool nativePoseMoving;
  int poseUpdates;
  Part recoveryAxisPart;
  Vector3 SeaUp(){return ((Vector3d)part.transform.position-vessel.mainBody.position).normalized;}
  Vector3 RecoveryPivot {get{return new Vector3(recoveryPivotX,recoveryPivotY,recoveryPivotZ);}}
  Quaternion CaptureRootRotation {get{return new Quaternion(lockedHook.lqx,lockedHook.lqy,lockedHook.lqz,lockedHook.lqw);}}
  Vector3 CaptureRootPosition {get{return new Vector3(lockedHook.lpx,lockedHook.lpy,lockedHook.lpz);}}
  static float UprightFraction(float time){
   // One small overshoot, a smaller return swing, then a zero-speed stop.
   if(time<1.5f)return 1.035f*Smooth(time/1.5f);
   if(time<2.3f)return Mathf.Lerp(1.035f,.988f,Smooth((time-1.5f)/.8f));
   return Mathf.Lerp(.988f,1,Smooth((time-2.3f)/.9f));
  }
  void PrepareNativeRecovery(){
   if(recoveryParts!=null)return;
   Part root=lockedHook.part;var parts=Subtree(root).ToArray();
   recoveryAxisPart=parts.FirstOrDefault(p=>p.partInfo!=null&&p.partInfo.name.Contains("Stage1.Tank"))
     ??parts.FirstOrDefault(p=>p.flightID==lockedHook.sourceRootFlightId)??root;
   var inv=Quaternion.Inverse(root.transform.rotation);
   recoveryParts=parts.Select(p=>new RecoveryPartPose{part=p,offset=inv*(p.transform.position-root.transform.position),rotation=inv*p.transform.rotation}).ToArray();
   recoveryBodies=parts.SelectMany(p=>new[]{p.rb,p.servoRb}).Where(b=>b!=null).Distinct().Select(b=>new RecoveryBodyPose{
    body=b,offset=inv*(b.position-root.transform.position),rotation=inv*b.rotation,kinematic=b.isKinematic,constraints=b.constraints}).ToArray();
   recoveryBodySet=new HashSet<Rigidbody>(recoveryBodies.Select(b=>b.body));
   if(!recoveryPoseNative){
    // Fresh captures and revision-4 saves have not moved their native Part poses.
    Vector3 pivot=hookPoses.Select(h=>part.transform.InverseTransformPoint(h.hinge.TransformPoint(h.throat))).Aggregate(Vector3.zero,(a,b)=>a+b)/hookPoses.Count;
    recoveryPivotX=pivot.x;recoveryPivotY=pivot.y;recoveryPivotZ=pivot.z;
    Vector3 axis=inv*recoveryAxisPart.transform.up;recoveryAxisX=axis.x;recoveryAxisY=axis.y;recoveryAxisZ=axis.z;
    Vector3 up=SeaUp(),worldPivot=part.transform.TransformPoint(pivot);
    Quaternion correction=Quaternion.FromToRotation(part.transform.rotation*CaptureRootRotation*axis,up);
    Vector3 finalAverage=hookPoses.Select(h=>worldPivot+correction*(part.transform.TransformPoint(h.finalMouth)-worldPivot)).Aggregate(Vector3.zero,(a,b)=>a+b)/hookPoses.Count;
    Vector3 target=part.transform.TransformPoint(new Vector3(pivot.x,Plane-cableCenterDrop,pivot.z));
    recoverySinkTarget=Math.Max(0,Vector3.Dot(finalAverage-target,up));recoveryPoseNative=true;
   }
   targetSink=recoverySinkTarget;
   // On a saved, settled ship the native poses and ordinary joint are already
   // correct. Resume only an unfinished motion or an explicitly requested replay.
   if(bufferElapsed<BufferDuration)BeginNativePoseMotion();
  }
  Rigidbody[] VesselBodiesIncludingKinematic(){return vessel.parts.SelectMany(p=>new[]{p.rb,p.servoRb}).Where(b=>b!=null).Distinct().ToArray();}
  void RetireCaptureBoundary(){
   FreeBoundaryJoints(VesselBodiesIncludingKinematic(),recoveryBodySet,recoveryRetiredJoints);
   var root=lockedHook.part;
   // Destroying the PartJoint directly avoids firing a fake joint-break event.
   // Its ConfigurableJoints have already been freed before deferred destruction.
   if(root.attachJoint!=null){UnityEngine.Object.Destroy(root.attachJoint);root.attachJoint=null;}
  }
  void BeginNativePoseMotion(){
   if(nativePoseMoving)return;
   foreach(var b in recoveryBodies)if(b.body!=null)b.body.isKinematic=true;
   nativePoseMoving=true;RetireCaptureBoundary();
  }
  void TickNativeRecovery(){
   if(!TryPrepareVisualBuffer()||!bufferActive||!recoveryPoseNative||poweredShutdownPending)return;
   if(releaseRequested){if(nativePoseMoving){RetireCaptureBoundary();ApplyNativeRecoveryPose(bufferElapsed);}return;}
   if(bufferElapsed>=BufferDuration){if(nativePoseMoving)FinishNativePoseMotion();else EnsureRecoverySupports();return;}
   BeginNativePoseMotion();
   // KJR/autostrut can add a cross-boundary constraint after the couple event;
   // none may continue to enforce the old entry angle during this bounded phase.
   RetireCaptureBoundary();
   bufferElapsed=Math.Min(BufferDuration,bufferElapsed+Time.fixedDeltaTime);
   visualSink=Depth(bufferElapsed,targetSink);guideDrop=guideDropDistance*Smooth(bufferElapsed/.8f);
   ApplyNativeRecoveryPose(bufferElapsed);
   if(bufferElapsed>=BufferDuration)FinishNativePoseMotion();
  }
  void ApplyNativeRecoveryPose(float elapsed){
   Vector3 up=SeaUp(),pivot=part.transform.TransformPoint(RecoveryPivot);
   Quaternion initial=part.transform.rotation*CaptureRootRotation;
   Vector3 axis=initial*new Vector3(recoveryAxisX,recoveryAxisY,recoveryAxisZ);
   Quaternion turn=Quaternion.SlerpUnclamped(Quaternion.identity,Quaternion.FromToRotation(axis,up),UprightFraction(elapsed));
   Vector3 origin=pivot+turn*(part.transform.TransformPoint(CaptureRootPosition)-pivot)-up*visualSink;
   Quaternion rotation=turn*initial;
   // Use cached relative poses so transform parentage cannot double-apply a move.
   foreach(var p in recoveryParts)if(p.part!=null){p.part.transform.position=origin+rotation*p.offset;p.part.transform.rotation=rotation*p.rotation;}
   foreach(var b in recoveryBodies)if(b.body!=null){b.body.position=origin+rotation*b.offset;b.body.rotation=rotation*b.rotation;}
   foreach(var p in recoveryParts)if(p.part!=null)p.part.UpdateOrgPosAndRot(vessel.rootPart);
   poseUpdates++;InvalidateFeedback();
  }
  Vector3 NativeRecoveryPointAtHold(Vector3 currentShipPoint){
   Vector3 up=SeaUp(),pivot=part.transform.TransformPoint(RecoveryPivot);Quaternion initial=part.transform.rotation*CaptureRootRotation;
   Quaternion turn=Quaternion.FromToRotation(initial*new Vector3(recoveryAxisX,recoveryAxisY,recoveryAxisZ),up);
   Vector3 origin=pivot+turn*(part.transform.TransformPoint(CaptureRootPosition)-pivot)-up*targetSink;
   Vector3 relative=Quaternion.Inverse(lockedHook.part.transform.rotation)*(part.transform.TransformPoint(currentShipPoint)-lockedHook.part.transform.position);
   return part.transform.InverseTransformPoint(origin+turn*initial*relative);
  }
  void FinishNativePoseMotion(){
   if(!nativePoseMoving||lockedHook==null)return;
   RetireCaptureBoundary();var root=lockedHook.part;
   foreach(var p in recoveryParts)if(p.part!=null)p.part.UpdateOrgPosAndRot(vessel.rootPart);
   Vector3 world=root.transform.TransformPoint(Mouth(root));
   lockedHook.SetAnchor(root.transform.InverseTransformPoint(world),root.transform.InverseTransformDirection(-SeaUp()));
   receiverLink.SetAnchor(part.transform.InverseTransformPoint(world),part.transform.InverseTransformDirection(SeaUp()));
   RestoreNativeBodyDynamics();
   root.CreateAttachJoint(AttachModes.STACK);
   if(root.attachJoint==null)throw new InvalidOperationException("Native upright recovery joint could not be restored");
   LockJoint(root);nativePoseMoving=false;recoveryRetiredJoints.Clear();EnsureRecoverySupports();
   Debug.Log("[KIUNetRecovery] native recovery settled coreTilt="+CoreTiltToSea()+" sink="+visualSink+" parts="+recoveryParts.Length);
  }
  void RestoreNativeBodyDynamics(){
   if(recoveryBodies==null)return;
   foreach(var b in recoveryBodies)if(b.body!=null){b.body.isKinematic=b.kinematic;b.body.constraints=b.constraints;
    if(!b.kinematic&&part!=null&&part.rb!=null){b.body.velocity=part.rb.GetPointVelocity(b.body.worldCenterOfMass);b.body.angularVelocity=part.rb.angularVelocity;}}
  }
  void ClearNativeRecoveryCache(){
   foreach(var j in recoverySupports)if(j!=null){j.xMotion=j.yMotion=j.zMotion=j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Free;UnityEngine.Object.Destroy(j);}recoverySupports.Clear();
   if(nativePoseMoving)RestoreNativeBodyDynamics();nativePoseMoving=false;
   recoveryParts=null;recoveryBodies=null;recoveryBodySet=null;recoveryAxisPart=null;recoveryRetiredJoints.Clear();
  }
  float CoreTiltToSea(){return recoveryAxisPart==null?0:Vector3.Angle(recoveryAxisPart.transform.up,SeaUp());}
  void EnsureRecoverySupports(){
   if(nativePoseMoving||releaseRequested||lockedHook==null||hookPoses==null||part.rb==null||lockedHook.part.attachJoint==null)return;
   recoverySupports.RemoveAll(j=>j==null);
   // Four contact points must share the load. Relying on one rerooted hook's
   // attachment lets the long core sag after the kinematic recovery ends.
   foreach(var h in hookPoses){
    if(h.part==null||h.part==lockedHook.part||h.part.rb==null||recoverySupports.Any(support=>support.GetComponent<Rigidbody>()==h.part.rb))continue;
    // Save restoration prepares hooks in their deployed calibration pose.
    // Restore the held clip before choosing an actual support anchor.
    SamplePose(h,h.targetTime);
    Vector3 world=h.hinge.TransformPoint(h.throat);var j=h.part.rb.gameObject.AddComponent<ConfigurableJoint>();
    j.autoConfigureConnectedAnchor=false;j.connectedBody=part.rb;
    j.anchor=h.part.rb.transform.InverseTransformPoint(world);j.connectedAnchor=part.rb.transform.InverseTransformPoint(world);
    j.xMotion=j.yMotion=j.zMotion=j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Locked;
    var native=lockedHook.part.attachJoint.joints.FirstOrDefault(x=>x!=null);
    j.breakForce=native==null?Math.Max(h.part.breakingForce,part.breakingForce):native.breakForce;
    j.breakTorque=native==null?Math.Max(h.part.breakingTorque,part.breakingTorque):native.breakTorque;
    recoverySupports.Add(j);
   }
  }
  float RecoverySupportAnchorError(){
   if(hookPoses==null)return 0;float error=0;
   foreach(var h in hookPoses){if(h.part==null||h.part.rb==null)continue;var joint=recoverySupports.FirstOrDefault(j=>j!=null&&j.GetComponent<Rigidbody>()==h.part.rb);
    if(joint!=null)error=Math.Max(error,Vector3.Distance(joint.transform.TransformPoint(joint.anchor),h.hinge.TransformPoint(h.throat)));}
   return error;
  }
  Vector3 MechanismCoreOffset {get{return recoveryPoseNative?Vector3.zero:Vector3.up*visualSink;}}
 }
}
