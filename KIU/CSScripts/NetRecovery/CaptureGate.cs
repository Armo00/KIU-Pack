using System;
namespace KIU.NetRecovery {
 public struct Point3 { public double x,y,z; public Point3(double a,double b,double c){x=a;y=b;z=c;} }
 public enum GateDecision { NoCrossing, WrongDirection, HookClosed, OutsideRope, OutsideThroat, BadOrientation, TooFast, Captured }
 public static class CaptureGate {
  // Prototype throat gate only: straight finite rope, platform-local Y normal.
  // Sweeps the hook's defined throat reference point; not arbitrary Part contact.
  public static GateDecision Check(Point3 before,Point3 after,bool deployed,double inwardAlignment,double downwardSpeed,double halfSpan,double throatRadius,double maxSpeed) {
   if(before.y<0 && after.y>=0)return GateDecision.WrongDirection;
   if(!(before.y>0 && after.y<=0))return GateDecision.NoCrossing;
   if(!deployed)return GateDecision.HookClosed;
   double t=before.y/(before.y-after.y);
   double x=before.x+t*(after.x-before.x), z=before.z+t*(after.z-before.z);
   if(Math.Abs(x)>halfSpan)return GateDecision.OutsideRope;
   if(Math.Abs(z)>throatRadius)return GateDecision.OutsideThroat;
   if(inwardAlignment<0.8)return GateDecision.BadOrientation;
   if(downwardSpeed<=0 || downwardSpeed>maxSpeed)return GateDecision.TooFast;
   return GateDecision.Captured;
  }
 }
}
