// Stable COM-frame energy budget integrated in P02R1. P02 first run preserved.
using System;
namespace KIU.NetRecovery {
 public struct V3 {
  public double x,y,z;public V3(double a,double b,double c){x=a;y=b;z=c;}
  public static V3 operator+(V3 a,V3 b){return new V3(a.x+b.x,a.y+b.y,a.z+b.z);}
  public static V3 operator-(V3 a,V3 b){return new V3(a.x-b.x,a.y-b.y,a.z-b.z);}
  public static V3 operator*(V3 a,double k){return new V3(a.x*k,a.y*k,a.z*k);}
  public static V3 operator/(V3 a,double k){return a*(1/k);}
  public static double Dot(V3 a,V3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
  public static V3 Cross(V3 a,V3 b){return new V3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
  public double Length {get{return Math.Sqrt(Dot(this,this));}}
 }
 public class M3 {
  public double[] m=new double[9];
  public static M3 Identity(double d){var r=new M3();r.m[0]=r.m[4]=r.m[8]=d;return r;}
  public static M3 Outer(V3 a,V3 b){var r=new M3();double[] aa={a.x,a.y,a.z},bb={b.x,b.y,b.z};for(int i=0;i<3;i++)for(int j=0;j<3;j++)r.m[i*3+j]=aa[i]*bb[j];return r;}
  public static M3 operator+(M3 a,M3 b){var r=new M3();for(int i=0;i<9;i++)r.m[i]=a.m[i]+b.m[i];return r;}
  public static M3 operator-(M3 a,M3 b){var r=new M3();for(int i=0;i<9;i++)r.m[i]=a.m[i]-b.m[i];return r;}
  public static M3 operator*(M3 a,double s){var r=new M3();for(int i=0;i<9;i++)r.m[i]=a.m[i]*s;return r;}
  public V3 Times(V3 v){return new V3(m[0]*v.x+m[1]*v.y+m[2]*v.z,m[3]*v.x+m[4]*v.y+m[5]*v.z,m[6]*v.x+m[7]*v.y+m[8]*v.z);}
  public V3 Solve(V3 b){double a=m[0],d=m[1],g=m[2],h=m[3],e=m[4],f=m[5],i=m[6],j=m[7],k=m[8];double det=a*(e*k-f*j)-d*(h*k-f*i)+g*(h*j-e*i);if(Math.Abs(det)<1e-15)throw new Exception("Singular inertia");var inv=new M3();inv.m=new[]{e*k-f*j,g*j-d*k,d*f-g*e,f*i-h*k,a*k-g*i,g*h-a*f,h*j-e*i,d*i-a*j,a*e-d*h};return inv.Times(b)/det;}
 }
 public class BodyMotion {public double mass;public V3 position,velocity,omega;public M3 inertia;}
 public class MergeMotion {public double mass,energyBefore,energyAfter,relativeEnergyBefore,relativeEnergyAfter;public double dissipatedEnergy {get{return relativeEnergyBefore-relativeEnergyAfter;}}public V3 center,velocity,omega,momentum,angularMomentum;}
 public static class MotionMath {
  public static MergeMotion Merge(BodyMotion[] bodies){
   var r=new MergeMotion();foreach(var b in bodies){if(b.mass<=0||b.inertia==null)throw new Exception("Invalid body");r.mass+=b.mass;r.center+=b.position*b.mass;r.momentum+=b.velocity*b.mass;}
   if(r.mass<=0)throw new Exception("Empty bodies");r.center/=r.mass;r.velocity=r.momentum/r.mass;var tensor=new M3();
   foreach(var b in bodies){V3 arm=b.position-r.center;tensor+=b.inertia+(M3.Identity(V3.Dot(arm,arm))-M3.Outer(arm,arm))*b.mass;r.angularMomentum+=b.inertia.Times(b.omega)+V3.Cross(arm,(b.velocity-r.velocity)*b.mass);r.energyBefore+=.5*b.mass*V3.Dot(b.velocity,b.velocity)+.5*V3.Dot(b.omega,b.inertia.Times(b.omega));V3 relative=b.velocity-r.velocity;r.relativeEnergyBefore+=.5*b.mass*V3.Dot(relative,relative)+.5*V3.Dot(b.omega,b.inertia.Times(b.omega));}
   r.omega=tensor.Solve(r.angularMomentum);foreach(var b in bodies){V3 v=r.velocity+V3.Cross(r.omega,b.position-r.center);r.energyAfter+=.5*b.mass*V3.Dot(v,v)+.5*V3.Dot(r.omega,b.inertia.Times(r.omega));V3 relative=V3.Cross(r.omega,b.position-r.center);r.relativeEnergyAfter+=.5*b.mass*V3.Dot(relative,relative)+.5*V3.Dot(r.omega,b.inertia.Times(r.omega));}return r;
  }
 }
}
