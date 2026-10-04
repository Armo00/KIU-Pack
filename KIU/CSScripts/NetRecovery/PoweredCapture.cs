using System;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 [Serializable] public class EngineCutEvidence {public float captureTime,shutdownTime,thrustAtCapture;public int poweredAtCapture;public uint selectedCraftId;public bool nativeCoupledBeforeShutdown;}
 public partial class LHZNetController {
  // Engine telemetry and post-capture shutdown only. Engines never veto contact.
  public EngineCutEvidence engineCut;bool poweredShutdownPending,restoreShutdownPending;float shutdownAt;ModuleEngines[] captureEngines;
  static bool Producing(ModuleEngines e){return e.EngineIgnited&&(e.currentThrottle>.001f||e.finalThrust>.001f);}
  ModuleEngines[] PoweredEngines(Vessel v){return v.parts.SelectMany(p=>p.FindModulesImplementing<ModuleEngines>()).Where(Producing).ToArray();}
  void CapturedEngineSequence(ModuleEngines[] engines){
   var powered=engines.Where(Producing).ToArray();if(powered.Length==0)return;captureEngines=engines;engineCut=new EngineCutEvidence{captureTime=Time.time,thrustAtCapture=powered.Sum(e=>e.finalThrust),poweredAtCapture=powered.Length,selectedCraftId=powered[0].part.craftID,nativeCoupledBeforeShutdown=lockedHook!=null&&lockedHook.part.parent==part&&lockedHook.part.vessel==vessel};poweredShutdownPending=true;shutdownAt=Time.time+.18f;
  }
  void ShutdownCapturedEngines(){if(captureEngines!=null)foreach(var e in captureEngines)if(e!=null){e.Shutdown();e.part.Effect("running_closed",0);}poweredShutdownPending=false;}
  void TickEngineCut(){
   // A receiver can start before its recovered engines. Shutdown touches their
   // initialized effect/UI objects, so restore it after all Part OnStart calls.
   if(restoreShutdownPending){ShutdownCapturedEngines();restoreShutdownPending=false;}
   if(!poweredShutdownPending||Time.time<shutdownAt)return;ShutdownCapturedEngines();engineCut.shutdownTime=Time.time;
  }
 }
}
