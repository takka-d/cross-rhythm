using System;
using System.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    int editorPanel;
    const float EditorTop=202;
    bool editorFileOpen;
    static readonly Rect EditorFileButtonRect=new Rect(110,86,90,38),EditorFileMenuRect=new Rect(110,132,280,264);
    void DismissEditorFileMenu(){
        if(Current!=Page.Edit||!editorFileOpen)return;
        var ev=Event.current;
        if((ev.type==EventType.MouseDown&&!EditorFileMenuRect.Contains(ev.mousePosition)&&!EditorFileButtonRect.Contains(ev.mousePosition))||(ev.type==EventType.KeyDown&&ev.keyCode==KeyCode.Escape)){editorFileOpen=false;GUIUtility.hotControl=0;ev.Use();}
    }
    void EditorPage(){
        bool enabled=GUI.enabled;GUI.enabled=enabled&&!showMeterPanel&&!Editor.ContextOpen&&!discardPrompt&&!draftRunning&&!editorFileOpen;
        Text(new Rect(24,87,75,38),"Edit",26,Color.white,true);
        GUI.enabled=enabled&&!showMeterPanel&&!Editor.ContextOpen&&!discardPrompt&&!draftRunning;
        if(Button(EditorFileButtonRect,"File",editorFileOpen,true,15,"editor-file")){EndSongInfoEdit();editorFileOpen=!editorFileOpen;GUI.FocusControl(null);}
        GUI.enabled=enabled&&!showMeterPanel&&!Editor.ContextOpen&&!discardPrompt&&!draftRunning&&!editorFileOpen;
        FittedText(new Rect(214,91,Math.Max(100,W-887),32),Project.Title+(Project.Dirty?" *":""),18,muted);
        if(Button(new Rect(W-660,86,96,38),Audio.Running?"Pause":"Play",true,loaded&&!busy,16)){if(Audio.Running)Audio.Pause();else{ResetScheduled();lastClick=Math.Floor(Audio.Beat)-1;Audio.Play(Audio.Beat);}}
        if(Button(new Rect(W-552,86,52,38),"|<",false,true,15)){Audio.Stop();Audio.AnchorBeat=0;SelectMeasure(0);editScroll.x=0;ResetScheduled();}
        if(Button(new Rect(W-472,86,90,38),"Undo",false,undo.Count>0,15))Restore(false);
        if(Button(new Rect(W-372,86,90,38),"Redo",false,redo.Count>0,15))Restore(true);
        SaveButton(new Rect(W-250,86,104,38),false);
        SaveButton(new Rect(W-134,86,110,38),true);
        SaveStatusLine(new Rect(24,128,W-48,25));
        RectFill(new Rect(24,160,W-48,34),panel);
        if(Button(new Rect(36,162,82,30),"Grid",Project.SnapToGrid,true,14)&&!Project.SnapToGrid){PushUndo();Project.Chart["editorSnapToGrid"]=true;QueueEditorRecovery();Project.Dirty=true;}
        if(Button(new Rect(126,162,82,30),"Free",!Project.SnapToGrid,true,14)&&Project.SnapToGrid){PushUndo();Project.Chart["editorSnapToGrid"]=false;QueueEditorRecovery();Project.Dirty=true;}
        Text(new Rect(236,166,42,24),"Grid",13,muted);int q=(int?)Project.Chart["quantize"]??16;
        if(Button(new Rect(278,162,76,30),"1/"+q,false,true,14)){int i=Array.IndexOf(Grids,q);PushUndo();Project.Chart["quantize"]=Grids[(i+1)%Grids.Length];Edited();gridField="";}
        if(gridField=="")gridField=q.ToString();gridField=EditField("grid",new Rect(363,162,58,30),gridField);
        if(Button(new Rect(429,162,52,30),"Set",false,true,13)&&int.TryParse(gridField,out int custom)&&custom>=1&&custom<=1024){PushUndo();Project.Chart["quantize"]=custom;Edited();}
        Text(new Rect(510,166,66,24),"Zoom X",13,muted);float previousZoom=zoom;zoom=GUI.HorizontalSlider(new Rect(580,174,160,18),zoom,30f/56f,20);if(Math.Abs(zoom-previousZoom)>.00001f)editScroll.x*=zoom/previousZoom;
        Text(new Rect(763,166,W-787,24),$"Bar {editMeasure+1} / {Project.Measures.Length}   ·   {selection.Count} "+T("選択","selected"),13,muted);
        EditorTimeline();EditorInspector();TrackSongInfoFocus();
        if(Event.current.rawType==EventType.MouseUp||Event.current.rawType==EventType.KeyUp||editorPanel!=4)mixerEditing="";
        Text(new Rect(24,H-50,W-48,22),T("Shift + 左クリック/ドラッグ: 範囲選択 / Ctrl + 左: 複数選択 / 右: メニュー / 左ダブルクリック: 削除 / 矢印: 移動","Shift + Left click/drag: range · Ctrl + Left: toggle · Right: menu · Double click: delete · Arrows: move"),12,muted);
        GUI.enabled=enabled;EditorFileMenu();EditorMeterControls();EditorContextMenu();if(!draftRunning)EditorKeys();
    }
    void EditorFileMenu(){
        if(!editorFileOpen)return;
        var r=EditorFileMenuRect;float menuX=r.x,menuY=r.y;RectFill(r,panel);Border(r,mint);
        ProjectButton(new Rect(menuX+12,menuY+12,256,34),false,true);
        if(Button(new Rect(menuX+12,menuY+54,256,34),"New Project",false,true,15)){editorFileOpen=false;NewProject();}
        if(Button(new Rect(menuX+12,menuY+96,256,34),"Audio",false,true,15))PickAudio();
        if(Button(new Rect(menuX+12,menuY+138,256,34),"Import MIDI",false,true,15)){editorFileOpen=false;PickMidi();}
        MidiExportButton(new Rect(menuX+12,menuY+180,256,34));
        if(Button(new Rect(menuX+12,menuY+222,256,34),"Auto Draft",false,true,15)){editorPanel=2;editorFileOpen=false;}
    }
    void EditorInspector(){
        float top=EditorBottom,x=40,y=top+55,cw=(W-104)/3;
        RectFill(new Rect(24,top,W-48,H-top-52),panel);Border(new Rect(24,top,W-48,H-top-52),line);
        string[] tabs={"Note","Song","Tempo","Mixer","Analyze"};int[] panels={0,1,3,4,2};for(int i=0;i<tabs.Length;i++)if(Button(new Rect(36+i*102,top+10,92,32),tabs[i],editorPanel==panels[i],true,14)){editorPanel=panels[i];if(editorPanel==3)SelectTempoPosition(Math.Max(0,Audio.Beat));GUI.FocusControl(null);}
        if(editorPanel==0){
            Text(new Rect(x,y,cw,24),LaneNames[instrument]+" · "+T("種別","Type"),14,mint,true);
            var types=Types[instrument];for(int i=0;i<types.Length;i++){int ix=i;if(Button(new Rect(x+i%2*(cw/2),y+32+i/2*35,cw/2-8,29),EditorTypeLabel(types[i]),kind==i,true,13)){kind=ix;ChangeSelected(e=>{if((string)e["instrument"]==Instruments[instrument])e["articulation"]=types[ix];});}}
            x+=cw+12;Text(new Rect(x,y,cw,24),T("強弱","Velocity"),14,muted);
            for(int i=0;i<6;i++){int v=i;if(Button(new Rect(x+i*43,y+32,37,30),i.ToString(),selection.Count==0?velocity==i:Project.Notes.Where(n=>selection.Contains(n.Index)).All(n=>n.Velocity==i),true,14)){velocity=v;ChangeSelected(e=>e["velocity"]=v);}}
            Text(new Rect(x,y+78,130,22),T("長さ(拍)","Length (beats)"),13,muted);
            durationField=EditField("duration",new Rect(x+132,y+72,96,30),durationField);
            if(Button(new Rect(x+238,y+72,58,30),"Set",false,true,14)&&double.TryParse(durationField,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var d)&&d>0&&d<=256){duration=d;Editor.SetDuration(d);SyncEditorNote();status=T("長さを変更しました","Length updated");}
            x+=cw+12;Text(new Rect(x,y,cw,24),selection.Count+" "+T("ノーツを選択","notes selected"),14,muted);
            if(Button(new Rect(x,y+32,92,32),"Copy",false,selection.Count>0,14))Copy();if(Button(new Rect(x+104,y+32,92,32),"Paste",false,Editor.HasClipboard,14))Paste();
            if(Button(new Rect(x+208,y+32,92,32),"Delete",false,selection.Count>0,14))DeleteSelected();
            if(Button(new Rect(x,y+74,300,30),"MIDI Velocity 0: "+midiZeroMode,false,true,13))midiZeroMode=(MidiImport.ZeroMode)(((int)midiZeroMode+1)%3);
        }else if(editorPanel==1){
            Text(new Rect(x,y,cw,22),T("曲名","Title"),13,muted);
            string next=EditField("title",new Rect(x,y+25,cw-10,30),Project.SongTitle);if(next!=Project.SongTitle)SetSongInfoField("title",next);
            Text(new Rect(x,y+66,cw,22),T("アーティスト名","Artist"),13,muted);
            string artist=EditField("artist",new Rect(x,y+91,cw-10,30),Project.Artist);if(artist!=Project.Artist)SetSongInfoField("artist",artist);
            x+=cw+12;Text(new Rect(x,y,cw,22),"Initial BPM",13,muted);
            if(bpmField=="")bpmField=Project.BPM.ToString(System.Globalization.CultureInfo.InvariantCulture);bpmField=EditField("bpm",new Rect(x,y+25,cw-90,30),bpmField);
            if(Button(new Rect(x+cw-80,y+25,68,30),"Set",false,true,14)&&double.TryParse(bpmField,out var bpm)&&bpm>=20&&bpm<=600){PushUndo();Audio.Pause();Project.SetTempo(0,0,bpm);Edited();}
            Text(new Rect(x,y+66,cw,22),T("音源の開始位置(秒)","Audio offset (sec)"),13,muted);
            offsetField=EditField("offset",new Rect(x,y+91,cw-90,30),offsetField);
            if(Button(new Rect(x+cw-80,y+91,68,30),"Set",false,true,14)&&double.TryParse(offsetField,out var sec)&&!double.IsNaN(sec)&&!double.IsInfinity(sec)){PushUndo();Audio.Pause();Project.Chart["audioOffsetSec"]=sec;Edited();}
            x+=cw+12;var meter=Project.Meter(editMeasure);Text(new Rect(x,y,cw,26),$"Bar {editMeasure+1} · {meter.Item1}/{meter.Item2}",17,mint);
            if(Button(new Rect(x,y+30,280,32),"Bar / Meter",false,true,15))showMeterPanel=true;
            Text(new Rect(x,y+76,cw,26),"Difficulty "+Project.Difficulty+" · Auto",18,mint,true);
            Text(new Rect(x,y+106,cw,24),Project.NotesPerSecond.ToString("0.00")+" "+T("ノーツ/秒","notes/sec"),12,muted);
        }else if(editorPanel==3)EditorTempoControls(x,y,cw);else if(editorPanel==4)EditorMixer(x,y);else DraftControls(x,y,cw);
        if(draftRunning){bool enabled=GUI.enabled;GUI.enabled=true;Text(new Rect(W-430,top+15,240,26),$"Analyze {draftProgress*100:0}%",14,mint);if(Button(new Rect(W-166,top+10,126,32),"Cancel",false,true,14))draftCancel=true;GUI.enabled=enabled;}
    }
    string EditorTypeLabel(string type){switch(type){case "center":case "normal":return "Normal";case "rim_closed":return "Closed rimshot";case "rim_open":return "Open rimshot";case "buzz":return "Buzz roll";case "auto":return "Auto";default:return char.ToUpperInvariant(type[0])+type.Substring(1);}}
    GUIStyle editFieldStyle;
    string EditField(string name,Rect rect,string value){if(editFieldStyle==null)editFieldStyle=new GUIStyle(field){fontSize=14,padding=new RectOffset(6,6,4,4)};GUI.SetNextControlName("edit-"+name);return GUI.TextField(rect,value,editFieldStyle);}
}
}
