using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace KIU.NetRecovery {
 public partial class LHZNetController {
  [KSPField(isPersistant=true)] public float stabilizeElapsed;
  class Rig {public Transform transform;public Vector3 origin;public Quaternion rotation;public Vector3 scale;public int role,end;public bool rotates;}
  readonly List<Rig> drums=new List<Rig>();readonly List<Rig> cradle=new List<Rig>();
  CableVisual[] steadyLines,feedLines;readonly Vector3[] feedPoints=new Vector3[33];bool mechanismsReady;Part cradleReference;float lastMechanismTime=-1;
  public string stabilizationState="Waiting";
  Vector3 lastCradleTarget,lastCradleUp;
  void InitializeMechanisms(){
   if(mechanismsReady)return;mechanismsReady=true;
   foreach(var t in part.GetComponentsInChildren<Transform>(true)){
    if(t.GetComponentInParent<Part>()!=part)continue;
    if(t.name.StartsWith("LHZ_WinchDrum_")||t.name.StartsWith("LHZ_WinchFrame_")||t.name.StartsWith("LHZ_WinchSheave_")){
     var bits=t.name.Split('_');int rope,end;if(bits.Length>=4&&int.TryParse(bits[2],out rope)&&int.TryParse(bits[3],out end))drums.Add(new Rig{transform=t,origin=t.localPosition,rotation=t.localRotation,scale=t.localScale,role=rope,end=end,rotates=t.name.StartsWith("LHZ_WinchDrum_")});
    }
    if(t.name.StartsWith("LHZ_Cradle"))cradle.Add(new Rig{transform=t,origin=t.localPosition,rotation=t.localRotation,scale=t.localScale});
   }
   steadyLines=new CableVisual[4];for(int i=0;i<4;i++){steadyLines[i]=new CableVisual(part.transform,"LHZ_SteadyLine_"+i,material);steadyLines[i].enabled=false;}
   feedLines=new CableVisual[8];for(int i=0;i<8;i++)feedLines[i]=new CableVisual(part.transform,"LHZ_WinchFeed_"+i,material);
   KIUModelRenderers.Refresh(part);
  }
  void UpdateWinches(){
   InitializeMechanisms();
   foreach(var d in drums){int r=d.role;if(r<0||r>3)continue;float delta=ropeOffsets[r]-(r%2==0?-1:1)*Gap,drop=(lockedHook==null?0:-guideDrop)+CableLayer(r);
    Vector3 shift=r<2?new Vector3(0,drop,delta):new Vector3(delta,drop,0);d.transform.localPosition=d.origin+d.transform.parent.InverseTransformVector(part.transform.TransformVector(shift));
    float payout=(float)Math.Max(0,cableGeometry.lengths[r]-2*Span)/2;Vector3 axis=r<2?Vector3.forward:Vector3.right;
    if(d.rotates)d.transform.localRotation=Quaternion.AngleAxis(payout/.42f*Mathf.Rad2Deg*(d.end==0?1:-1),d.transform.parent.InverseTransformDirection(part.transform.TransformDirection(axis)))*d.rotation;
   }
   for(int r=0;r<4;r++)for(int end=0;end<2;end++){float sign=end==0?-1:1;Vector3 a=ropes[r].GetPosition(end==0?0:32),b=a+(r<2?Vector3.right:Vector3.forward)*(sign*1.3f)+Vector3.up*.42f;for(int j=0;j<33;j++){float u=j/32f;feedPoints[j]=Vector3.Lerp(a,b,u)-Vector3.up*(.025f*4*u*(1-u));}feedLines[r*2+end].SetPositions(feedPoints,.025f);}
  }
  void TickRecoveryMechanisms(){
   InitializeMechanisms();if(lockedHook==null||hookPoses==null)return;
   if(!releaseRequested&&bufferElapsed>=BufferDuration)stabilizeElapsed=Math.Min(3,stabilizeElapsed+Time.deltaTime);
   if(cradleReference==null)cradleReference=Subtree(lockedHook.part).FirstOrDefault(p=>p.partInfo!=null&&p.partInfo.name.Contains("Stage1.Engine.Bay"))??lockedHook.part;
   Vector3 actualTarget=part.transform.InverseTransformPoint(cradleReference.transform.position)-MechanismCoreOffset;
   Vector3 actualUp=part.transform.InverseTransformDirection(cradleReference.transform.up).normalized;
   float time=releaseRequested?-releaseElapsed:stabilizeElapsed;if(lastMechanismTime==time&&bufferElapsed>=BufferDuration&&(actualTarget-lastCradleTarget).sqrMagnitude<1e-6f&&(actualUp-lastCradleUp).sqrMagnitude<1e-8f)return;lastMechanismTime=time;lastCradleTarget=actualTarget;lastCradleUp=actualUp;
   float tether=Smooth(stabilizeElapsed/.8f),travel=Smooth((stabilizeElapsed-.6f)/1.6f),grip=Smooth((stabilizeElapsed-2.1f)/.8f);
   if(releaseRequested){float withdraw=1-Smooth(releaseElapsed/.45f);tether*=withdraw;grip*=withdraw;travel*=withdraw;}
   stabilizationState=releaseRequested?"Releasing":stabilizeElapsed>=2.9f?"Secured":stabilizeElapsed>=.6f?"Cradle":stabilizeElapsed>0?"Tethers":"Buffering";
   Vector3 target=actualTarget;
   if(cradleReference==lockedHook.part)target.y-=24;
   float lift=Mathf.Clamp(target.y+3-12.5f,0,26+HeightOffset);Vector3 center=new Vector3(target.x-15,0,target.z-CenterZ)*travel;
   foreach(var rig in cradle){
    string name=rig.transform.name;Vector3 shift=center;rig.transform.localScale=rig.scale;
    if(name.Contains("Lift")){shift.y=lift*travel*.5f;rig.transform.localScale=new Vector3(rig.scale.x,rig.scale.y*(1+lift*travel/8.5f),rig.scale.z);}
    else if(!name.Contains("Base"))shift.y=lift*travel;
    if(name.Contains("Jaw")){var local=part.transform.InverseTransformPoint(rig.transform.parent.TransformPoint(rig.origin));Vector3 radial=new Vector3(local.x-15,0,local.z-CenterZ).normalized;shift+=radial*(1-grip)*1.6f;}
    rig.transform.localRotation=rig.rotation;
    if(name.Contains("Jaw")){
     Vector3 up=part.transform.InverseTransformDirection(cradleReference.transform.up).normalized;
     Quaternion tilt=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(Vector3.up,up),travel);
     Vector3 home=new Vector3(15,13.65f,CenterZ),original=part.transform.InverseTransformPoint(rig.transform.parent.TransformPoint(rig.origin));
     Vector3 radial=new Vector3(original.x-15,0,original.z-CenterZ).normalized;
     Vector3 position=home+center+Vector3.up*(lift*travel)+(up-Vector3.up)*(4.15f*travel)+tilt*(original-home+radial*(1-grip)*1.6f);
     rig.transform.localPosition=rig.transform.parent.InverseTransformPoint(part.transform.TransformPoint(position));
     rig.transform.rotation=part.transform.rotation*tilt*Quaternion.Inverse(part.transform.rotation)*rig.transform.parent.rotation*rig.rotation;
    }else rig.transform.localPosition=rig.origin+rig.transform.parent.InverseTransformVector(part.transform.TransformVector(shift));
   }
   for(int i=0;i<4;i++){
    var h=hookPoses[i];int role=h.rope;Vector3 anchor=role<2?new Vector3(0,Plane-6,CenterZ+(role==0?-21.5f:21.5f)):new Vector3(role==2?-21.5f:21.5f,Plane-6,CenterZ);
    Vector3 end=part.transform.InverseTransformPoint(h.part.transform.position)-MechanismCoreOffset;end=Vector3.Lerp(anchor,end,tether);
    for(int j=0;j<33;j++){float u=j/32f;tubePoints[i][j]=Vector3.Lerp(anchor,end,u)-Vector3.up*(.16f*4*u*(1-u));}
    steadyLines[i].enabled=tether>.01f;if(steadyLines[i].enabled)steadyLines[i].SetPositions(tubePoints[i],.014f);
   }
  }
  void ResetRecoveryMechanisms(){
   lastMechanismTime=-1;cradleReference=null;stabilizationState="Waiting";
   foreach(var r in cradle)if(r.transform!=null){r.transform.localPosition=r.origin;r.transform.localRotation=r.rotation;r.transform.localScale=r.scale;}
   if(steadyLines!=null)foreach(var line in steadyLines)line.enabled=false;
  }
  void DestroyRecoveryMechanisms(){if(steadyLines!=null)foreach(var line in steadyLines)line.Destroy();if(feedLines!=null)foreach(var line in feedLines)line.Destroy();}
  float RopeHeightAt(int rope,float along){
   if(ropes==null||!shapeInitialized)return Plane+CableLayer(rope);int axis=rope<2?0:2;
   for(int i=1;i<33;i++){var a=ropes[rope].GetPosition(i-1);var b=ropes[rope].GetPosition(i);if(along>=a[axis]&&along<=b[axis])return Mathf.Lerp(a.y,b.y,Mathf.InverseLerp(a[axis],b[axis],along));}
   return Plane+CableLayer(rope);
  }
 }
}
