using System;
using System.Linq;
namespace KIU.NetRecovery {
 public sealed class RailAxis {
  public double position,velocity;
  public RailAxis(double p){position=p;}
  public void Step(double target,double feedForward,double dt,double speed,double acceleration,double limit){
   if(dt<=0||dt>.25||Double.IsNaN(target)||Double.IsInfinity(target))return;
   target=Math.Max(-limit,Math.Min(limit,target));
   double desired=Math.Max(-speed,Math.Min(speed,feedForward+6*(target-position)));
   velocity+=Math.Max(-acceleration*dt,Math.Min(acceleration*dt,desired-velocity));
   position+=velocity*dt;
   if(position>limit){position=limit;velocity=Math.Min(0,velocity);}if(position< -limit){position=-limit;velocity=Math.Max(0,velocity);}
  }
 }
 public static class ActiveNetMath {
  public const double RailLimit=20,MaxSpeed=12,MaxAcceleration=24,CentralHalf=12.5,LookAhead=4;
  public static int[] Assign(Point3[] points){
   if(points.Length!=4)return null;double x=points.Average(p=>p.x),z=points.Average(p=>p.z);
   // Relative to the selected stage, never relative to the ship origin.
   int[] r=points.Select(p=>p.x>=x?(p.z>=z?1:3):(p.z>=z?2:0)).ToArray();
   return r.Distinct().Count()==4?r:null;
  }
  public static double Eta(double height,double down){return down>.5?Math.Max(0,Math.Min(LookAhead,height/down)):0;}
  public static double Cross(Point3 p,int rope){return rope<2?p.z:p.x;}
  public static double Along(Point3 p,int rope){return rope<2?p.x:p.z;}
  public static bool Envelope(Point3[] p,Point3[] v,int[] pairs){
   if(p.Length!=4||v.Length!=4||pairs==null||pairs.Distinct().Count()!=4)return false;
   double cx=0,cz=0;
   for(int i=0;i<4;i++){
    double t=Eta(p[i].y,-v[i].y),x=p[i].x+v[i].x*t,z=p[i].z+v[i].z*t;cx+=x/4;cz+=z/4;
    if(Math.Abs(x)>RailLimit||Math.Abs(z)>RailLimit)return false;
   }
   return Math.Abs(cx)<=CentralHalf&&Math.Abs(cz)<=CentralHalf;
  }
 }
}
