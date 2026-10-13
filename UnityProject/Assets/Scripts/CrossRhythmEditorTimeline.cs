using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    const float EditorPPB=56;
    float EditorRowHeight=>Mathf.Clamp((H-EditorTop-90-254)/9,26,46);
    float EditorBottom=>EditorTop+90+9*EditorRowHeight+10;
    bool showMeterPanel;
    readonly List<(ChartNote note,Rect rect)> visibleNotes=new List<(ChartNote,Rect)>();
    readonly Dictionary<int,float> badgeEnds=new Dictionary<int,float>();
    readonly SortedSet<double> editorGrid=new SortedSet<double>();
    readonly Dictionary<int,Rect> editorBadges=new Dictionary<int,Rect>();
    static readonly string[] LaneNames={"CRASH (CR)","RIDE (RD)","HI-HAT (HH)","SNARE (SN)","HIGH TOM (HT)","MID TOM (MT)","FLOOR TOM (FT)","KICK (BD)","HH PEDAL STATE"};
    void OpenMeterPanel(){
        EndSongInfoEdit();Audio.Pause();Editor.Cancel();Editor.ContextOpen=false;editorFileOpen=false;gridMenuOpen=false;
        SelectMeasure(editMeasure);meterKeepTiming=true;showMeterPanel=true;status="";GUI.FocusControl(null);
    }
    void EditorMeterControls(){
        if(!showMeterPanel)return;
        RectFill(new Rect(0,0,W,H),new Color(0,0,0,.65f));
        GUI.ModalWindow(701,new Rect(W/2-370,H/2-204,740,408),id=>{
            RoundFill(new Rect(0,0,740,408),panel,10);RoundBorder(new Rect(0,0,740,408),line,1,10);
            Text(new Rect(24,16,580,34),"Bar / Meter",24,mint,true);
            if(Button(new Rect(632,16,84,32),"Close",false,true,14))showMeterPanel=false;
            Text(new Rect(24,70,390,30),$"Bar {editMeasure+1} / {Project.Measures.Length}",20);
            if(Button(new Rect(514,68,96,32),T("前の小節","Previous"),false,editMeasure>0,14))SelectMeasure(editMeasure-1,true);
            if(Button(new Rect(620,68,96,32),T("次の小節","Next"),false,editMeasure<Project.Measures.Length-1,14))SelectMeasure(editMeasure+1,true);
            Text(new Rect(24,119,220,28),T("選択した小節の拍子","Selected bar's meter"),16,muted);
            meterNumerator=EditField("meter-n",new Rect(266,113,82,38),meterNumerator);
            Text(new Rect(359,119,24,28),"/",20,muted);
            meterDenominator=EditField("meter-d",new Rect(389,113,82,38),meterDenominator);
            if(Button(new Rect(494,113,222,38),T("拍子を適用","Apply Meter"),true,true,16))ApplyMeter();
            if(Button(new Rect(24,165,334,32),T("ノーツとテンポの時刻を維持","Keep note / tempo timing"),meterKeepTiming,true,14,kind:ControlKind.Choice))meterKeepTiming=true;
            if(Button(new Rect(376,165,340,32),T("後続を小節ごと移動","Shift following bars"),!meterKeepTiming,true,14,kind:ControlKind.Choice))meterKeepTiming=false;
            Text(new Rect(24,203,690,26),meterKeepTiming?T("小節の区切りだけ変更します。ノーツは次の小節へ繰り越します。","Only bar boundaries change. Overflow notes continue in the next bar."):T("後続のノーツも移動します。はみ出すノーツは先に移動してください。","Later notes move too. Move notes beyond a shortened bar first."),13,muted);
            RectFill(new Rect(24,241,692,1),line);
            var meter=Project.Meter(editMeasure);
            if(Button(new Rect(24,262,334,38),T("後ろに小節を追加","Insert Bar After")+"  ("+meter.Item1+"/"+meter.Item2+")",false,true,15))InsertEditorMeasure();
            if(Button(new Rect(376,262,340,38),T("空の小節を削除","Remove Empty Bar"),false,Project.CanRemoveMeasure(editMeasure),15))RemoveEditorMeasure();
            if(Button(new Rect(24,316,334,36),T("音源の長さまで追加","Extend to Audio"),false,Audio.Song!=null,15))ExtendEditorToAudio();
            FittedText(new Rect(24,369,690,24),status,12,muted);
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){showMeterPanel=false;Event.current.Use();}
        },"",GUIStyle.none);
    }
    void EditorTimeline(){
        float rh=EditorRowHeight;const float top=EditorTop,waveHeight=48,rulerHeight=24,labelWidth=256;
        float ppb=EditorPPB*zoom,notesTop=waveHeight+rulerHeight,notesHeight=9*rh,totalWidth=(float)Project.Length*ppb;
        Rect viewport=new Rect(24+labelWidth,top,W-48-labelWidth,notesTop+notesHeight+18);
        EditorPointerInput(viewport,notesTop);
        RectFill(new Rect(24,top,labelWidth,notesTop+notesHeight),C("#101821"));
        for(int row=0;row<9;row++){
            int laneCount=Project.InstrumentCount(Instruments[row]),selectedCount=0;if(Event.current.type==EventType.Repaint)foreach(int index in selection)if(Project.NoteAtIndex(index)?.Instrument==Instruments[row])selectedCount++;float y=top+notesTop+row*rh;
            if(selectedCount>0){RectFill(new Rect(24,y,labelWidth,rh),C(selectedCount==laneCount?"#203c55":"#182c3e"));RectFill(new Rect(24,y,3,rh),C("#94d5ff"));}
            string[] names={"CRASH","RIDE","HI-HAT","SNARE","HIGH TOM","MID TOM","FLOOR TOM","KICK","PEDAL"};
            Text(new Rect(34,y+(rh-24)/2,96,25),names[row],12,selectedCount>0?C("#eef8ff"):C("#b6c5d3"));
            var laneRect=new Rect(24,y,100,rh);if(GUI.Button(laneRect,GUIContent.none,GUIStyle.none)||ControllerButtonHit(laneRect,GUI.enabled)){instrument=row;kind=Math.Max(0,Array.IndexOf(Types[row],EditorNoteTypes.Get(Project,row)));Editor.SelectLane(row,Event.current.control||Event.current.command);GUI.FocusControl(null);}
            string inputType=EditorNoteTypes.Label(Instruments[row],EditorNoteTypes.Get(Project,row));
            string monitor=row==8?"HH":Instruments[row];
            if(Button(new Rect(124,y+2,32,rh-4),"M",Project.MonitorMuted(monitor),true,11,"lane-mute-"+row,ControlKind.Toggle))ToggleMonitor(monitor);
            if(Button(new Rect(162,y+2,112,rh-4),inputType,false,Types[row].Length>1,11,"lane-type-"+row,ControlKind.Menu)){laneTypeRow=row;Editor.ContextOpen=false;Editor.Cancel();GUI.FocusControl(null);}

        }
        Text(new Rect(162,top+rulerHeight+24,112,24),"Type",11,muted);
        if(Button(new Rect(124,top+rulerHeight+2,32,22),"M",Project.MonitorMuted("music"),true,11,"music-mute",ControlKind.Toggle))ToggleMonitor("music");
        if(Button(new Rect(162,top+rulerHeight+2,112,22),"Drums M",Project.MonitorMuted("drums"),true,10,"drums-mute",ControlKind.Toggle))ToggleMonitor("drums");Text(new Rect(36,top+rulerHeight+2,124,20),"Wave ↔",13,muted);Text(new Rect(36,top+rulerHeight+23,124,20),$"{EditorAudioOffset:0.000} s",11,waveCaptured?mint:muted);if(Button(new Rect(34,top+1,236,22),"Bar / Meter",false,true,12,"bar-meter",ControlKind.Dialog))OpenMeterPanel();
        var wheel=Event.current;
        if(GUI.enabled&&!MidiPromptOpen&&!showMeterPanel&&!editorFileOpen&&!draftRunning&&!Editor.ContextOpen&&viewport.Contains(wheel.mousePosition)&&wheel.type==EventType.ScrollWheel){
            // v175: Shift+wheel or a horizontal trackpad gesture pans the timeline.
            float delta=Math.Abs(wheel.delta.x)>Math.Abs(wheel.delta.y)*.55f?wheel.delta.x:wheel.shift?wheel.delta.y:0;
            if(delta!=0)editScroll.x=Mathf.Clamp(editScroll.x+delta*48,0,Math.Max(0,totalWidth-viewport.width+16));else if(wheel.delta.y!=0)EditorSeek(EditorInteraction.WheelBeat(Math.Max(0,Audio.Beat),wheel.delta.y,Project.Grid,Project.Length));wheel.Use();
        }
        if(Event.current.type==EventType.Repaint&&GUI.enabled&&!MidiPromptOpen&&!showMeterPanel&&!editorFileOpen&&!draftRunning&&!Editor.ContextOpen&&ControllerScrollActive(viewport)){
            if(Math.Abs(controllerWheel.x)>Math.Abs(controllerWheel.y)*.55f)editScroll.x=Mathf.Clamp(editScroll.x+controllerWheel.x*620*Time.unscaledDeltaTime,0,Math.Max(0,totalWidth-viewport.width+16));
            else if(Math.Abs(controllerWheel.y)>.001f)EditorSeek(Math.Max(0,Math.Min(Project.Length,Audio.Beat+controllerWheel.y*8*Time.unscaledDeltaTime)));
        }
        editScroll.y=0;editScroll=GUI.BeginScrollView(viewport,editScroll,new Rect(0,0,totalWidth,notesTop+notesHeight),false,false);
        if(Event.current.type==EventType.Repaint){
        RectFill(new Rect(0,0,totalWidth,notesTop+notesHeight),C("#070b10"));
        float left=editScroll.x,right=Math.Min(totalWidth,left+viewport.width);
        RectFill(new Rect(left,rulerHeight+waveHeight/2,right-left,1),line);
        if(Event.current.type==EventType.Repaint&&Audio.Waveform!=null){for(float xx=left;xx<right;xx+=2){double from=EditorAudioOffset+Project.SecondsAtBeat(xx/ppb),to=EditorAudioOffset+Project.SecondsAtBeat((xx+2)/ppb);Audio.Waveform.Range(from,to,out float lo,out float hi);float amp=(waveHeight-6)/2;float y=rulerHeight+waveHeight/2-Mathf.Clamp(hi,-1,1)*amp;float h=Math.Max(1,(Mathf.Clamp(hi,-1,1)-Mathf.Clamp(lo,-1,1))*amp);RectFill(new Rect(xx,y,1.5f,h),C("#a7bdca"));}}
        else if(Audio.Waveform==null)Text(new Rect(left+12,rulerHeight+22,600,24),busy?T("音源を読込中…","Loading audio…"):T("Audioで音源を選択","Choose a track with Audio"),13,muted);
        for(int row=0;row<9;row++){RectFill(new Rect(left,notesTop+row*rh,right-left,rh),C(row%2==0?"#0d141c":"#0a1017"));RectFill(new Rect(left,notesTop+row*rh,right-left,1),C("#202c39"));}
        int first=Math.Max(0,Project.BarAt(left/ppb)),last=Project.BarAt(right/ppb);
        for(int m=first;m<=last;m++){
            float xx=(float)Project.Starts[m]*ppb,ww=(float)Project.Measures[m]*ppb;
            RectFill(new Rect(xx,0,ww,rulerHeight),m==editMeasure?C("#203c55"):bg);var sig=Project.Meter(m);Text(new Rect(xx+5,2,ww-6,22),$"M{m+1}  {sig.Item1}/{sig.Item2}",12,m==editMeasure?mint:muted);
            var grid=editorGrid;ChartVisuals.EditGridPoints(Project,m,grid);
            foreach(double local in grid){if(!Project.SnapToGrid)continue;float gx=(float)(Project.Starts[m]+local)*ppb;if(gx<left||gx>right)continue;bool major=ChartVisuals.IsBeatLine(Project,m,local);RectFill(new Rect(gx,notesTop,major?1:.6f,notesHeight),C(major?"#344456":"#1b2631"));}
            RectFill(new Rect(xx,0,1.5f,notesTop+notesHeight),C("#344456"));
        }
        float tempoLabelEnd=left;
        foreach(var tempo in Project.Tempo.Points){float tx=(float)tempo.Beat*ppb;if(tx<left||tx>right)continue;
            RectFill(new Rect(tx,rulerHeight,1,waveHeight+notesHeight),new Color(.57f,.91f,.79f,.38f));
            if(tx>=tempoLabelEnd){float tw=92;RectFill(new Rect(tx+2,rulerHeight+1,tw,18),bg);Text(new Rect(tx+5,rulerHeight+1,tw,18),$"{tempo.BPM:0.##} BPM",11,mint);tempoLabelEnd=tx+tw+4;}
        }
        var visible=visibleNotes;visible.Clear();
        foreach(var n in Project.NotesOverlapping((left-4)/ppb,(right+4)/ppb)){int row=Array.IndexOf(Instruments,n.Instrument);if(row<0)continue;Rect rect=ChartVisuals.EditRect(Project,n,row,ppb,rh);rect.y+=notesTop;if(rect.xMax<left||rect.x>right)continue;visible.Add((n,rect));bool sel=selection.Contains(n.Index);RoundFill(rect,ChartVisuals.EditColor(Project,n),3);RoundBorder(rect,sel?Color.white:new Color(1,1,1,.55f),sel?2.5f:1,3);
            string mark=n.Instrument=="HH"?(Project.ClosedAt(n.Beat)?"C":"O"):n.Pedal&&rect.width>=42?"CLOSED":"";if(mark!="")Centered(rect,mark,n.Pedal?9:10,n.Pedal?C("#f4e8ff"):C("#101317"));
        }
        editorBadges.Clear();badgeEnds.Clear();
        visible.Sort((a,b)=>a.rect.center.x.CompareTo(b.rect.center.x));
        foreach(var item in visible){var n=item.note;var r=item.rect;string code=ChartVisuals.TypeCode(n);if(code=="")continue;int row=Array.IndexOf(Instruments,n.Instrument);float bw=code.Length*7+6;Rect badge=default;bool placed=false;
            for(int tier=0;tier<2;tier++){int key=row*2+tier;float bx=Mathf.Clamp(r.center.x-bw/2,left+1,Math.Max(left+1,right-bw-1));if(badgeEnds.TryGetValue(key,out float end)&&bx<end+2)continue;badge=new Rect(bx,notesTop+row*rh+(tier==0?1:rh-13),bw,12);badgeEnds[key]=badge.xMax;placed=true;break;}
            if(placed){bool sel=selection.Contains(n.Index);RoundFill(badge,C(sel?"#244567":"#111f2d"),3);RoundBorder(badge,C(sel?"#cceaff":"#58738c"),1,3);Centered(badge,code,10,C("#f1f7fc"));float y1=badge.y<r.center.y?badge.yMax:r.yMax,y2=badge.y<r.center.y?r.y:badge.y;RectFill(new Rect(r.center.x,Math.Min(y1,y2),1,Math.Abs(y2-y1)),C("#7895b0"));var localBadge=badge;localBadge.y-=notesTop;editorBadges[n.Index]=localBadge;}
            else{Centered(new Rect(r.x,r.center.y-10,r.width,10),code.Substring(0,1),8,C("#081019"));if(code.Length>1)Centered(new Rect(r.x,r.center.y,r.width,10),code.Substring(1),8,C("#081019"));}
        }
        RectFill(new Rect((float)Audio.Beat*ppb,0,2,notesTop+notesHeight),Color.white);
        DrawEditorSelection(notesTop);
        }
        GUI.EndScrollView();
    }
    void Centered(Rect rect,string value,int size,Color color){var style=TextStyle(size,false,true);style.normal.textColor=color;GUI.Label(rect,value,style);}
}
}
