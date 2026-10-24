using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 public partial class LHZNetController {
  [KSPField(isPersistant=true)] public bool releaseRequested;
  [KSPField(isPersistant=true)] public float releaseElapsed;
  public float lastReleasePositionJump=-1;
  public int lastReleaseClearedJoints;
  static int FreeBoundaryJoints(Rigidbody[] all,HashSet<Rigidbody> recovered,HashSet<ConfigurableJoint> released){
   int count=0;foreach(var body in all)if(body!=null)foreach(var j in body.GetComponents<ConfigurableJoint>()){
    if(j==null||j.connectedBody==null||recovered.Contains(body)==recovered.Contains(j.connectedBody)||!released.Add(j))continue;
    // Stock and KJR destruction is deferred until the frame ends. Remove their
    // solver response before translating the newly released native vessel.
    j.xMotion=j.yMotion=j.zMotion=j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Free;
    var drive=new JointDrive();j.xDrive=j.yDrive=j.zDrive=j.angularXDrive=j.angularYZDrive=j.slerpDrive=drive;
    UnityEngine.Object.Destroy(j);count++;
   }return count;
  }
  [KSPEvent(guiActive=true,guiName="#LHZ_Release")]
  public void ReleaseStage(){
   if(lockedHook==null||lockedHook.part.parent!=part||lockedHook.part.vessel!=vessel||releaseRequested)return;
   ShutdownCapturedEngines();if(hookPoses!=null)foreach(var h in hookPoses)h.releaseFrom=h.lastTime;if(extraHookPoses!=null)foreach(var h in extraHookPoses)h.releaseFrom=h.lastTime;releaseRequested=true;releaseElapsed=0;recoveryState="Releasing";InvalidateFeedback();
  }
  void CompleteRelease(){
   if(lockedHook==null)return;var h=lockedHook;var p=h.part;
   if(nativePoseMoving)FinishNativePoseMotion();
   var recovered=Subtree(p).ToArray();var models=modelPoses==null?new ModelPose[0]:modelPoses.ToArray();
   var before=models.Select(m=>m.root.position).ToArray();Vector3 move=recoveryPoseNative?Vector3.zero:-part.transform.up*visualSink;
   var all=Bodies(p.vessel);var bodies=all.Where(b=>recovered.Any(q=>q.rb==b||q.servoRb==b)).ToArray();var bodySet=new HashSet<Rigidbody>(bodies);var releasedJoints=new HashSet<ConfigurableJoint>();
   var velocities=bodies.Select(b=>b.velocity+Vector3.Cross(b.angularVelocity,move)).ToArray();var angular=bodies.Select(b=>b.angularVelocity).ToArray();
   h.Node.attachedPart=null;h.Node.attachedPartId=0;receiverLink.Node.attachedPart=null;receiverLink.Node.attachedPartId=0;
   lastReleaseClearedJoints=FreeBoundaryJoints(all,bodySet,releasedJoints);
   if(h.sourceAttachMode>=0)p.attachMode=(AttachModes)h.sourceAttachMode;
   p.Undock(new DockedVesselInfo{name=h.sourceName,vesselType=(VesselType)h.sourceType,rootPartUId=h.sourceRootFlightId});
   if(p.vessel==vessel||p.parent==part)throw new InvalidOperationException("Native release did not separate recovered core");
   lastReleaseClearedJoints+=FreeBoundaryJoints(all,bodySet,releasedJoints);
   // The entire recovered vessel takes over the visual displacement in the
   // same physics callback. Native joints, CoM and colliders now share that pose.
   p.vessel.SetPosition(p.vessel.transform.position+(Vector3d)move,false);
   for(int i=0;i<bodies.Length;i++)if(bodies[i]!=null){bodies[i].position=bodies[i].transform.position;bodies[i].rotation=bodies[i].transform.rotation;bodies[i].velocity=velocities[i];bodies[i].angularVelocity=angular[i];}
   foreach(var m in models)if(m.root!=null)m.root.localPosition=m.origin;
   lastReleasePositionJump=0;for(int i=0;i<models.Length;i++)if(models[i].root!=null)lastReleasePositionJump=Math.Max(lastReleasePositionJump,Vector3.Distance(before[i],models[i].root.position));
   h.linkActive=receiverLink.linkActive=false;h.hardConstraint=false;h.poseSaved=false;h.captureState="Released";
   foreach(var q in recovered){var a=q.FindModuleImplementing<ModuleAnimateGeneric>();if(q.FindModuleImplementing<LHZHookProbe>()!=null&&a!=null){a.animTime=1;a.animSpeed=0;a.aniState=ModuleAnimateGeneric.animationStates.LOCKED;a.animSwitch=true;}}
   bool keepCoreControl=capturedCamera!=null&&FlightGlobals.ActiveVessel==vessel;
   lockedHook=null;ClearVisualBufferState();recoveryPoseNative=false;automatic=false;receiverLink.armed=false;recoveryState="Released";approaches.Clear();ClearCaptureCamera();if(keepCoreControl)FlightGlobals.ForceSetActiveVessel(p.vessel);DrawNet();InvalidateFeedback();
   Debug.Log("[KIUNetRecovery] continuous release visualJump="+lastReleasePositionJump+" boundaryConstraints="+lastReleaseClearedJoints);
  }
 }
}
