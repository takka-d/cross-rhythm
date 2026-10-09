using System;
using System.Linq;
using UnityEngine;

namespace CrossRhythm {
public partial class CrossRhythmApp {
    EditorInteraction interaction;
    Rect editorViewport;
    Vector2 editorPointer;
    float editorNotesTop;
    Vector2 menuPosition;
    double lastEditorFrame;
    bool editTextFocused;
    EditorInteraction Editor {
        get {
            if(interaction==null){
                interaction=new EditorInteraction(selection){BeforeEdit=PushUndo,Changed=Edited,Seek=EditorSeek,
                    Preview=n=>{if(n!=null&&!Audio.Running)Audio.Drum(n,Project.ClosedAt(n.Beat));}};
            }
            interaction.Project=Project;interaction.PPB=EditorPPB*zoom;interaction.RowHeight=EditorRowHeight;interaction.DefaultVelocity=velocity;interaction.DefaultDuration=duration;return interaction;
        }
    }
    void EditorSeek(double beat){Audio.Seek(beat);ResetScheduled();lastClick=Math.Floor(beat)-1;pasteBeat=beat;SelectMeasure(Project.BarAt(beat));if(editorViewport.width>16)editScroll.x=(float)(EditorInteraction.Follow(editScroll.x/(EditorPPB*zoom),(editorViewport.width-16)/(EditorPPB*zoom),beat,Project.Length)*EditorPPB*zoom);}
    void SyncEditorNote(){var n=Project.Notes.FirstOrDefault(v=>v.Index==Editor.Active);if(n==null)return;instrument=Array.IndexOf(Instruments,n.Instrument);kind=Math.Max(0,Array.IndexOf(Types[instrument],n.Articulation));duration=n.Duration;durationField=duration.ToString("0.########",System.Globalization.CultureInfo.InvariantCulture);}
    void EditorPlayback(){if(Audio.Running)Audio.Pause();else if(loaded&&!busy){ResetScheduled();lastClick=Math.Floor(Audio.Beat)-1;Audio.Play(Audio.Beat);}}
    public void OnEditorShortcut(string command){
        if(InputBlocked)return;
        if(Current!=Page.Edit||showMeterPanel||discardPrompt||MidiPromptOpen||LaneTypeOpen||draftRunning||editorFileOpen||(editTextFocused&&command!="Save"&&command!="SaveAs"))return;
        Editor.ContextOpen=false;
        switch(command){case "SelectAll":Editor.SelectAll();break;case "Copy":Copy();break;case "Paste":Paste();break;case "Undo":Restore(false);break;case "Redo":Restore(true);break;case "Save":Save(false);break;case "SaveAs":Save(true);break;case "Open":PickProject(false,true);break;}
        SyncEditorNote();
    }
    void EditorKeys(){
        var ev=Event.current;if(ev.type!=EventType.KeyDown||showMeterPanel||discardPrompt||MidiPromptOpen||LaneTypeOpen||draftRunning||editorFileOpen)return;
        bool mod=ev.control||ev.command,text=GUI.GetNameOfFocusedControl().StartsWith("edit-");
        if(mod&&ev.keyCode==KeyCode.S){Save(ev.shift);ev.Use();return;}
        if(text)return;
        if(mod){switch(ev.keyCode){case KeyCode.O:PickProject(false,true);break;case KeyCode.A:Editor.SelectAll();break;case KeyCode.Z:Restore(ev.shift);break;case KeyCode.Y:Restore(true);break;case KeyCode.C:Copy();break;case KeyCode.V:Paste();break;default:return;}}
        else {switch(ev.keyCode){case KeyCode.Space:EditorPlayback();break;case KeyCode.Escape:Editor.ContextOpen=false;Editor.Clear();Editor.Cancel();GUIUtility.hotControl=0;break;case KeyCode.Delete:case KeyCode.Backspace:DeleteSelected();break;case KeyCode.LeftArrow:Editor.Nudge(-(Project.SnapToGrid?Project.Grid:1/(EditorPPB*zoom)),0);break;case KeyCode.RightArrow:Editor.Nudge(Project.SnapToGrid?Project.Grid:1/(EditorPPB*zoom),0);break;case KeyCode.UpArrow:Editor.Nudge(0,-1);break;case KeyCode.DownArrow:Editor.Nudge(0,1);break;default:return;}}
        SyncEditorNote();ev.Use();
    }
    void EditorPointerInput(Rect viewport,float notesTop){
        editorViewport=viewport;editorNotesTop=notesTop;var ev=Event.current;
        bool can=GUI.enabled&&!MidiPromptOpen&&!showMeterPanel&&!discardPrompt&&!editorFileOpen&&!draftRunning;int id=GUIUtility.GetControlID(73517,FocusType.Passive);
        Vector2 screen=ev.mousePosition;if(ev.isMouse)editorPointer=screen;
        Vector2 Local(Vector2 p)=>new Vector2(Mathf.Clamp(p.x-viewport.x,0,viewport.width-17)+editScroll.x,p.y-viewport.y-notesTop);
        bool inside=new Rect(viewport.x,viewport.y,viewport.width-16,viewport.height-18).Contains(screen);
        if(!can){CancelWaveMove();return;}
        if(!Editor.ContextOpen&&WavePointerInput(viewport,notesTop,Local(screen)))return;
        if(Editor.Capturing&&(ev.type==EventType.MouseDrag||ev.type==EventType.MouseUp)){
            if(ev.type==EventType.MouseUp){Editor.Up(Local(screen));GUIUtility.hotControl=0;if(Editor.ContextOpen)menuPosition=screen;}else Editor.Move(Local(screen));
            SyncEditorNote();ev.Use();return;
        }
        if(Editor.ContextOpen)return;
        if(inside&&ev.type==EventType.MouseDown&&(ev.button==0||ev.button==1)){
            GUI.FocusControl(null);Editor.Down(Local(screen),ev.button,ev.clickCount,ev.shift,ev.control||ev.command);if(Editor.Capturing)GUIUtility.hotControl=id;SyncEditorNote();ev.Use();
        }
        if(inside&&ev.type==EventType.ContextClick)ev.Use();
    }
    void EditorFrame(){
        if(InputBlocked||Current!=Page.Edit||MidiPromptOpen||LaneTypeOpen||Project==null||editorViewport.width<=0){lastEditorFrame=Time.realtimeSinceStartupAsDouble;return;}
        double now=Time.realtimeSinceStartupAsDouble,dt=Math.Min(.05,Math.Max(0,now-lastEditorFrame));lastEditorFrame=now;
        double ppb=EditorPPB*zoom,span=(editorViewport.width-16)/ppb;
        if(waveCaptured){
            double speed=waveMoved?EditorInteraction.EdgeSpeed(editorPointer.x-editorViewport.x,editorViewport.width-16):0;
            if(speed!=0){editScroll.x=(float)Math.Max(0,Math.Min(Math.Max(0,Project.Length-span)*ppb,editScroll.x+speed*dt*ppb));MoveWave(new Vector2(Mathf.Clamp(editorPointer.x-editorViewport.x,0,editorViewport.width-17)+editScroll.x,0));}
        }else if(Editor.Capturing){
            double speed=Editor.AutoScrolling?EditorInteraction.EdgeSpeed(editorPointer.x-editorViewport.x,editorViewport.width-16):0;
            if(speed!=0){float before=editScroll.x;editScroll.x=(float)Math.Max(0,Math.Min(Math.Max(0,Project.Length-span)*ppb,editScroll.x+speed*dt*ppb));if(before!=editScroll.x)Editor.Move(new Vector2(Mathf.Clamp(editorPointer.x-editorViewport.x,0,editorViewport.width-17)+editScroll.x,editorPointer.y-editorViewport.y-editorNotesTop));}
        }else if(Audio.Running){editScroll.x=(float)(EditorInteraction.Follow(editScroll.x/ppb,span,Audio.Beat,Project.Length)*ppb);SelectMeasure(Project.BarAt(Math.Max(0,Audio.Beat)));}
    }
    void OnApplicationFocus(bool focused){ReleaseOnFocusLoss(focused);if(!focused){interaction?.Cancel();CancelWaveMove();EndSongInfoEdit();}}
    void DrawEditorSelection(float notesTop){
        if(selection.Count>1){var r=Editor.Bounds();r.y+=notesTop;r.xMin-=5;r.xMax+=5;r.yMin-=5;r.yMax+=5;Border(r,Color.white,1);}
        if(selection.Count==1){var n=Project.Notes.FirstOrDefault(v=>selection.Contains(v.Index));if(n!=null&&(n.Pedal||n.Instrument=="SN")){Rect r=Editor.NoteRect(n);r.y+=notesTop;float w=Math.Min(8,Math.Max(2,r.width*.18f));RectFill(new Rect(r.x,r.y,w,r.height),Color.white);RectFill(new Rect(r.xMax-w,r.y,w,r.height),Color.white);}}
        if(Editor.Selecting){var r=Editor.SelectionBox;r.y+=notesTop;RectFill(r,new Color(.37f,.64f,1,.12f));Border(r,C("#73b2ff"),1.5f);}
    }
    void EditorContextMenu(){
        if(!Editor.ContextOpen)return;var target=Project.Notes.FirstOrDefault(n=>n.Index==Editor.ContextTarget);
        int row=target==null?-1:Array.IndexOf(Instruments,target.Instrument);string[] types=row>=0&&row!=2&&row!=7&&row!=8?Types[row]:new string[0];
        float h=184+(types.Length>0?32+types.Length*32:0);Rect r=new Rect(Mathf.Clamp(menuPosition.x,12,W-262),Mathf.Clamp(menuPosition.y,76,H-h-12),250,h);
        var ev=Event.current;if(ev.type==EventType.MouseDown&&!r.Contains(ev.mousePosition)){Editor.ContextOpen=false;ev.Use();return;}
        RectFill(r,panel);Border(r,mint);float x=r.x+10,y=r.y+10;
        if(Button(new Rect(x,y,230,32),"Copy",false,selection.Count>0,14)){Copy();Editor.ContextOpen=false;}y+=40;
        if(Button(new Rect(x,y,230,32),"Paste",false,Editor.HasClipboard,14)){Editor.Paste(Editor.ContextBeat);Editor.ContextOpen=false;}y+=40;
        if(Button(new Rect(x,y,230,32),"Delete",false,selection.Count>0,14)){DeleteSelected();Editor.ContextOpen=false;}y+=42;
        if(types.Length>0){Text(new Rect(x,y,230,26),T("種別","Type"),14,mint);y+=28;foreach(string type in types){if(Button(new Rect(x,y,230,28),EditorTypeLabel(type),target.Articulation==type,true,13)){ChangeSelected(e=>{if((string)e["instrument"]==target.Instrument)e["articulation"]=type;});Editor.ContextOpen=false;SyncEditorNote();}y+=32;}}
        if(Button(new Rect(x,y+4,230,32),"Close",false,true,14))Editor.ContextOpen=false;
        if(r.Contains(ev.mousePosition)&&(ev.isMouse||ev.type==EventType.ScrollWheel))ev.Use();
    }
}
}
