// Production recovery PartModule; validation bridge is maintained outside KIU.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 public partial class LHZNetController : PartModule {
  [KSPField(isPersistant=true)] public bool automatic,stationKeeping;
  [KSPField(isPersistant=true)] public string recoveryState="Ready";
  [KSPField(guiActive=true,guiName="#LHZ_NetState")] public string displayedState;
  [KSPField(guiActive=true,guiName="#LHZ_StationState")] public string displayedStation;
  [KSPField(isPersistant=true)] public double stationLat,stationLon;
  [KSPField] public float maxCaptureSpeed=25,captureThroatRadius=1.25f,maxCaptureTiltDegrees=20,contactRetentionSeconds=1.5f,contactYieldStroke=4,minimumAperture=5.6f;
  public CaptureRecord lastCapture;public List<ContactEvidence> lastContacts=new List<ContactEvidence>();
  const float BasePlane=62.5f,Gap=5.451439f,Span=22,CenterZ=1.02f;
  float Plane {get{return CapturePlaneHeight;}}
  LHZReceiverProbe receiverLink;LHZHookProbe lockedHook;Material material;LineRenderer[] ropes;
  Dictionary<Guid,Approach> approaches=new Dictionary<Guid,Approach>();
  class Approach {public Part[] hooks;public Vector3[] previous;public CaptureLatch contacts=new CaptureLatch();public int[] rope=new int[4];}
  [KSPEvent(guiActive=true,guiName="#LHZ_Arm")]
  public void ArmNet(){if(lockedHook!=null)return;if(receiverLink==null)receiverLink=part.FindModuleImplementing<LHZReceiverProbe>();if(receiverLink==null)return;InitTracking();ForgetTarget();ClearFeedback();hookRopePairs="";savedPairs.Clear();lastCapture=null;automatic=true;receiverLink.armed=true;recoveryState="Armed";lastContacts.Clear();DrawNet();foreach(var r in ropes)r.enabled=true;}
  [KSPEvent(guiActive=true,guiName="#LHZ_Disarm")]
  public void DisarmNet(){automatic=false;receiverLink.armed=false;if(lockedHook==null)recoveryState="Ready";ForgetTarget();}
  [KSPEvent(guiActive=true,guiName="#LHZ_Release")]
  public void ReleaseStage(){
   if(lockedHook==null||lockedHook.part.parent!=part||lockedHook.part.vessel!=vessel)return;
   ShutdownCapturedEngines();ResetVisualBuffer();var h=lockedHook;var p=h.part;h.Node.attachedPart=null;h.Node.attachedPartId=0;receiverLink.Node.attachedPart=null;receiverLink.Node.attachedPartId=0;
   p.Undock(new DockedVesselInfo{name=h.sourceName,vesselType=(VesselType)h.sourceType,rootPartUId=h.sourceRootFlightId});h.linkActive=receiverLink.linkActive=false;h.captureState="Released";lockedHook=null;automatic=false;receiverLink.armed=false;recoveryState="Released";approaches.Clear();DrawNet();
  }
  [KSPEvent(guiActive=true,guiName="#LHZ_Reset")]
  public void ResetNet(){if(lockedHook!=null)return;ResetVisualBuffer();ArmNet();}
  [KSPEvent(guiActive=true,guiName="#LHZ_Station")]
  public void ToggleStation(){if(vessel==null)return;stationKeeping=!stationKeeping;if(stationKeeping){stationLat=vessel.latitude;stationLon=vessel.longitude;}}
  public override void OnStart(StartState start){base.OnStart(start);InitializeHeight();ClearObsoleteRejection();InitTracking();DrawNet();if(!HighLogic.LoadedSceneIsFlight)return;receiverLink=part.FindModuleImplementing<LHZReceiverProbe>();InitTracking();RestoreLink();if(lockedHook!=null){captureEngines=Subtree(lockedHook.part).SelectMany(p=>p.FindModulesImplementing<ModuleEngines>()).ToArray();restoreShutdownPending=true;}DrawNet();RegisterToolbar();}
  void RestoreLink(){
   if(receiverLink==null||vessel==null)return;
   lockedHook=vessel.parts.Select(p=>p.FindModuleImplementing<LHZHookProbe>()).FirstOrDefault(h=>h!=null&&h.captureState=="Locked"&&h.receiverId==part.persistentId&&h.part.parent==part);
   if(lockedHook!=null){recoveryState="Locked";receiverLink.Apply();lockedHook.Apply();LockJoint(lockedHook.part);if(bufferRevision<BufferRevision){BeginVisualBuffer();Debug.Log("[KIUNetRecovery] restored legacy buffer: replay revision=3");}else if(!bufferActive){bufferActive=true;bufferElapsed=BufferDuration;}}
  }
  static Transform Tip(Part p){return p.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="HookTipMarker"&&t.GetComponentInParent<Part>()==p);}
  static Vector3 Mouth(Part p){var t=Tip(p);return p.transform.InverseTransformPoint(t.position)+new Vector3(-.16f,-.12f,0);}
  static bool Deployed(Part p){var t=Tip(p);if(t==null)return false;var v=p.transform.InverseTransformPoint(t.position);return v.x>4.7f&&v.y<1.8f;}
  Vector3 Local(Part p){return part.transform.InverseTransformPoint(p.transform.TransformPoint(Mouth(p)));}
  int Rope(Vector3 p){p.z-=CenterZ;return p.x>=0?(p.z>=0?1:3):(p.z>=0?2:0);}
  Point3 Relative(Vector3 p,int rope,float offset){return rope<2?new Point3(p.x,p.y-Plane,p.z-CenterZ-offset):new Point3(p.z-CenterZ,p.y-Plane,p.x-offset);}
  public void FixedUpdate(){
   if(!HighLogic.LoadedSceneIsFlight||part==null||part.rb==null||vessel==null||vessel.packed)return;
   if(receiverLink==null)receiverLink=part.FindModuleImplementing<LHZReceiverProbe>();if(receiverLink==null)return;TickEngineCut();
   if(stationKeeping&&part.partBuoyancy!=null&&part.partBuoyancy.splashed){Vector3 up=(part.transform.position-(Vector3)vessel.mainBody.position).normalized;var target=(Vector3)vessel.mainBody.GetWorldSurfacePosition(stationLat,stationLon,vessel.altitude);var error=Vector3.ProjectOnPlane(target-part.transform.position,up);var velocity=Vector3.ProjectOnPlane((Vector3)vessel.srf_velocity,up);part.rb.AddForce(Vector3.ClampMagnitude(error*80-velocity*400,800),ForceMode.Force);part.rb.AddTorque(Vector3.ClampMagnitude(Vector3.Cross(part.transform.up,up)*20000-part.rb.angularVelocity*12000,20000),ForceMode.Force);}
   if(lockedHook==null&&recoveryState=="Locked")RestoreLink();
   if(lockedHook!=null){if(lockedHook.part==null||lockedHook.part.parent!=part||lockedHook.part.vessel!=vessel){ResetVisualBuffer();lockedHook=null;recoveryState="Failed";automatic=false;receiverLink.linkActive=false;}else{LockJoint(lockedHook.part);return;}}
   if(!automatic||!receiverLink.armed||TimeWarp.CurrentRateIndex!=0){HomeTracking(Time.fixedDeltaTime);NoticeActiveTarget(!automatic||!receiverLink.armed?"NetDisabled":"Warp");return;}
   TickTracking(Time.fixedDeltaTime);
  }
  static Rigidbody[] Bodies(Vessel v){return v.parts.SelectMany(p=>new[]{p.rb,p.servoRb}).Where(b=>b!=null&&!b.isKinematic).Distinct().ToArray();}
  static V3 D(Vector3 v){return new V3(v.x,v.y,v.z);}static Vector3 F(V3 v){return new Vector3((float)v.x,(float)v.y,(float)v.z);}
  static BodyMotion Motion(Rigidbody b){Quaternion q=b.rotation*b.inertiaTensorRotation;return new BodyMotion{mass=b.mass,position=D(b.worldCenterOfMass),velocity=D(b.velocity),omega=D(b.angularVelocity),inertia=M3.Outer(D(q*Vector3.right),D(q*Vector3.right))*b.inertiaTensor.x+M3.Outer(D(q*Vector3.up),D(q*Vector3.up))*b.inertiaTensor.y+M3.Outer(D(q*Vector3.forward),D(q*Vector3.forward))*b.inertiaTensor.z};}
  void Capture(Part p){
   var h=p.FindModuleImplementing<LHZHookProbe>();if(h==null||h.Node==null||receiverLink.Node==null||vessel==p.vessel)return;
   var sourceEngines=p.vessel.parts.SelectMany(sourcePart=>sourcePart.FindModulesImplementing<ModuleEngines>()).ToArray();var source=Bodies(p.vessel);var target=Bodies(vessel);var bodies=source.Concat(target).Distinct().ToArray();var merge=MotionMath.Merge(bodies.Select(Motion).ToArray());var mouth=Mouth(p);var world=p.transform.TransformPoint(mouth);float speed=(p.rb.GetPointVelocity(world)-part.rb.GetPointVelocity(world)).magnitude;
   h.sourceName=p.vessel.vesselName;h.sourceType=(int)p.vessel.vesselType;h.sourceGuid=p.vessel.id.ToString();h.sourceRootFlightId=p.vessel.rootPart.flightID;h.receiverId=part.persistentId;h.receiverName=vessel.vesselName;h.receiverType=(int)vessel.vesselType;h.receiverRootFlightId=vessel.rootPart.flightID;h.receiverFixed=false;
   foreach(var b in bodies){b.velocity=F(merge.velocity+V3.Cross(merge.omega,D(b.worldCenterOfMass)-merge.center));b.angularVelocity=F(merge.omega);}
   var after=MotionMath.Merge(bodies.Select(Motion).ToArray());
   lastCapture=new CaptureRecord{caseName="player_auto",sourceMass=source.Sum(b=>(double)b.mass),receiverMass=target.Sum(b=>(double)b.mass),relativeSpeedBefore=speed,relativeSpeedAfter=(p.rb.GetPointVelocity(world)-part.rb.GetPointVelocity(world)).magnitude,absorbedEnergy=merge.relativeEnergyBefore-after.relativeEnergyBefore,linearMomentumError=(merge.momentum-after.momentum).Length,angularMomentumError=(merge.angularMomentum-after.angularMomentum).Length,momentumBefore=merge.momentum,momentumAfter=after.momentum,angularBefore=merge.angularMomentum,angularAfter=after.angularMomentum,fixedReceiver=false};
   h.SetAnchor(mouth,p.transform.InverseTransformDirection(-part.transform.up));receiverLink.SetAnchor(part.transform.InverseTransformPoint(world),Vector3.up);h.Node.attachedPart=part;h.Node.attachedPartId=part.flightID;receiverLink.Node.attachedPart=p;receiverLink.Node.attachedPartId=p.flightID;p.attachMode=AttachModes.STACK;
   var local=part.transform.InverseTransformPoint(p.transform.position);var rotation=Quaternion.Inverse(part.transform.rotation)*p.transform.rotation;
   if(p.vessel.IsAnchored)p.vessel.ResetRBAnchor();bool active=FlightGlobals.ActiveVessel==p.vessel;var cameraView=active?SaveCaptureCamera(p.vessel.rootPart):null;p.Couple(part);if(active){FlightGlobals.ForceSetActiveVessel(vessel);RestoreCaptureCamera(cameraView);}
   if(p.parent!=part||p.vessel!=vessel)throw new Exception("Native net coupling invariant failed");
   lastCapture.nativePositionJump=Vector3.Distance(local,part.transform.InverseTransformPoint(p.transform.position));lastCapture.nativeAngleJump=Quaternion.Angle(rotation,Quaternion.Inverse(part.transform.rotation)*p.transform.rotation);
   var nativeMotion=MotionMath.Merge(bodies.Select(Motion).ToArray());lastCapture.nativeMomentumChange=(nativeMotion.momentum-after.momentum).Length;lastCapture.nativeAngularChange=(nativeMotion.angularMomentum-after.angularMomentum).Length;
   h.hardConstraint=true;h.captureState="Locked";h.poseSaved=true;var lp=part.transform.InverseTransformPoint(p.transform.position);var q=Quaternion.Inverse(part.transform.rotation)*p.transform.rotation;h.lpx=lp.x;h.lpy=lp.y;h.lpz=lp.z;h.lqx=q.x;h.lqy=q.y;h.lqz=q.z;h.lqw=q.w;lockedHook=h;recoveryState="Locked";automatic=false;feedbackTarget=vessel;feedbackUntil=Time.time+8;feedbackReason="";feedbackContacts=4;BeginVisualBuffer();LockJoint(p);CapturedEngineSequence(sourceEngines);DrawNet();
  }
  static void LockJoint(Part p){if(p.attachJoint==null)return;foreach(var j in p.attachJoint.joints){j.xMotion=j.yMotion=j.zMotion=ConfigurableJointMotion.Locked;j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Locked;var d=new JointDrive();j.xDrive=j.yDrive=j.zDrive=j.angularXDrive=j.angularYZDrive=j.slerpDrive=d;}}
  void DrawNet(){
   if(part==null)return;if(ropes==null){var shader=Shader.Find("Unlit/Color")??Shader.Find("KSP/Specular");material=new Material(shader);material.color=new Color(.22f,.22f,.24f);if(material.HasProperty("_MainTex"))material.mainTexture=GameDatabase.Instance.GetTexture("KIU/Common/KIU_Common_texture/Shared_White.v3.3.0",false);ropes=new LineRenderer[4];for(int i=0;i<4;i++){var go=new GameObject("LHZ_PlayerNet_"+i);go.transform.SetParent(part.transform,false);var r=go.AddComponent<LineRenderer>();r.useWorldSpace=false;r.positionCount=33;r.startWidth=r.endWidth=.06f;r.numCornerVertices=2;r.numCapVertices=3;r.generateLightingData=true;r.sharedMaterial=material;ropes[i]=r;}}
   DrawBufferedRopes();
  }

  public void Update(){
   if(!HighLogic.LoadedSceneIsFlight)return;
   EnsureToolbar();
   string key=recoveryState=="Locked"?"Locked":recoveryState=="Tracking"?"Tracking":recoveryState=="Released"?"Released":recoveryState=="Failed"?"Failed":recoveryState.StartsWith("Rejected")?"Rejected":automatic?"Armed":"Ready";
   displayedState=KSP.Localization.Localizer.Format("#LHZ_State_"+key);
   displayedReason=ReasonText(lastRejectReason);
   displayedStation=KSP.Localization.Localizer.Format(stationKeeping?"#LHZ_On":"#LHZ_Off");
   bool held=lockedHook!=null;Events["ArmNet"].active=!held&&!automatic;Events["DisarmNet"].active=!held&&automatic;Events["ReleaseStage"].active=held;Events["ResetNet"].active=!held;Events["ReplayBuffer"].active=held;
  }
  [KSPAction("#LHZ_Arm")] public void ArmAction(KSPActionParam p){ArmNet();}
  [KSPAction("#LHZ_Disarm")] public void DisarmAction(KSPActionParam p){DisarmNet();}
  [KSPAction("#LHZ_Release")] public void ReleaseAction(KSPActionParam p){ReleaseStage();}
  [KSPAction("#LHZ_Station")] public void StationAction(KSPActionParam p){ToggleStation();}
  public override string GetInfo(){return KSP.Localization.Localizer.Format("#LHZ_ModuleInfo");}
  public void OnDestroy(){UnregisterToolbar();ClearCaptureCamera();ResetVisualBuffer();if(material!=null)UnityEngine.Object.Destroy(material);if(ropes!=null)foreach(var r in ropes)if(r!=null)UnityEngine.Object.Destroy(r.gameObject);}
 }
}
