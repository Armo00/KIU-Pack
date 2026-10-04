using System;
using System.Collections.Generic;
using System.Linq;
using KSP.UI.Screens;
using UnityEngine;
namespace KIU.NetRecovery {
 public partial class LHZNetController {
  static List<LHZNetController> receivers=new List<LHZNetController>();
  static ApplicationLauncherButton recoveryButton;static Texture2D recoveryIcon;
  static bool recoveryWindowOpen,recoveryUiVisible=true;static LHZNetController selectedReceiver;
  static Rect recoveryWindow=new Rect(24,140,550,300);GUIStyle recoveryText;
  bool toolbarRegistered;
  static string Loc(string key){return KSP.Localization.Localizer.Format("#LHZ_"+key);}
  static void OpenWindow(){recoveryWindowOpen=true;}
  static void CloseWindow(){recoveryWindowOpen=false;}
  static void HideWindowUi(){recoveryUiVisible=false;}static void ShowWindowUi(){recoveryUiVisible=true;}
  // KSP EventVoid records Delegate.Target.GetType(); static callbacks have no target.
  void LauncherReady(){EnsureToolbar();}
  void WindowUiHidden(){HideWindowUi();}void WindowUiShown(){ShowWindowUi();}
  void RegisterToolbar(){if(!receivers.Contains(this))receivers.Add(this);if(selectedReceiver==null)selectedReceiver=this;if(!toolbarRegistered){GameEvents.onGUIApplicationLauncherReady.Add(LauncherReady);GameEvents.onHideUI.Add(WindowUiHidden);GameEvents.onShowUI.Add(WindowUiShown);toolbarRegistered=true;}EnsureToolbar();}
  static void EnsureToolbar(){
   if(recoveryButton!=null||!ApplicationLauncher.Ready||ApplicationLauncher.Instance==null||receivers.Count==0)return;
   if(recoveryIcon==null){recoveryIcon=new Texture2D(32,32,TextureFormat.RGBA32,false);for(int y=0;y<32;y++)for(int x=0;x<32;x++){bool frame=(x==4||x==27)&&y>=4&&y<=27||y==27&&x>=4&&x<=27;bool net=(x==11||x==20)&&y>=8&&y<=24||(y==12||y==20)&&x>=8&&x<=24;recoveryIcon.SetPixel(x,y,frame?Color.white:net?new Color(.25f,.8f,1,1):Color.clear);}recoveryIcon.Apply();}
   recoveryButton=ApplicationLauncher.Instance.AddModApplication(OpenWindow,CloseWindow,null,null,null,null,ApplicationLauncher.AppScenes.FLIGHT|ApplicationLauncher.AppScenes.MAPVIEW,recoveryIcon);
  }
  void UnregisterToolbar(){if(toolbarRegistered){GameEvents.onGUIApplicationLauncherReady.Remove(LauncherReady);GameEvents.onHideUI.Remove(WindowUiHidden);GameEvents.onShowUI.Remove(WindowUiShown);toolbarRegistered=false;}receivers.Remove(this);if(selectedReceiver==this)selectedReceiver=receivers.FirstOrDefault();if(receivers.Count!=0)return;if(recoveryButton!=null&&ApplicationLauncher.Instance!=null)ApplicationLauncher.Instance.RemoveModApplication(recoveryButton);recoveryButton=null;recoveryWindowOpen=false;if(recoveryIcon!=null)UnityEngine.Object.Destroy(recoveryIcon);recoveryIcon=null;}
  public void OpenControlWindow(){selectedReceiver=this;OpenWindow();if(recoveryButton!=null)recoveryButton.SetTrue(false);}
  public bool ToolbarReady(){return recoveryButton!=null;}
  public void OnGUI(){
   if(!HighLogic.LoadedSceneIsFlight||receivers.Count==0||receivers[0]!=this||!recoveryWindowOpen||!recoveryUiVisible)return;
   if(selectedReceiver==null)selectedReceiver=receivers.FirstOrDefault();if(selectedReceiver==null)return;
   var skin=GUI.skin;bool enabled=GUI.enabled;try{GUI.skin=HighLogic.Skin;recoveryWindow=GUILayout.Window(14262860,recoveryWindow,DrawControlWindow,Loc("WindowTitle"));}finally{GUI.skin=skin;GUI.enabled=enabled;}
  }
  void DrawControlWindow(int id){
   var c=selectedReceiver;if(c==null||c.part==null||c.vessel==null)return;
   if(recoveryText==null){recoveryText=new GUIStyle(HighLogic.Skin.label);recoveryText.wordWrap=true;recoveryText.fontSize=16;}
   if(receivers.Count>1){GUILayout.BeginHorizontal();foreach(var ship in receivers.ToArray())if(ship!=null&&ship.vessel!=null&&GUILayout.Button(ship.vessel.vesselName))selectedReceiver=ship;GUILayout.EndHorizontal();}
   GUILayout.Label(Loc("WindowShip")+": "+c.vessel.vesselName,recoveryText);
   GUILayout.Label(c.heightSpec,recoveryText);
   if(c.vessel.packed){GUILayout.Label(Loc("WindowUnloaded"),recoveryText);GUI.enabled=false;}
   var e=c.FeedbackState();GUILayout.Label(e.text,recoveryText);
   GUILayout.Label(Loc("WindowReason")+": "+(c.lastRejectReason==""?Loc("NoRejection"):ReasonText(c.lastRejectReason)),recoveryText);
   GUILayout.BeginHorizontal();bool held=c.lockedHook!=null;GUI.enabled=!c.vessel.packed&&!held;
   if(GUILayout.Button(Loc(c.automatic?"Disarm":"Arm"))){if(c.automatic)c.DisarmNet();else c.ArmNet();}
   if(GUILayout.Button(Loc("Reset")))c.ResetNet();
   GUI.enabled=!c.vessel.packed;if(GUILayout.Button(Loc("SasToggle")))c.vessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
   GUI.enabled=!c.vessel.packed;if(GUILayout.Button(Loc("Station")))c.ToggleStation();GUILayout.EndHorizontal();
   GUILayout.Label(Loc("StationState")+": "+Loc(c.stationKeeping?"On":"Off"),recoveryText);
   GUILayout.Label(Loc("SasState")+": "+Loc(c.vessel.ActionGroups[KSPActionGroup.SAS]?"On":"Off"),recoveryText);
   GUI.enabled=!c.vessel.packed&&held;if(GUILayout.Button(Loc("Release")))c.ReleaseStage();if(GUILayout.Button(Loc("ReplayBuffer")))c.ReplayBuffer();GUI.enabled=true;
   if(GUILayout.Button(Loc("WindowClose"))){CloseWindow();if(recoveryButton!=null)recoveryButton.SetFalse(false);}
   GUI.DragWindow(new Rect(0,0,recoveryWindow.width,25));
  }
 }
}
