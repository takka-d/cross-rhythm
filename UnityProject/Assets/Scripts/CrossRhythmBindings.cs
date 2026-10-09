using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    ControlBindings normalBindings,proBindings;
    readonly Dictionary<InputControl,Key> physicalHeld=new Dictionary<InputControl,Key>();
    bool bindingsOpen,bindingPro,bindingPad,nintendo=true,lastInputPad;
    int bindingRow=-1,captureCancelFrame=-1;double captureAfter;
    readonly Queue<KeyCode> padMenu=new Queue<KeyCode>();
    string bindingNotice="",lastPadPath="";int lastPadId;double lastPadAt;
    ControlBindings ActiveBindings=>pro?proBindings:normalBindings;
    ControlBindings EditingBindings=>bindingPro?proBindings:normalBindings;
    void LoadBindings(){nintendo=PlayerPrefs.GetInt("padNintendo",1)==1;normalBindings=ControlBindings.Load(PlayerPrefs.GetString("bindingsNormal",""),false,nintendo);proBindings=ControlBindings.Load(PlayerPrefs.GetString("bindingsPro",""),true,nintendo);}
    void SaveBindings(){PlayerPrefs.SetString("bindingsNormal",normalBindings.Save());PlayerPrefs.SetString("bindingsPro",proBindings.Save());PlayerPrefs.SetInt("padNintendo",nintendo?1:0);PlayerPrefs.Save();ReleaseInputs();}
    void ReleaseInputs(){physicalHeld.Clear();held.Clear();footRoles.Clear();padMenu.Clear();}
    void InputDeviceChanged(InputDevice device,InputDeviceChange change){if(change==InputDeviceChange.Removed||change==InputDeviceChange.Disconnected||change==InputDeviceChange.Disabled){foreach(var c in physicalHeld.Keys.Where(c=>c.device==device).ToArray())physicalHeld.Remove(c);RefreshHeld(Audio.Beat);}}
    void ReleaseOnFocusLoss(bool focus){if(!focus&&Audio!=null){ReleaseInputs();SetPedal(false,Audio.Beat);}}
    void RefreshHeld(double beat){held.Clear();foreach(var k in physicalHeld.Values)held.Add(k);foreach(var k in footRoles.Keys.Where(k=>!held.Contains(k)).ToArray())footRoles.Remove(k);SetPedal(footRoles.Values.Contains("pedal")||(pro&&(held.Contains(Key.C)||held.Contains(Key.Comma))),beat);}
    static string PadPath(InputControl control)=>control.path.Substring(control.device.path.Length+1);
    void OnInput(InputEventPtr evt,InputDevice device){
        if(device is not Keyboard&&device is not Gamepad)return;
        if(!evt.IsA<StateEvent>()&&!evt.IsA<DeltaStateEvent>())return;
        bool pad=device is Gamepad;
        var changes=new List<(ButtonControl control,bool down)>();
        foreach(var c in evt.EnumerateChangedControls(device))if(c is ButtonControl b){bool down=b.ReadValueFromEvent(evt)>.5f;changes.Add((b,down));}
        if(pad)foreach(var c in changes)if(c.down&&AllowedPad(PadPath(c.control))){lastPadPath=PadPath(c.control);lastPadId=device.deviceId;lastPadAt=Time.unscaledTimeAsDouble;}
        if(bindingRow>=0){
            foreach(var c in changes){if(!c.down||Time.unscaledTimeAsDouble<captureAfter)continue;
                if(c.control is KeyControl cancel&&cancel.keyCode==Key.Escape){bindingRow=-1;captureCancelFrame=Time.frameCount;return;}
                if(pad!=bindingPad)continue;
                if(pad){string path=PadPath(c.control);if(!AllowedPad(path))continue;EditingBindings.BindPad(bindingRow,path);}
                else if(c.control is KeyControl key&&key.keyCode!=Key.None)EditingBindings.BindKey(bindingRow,key.keyCode);else continue;
                SaveBindings();bindingNotice=T("割り当てを保存しました","Binding saved");bindingRow=-1;return;
            }return;
        }
        if(bindingsOpen)return;
        if(pad&&!Audio.Running&&Current!=Page.Play&&Current!=Page.Edit){foreach(var c in changes)if(c.down&&!c.control.isPressed){var path=PadPath(c.control);KeyCode key=path=="dpad/up"?KeyCode.UpArrow:path=="dpad/down"?KeyCode.DownArrow:path=="dpad/left"?KeyCode.LeftArrow:path=="dpad/right"?KeyCode.RightArrow:path==(nintendo?"buttonEast":"buttonSouth")?KeyCode.Return:path==(nintendo?"buttonSouth":"buttonEast")?KeyCode.Escape:KeyCode.None;if(key!=KeyCode.None)padMenu.Enqueue(key);}return;}
        if(!Audio.Running||Audio.Preparing||(Current!=Page.Play&&Current!=Page.Practice))return;
        double dsp=RhythmAudio.Clock+(evt.time-InputState.currentTime),beat=Audio.BeatAt(dsp-inputOffset/1000);
        // Resolve all releases and foot presses before hand strikes from the same device report.
        foreach(var c in changes)if(!c.down)physicalHeld.Remove(c.control);
        RefreshHeld(beat);
        var actions=new List<(InputControl control,Key action)>();var map=ActiveBindings;var logical=pro?ControlBindings.Pro:ControlBindings.Normal;
        foreach(var c in changes){if(!c.down||physicalHeld.ContainsKey(c.control))continue;int row=pad?map.PadRow(PadPath(c.control)):c.control is KeyControl key?map.KeyRow(key.keyCode):-1;if(row>=0)actions.Add((c.control,logical[row]));}
        foreach(var a in actions.OrderBy(a=>a.action==Key.C||a.action==Key.Comma?0:a.action==Key.V||a.action==Key.M?1:2)){physicalHeld[a.control]=a.action;lastInputPad=pad;if(held.Add(a.action))HandleKey(a.action,beat);}
    }
    static bool AllowedPad(string path)=>new[]{"buttonSouth","buttonEast","buttonWest","buttonNorth","leftShoulder","rightShoulder","leftTrigger","rightTrigger","leftStickPress","rightStickPress","start","select","dpad/up","dpad/down","dpad/left","dpad/right"}.Contains(path);
    void BindingKeys(){if(Event.current.type!=EventType.KeyDown)return;if(bindingsOpen){if(Event.current.keyCode==KeyCode.Escape){if(bindingRow>=0)bindingRow=-1;else if(captureCancelFrame!=Time.frameCount)bindingsOpen=false;}Event.current.Use();}}
    string PadName(string path){switch(path){case "buttonSouth":return nintendo?"B (↓)":"A / ×";case "buttonEast":return nintendo?"A (→)":"B / ○";case "buttonNorth":return nintendo?"X (↑)":"Y / △";case "buttonWest":return nintendo?"Y (←)":"X / □";case "leftShoulder":return nintendo?"L":"LB / L1";case "rightShoulder":return nintendo?"R":"RB / R1";case "leftTrigger":return nintendo?"ZL":"LT / L2";case "rightTrigger":return nintendo?"ZR":"RT / R2";case "dpad/up":return "↑";case "dpad/down":return "↓";case "dpad/left":return "←";case "dpad/right":return "→";default:return path;}}
    string PadGroupName(string paths)=>string.Join(" / ",paths.Split('|').Select(PadName));
    string ControllerStatus()=>Gamepad.all.Count==0?T("コントローラー未接続","No controller connected"):string.Join(" / ",Gamepad.all.Select((p,i)=>ControllerIdentity.Label(p.description,i+1,english)));
    string ControllerInputStatus(){var pads=Gamepad.all.ToArray();int at=Array.FindIndex(pads,p=>p.deviceId==lastPadId);return at>=0&&Time.unscaledTimeAsDouble-lastPadAt<3?"Input: Controller "+(at+1)+" · "+PadName(lastPadPath):T("確認: コントローラーのボタンを押してください","Input check: press a controller button");}
    string ControllerInfo()=>Gamepad.all.Any(p=>ControllerIdentity.ReportedModel(p.description)=="")?T("接続方式から機種は特定できません。下のボタン配置を選んでください。","The connection does not identify the model. Choose the button layout below."):T("ボタン配置は手元のコントローラーに合わせて選択できます。","Choose the button layout that matches your controller.");
    void SetButtonLayout(bool right){if(nintendo==right)return;nintendo=right;SaveBindings();bindingRow=-1;bindingNotice=T("ボタン表示とメニュー決定位置を変更しました。演奏の割り当ては保持します。","Button labels and menu confirm position changed. Performance bindings kept.");}
    void BindingPanel(Rect r){RectFill(r,panel);Text(new Rect(r.x+24,r.y+16,500,32),"Controls",22,Color.white,true);FittedText(new Rect(r.x+24,r.y+60,r.width-48,30),ControllerStatus(),17,mint);Text(new Rect(r.x+24,r.y+101,r.width-48,25),T("Keyboard / Controller の割り当てを変更","Change keyboard and controller bindings"),15,muted);FittedText(new Rect(r.x+24,r.y+135,r.width-48,25),ControllerInputStatus(),15,muted);if(Button(new Rect(r.x+24,r.y+184,r.width-48,48),"Key Bindings",true)){bindingsOpen=true;bindingPro=pro;bindingNotice="";GUI.FocusControl(null);}Text(new Rect(r.x+24,r.y+253,r.width-48,26),"Practice / Edit   Space: Play / Pause",15,muted);}
    void BindingSettings(){
        bool previous=GUI.enabled;GUI.enabled=true;RectFill(new Rect(0,0,W,H),new Color(0,0,0,.9f));var r=new Rect((W-1060)/2,Math.Max(20,(H-718)/2),1060,718);RectFill(r,panel);Border(r,mint);
        Text(new Rect(r.x+24,r.y+16,510,35),"Key Bindings",26,Color.white,true);
        if(Button(new Rect(r.x+600,r.y+16,126,36),"Normal",!bindingPro)){bindingPro=false;bindingRow=-1;}if(Button(new Rect(r.x+736,r.y+16,126,36),"Pro",bindingPro)){bindingPro=true;bindingRow=-1;}if(Button(new Rect(r.x+882,r.y+16,150,36),"Close")){bindingsOpen=false;bindingRow=-1;}
        FittedText(new Rect(r.x+24,r.y+60,990,28),ControllerStatus(),16,mint);
        FittedText(new Rect(r.x+24,r.y+91,990,25),ControllerInputStatus(),14,muted);
        Text(new Rect(r.x+24,r.y+120,160,27),"Action",15,muted);Text(new Rect(r.x+218,r.y+120,215,27),"Keyboard",15,muted);Text(new Rect(r.x+470,r.y+120,555,27),"Controller",15,muted);
        var map=EditingBindings;var keys=bindingPro?ControlBindings.Pro:ControlBindings.Normal;string[] labels=bindingPro?new[]{"SN  1","SN  2","TOM  1","TOM  2","HH  1","HH  2","CYM  1","CYM  2","BD  1","BD  2","PEDAL  1","PEDAL  2"}:new[]{"Hands  1","Hands  2","Feet  1","Feet  2"};
        for(int i=0;i<keys.Length;i++){float y=r.y+151+i*31;Text(new Rect(r.x+24,y,175,30),labels[i],16);if(Button(new Rect(r.x+210,y,226,30),ControlBindings.KeyName(map.Keys[i]),bindingRow==i&&!bindingPad,true,15))Capture(i,false);if(Button(new Rect(r.x+456,y,576,30),PadGroupName(map.Pads[i]),bindingRow==i&&bindingPad,true,15))Capture(i,true);}
        Text(new Rect(r.x+24,r.y+527,990,28),bindingRow>=0?T("割り当てるキー / ボタンを押す。Escでキャンセル。","Press a key / button to assign. Esc cancels."):bindingNotice,17,mint);
        Text(new Rect(r.x+24,r.y+567,200,28),T("ボタン表示 / 決定位置","Labels / Confirm"),14,muted);
        if(Button(new Rect(r.x+242,r.y+562,250,36),T("Aボタンが右 →","A button on right →"),nintendo,true,15))SetButtonLayout(true);
        if(Button(new Rect(r.x+508,r.y+562,310,36),T("A / ×ボタンが下 ↓","A / × button on bottom ↓"),!nintendo,true,15))SetButtonLayout(false);
        if(Button(new Rect(r.x+24,r.y+610,306,38),T("初期化: Aボタンが右","Reset: A button on right"),false,true,15)){nintendo=true;normalBindings=ControlBindings.Defaults(false,true);proBindings=ControlBindings.Defaults(true,true);SaveBindings();bindingRow=-1;}
        if(Button(new Rect(r.x+348,r.y+610,354,38),T("初期化: A / ×ボタンが下","Reset: A / × button on bottom"),false,true,15)){nintendo=false;normalBindings=ControlBindings.Defaults(false,false);proBindings=ControlBindings.Defaults(true,false);SaveBindings();bindingRow=-1;}
        Text(new Rect(r.x+720,r.y+616,312,28),T("キーとボタンの割り当てを初期化","Restore default keys and buttons"),14,muted);
        FittedText(new Rect(r.x+24,r.y+657,990,23),ControllerInfo(),13,muted);
        Text(new Rect(r.x+24,r.y+685,990,23),T("重複する割り当ては入れ替えます。設定は自動保存。","Conflicting bindings are swapped. Changes save automatically."),13,muted);GUI.enabled=previous;
    }

    void Capture(int row,bool pad){bindingRow=row;bindingPad=pad;captureAfter=Time.unscaledTimeAsDouble+.15;bindingNotice="";}
    string BoundLabel(Key logical){int row=Array.IndexOf(pro?ControlBindings.Pro:ControlBindings.Normal,logical);if(row<0)return "";if(!lastInputPad)return ControlBindings.KeyName(ActiveBindings.Keys[row]);var paths=ActiveBindings.Pads[row];if(paths.Contains("|"))return paths.StartsWith("dpad")?"D-pad":paths.StartsWith("button")?"ABXY":paths.StartsWith("left")?"L/ZL":"R/ZR";return PadName(paths);}
}
}
