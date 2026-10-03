// Keep the pilot's core view when KSP changes the active vessel during native coupling.
using UnityEngine;
namespace KIU.NetRecovery {
 public partial class LHZNetController {
  class CaptureCameraView {public Transform anchor;public float distance,pitch,heading;public int mode;}
  CaptureCameraView capturedCamera;int captureCameraFrames;
  CaptureCameraView SaveCaptureCamera(Part sourceRoot){
   var camera=FlightCamera.fetch;if(camera==null||sourceRoot==null)return null;
   Vector3 position=camera.Target==null?sourceRoot.transform.position:camera.Target.position;
   Quaternion rotation=camera.Target==null?sourceRoot.transform.rotation:camera.Target.rotation;
   var view=new CaptureCameraView{distance=camera.Distance,pitch=FlightCamera.CamPitch,heading=FlightCamera.CamHdg,mode=FlightCamera.CamMode};ClearCaptureCamera();
   var anchor=new GameObject("LHZ_CapturedCoreCamera").transform;
   anchor.SetParent(sourceRoot.transform,false);anchor.position=position;anchor.rotation=rotation;
   view.anchor=anchor;return view;
  }
  void ApplyCaptureCamera(CaptureCameraView view){
   if(view==null||view.anchor==null||FlightCamera.fetch==null)return;
   FlightCamera.SetTarget(view.anchor);FlightCamera.SetModeImmediate((FlightCamera.Modes)view.mode);
   FlightCamera.fetch.SetDistanceImmediate(view.distance);FlightCamera.CamPitch=view.pitch;FlightCamera.CamHdg=view.heading;
  }
  void RestoreCaptureCamera(CaptureCameraView view){capturedCamera=view;captureCameraFrames=3;ApplyCaptureCamera(view);}
  void TickCaptureCamera(){if(captureCameraFrames<=0)return;captureCameraFrames--;ApplyCaptureCamera(capturedCamera);}
  void ClearCaptureCamera(){
   if(capturedCamera!=null&&capturedCamera.anchor!=null){
    if(FlightCamera.fetch!=null&&FlightCamera.fetch.Target==capturedCamera.anchor&&capturedCamera.anchor.parent!=null)FlightCamera.SetTarget(capturedCamera.anchor.parent);
    UnityEngine.Object.Destroy(capturedCamera.anchor.gameObject);
   }
   capturedCamera=null;captureCameraFrames=0;
  }
 }
}
