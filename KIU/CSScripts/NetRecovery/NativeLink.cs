using System;
using UnityEngine;
namespace KIU.NetRecovery {
 public class LinkProbe : PartModule {
  [KSPField(isPersistant=true)] public bool linkActive;
  [KSPField(isPersistant=true)] public float ax,ay,az,ox,oy=1,oz;
  public AttachNode Node {get{return part.FindAttachNode("lhzCapture");}}
  public void SetAnchor(Vector3 p,Vector3 o){ax=p.x;ay=p.y;az=p.z;ox=o.x;oy=o.y;oz=o.z;linkActive=true;Apply();}
  public void Apply(){if(!linkActive||part==null||Node==null)return;Node.position=Node.originalPosition=new Vector3(ax,ay,az);Node.orientation=Node.originalOrientation=new Vector3(ox,oy,oz);Node.owner=part;}
  public override void OnLoad(ConfigNode node){base.OnLoad(node);Apply();}
  public override void OnStart(StartState start){base.OnStart(start);Apply();}
 }
 public class LHZHookProbe : LinkProbe {
  [KSPField(isPersistant=true)] public bool poseSaved,hardConstraint;
  [KSPField(isPersistant=true)] public float lpx,lpy,lpz,lqx,lqy,lqz,lqw=1;
  [KSPField(isPersistant=true)] public string captureState="Idle",sourceName="",receiverName="",sourceGuid="";
  [KSPField(isPersistant=true)] public uint receiverId,sourceRootFlightId,receiverRootFlightId;
  [KSPField(isPersistant=true)] public int sourceType,receiverType;
  [KSPField(isPersistant=true)] public bool receiverFixed=true;
 }
 public class LHZReceiverProbe : LinkProbe {
 [KSPField(isPersistant=true)] public bool armed=true;

}
 [Serializable] public class CaptureRecord {public string caseName;public double sourceMass,receiverMass,absorbedEnergy,linearMomentumError,angularMomentumError;public float relativeSpeedBefore,relativeSpeedAfter;public float nativePositionJump,nativeAngleJump;public double nativeMomentumChange,nativeAngularChange;public V3 momentumBefore,momentumAfter,angularBefore,angularAfter;public bool fixedReceiver;}
 [Serializable] public class ContactEvidence {public uint craftId;public float time;public string decision;public float[] position;}
}
