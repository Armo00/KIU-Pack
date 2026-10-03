using System;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 [Serializable] public class RecoveryFeedbackEvidence {
  public bool visible,activeTarget;public string target,reason,lastRejection,text;public int contacts,poweredEngines;
  public float height,relativeSpeed,downwardSpeed,tilt,thrust,apertureX,apertureZ;public int installedHooks;
 }
 public partial class LHZNetController {
  [KSPField(isPersistant=true)] public string lastRejectReason="";
  [KSPField(guiActive=true,guiName="#LHZ_LastReject")] public string displayedReason;
  Vessel feedbackTarget;float feedbackUntil;string feedbackReason="";int feedbackContacts;
  static bool ObsoleteRejection(string reason){switch(reason){case "EngineThrust":case "EngineCount":case "EngineType":case "EngineOffRequired":case "EngineActive":case "outside tracking envelope":return true;default:return false;}}
  void ClearObsoleteRejection(){if(ObsoleteRejection(lastRejectReason))lastRejectReason="";const string prefix="Rejected: ";if(recoveryState!=null&&recoveryState.StartsWith(prefix)&&ObsoleteRejection(recoveryState.Substring(prefix.Length)))recoveryState=automatic?"Armed":"Ready";}
  void ClearFeedback(){lastRejectReason="";feedbackReason="";feedbackTarget=null;feedbackUntil=0;feedbackContacts=0;}
  void NoticeTarget(Vessel target,string reason){feedbackTarget=target;feedbackUntil=Time.time+8;feedbackReason=reason;}
  void NoticeActiveTarget(string reason){var v=FlightGlobals.ActiveVessel;if(v==null||v==vessel||!v.loaded||v.packed||Vector3.Distance(v.transform.position,part.transform.position)>500)return;if(v.parts.Any(p=>p.FindModuleImplementing<LHZHookProbe>()!=null))NoticeTarget(v,reason);}
  void RejectApproach(string reason){lastRejectReason=reason;feedbackReason=reason;feedbackUntil=Time.time+8;recoveryState="Rejected: "+reason;UnityEngine.Debug.Log("[KIUNetRecovery] rejected="+reason+" contacts="+(tracked==null?0:tracked.contacts.Count)+" target="+(trackedVessel==null?"none":trackedVessel.id.ToString()));}
  static string ReasonText(string reason){
   string key;
   switch(reason??""){
    case "":key="None";break;case "contact window":key="ContactWindow";break;case "contact stroke":key="ContactStroke";break;
    case "outside tracking envelope":key="Envelope";break;case "Armed: target lost":key="TargetLost";break;
    default:key=reason;break;
   }
   return KSP.Localization.Localizer.Format("#LHZ_Reason_"+key);
  }
  public RecoveryFeedbackEvidence FeedbackState(){
   var v=lockedHook!=null?vessel:trackedVessel??feedbackTarget;var hs=v==null?new Part[0]:v.parts.Where(p=>p.FindModuleImplementing<LHZHookProbe>()!=null&&Tip(p)!=null&&p.rb!=null).ToArray();
   var e=new RecoveryFeedbackEvidence{target=v==null?"":v.id.ToString(),reason=feedbackReason,lastRejection=lastRejectReason,contacts=lockedHook!=null?4:feedbackContacts,activeTarget=v!=null&&FlightGlobals.ActiveVessel==v};
   e.visible=recoveryWindowOpen&&recoveryUiVisible&&selectedReceiver==this;
   InitTracking();e.apertureX=part.transform.TransformVector(Vector3.right*(float)(axes[3].position-axes[2].position)).magnitude;e.apertureZ=part.transform.TransformVector(Vector3.forward*(float)(axes[1].position-axes[0].position)).magnitude;e.installedHooks=hs.Length;
   if(hs.Length>0){e.height=hs.Average(p=>Local(p).y-Plane);e.relativeSpeed=hs.Max(p=>{var w=p.transform.TransformPoint(Mouth(p));return (p.rb.GetPointVelocity(w)-part.rb.GetPointVelocity(w)).magnitude;});e.downwardSpeed=hs.Average(p=>(float)-Velocity(p).y);e.tilt=hs.Max(p=>Vector3.Angle(p.transform.up,part.transform.up));}
   if(v!=null){var engines=PoweredEngines(v);e.poweredEngines=engines.Length;e.thrust=engines.Sum(x=>x.finalThrust);}
   string state=KSP.Localization.Localizer.Format("#LHZ_State_"+(recoveryState=="Locked"?"Locked":tracked!=null?"Tracking":automatic?"Armed":"Ready"));
   e.text=KSP.Localization.Localizer.Format("#LHZ_HudStatus",state,e.contacts.ToString())+"\n"+KSP.Localization.Localizer.Format("#LHZ_HudMotion",e.height.ToString("F1"),e.downwardSpeed.ToString("F1"),e.relativeSpeed.ToString("F1"),e.tilt.ToString("F1"),maxCaptureSpeed.ToString("F0"))+"\n"+KSP.Localization.Localizer.Format("#LHZ_HudAperture",e.installedHooks.ToString(),e.apertureX.ToString("F2"),e.apertureZ.ToString("F2"));
   e.text+="\n"+KSP.Localization.Localizer.Format("#LHZ_HudEngine",e.poweredEngines.ToString(),e.thrust.ToString("F0"));
   if(feedbackReason!="")e.text+="\n"+ReasonText(feedbackReason);
   if(lastRejectReason!=""&&lastRejectReason!=feedbackReason)e.text+="\n"+KSP.Localization.Localizer.Format("#LHZ_HudLast",ReasonText(lastRejectReason));
   return e;
  }
 }
}
