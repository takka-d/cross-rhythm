using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CrossRhythm {
public static class ControllerNavigationMath {
    public const float PointerDeadzone=.24f,ScrollDeadzone=.28f;
    public static Vector2 Deadzone(Vector2 value,float deadzone){
        float magnitude=value.magnitude;if(magnitude<=deadzone)return Vector2.zero;
        return value.normalized*((magnitude-deadzone)/(1-deadzone));
    }
    public static KeyCode Direction(Vector2 value){
        if(value.sqrMagnitude<.25f)return KeyCode.None;
        return Math.Abs(value.x)>Math.Abs(value.y)?(value.x>0?KeyCode.RightArrow:KeyCode.LeftArrow):(value.y>0?KeyCode.UpArrow:KeyCode.DownArrow);
    }
    public static KeyCode Direction(string path){switch(path){case "dpad/up":return KeyCode.UpArrow;case "dpad/down":return KeyCode.DownArrow;case "dpad/left":return KeyCode.LeftArrow;case "dpad/right":return KeyCode.RightArrow;default:return KeyCode.None;}}
    public static bool ActivatesPointer(Vector2 pointer,Vector2 scroll,bool secondaryPressed)=>pointer!=Vector2.zero||scroll!=Vector2.zero||secondaryPressed;
    public static bool MouseReclaimsPointer(bool virtualPointerVisible,Vector2 mouseDelta)=>virtualPointerVisible&&mouseDelta.sqrMagnitude>.04f;
    public static bool SystemPointerVisible(bool virtualPointerVisible)=>!virtualPointerVisible;
    public static bool ControllerHover(bool virtualPointerVisible,bool mouseContains,bool controllerContains)=>virtualPointerVisible?controllerContains:mouseContains;
    public static bool HomePath(string path)=>path=="home"||path=="homeButton"||path=="systemButton";
    public static Vector2 Scroll(Vector2 value,Vector2 wheel,float distance,float maximumX,float maximumY,bool horizontal,bool vertical){
        if(horizontal)value.x=Mathf.Clamp(value.x+wheel.x*distance,0,Math.Max(0,maximumX));
        if(vertical)value.y=Mathf.Clamp(value.y+wheel.y*distance,0,Math.Max(0,maximumY));
        return value;
    }
}

public partial class CrossRhythmApp {
    Vector2 controllerPointerScreen;
    Vector2 controllerPointerDelta;
    Vector2 controllerWheel;
    bool controllerPointerVisibleState,controllerStageUiMode;
    bool controllerPointerVisible {get=>controllerPointerVisibleState;set{if(controllerPointerVisibleState==value){Cursor.visible=ControllerNavigationMath.SystemPointerVisible(value);return;}controllerPointerVisibleState=value;SetSystemPointerVisible(ControllerNavigationMath.SystemPointerVisible(value));}}
    bool controllerPrimaryPressed,controllerPrimaryHeld,controllerPrimaryReleased,controllerSecondaryPressed;
    bool controllerPrimaryConsumed,controllerSecondaryConsumed;
    readonly System.Collections.Generic.HashSet<(int device,string path)> controllerPressedEvents=new System.Collections.Generic.HashSet<(int,string)>();
    readonly System.Collections.Generic.HashSet<(int device,string path)> controllerReleasedEvents=new System.Collections.Generic.HashSet<(int,string)>();
    bool controllerEditorPrimaryCapture;
    string controllerDrag="";
    int controllerPadId;
    Page controllerPage;
    KeyCode controllerRepeatKey=KeyCode.None;
    double controllerRepeatAt;
    const double ControllerRepeatDelay=.36,ControllerRepeatRate=.11;

    void SetSystemPointerVisible(bool visible){
        Cursor.visible=visible;
#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CRSystemPointer(visible?1:0);
#endif
    }
    public void OnSystemPointerMoved(string _){if(controllerPointerVisible)controllerPointerVisible=false;}

    Gamepad ControllerPad(){
        var known=Gamepad.all.FirstOrDefault(p=>p.deviceId==controllerPadId);
        return known??Gamepad.current??Gamepad.all.FirstOrDefault();
    }
    ButtonControl ControllerConfirm(Gamepad pad)=>nintendo?pad.buttonEast:pad.buttonSouth;
    ButtonControl ControllerBack(Gamepad pad)=>nintendo?pad.buttonSouth:pad.buttonEast;
    ButtonControl ControllerHome(Gamepad pad)=>pad?.TryGetChildControl<ButtonControl>("home")??pad?.TryGetChildControl<ButtonControl>("homeButton")??pad?.TryGetChildControl<ButtonControl>("systemButton");
    static string ControllerEventPath(ButtonControl control)=>control==null?"":PadPath(control);
    bool ControllerPressed(Gamepad pad,ButtonControl control)=>pad!=null&&control!=null&&(control.wasPressedThisFrame||controllerPressedEvents.Contains((pad.deviceId,ControllerEventPath(control))));
    bool ControllerReleased(Gamepad pad,ButtonControl control)=>pad!=null&&control!=null&&(control.wasReleasedThisFrame||controllerReleasedEvents.Contains((pad.deviceId,ControllerEventPath(control))));
    void CaptureControllerButtonEvent(Gamepad pad,string path,bool down){if(pad==null||string.IsNullOrEmpty(path))return;(down?controllerPressedEvents:controllerReleasedEvents).Add((pad.deviceId,path));}
    bool ControllerHomePressed(Gamepad pad)=>pad!=null&&(ControllerPressed(pad,ControllerHome(pad))||controllerPressedEvents.Any(e=>e.device==pad.deviceId&&ControllerNavigationMath.HomePath(e.path)));
    KeyCode ControllerDirection(Gamepad pad){
        var current=ControllerNavigationMath.Direction(pad.dpad.ReadValue());if(current!=KeyCode.None)return current;
        foreach(var input in controllerPressedEvents)if(input.device==pad.deviceId){var latched=ControllerNavigationMath.Direction(input.path);if(latched!=KeyCode.None)return latched;}
        return KeyCode.None;
    }
    bool ControllerHasActivity(Gamepad pad)=>pad!=null&&(ControllerNavigationMath.Deadzone(pad.leftStick.ReadValue(),ControllerNavigationMath.PointerDeadzone)!=Vector2.zero||ControllerNavigationMath.Deadzone(pad.rightStick.ReadValue(),ControllerNavigationMath.ScrollDeadzone)!=Vector2.zero||controllerPressedEvents.Any(e=>e.device==pad.deviceId)||ControllerPressed(pad,pad.startButton)||ControllerPressed(pad,pad.selectButton)||ControllerPressed(pad,pad.leftStickButton)||ControllerPressed(pad,pad.rightStickButton)||ControllerPressed(pad,ControllerConfirm(pad))||ControllerPressed(pad,ControllerBack(pad))||ControllerHomePressed(pad)||pad.dpad.ReadValue()!=Vector2.zero);
    bool ControllerStage=>Current==Page.Play||Current==Page.Practice;
    bool ControllerOwnsPerformance(Gamepad pad)=>pad!=null&&pad.deviceId==controllerPadId&&ControllerStage&&(controllerStageUiMode||stageExitPrompt);
    bool ControllerReservedPath(string path)=>path=="start"||path=="select"||path=="leftStickPress"||path=="rightStickPress"||ControllerNavigationMath.HomePath(path);
    Vector2 ControllerGuiPointer()=>GUIUtility.ScreenToGUIPoint(controllerPointerScreen);
    bool ControllerContains(Rect rect)=>controllerPointerVisible&&rect.Contains(ControllerGuiPointer());
    bool ControllerPointerMoved=>controllerPointerDelta.sqrMagnitude>.01f;
    bool ControllerScrollActive(Rect rect)=>controllerPointerVisible&&ControllerContains(rect)&&controllerWheel.sqrMagnitude>.0001f;

    void ActivateControllerPointer(Gamepad pad){
        if(pad==null)return;controllerPadId=pad.deviceId;
        if(controllerPointerScreen==Vector2.zero)controllerPointerScreen=new Vector2(Screen.width*.5f,Screen.height*.5f);
        controllerPointerVisible=true;keyboardMenu=false;
        if(ControllerStage)controllerStageUiMode=true;
    }
    void ResetControllerNavigationForPage(){
        controllerPage=Current;controllerPrimaryPressed=controllerPrimaryHeld=controllerPrimaryReleased=controllerSecondaryPressed=false;
        controllerPrimaryConsumed=controllerSecondaryConsumed=false;controllerEditorPrimaryCapture=false;controllerDrag="";controllerRepeatKey=KeyCode.None;padMenu.Clear();
        if(ControllerStage){controllerStageUiMode=false;controllerPointerVisible=false;}else controllerPointerVisible=Gamepad.all.Count>0&&controllerPointerVisible;
    }
    void QueueControllerKey(KeyCode key){if(key!=KeyCode.None){padMenu.Enqueue(key);keyboardMenu=true;controllerPointerVisible=false;}}
    void UpdateControllerRepeat(KeyCode key){
        double now=Time.unscaledTimeAsDouble;
        if(key==KeyCode.None){controllerRepeatKey=KeyCode.None;return;}
        if(key!=controllerRepeatKey){controllerRepeatKey=key;controllerRepeatAt=now+ControllerRepeatDelay;QueueControllerKey(key);return;}
        if(now>=controllerRepeatAt){controllerRepeatAt=now+ControllerRepeatRate;QueueControllerKey(key);}
    }
    void ControllerBackAction(){
        controllerDrag="";controllerPrimaryHeld=false;
        if(stageExitPrompt){QueueControllerKey(KeyCode.Escape);return;}
        if(bindingsOpen){if(bindingRow>=0)bindingRow=-1;else bindingsOpen=false;return;}
        if(discardPrompt){CancelLeaving();return;}
        if(MidiPromptOpen){CancelMidi();return;}
        if(LaneTypeOpen){laneTypeRow=-1;return;}
        if(gridMenuOpen){gridMenuOpen=false;return;}
        if(showMeterPanel){showMeterPanel=false;return;}
        if(editorFileOpen){editorFileOpen=false;return;}
        if(Current==Page.Edit&&Editor!=null&&(Editor.ContextOpen||selection.Count>0)){Editor.ContextOpen=false;Editor.Clear();Editor.Cancel();return;}
        if(ControllerStage&&controllerStageUiMode){controllerStageUiMode=false;controllerPointerVisible=false;ReleaseInputs();return;}
        if(ControllerStage){RequestStageExit();return;}
        Navigate(Page.Title);
    }
    void ControllerHomeAction(){
        if(InputBlocked)return;
        controllerDrag="";controllerPrimaryHeld=false;controllerStageUiMode=false;controllerPointerVisible=false;
        bindingsOpen=false;bindingRow=-1;editorFileOpen=false;showMeterPanel=false;gridMenuOpen=false;laneTypeRow=-1;
        if(MidiPromptOpen)CancelMidi();
        if(discardPrompt)CancelLeaving();
        if(stageExitPrompt)stageExitPrompt=false;
        Navigate(Page.Title);
    }
    void UpdateControllerNavigation(){
        if(controllerPage!=Current)ResetControllerNavigationForPage();
        controllerPrimaryPressed=controllerPrimaryReleased=controllerSecondaryPressed=false;
        controllerPrimaryConsumed=controllerSecondaryConsumed=false;controllerPointerDelta=Vector2.zero;controllerWheel=Vector2.zero;
        Vector2 mouseDelta=Mouse.current?.delta.ReadValue()??Vector2.zero;
        bool mouseAction=Mouse.current!=null&&(Mouse.current.leftButton.wasPressedThisFrame||Mouse.current.rightButton.wasPressedThisFrame||Mouse.current.middleButton.wasPressedThisFrame||Mouse.current.scroll.ReadValue().sqrMagnitude>.01f);
        Gamepad pad=Gamepad.all.FirstOrDefault(ControllerHasActivity)??ControllerPad();if(pad==null){controllerPadId=0;controllerStageUiMode=false;controllerPointerVisible=false;controllerDrag="";controllerPressedEvents.Clear();controllerReleasedEvents.Clear();return;}
        Vector2 pointer=ControllerNavigationMath.Deadzone(pad.leftStick.ReadValue(),ControllerNavigationMath.PointerDeadzone);
        Vector2 scroll=ControllerNavigationMath.Deadzone(pad.rightStick.ReadValue(),ControllerNavigationMath.ScrollDeadzone);
        bool secondary=ControllerPressed(pad,pad.rightStickButton),pointerAction=ControllerNavigationMath.ActivatesPointer(pointer,scroll,secondary);
        bool padActivity=ControllerHasActivity(pad);if(padActivity)controllerPadId=pad.deviceId;
        if(pointerAction)ActivateControllerPointer(pad);
        else if((mouseAction||ControllerNavigationMath.MouseReclaimsPointer(controllerPointerVisible,mouseDelta))&&controllerPointerVisible)controllerPointerVisible=false;
        if(ControllerHomePressed(pad)){ControllerHomeAction();controllerPressedEvents.Clear();controllerReleasedEvents.Clear();return;}
        if(ControllerStage&&ControllerPressed(pad,pad.startButton)&&!stageExitPrompt){
            controllerStageUiMode=!controllerStageUiMode;controllerPointerVisible=controllerStageUiMode;ReleaseInputs();
            if(controllerStageUiMode&&controllerPointerScreen==Vector2.zero)controllerPointerScreen=new Vector2(Screen.width*.5f,Screen.height*.5f);
        }
        if(ControllerStage&&ControllerPressed(pad,pad.selectButton)&&!stageExitPrompt){controllerStageUiMode=true;controllerPointerVisible=true;RequestStageExit();}
        if(pointer!=Vector2.zero){
            float speed=720f*(.45f+.55f*pointer.magnitude)*Time.unscaledDeltaTime;
            controllerPointerDelta=new Vector2(pointer.x,-pointer.y)*speed;controllerPointerScreen+=controllerPointerDelta;
            controllerPointerScreen.x=Mathf.Clamp(controllerPointerScreen.x,6,Math.Max(6,Screen.width-6));controllerPointerScreen.y=Mathf.Clamp(controllerPointerScreen.y,6,Math.Max(6,Screen.height-6));
        }
        if(controllerPointerVisible&&(controllerStageUiMode||!ControllerStage))controllerWheel=new Vector2(scroll.x,-scroll.y);
        bool uiAllowed=!ControllerStage||controllerStageUiMode||stageExitPrompt;
        if(bindingRow>=0&&(ControllerPressed(pad,pad.startButton)||ControllerPressed(pad,pad.selectButton))){
            bindingRow=-1;captureCancelFrame=Time.frameCount;bindingNotice=T("割り当てをキャンセルしました","Binding cancelled");
        }
        if(uiAllowed&&bindingRow<0){
            var confirm=ControllerConfirm(pad);controllerPrimaryPressed=ControllerPressed(pad,confirm);controllerPrimaryHeld=confirm.isPressed;controllerPrimaryReleased=ControllerReleased(pad,confirm);controllerSecondaryPressed=secondary;
            if(ControllerPressed(pad,ControllerBack(pad)))ControllerBackAction();
            if(Current!=Page.Edit||stageExitPrompt)UpdateControllerRepeat(ControllerDirection(pad));else UpdateControllerRepeat(KeyCode.None);
            if(!controllerPointerVisible&&controllerPrimaryPressed){QueueControllerKey(KeyCode.Return);controllerPrimaryPressed=false;}
        }else UpdateControllerRepeat(KeyCode.None);
        if(controllerPrimaryReleased)controllerDrag="";
        Cursor.visible=ControllerNavigationMath.SystemPointerVisible(controllerPointerVisible);
        controllerPressedEvents.Clear();controllerReleasedEvents.Clear();
    }
    bool ControllerButtonHit(Rect rect,bool active){
        if(!active||controllerPrimaryConsumed||!controllerPrimaryPressed||!ControllerContains(rect))return false;
        controllerPrimaryConsumed=true;controllerPrimaryPressed=false;return true;
    }
    bool ControllerSecondaryHit(Rect rect){
        if(controllerSecondaryConsumed||!controllerSecondaryPressed||!ControllerContains(rect))return false;
        controllerSecondaryConsumed=true;controllerSecondaryPressed=false;return true;
    }
    float ControllerHorizontalSlider(Rect rect,float value,float minimum,float maximum,string id){
        float next=GUI.HorizontalSlider(rect,value,minimum,maximum);bool active=GUI.enabled;
        RegisterMenu(id,rect,active);Rect hit=new Rect(rect.x-8,rect.y-10,rect.width+16,rect.height+20);
        if(active&&controllerPrimaryPressed&&!controllerPrimaryConsumed&&ControllerContains(hit)){controllerDrag=id;controllerPrimaryConsumed=true;controllerPrimaryPressed=false;}
        if(active&&controllerDrag==id&&controllerPrimaryHeld){float x=Mathf.InverseLerp(rect.x+5,rect.xMax-5,ControllerGuiPointer().x);next=Mathf.Lerp(minimum,maximum,x);}
        return Mathf.Clamp(next,minimum,maximum);
    }
    float ControllerVerticalSlider(Rect rect,float value,float minimum,float maximum,string id){
        float next=GUI.VerticalSlider(rect,value,minimum,maximum);bool active=GUI.enabled;
        RegisterMenu(id,rect,active);Rect hit=new Rect(rect.x-10,rect.y-8,rect.width+20,rect.height+16);
        if(active&&controllerPrimaryPressed&&!controllerPrimaryConsumed&&ControllerContains(hit)){controllerDrag=id;controllerPrimaryConsumed=true;controllerPrimaryPressed=false;}
        if(active&&controllerDrag==id&&controllerPrimaryHeld){float y=Mathf.InverseLerp(rect.y+5,rect.yMax-5,ControllerGuiPointer().y);next=Mathf.Lerp(minimum,maximum,y);}
        return Mathf.Clamp(next,Mathf.Min(minimum,maximum),Mathf.Max(minimum,maximum));
    }
    Vector2 ControllerScrollView(Rect view,Vector2 value,float contentWidth,float contentHeight,bool horizontal=false,bool vertical=true){
        if(Event.current.type!=EventType.Repaint||!ControllerScrollActive(view))return value;float distance=620f*Time.unscaledDeltaTime;
        return ControllerNavigationMath.Scroll(value,controllerWheel,distance,contentWidth-view.width,contentHeight-view.height,horizontal,vertical);
    }
    void DrawControllerPointer(){
        if(!controllerPointerVisible||Event.current.type!=EventType.Repaint)return;Vector2 p=ControllerGuiPointer();
        RoundFill(new Rect(p.x-3,p.y-3,7,7),Color.white,4);RoundBorder(new Rect(p.x-9,p.y-9,19,19),mint,2,10);
        RectFill(new Rect(p.x+10,p.y,9,1.5f),mint);RectFill(new Rect(p.x,p.y+10,1.5f,9),mint);
    }
    void OnDisable(){controllerPointerVisible=false;}
}
}
