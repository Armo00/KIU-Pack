using System;
using System.Linq;
namespace KIU.NetRecovery {
 public sealed class RailAxis {
  public double position,velocity;
  public RailAxis(double p){position=p;}
  public void Step(double target,double feedForward,double dt,double speed,double acceleration,double limit){
   if(dt<=0||dt>.25||Double.IsNaN(target)||Double.IsInfinity(target))return;
   target=Math.Max(-limit,Math.Min(limit,target));
   double desired=Math.Max(-speed,Math.Min(speed,feedForward+ActiveNetMath.PositionGain*(target-position)));
   velocity+=Math.Max(-acceleration*dt,Math.Min(acceleration*dt,desired-velocity));
   position+=velocity*dt;
   if(position>limit){position=limit;velocity=Math.Min(0,velocity);}if(position< -limit){position=-limit;velocity=Math.Max(0,velocity);}
  }
 }
 public static class ActiveNetMath {
  public const double RailLimit=20,HalfSpan=22,MaxSpeed=48,MaxAcceleration=144,PositionGain=12,LookAhead=1.5,MaxLead=6;
  public static int[] Assign(Point3[] points){
   if(points.Length!=4)return null;double x=points.Average(p=>p.x),z=points.Average(p=>p.z);
   // Relative to the selected stage, never relative to the ship origin.
   int[] r=points.Select(p=>p.x>=x?(p.z>=z?1:3):(p.z>=z?2:0)).ToArray();
   return r.Distinct().Count()==4?r:null;
  }
  public static double Eta(double height,double down){return down>.5?Math.Max(0,Math.Min(LookAhead,height/down)):0;}
  public static double Goal(Point3 point,Point3 velocity,int rope,bool retained){double lead=retained?0:Cross(velocity,rope)*Eta(point.y,-velocity.y);return Math.Max(-RailLimit,Math.Min(RailLimit,Cross(point,rope)+Math.Max(-MaxLead,Math.Min(MaxLead,lead))));}
  // Four rope roles, any number of hooks. Distinct assignment also supports
  // cardinal layouts without classifying boundary points into quadrants.
  public static int[] SelectHooks(Point3[] points,double clearance,double[] currentRails=null){
   if(points.Length<4)return null;double cx=points.Average(p=>p.x),cz=points.Average(p=>p.z);
   double[] costs=Enumerable.Repeat(Double.PositiveInfinity,16).ToArray();costs[0]=0;int[][] picks=new int[16][];picks[0]=new[]{-1,-1,-1,-1};
   for(int i=0;i<points.Length;i++){
    var next=(double[])costs.Clone();var chosen=(int[][])picks.Clone();
    for(int mask=0;mask<16;mask++)if(picks[mask]!=null)for(int r=0;r<4;r++)if((mask&(1<<r))==0){
     double cross=Cross(points[i],r)-(r<2?cz:cx),outward=(r%2==0?-1:1)*cross,along=Along(points[i],r)-(r<2?cx:cz),missing=Math.Max(0,clearance/2-outward);
     double travel=currentRails==null?0:Cross(points[i],r)-currentRails[r],outsideRail=Math.Max(0,Math.Abs(Cross(points[i],r))-RailLimit),outsideSpan=Math.Max(0,Math.Abs(Along(points[i],r))-HalfSpan);double score=costs[mask]+100*missing*missing+travel*travel+1000000*(outsideRail*outsideRail+outsideSpan*outsideSpan)-.01*outward+.001*along*along;int m=mask|(1<<r);
     if(score<next[m]){next[m]=score;chosen[m]=(int[])picks[mask].Clone();chosen[m][r]=i;}
    }costs=next;picks=chosen;
   }return picks[15];
  }
  public static void StepRails(RailAxis[] axes,double[] goals,double[] feeds,double dt,double clearance){
   clearance=Math.Max(5.4,Math.Min(2*RailLimit,clearance));
   for(int low=0;low<4;low+=2){int high=low+1;
    double width=Math.Max(clearance,goals[high]-goals[low]),center=(goals[high]+goals[low])/2;width=Math.Min(2*RailLimit,width);center=Math.Max(-RailLimit+width/2,Math.Min(RailLimit-width/2,center));goals[low]=center-width/2;goals[high]=center+width/2;
    double p0=axes[low].position,p1=axes[high].position;axes[low].Step(goals[low],feeds[low],dt,MaxSpeed,MaxAcceleration,RailLimit);axes[high].Step(goals[high],feeds[high],dt,MaxSpeed,MaxAcceleration,RailLimit);
    if(dt<=0||dt>.25)continue;
    double gap=Math.Max(0,p1-p0-clearance),safe=Math.Sqrt(MaxAcceleration*MaxAcceleration*dt*dt+4*MaxAcceleration*gap)-MaxAcceleration*dt,closing=axes[low].velocity-axes[high].velocity;
    if(closing>safe){double correction=(closing-safe)/2;axes[low].velocity-=correction;axes[high].velocity+=correction;axes[low].position=p0+axes[low].velocity*dt;axes[high].position=p1+axes[high].velocity*dt;}
    if(axes[high].position-axes[low].position<clearance){center=Math.Max(-RailLimit+clearance/2,Math.Min(RailLimit-clearance/2,(axes[high].position+axes[low].position)/2));axes[low].position=center-clearance/2;axes[high].position=center+clearance/2;double velocity=(axes[low].velocity+axes[high].velocity)/2;axes[low].velocity=axes[high].velocity=velocity;}
    if(axes[high].position>RailLimit){double shift=axes[high].position-RailLimit;axes[high].position-=shift;axes[low].position-=shift;axes[high].velocity=Math.Min(0,axes[high].velocity);}
    if(axes[low].position<-RailLimit){double shift=-RailLimit-axes[low].position;axes[high].position+=shift;axes[low].position+=shift;axes[low].velocity=Math.Max(0,axes[low].velocity);}
   }
  }
  public static double Cross(Point3 p,int rope){return rope<2?p.z:p.x;}
  public static double Along(Point3 p,int rope){return rope<2?p.x:p.z;}
  // Acquisition uses the current finite cable geometry. A predicted point outside
  // the rails is clamped for steering; it must not reject an in-frame target.
  public static string EnvelopeReason(Point3[] p,int[] pairs,double tolerance=0){
   if(p==null||p.Length!=4||pairs==null||pairs.Length!=4||pairs.Any(r=>r<0||r>3)||pairs.Distinct().Count()!=4)return "HookLayout";
   for(int i=0;i<4;i++){
    double cross=Cross(p[i],pairs[i]),along=Along(p[i],pairs[i]);
    if(Double.IsNaN(cross)||Double.IsInfinity(cross)||Double.IsNaN(along)||Double.IsInfinity(along))return "HookLayout";
    if(Math.Abs(cross)>RailLimit+Math.Max(0,tolerance))return "OutsideRailTravel";
    if(Math.Abs(along)>HalfSpan)return "OutsideCableSpan";
   }
   return "";
  }
  public static bool Envelope(Point3[] p,Point3[] v,int[] pairs){return v!=null&&v.Length==4&&EnvelopeReason(p,pairs)=="";}
 }
}
