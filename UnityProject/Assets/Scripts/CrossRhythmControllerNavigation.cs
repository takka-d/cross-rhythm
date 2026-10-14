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
    public static bool ActivatesPointer(Vector2 pointer,Vector2 scroll,bool secondaryPressed)=>pointer!=Vector2.zero||scroll!=Vector2.zero||secondaryPressed;
}

public partial class CrossRhythmApp {
    Vector2 controllerPointerScreen;
    Vector2 controllerPointerDelta;
    Vector2 controllerWheel;
    bool controllerPointerVisible,controllerStageUiMode;
    bool controllerPrimaryPressed,controllerPrimaryHeld,controllerPrimaryReleased,controllerSecondaryPressed;
    bool controllerPrimaryConsumed,controllerSecondaryConsumed;
    bool controllerEditorPrimaryCapture;
    string controllerDrag="";
    int controllerPadId;
    Page controllerPage;
    KeyCode controllerRepeatKey=KeyCode.None;
    double controllerRepeatAt;
    const double ControllerRepeatDelay=.36,ControllerRepeatRate=.11;

    Gamepad ControllerPad(){
        var known=Gamepad.all.FirstOrDefault(p=>p.deviceId==controllerPadId);
        return known??Gamepad.current??Gamepad.all.FirstOrDefault();
    }
    ButtonControl ControllerConfirm(Gamepad pad)=>nintendo?pad.buttonEast:pad.buttonSouth;
    ButtonControl ControllerBack(Gamepad pad)=>nintendo?pad.buttonSouth:pad.buttonEast;
    bool ControllerHasActivity(Gamepad pad)=>pad!=null&&(ControllerNavigationMath.Deadzone(pad.leftStick.ReadValue(),ControllerNavigationMath.PointerDeadzone)!=Vector2.zero||ControllerNavigationMath.Deadzone(pad.rightStick.ReadValue(),ControllerNavigationMath.ScrollDeadzone)!=Vector2.zero||pad.startButton.wasPressedThisFrame||pad.selectButton.wasPressedThisFrame||pad.leftStickButton.wasPressedThisFrame||pad.rightStickButton.wasPressedThisFrame||ControllerConfirm(pad).wasPressedThisFrame||ControllerBack(pad).wasPressedThisFrame||pad.dpad.ReadValue()!=Vector2.zero);
    bool ControllerStage=>Current==Page.Play||Current==Page.Practice;
    bool ControllerOwnsPerformance(Gamepad pad)=>pad!=null&&pad.deviceId==controllerPadId&&ControllerStage&&(controllerStageUiMode||stageExitPrompt);
    bool ControllerReservedPath(string path)=>path=="start"||path=="select"||path=="leftStickPress"||path=="rightStickPress";
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
    void UpdateControllerNavigation(){
        if(controllerPage!=Current)ResetControllerNavigationForPage();
        controllerPrimaryPressed=controllerPrimaryReleased=controllerSecondaryPressed=false;
        controllerPrimaryConsumed=controllerSecondaryConsumed=false;controllerPointerDelta=Vector2.zero;controllerWheel=Vector2.zero;
        if(Mouse.current!=null&&Mouse.current.delta.ReadValue().sqrMagnitude>.04f&&!ControllerStage)controllerPointerVisible=false;
        Gamepad pad=Gamepad.all.FirstOrDefault(ControllerHasActivity)??ControllerPad();if(pad==null){controllerPadId=0;controllerStageUiMode=false;controllerPointerVisible=false;controllerDrag="";return;}
        Vector2 pointer=ControllerNavigationMath.Deadzone(pad.leftStick.ReadValue(),ControllerNavigationMath.PointerDeadzone);
        Vector2 scroll=ControllerNavigationMath.Deadzone(pad.rightStick.ReadValue(),ControllerNavigationMath.ScrollDeadzone);
        bool secondary=pad.rightStickButton.wasPressedThisFrame;
        bool padActivity=ControllerHasActivity(pad);if(padActivity)controllerPadId=pad.deviceId;
        if(ControllerStage&&pad.startButton.wasPressedThisFrame&&!stageExitPrompt){
            controllerStageUiMode=!controllerStageUiMode;controllerPointerVisible=controllerStageUiMode;ReleaseInputs();
            if(controllerStageUiMode&&controllerPointerScreen==Vector2.zero)controllerPointerScreen=new Vector2(Screen.width*.5f,Screen.height*.5f);
        }
        if(ControllerStage&&pad.selectButton.wasPressedThisFrame&&!stageExitPrompt){controllerStageUiMode=true;controllerPointerVisible=true;RequestStageExit();}
        // Right-stick scrolling and its press must work before the player has
        // moved the pointer. Previously both inputs were discarded while the
        // virtual pointer was hidden, which made wheel and right-click appear
        // completely unresponsive after keyboard or mouse navigation.
        if(ControllerNavigationMath.ActivatesPointer(pointer,scroll,secondary))ActivateControllerPointer(pad);
        if(pointer!=Vector2.zero){
            float speed=720f*(.45f+.55f*pointer.magnitude)*Time.unscaledDeltaTime;
            controllerPointerDelta=new Vector2(pointer.x,-pointer.y)*speed;controllerPointerScreen+=controllerPointerDelta;
            controllerPointerScreen.x=Mathf.Clamp(controllerPointerScreen.x,6,Math.Max(6,Screen.width-6));controllerPointerScreen.y=Mathf.Clamp(controllerPointerScreen.y,6,Math.Max(6,Screen.height-6));
        }
        if(controllerPointerVisible&&(controllerStageUiMode||!ControllerStage))controllerWheel=new Vector2(scroll.x,-scroll.y);
        bool uiAllowed=!ControllerStage||controllerStageUiMode||stageExitPrompt;
        if(bindingRow>=0&&(pad.startButton.wasPressedThisFrame||pad.selectButton.wasPressedThisFrame)){
            bindingRow=-1;captureCancelFrame=Time.frameCount;bindingNotice=T("割り当てをキャンセルしました","Binding cancelled");
        }
        if(uiAllowed&&bindingRow<0){
            var confirm=ControllerConfirm(pad);controllerPrimaryPressed=confirm.wasPressedThisFrame;controllerPrimaryHeld=confirm.isPressed;controllerPrimaryReleased=confirm.wasReleasedThisFrame;controllerSecondaryPressed=secondary;
            if(ControllerBack(pad).wasPressedThisFrame)ControllerBackAction();
            if(Current!=Page.Edit||stageExitPrompt)UpdateControllerRepeat(ControllerNavigationMath.Direction(pad.dpad.ReadValue()));else UpdateControllerRepeat(KeyCode.None);
            if(!controllerPointerVisible&&controllerPrimaryPressed){QueueControllerKey(KeyCode.Return);controllerPrimaryPressed=false;}
        }else UpdateControllerRepeat(KeyCode.None);
        if(controllerPrimaryReleased)controllerDrag="";
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
        if(horizontal)value.x=Mathf.Clamp(value.x+controllerWheel.x*distance,0,Math.Max(0,contentWidth-view.width));
        if(vertical)value.y=Mathf.Clamp(value.y+controllerWheel.y*distance,0,Math.Max(0,contentHeight-view.height));
        return value;
    }
    void DrawControllerPointer(){
        if(!controllerPointerVisible||Event.current.type!=EventType.Repaint)return;Vector2 p=ControllerGuiPointer();
        RoundFill(new Rect(p.x-3,p.y-3,7,7),Color.white,4);RoundBorder(new Rect(p.x-9,p.y-9,19,19),mint,2,10);
        RectFill(new Rect(p.x+10,p.y,9,1.5f),mint);RectFill(new Rect(p.x,p.y+10,1.5f,9),mint);
    }
}
}
