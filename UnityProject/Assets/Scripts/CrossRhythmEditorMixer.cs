using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    string mixerEditing="";
    void SetEditorInstrumentGain(string instrument,float value){
        if(Math.Abs(Project.InstrumentGain(instrument)-value)<.0001f)return;
        if(mixerEditing!=instrument){PushUndo();mixerEditing=instrument;}
        Project.SetInstrumentGain(instrument,value);QueueEditorRecovery();
    }
    void ToggleMonitor(string channel){PushUndo();Project.SetMonitorMuted(channel,!Project.MonitorMuted(channel));QueueEditorRecovery();Audio.EditorMonitoring=Current==Page.Edit;Audio.ApplyMonitor();if(channel!="music"){Audio.CancelFutureDrums();ResetScheduled();if(Audio.Running)foreach(var n in Project.Notes)if(n.Beat<Audio.Beat)scheduled.Add(n.Index);}}
    void SetMetronome(bool value){metronome=value;Audio.StopClicks();lastClick=Audio.Beat-0.00001;if(string.IsNullOrEmpty(workspaceCheckRoot)){PlayerPrefs.SetInt("metronome",value?1:0);PlayerPrefs.Save();}}
    void EditorMixer(float x,float y){
        FittedText(new Rect(564,EditorBottom+14,W-608,27),T("MuteはEditの試聴のみ · HHはペダル音を含む","Mute: Edit monitoring only · HH includes pedal"),13,muted);
        if(Button(new Rect(x,y,164,28),"Music Mute: "+(Project.MonitorMuted("music")?"ON":"OFF"),Project.MonitorMuted("music"),true,13))ToggleMonitor("music");
        if(Button(new Rect(x+176,y,164,28),"Drums Mute: "+(Project.MonitorMuted("drums")?"ON":"OFF"),Project.MonitorMuted("drums"),true,13))ToggleMonitor("drums");
        if(Button(new Rect(x+352,y,196,28),"Metronome: "+(metronome?"ON":"OFF"),metronome,true,13))SetMetronome(!metronome);
        y+=32;
        float width=(W-80)/4;
        for(int i=0;i<8;i++){
            float xx=x+i%4*width,yy=y+i/4*48;string instrument=Instruments[i];float value=Project.InstrumentGain(instrument),maximum=Math.Max(ChartProject.MaxInstrumentGain,value);
            Text(new Rect(xx,yy,width-144,24),LaneNames[i],13,mint,true);
            if(Button(new Rect(xx+width-142,yy,58,24),"Mute",Project.MonitorMuted(instrument),true,12))ToggleMonitor(instrument);
            Text(new Rect(xx+width-78,yy,60,24),Mathf.RoundToInt(value*100)+"%",14,Color.white);
            var slider=new Rect(xx,yy+25,width-24,20);
            RectFill(new Rect(slider.x+5+(slider.width-10)/maximum,slider.y-5,1,16),muted);
            GUI.SetNextControlName("mixer-"+instrument);
            float next=GUI.HorizontalSlider(slider,value,0,maximum);
            if(Math.Abs(next-value)>.0001f)SetEditorInstrumentGain(instrument,Mathf.Round(next*100)/100);
        }
    }
}
}
