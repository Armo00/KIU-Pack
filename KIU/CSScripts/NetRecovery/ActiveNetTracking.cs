using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 [Serializable] public class TrackingEvidence {
  public string state,target,pairs;public bool selected,withinEnvelope;public int[] assignment;public uint[] hookIds;
  public float[] offsets,speeds,goals,center,predictedCenter;public float eta,maxError,railLimit,maxSpeed,maxAcceleration;public int guides;
 }
 public partial class LHZNetController {
  [KSPField(isPersistant=true)] public string hookRopePairs="",railPositions="";
  RailAxis[] axes;Approach tracked;Vessel trackedVessel;float[] trackingGoals=new float[4];bool trackingEnvelope;
  Dictionary<uint,int> savedPairs=new Dictionary<uint,int>();
  void InitTracking(){
   if(axes!=null)return;axes=Enumerable.Range(0,4).Select(i=>new RailAxis((i%2==0?-1:1)*Gap)).ToArray();
   var f=(railPositions??"").Split(',');if(f.Length==4)for(int i=0;i<4;i++){double n;if(Double.TryParse(f[i],NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!Double.IsNaN(n)&&Math.Abs(n)<=ActiveNetMath.RailLimit)axes[i].position=n;}
   savedPairs.Clear();foreach(var entry in (hookRopePairs??"").Split(',')){var q=entry.Split(':');uint id;int index;if(q.Length==2&&UInt32.TryParse(q[0],out id)&&Int32.TryParse(q[1],out index)&&index>=0&&index<4&&!savedPairs.ContainsKey(id))savedPairs[id]=index;}
  }
  void SaveRails(){railPositions=String.Join(",",axes.Select(a=>a.position.ToString("R",CultureInfo.InvariantCulture)).ToArray());}
  void ForgetTarget(){tracked=null;trackedVessel=null;trackingEnvelope=false;approaches.Clear();}
  void HomeTracking(float dt){InitTracking();ForgetTarget();for(int i=0;i<4;i++){trackingGoals[i]=(i%2==0?-1:1)*Gap;axes[i].Step(trackingGoals[i],0,dt,ActiveNetMath.MaxSpeed,ActiveNetMath.MaxAcceleration,ActiveNetMath.RailLimit);}SaveRails();DrawNet();}
  Point3 TrackingPoint(Vector3 p){return new Point3(p.x,p.y-Plane,p.z-CenterZ);}
  Point3 Velocity(Part p){var w=p.transform.TransformPoint(Mouth(p));var v=part.transform.InverseTransformVector(p.rb.GetPointVelocity(w)-part.rb.GetPointVelocity(w));return new Point3(v.x,v.y,v.z);}
  int SavedRope(Part p){InitTracking();int r;if(!savedPairs.TryGetValue(p.persistentId,out r))throw new Exception("Persistent hook/rope pair missing for "+p.persistentId);return r;}
  void SavePairs(Approach a){savedPairs.Clear();for(int i=0;i<4;i++)savedPairs.Add(a.hooks[i].persistentId,a.rope[i]);hookRopePairs=String.Join(",",savedPairs.OrderBy(x=>x.Value).Select(x=>x.Key+":"+x.Value).ToArray());SaveRails();}
  void RestorePairs(Part[] hs){
   InitTracking();if(hs.Length!=4)throw new Exception("Four recovered hooks required");
   if(hs.All(p=>savedPairs.ContainsKey(p.persistentId))&&hs.Select(p=>savedPairs[p.persistentId]).Distinct().Count()==4)return;
   if(!String.IsNullOrEmpty(hookRopePairs))throw new Exception("Saved net pairing is invalid; refuse reassignment");
   // One-time migration for older center-only saves, based on stage-relative geometry.
   var ps=hs.Select(p=>TrackingPoint(Local(p))).ToArray();var map=ActiveNetMath.Assign(ps);if(map==null)throw new Exception("Legacy net pairing cannot be migrated");SavePairs(new Approach{hooks=hs,rope=map});
  }
  void TickTracking(float dt){
   InitTracking();var candidates=FlightGlobals.Vessels.Where(v=>v!=null&&v!=vessel&&v.loaded&&!v.packed&&Vector3.Distance(v.transform.position,part.transform.position)<180).ToArray();
   if(trackedVessel!=null&&(!candidates.Contains(trackedVessel)||tracked.hooks.Any(p=>p==null||p.rb==null||p.vessel!=trackedVessel))){ForgetTarget();recoveryState="Armed: target lost";}
   if(tracked==null){
    float best=float.MaxValue;Approach chosen=null;Vessel target=null;string rejection="Armed";
    foreach(var v in candidates){
     var hs=v.parts.Where(p=>p.FindModuleImplementing<LHZHookProbe>()!=null&&Tip(p)!=null&&p.rb!=null).OrderBy(p=>p.flightID).ToArray();if(hs.Length!=4)continue;
     var ps=hs.Select(p=>TrackingPoint(Local(p))).ToArray();var vs=hs.Select(Velocity).ToArray();float height=(float)ps.Average(p=>p.y),down=(float)-vs.Average(p=>p.y);if(height<=0||height>140||down<=.5f)continue;
     if(!hs.All(Deployed)){rejection="Rejected: HookClosed";continue;}
     var map=ActiveNetMath.Assign(ps);if(!ActiveNetMath.Envelope(ps,vs,map)){rejection="Rejected: outside tracking envelope";continue;}
     float eta=height/down;if(eta<best){best=eta;target=v;chosen=new Approach{hooks=hs,previous=hs.Select(Local).ToArray(),rope=map};}
    }
    if(chosen==null){HomeTracking(dt);recoveryState=rejection;return;}tracked=chosen;trackedVessel=target;recoveryState="Tracking";
   }
   var a=tracked;var points=a.hooks.Select(p=>TrackingPoint(Local(p))).ToArray();var velocities=a.hooks.Select(Velocity).ToArray();trackingEnvelope=ActiveNetMath.Envelope(points,velocities,a.rope);
   if(!trackingEnvelope){HomeTracking(dt);recoveryState="Rejected: outside tracking envelope";return;}
   double[] before=axes.Select(x=>x.position).ToArray();
   for(int i=0;i<4;i++){int r=a.rope[i];double eta=ActiveNetMath.Eta(points[i].y,-velocities[i].y),lateral=ActiveNetMath.Cross(velocities[i],r),goal=ActiveNetMath.Cross(points[i],r)+lateral*eta;
    double feed=Math.Max(-ActiveNetMath.MaxSpeed,Math.Min(ActiveNetMath.MaxSpeed,(goal-trackingGoals[r])/dt));trackingGoals[r]=(float)goal;axes[r].Step(goal,feed,dt,ActiveNetMath.MaxSpeed,ActiveNetMath.MaxAcceleration,ActiveNetMath.RailLimit);}
   bool engine=EngineBlocked(trackedVessel);
   for(int i=0;i<4;i++){
    var p=a.hooks[i];var point=Local(p);int r=a.rope[i];var w=p.transform.TransformPoint(Mouth(p));var velocity=p.rb.GetPointVelocity(w)-part.rb.GetPointVelocity(w);
    var d=CaptureGate.Check(Relative(a.previous[i],r,(float)before[r]),Relative(point,r,(float)axes[r].position),Deployed(p),Vector3.Dot(p.transform.up,part.transform.up),-Vector3.Dot(velocity,part.transform.up),Span,.18,20);
    if(d!=GateDecision.NoCrossing){string reason=engine?"EngineActive":velocity.magnitude>20?"TooFast":d.ToString();if(lastContacts.Count>=64)lastContacts.RemoveAt(0);lastContacts.Add(new ContactEvidence{craftId=p.craftID,time=Time.time,decision=reason,position=new[]{point.x,point.y,point.z}});if(reason=="Captured"){a.hit[i]=true;if(a.first<0)a.first=Time.time;}else recoveryState="Rejected: "+reason;}
    a.previous[i]=point;
   }
   SaveRails();DrawNet();if(a.hit.All(x=>x)){SavePairs(a);Capture(a.hooks[0]);ForgetTarget();return;}
   if(a.first>=0&&Time.time-a.first>.12f){a.hit=new bool[4];a.first=-1;recoveryState="Rejected: contact window";}
  }
  public TrackingEvidence TrackingState(){
   InitTracking();var ps=tracked==null?new Point3[0]:tracked.hooks.Select(p=>TrackingPoint(Local(p))).ToArray();var vs=tracked==null?new Point3[0]:tracked.hooks.Select(Velocity).ToArray();float err=0;
   if(tracked!=null)for(int i=0;i<4;i++)err=Math.Max(err,(float)Math.Abs(ActiveNetMath.Cross(ps[i],tracked.rope[i])-axes[tracked.rope[i]].position));
   return new TrackingEvidence{state=recoveryState,target=trackedVessel==null?"":trackedVessel.id.ToString(),pairs=hookRopePairs,selected=tracked!=null,withinEnvelope=trackingEnvelope,assignment=tracked==null?new int[0]:tracked.rope.ToArray(),hookIds=tracked==null?new uint[0]:tracked.hooks.Select(p=>p.persistentId).ToArray(),offsets=axes.Select(x=>(float)x.position).ToArray(),speeds=axes.Select(x=>(float)x.velocity).ToArray(),goals=trackingGoals.ToArray(),center=ps.Length==0?new float[0]:new[]{(float)ps.Average(p=>p.x),(float)ps.Average(p=>p.y),(float)ps.Average(p=>p.z)},predictedCenter=ps.Length==0?new float[0]:new[]{(float)ps.Select((p,i)=>p.x+vs[i].x*ActiveNetMath.Eta(p.y,-vs[i].y)).Average(),(float)ps.Select((p,i)=>p.z+vs[i].z*ActiveNetMath.Eta(p.y,-vs[i].y)).Average()},eta=ps.Length==0?0:(float)ActiveNetMath.Eta(ps.Average(p=>p.y),-vs.Average(p=>p.y)),maxError=err,railLimit=(float)ActiveNetMath.RailLimit,maxSpeed=(float)ActiveNetMath.MaxSpeed,maxAcceleration=(float)ActiveNetMath.MaxAcceleration,guides=guidePoses==null?0:guidePoses.Count};
  }
 }
}
