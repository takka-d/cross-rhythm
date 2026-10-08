using System;
using System.Linq;
using System.Globalization;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    string tempoBar="1",tempoBeat="1",tempoBPM="120";
    Vector2 tempoScroll;
    void ResetTempoFields(){
        if(TempoPosition(out var bar,out var beat))tempoBPM=Project.Tempo.BPMAt(Project.Starts[bar]+beat).ToString("G17",CultureInfo.InvariantCulture);
        else SelectTempoPosition(Math.Max(0,Audio.Beat));
    }
    void SelectTempoPosition(double beat){
        beat=Math.Max(0,Math.Min(Project.Length-1e-8,beat));int bar=Project.BarAt(beat);
        tempoBar=(bar+1).ToString();tempoBeat=(beat-Project.Starts[bar]+1).ToString("G17",CultureInfo.InvariantCulture);tempoBPM=Project.Tempo.BPMAt(beat).ToString("G17",CultureInfo.InvariantCulture);
    }
    bool TempoPosition(out int bar,out double beat){
        bar=-1;beat=0;
        if(!int.TryParse(tempoBar,out var shown)||!double.TryParse(tempoBeat,NumberStyles.Float,CultureInfo.InvariantCulture,out var local))return false;
        bar=shown-1;beat=local-1;return bar>=0&&bar<Project.Measures.Length&&double.IsFinite(beat)&&beat>=0&&beat<Project.Measures[bar];
    }
    void ApplyTempo(bool remove){
        if(!TempoPosition(out var bar,out var beat)||(!remove&&(!double.TryParse(tempoBPM,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!TempoMap.ValidBPM(value)))){status=T("小節・拍の位置とBPM(20-600)を確認してください","Check Bar, Beat and BPM (20-600)");return;}
        if(remove&&!Project.HasTempoChange(bar,beat)){status=T("この位置に削除できるテンポ変更はありません","No tempo change to remove at this position");return;}
        Audio.Pause();PushUndo();
        if(remove)Project.RemoveTempo(bar,beat);else Project.SetTempo(bar,beat,double.Parse(tempoBPM,CultureInfo.InvariantCulture));
        Edited();ResetEditorFields();status=remove?T("テンポ変更を削除しました","Tempo change removed"):T("テンポを設定しました","Tempo set");
    }
    void EditorTempoControls(float x,float y,float cw){
        Text(new Rect(x,y,70,22),"Bar",13,muted);Text(new Rect(x+90,y,110,22),T("拍(4分音符)","Beat (quarter)"),13,muted);Text(new Rect(x+220,y,100,22),"BPM",13,muted);
        tempoBar=EditField("tempo-bar",new Rect(x,y+25,76,30),tempoBar);tempoBeat=EditField("tempo-beat",new Rect(x+90,y+25,116,30),tempoBeat);tempoBPM=EditField("tempo-bpm",new Rect(x+220,y+25,100,30),tempoBPM);
        if(Button(new Rect(x,y+66,98,30),"Use Cursor",false,true,12))SelectTempoPosition(Math.Max(0,Audio.Beat));
        if(Button(new Rect(x+108,y+66,66,30),"Set",false,true,14))ApplyTempo(false);
        bool canRemove=TempoPosition(out var bar,out var beat)&&Project.HasTempoChange(bar,beat);
        if(Button(new Rect(x+184,y+66,98,30),"Remove",false,canRemove,13))ApplyTempo(true);
        Text(new Rect(x,y+104,cw,26),T("拍は1から。小節の途中にも設定できます","Beats start at 1; changes can be inside a bar"),11,muted);
        x+=cw+12;float width=W-x-46;Text(new Rect(x,y,width,22),T("テンポ変更一覧","Tempo changes"),14,mint);
        tempoScroll=GUI.BeginScrollView(new Rect(x,y+26,width,102),tempoScroll,new Rect(0,0,width-20,Project.Tempo.Points.Length*34),false,false);
        for(int i=0;i<Project.Tempo.Points.Length;i++){var p=Project.Tempo.Points[i];string label=$"Bar {p.Measure+1}  ·  Beat {p.Local+1:0.######}     {p.BPM:0.###} BPM"+(p.Beat==0?"  ·  Initial":"");
            if(Button(new Rect(0,i*34,width-22,30),label,false,true,13)){SelectTempoPosition(p.Beat);Audio.Pause();EditorSeek(p.Beat);}
        }
        GUI.EndScrollView();
    }
}
}
