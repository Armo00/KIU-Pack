using System;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 [Serializable] public class EngineCutEvidence {public float captureTime,shutdownTime,thrustAtCapture;public int poweredAtCapture;public uint selectedCraftId;public bool nativeCoupledBeforeShutdown;}
 public partial class LHZNetController {
  // Explicit option for a single YF-100 landing burn; other captures retain the engine-off gate.
  [KSPField] public bool allowSinglePoweredCapture=true;
  [KSPField] public string landingEngineID="YF-100";
  [KSPField] public float maxPoweredThrustFraction=.70f;
  public EngineCutEvidence engineCut;bool poweredShutdownPending;float shutdownAt;ModuleEngines[] captureEngines;
  static bool Producing(ModuleEngines e){return e.EngineIgnited&&(e.currentThrottle>.001f||e.finalThrust>.001f);}
  bool EngineBlocked(Vessel v){var engines=v.parts.SelectMany(p=>p.FindModulesImplementing<ModuleEngines>()).Where(Producing).ToArray();return engines.Length>0&&(!allowSinglePoweredCapture||engines.Length!=1||engines[0].engineID!=landingEngineID||engines[0].maxThrust<=0||engines[0].finalThrust>engines[0].maxThrust*Mathf.Clamp01(maxPoweredThrustFraction));}
  void CapturedEngineSequence(ModuleEngines[] engines){
   var powered=engines.Where(Producing).ToArray();if(powered.Length==0)return;captureEngines=engines;engineCut=new EngineCutEvidence{captureTime=Time.time,thrustAtCapture=powered.Sum(e=>e.finalThrust),poweredAtCapture=powered.Length,selectedCraftId=powered[0].part.craftID,nativeCoupledBeforeShutdown=lockedHook!=null&&lockedHook.part.parent==part&&lockedHook.part.vessel==vessel};poweredShutdownPending=true;shutdownAt=Time.time+.18f;
  }
  void ShutdownCapturedEngines(){if(captureEngines!=null)foreach(var e in captureEngines)if(e!=null){e.Shutdown();e.part.Effect("running_closed",0);}poweredShutdownPending=false;}
  void TickEngineCut(){if(!poweredShutdownPending||Time.time<shutdownAt)return;foreach(var e in captureEngines)if(e!=null){e.Shutdown();e.part.Effect("running_closed",0);}engineCut.shutdownTime=Time.time;poweredShutdownPending=false;}
 }
}
