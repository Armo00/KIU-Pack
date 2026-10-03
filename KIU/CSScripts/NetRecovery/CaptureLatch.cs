using System;
using System.Linq;
namespace KIU.NetRecovery {
 // Retain real, gated contacts while the other hooks cross the plane. A visual
 // cable can yield within this stroke; no rigidbody rope or long-lived latch.
 public sealed class CaptureLatch {
  public const double MaxSeconds=1.5,MaxDrop=4,MaxLift=.25;
  readonly double seconds,drop;
  public CaptureLatch(double seconds=MaxSeconds,double drop=MaxDrop){this.seconds=seconds;this.drop=drop;}
  public readonly bool[] hit=new bool[4];
  public double first=-1;
  public int Count {get{return hit.Count(x=>x);}}
  public bool Complete {get{return Count==4;}}
  public void Record(int index,double time){hit[index]=true;if(first<0)first=time;}
  public string Expired(double time,double[] heights){
   if(first<0)return null;
   if(time-first>seconds)return "contact window";
   for(int i=0;i<4;i++)if(hit[i]&&(heights[i]<-drop||heights[i]>MaxLift))return "contact stroke";
   return null;
  }
 }
}
