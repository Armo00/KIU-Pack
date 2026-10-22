using UnityEngine;
namespace KIU.NetRecovery {
 public partial class LHZNetController {
  [KSPField(isPersistant=true)] public float stationHeading;
  void RememberStationHeading(){
   Vector3 up=(part.transform.position-(Vector3)vessel.mainBody.position).normalized;
   Vector3 east=Vector3.Cross(vessel.mainBody.transform.up,up).normalized,north=Vector3.Cross(up,east);
   stationHeading=Mathf.Atan2(Vector3.Dot(part.transform.forward,east),Vector3.Dot(part.transform.forward,north));
  }
  void MaintainStation(){
   if(!stationKeeping||part.partBuoyancy==null||!part.partBuoyancy.splashed)return;
   Vector3 up=(part.transform.position-(Vector3)vessel.mainBody.position).normalized;
   Vector3 target=(Vector3)vessel.mainBody.GetWorldSurfacePosition(stationLat,stationLon,vessel.altitude);
   Vector3 error=Vector3.ProjectOnPlane(target-part.transform.position,up),velocity=Vector3.ProjectOnPlane((Vector3)vessel.srf_velocity,up);
   // Acceleration and inertia normalization preserves the controller response
   // across the full-load ship and captured core. No vertical force is applied.
   Vector3 acceleration=Vector3.ClampMagnitude(error*.0324f-velocity*.324f,.08f);
   part.rb.AddForce(acceleration*(float)vessel.GetTotalMass(),ForceMode.Force);
   Vector3 east=Vector3.Cross(vessel.mainBody.transform.up,up).normalized,north=Vector3.Cross(up,east);
   Vector3 forward=north*Mathf.Cos(stationHeading)+east*Mathf.Sin(stationHeading);
   Quaternion delta=Quaternion.LookRotation(forward,up)*Quaternion.Inverse(part.transform.rotation);float angle;Vector3 axis;delta.ToAngleAxis(out angle,out axis);if(angle>180)angle-=360;
   Vector3 alpha=Vector3.ClampMagnitude(axis*(angle*Mathf.Deg2Rad*.04f)-part.rb.angularVelocity*.4f,.015f);
   Quaternion inertiaFrame=part.rb.rotation*part.rb.inertiaTensorRotation;Vector3 local=Quaternion.Inverse(inertiaFrame)*alpha;
   part.rb.AddTorque(inertiaFrame*Vector3.Scale(part.rb.inertiaTensor,local),ForceMode.Force);
  }
 }
}
