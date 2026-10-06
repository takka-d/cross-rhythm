using UnityEngine;
using UnityEngine.InputSystem;

namespace CrossRhythm {
public partial class CrossRhythmApp {
    int windowWidth=1440,windowHeight=900;
    void DisplaySizeButton(){
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        // Native mobile already owns the app viewport; a desktop window/maximize toggle has no useful equivalent here.
        return;
#else
        bool stage=Current==Page.Play||Current==Page.Practice;
        var r=new Rect(W-66,stage?16:14,42,42);
#if UNITY_WEBGL && !UNITY_EDITOR
        // The DOM button expands the containing article viewport without OS fullscreen. Touch/click uses the same DOM button on mobile WebGL.
        if(Event.current.type==EventType.Repaint)PlatformFiles.CRDisplayLayout(r.x/W,r.y/H,r.width/W,r.height/H,english?1:0,discardPrompt||showMeterPanel||(Current==Page.Edit&&(Editor.ContextOpen||editorFileOpen))?0:1);
#else
        if(Button(r,"",false,!discardPrompt&&!showMeterPanel&&!(Current==Page.Edit&&(Editor.ContextOpen||editorFileOpen))))ToggleDisplay();
        DisplayGlyph(r,Screen.fullScreen);
#endif
#endif
    }
    void DisplayGlyph(Rect r,bool active){
        float left=r.center.x-12,top=r.center.y-12;
        void Line(float x1,float y1,float x2,float y2){RectFill(new Rect(left+Mathf.Min(x1,x2),top+Mathf.Min(y1,y2),Mathf.Max(1.5f,Mathf.Abs(x2-x1)),Mathf.Max(1.5f,Mathf.Abs(y2-y1))),Color.white);}
        foreach(int sx in new[]{-1,1})foreach(int sy in new[]{-1,1}){float x=12+sx*(active?3:9),y=12+sy*(active?3:9),direction=active?1:-1;Line(x,y,x+sx*6*direction,y);Line(x,y,x,y+sy*6*direction);}
    }
    void UpdateDisplayKeys(){
#if !UNITY_WEBGL || UNITY_EDITOR
        var keys=Keyboard.current;if(keys==null)return;
        if(keys.escapeKey.wasPressedThisFrame&&Screen.fullScreen&&Current!=Page.Edit)ToggleDisplay();
#endif
    }
    void ToggleDisplay(){
        if(Screen.fullScreen)Screen.SetResolution(windowWidth,windowHeight,FullScreenMode.Windowed);
        else {windowWidth=Screen.width;windowHeight=Screen.height;var display=Screen.currentResolution;Screen.SetResolution(display.width,display.height,FullScreenMode.FullScreenWindow);}
    }
}
}
