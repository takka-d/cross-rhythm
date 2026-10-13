using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    double practiceSpeed=1;
    double? pendingPracticeSpeed;
    bool practiceVolumeOpen,practiceVolumeDirty;
    void UpdatePracticeSpeed(){
        if(Current!=Page.Practice){pendingPracticeSpeed=null;ClosePracticeVolume();return;}
        if(pendingPracticeSpeed.HasValue&&(UnityEngine.InputSystem.Mouse.current==null||!UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)){
            double next=pendingPracticeSpeed.Value;pendingPracticeSpeed=null;ChangePractice(next,pro);
        }
    }
    void ChangePractice(double speed,bool nextPro){
        speed=Math.Round(Math.Max(.25,Math.Min(2,speed)),2);
        if(Math.Abs(speed-practiceSpeed)<1e-8&&Math.Abs(Audio.Rate-speed)<1e-8){pro=nextPro;ReleaseInputs();SetPedal(false,Audio.Beat);PlayerPrefs.SetInt("pro",pro?1:0);PlayerPrefs.Save();return;}
        double beat=Audio.Beat;bool running=Audio.Running;
        Audio.Pause();pro=nextPro;practiceSpeed=speed;Audio.Rate=speed;
        Seek(beat);if(running)Audio.Play(beat);
        PlayerPrefs.SetInt("pro",pro?1:0);PlayerPrefs.Save();
    }
    string PracticePosition(double beat){
        int bar=Project.BarAt(Math.Min(Project.Length-1e-8,beat));
        double start=bar<0?bar*CountIn.Length(Project):Project.Starts[bar];
        double unit=ChartVisuals.BeatUnit(Project,bar),local=Math.Max(0,beat-start)/unit;
        int beats=Project.Meter(Math.Max(0,bar)).Item1;
        return (bar<0?"Count "+(-bar):"M"+(bar+1))+"\n"+Math.Min(beats,Math.Floor(local+1e-7)+1).ToString("0")+" / "+beats;
    }
    void PracticeSeekBar(double beat,Rect area){
        Rect rail=new Rect(W-27,area.y+4,19,area.height-8);
        bool enabled=GUI.enabled;GUI.enabled=enabled&&loaded&&!busy&&pendingPerformance==null;
        GUI.SetNextControlName("practice-position");
        float next=GUI.VerticalSlider(rail,(float)beat,(float)CountIn.Start(Project),(float)Project.Length);
        if(Math.Abs(next-beat)>.01)Seek(next);GUI.enabled=enabled;
    }
    void PracticeControls(){
        float y=H-60;RectFill(new Rect(12,y,W-24,46),panel);
        if(Button(new Rect(28,y+6,104,32),"Normal",!pro,true,14,kind:ControlKind.Choice))ChangePractice(practiceSpeed,false);
        if(Button(new Rect(140,y+6,80,32),"Pro",pro,true,14,kind:ControlKind.Choice))ChangePractice(practiceSpeed,true);
        Text(new Rect(242,y+11,132,26),$"Speed ×{(pendingPracticeSpeed??practiceSpeed):0.00}",15,mint);
        if(Button(new Rect(390,y+6,68,32),"×1",false,true,14)){pendingPracticeSpeed=null;ChangePractice(1,pro);}
        GUI.SetNextControlName("practice-speed");
        float laneX=W-342,volumeX=laneX-128;
        Rect slider=new Rect(480,y+17,Math.Max(56,volumeX-498),20);
        float speed=GUI.HorizontalSlider(slider,(float)(pendingPracticeSpeed??practiceSpeed),.25f,2f);
        if(Math.Abs(speed-(pendingPracticeSpeed??practiceSpeed))>.0001)pendingPracticeSpeed=Math.Round(speed,2);
        float thumb=GUI.skin.horizontalSliderThumb.fixedWidth;
        float normal=slider.x+thumb/2+(slider.width-thumb)*(1f-.25f)/(2f-.25f);
        RectFill(new Rect(normal,y+8,1,7),mint);RectFill(new Rect(normal,y+28,1,7),mint);
        Rect volumeButton=new Rect(volumeX,y+6,116,32);
        if(Button(volumeButton,"Volume",practiceVolumeOpen,true,14,"practice-volume",ControlKind.Menu)){if(practiceVolumeOpen)ClosePracticeVolume();else practiceVolumeOpen=true;}
        LaneOrderControls(new Rect(laneX,y+6,310,32),14);
        if(practiceVolumeOpen){
            Rect popup=new Rect(volumeButton.xMax-382,y-132,382,126);
            if(Event.current.type==EventType.MouseDown&&!popup.Contains(Event.current.mousePosition)&&!volumeButton.Contains(Event.current.mousePosition))ClosePracticeVolume();
            RoundFill(popup,panel);RoundBorder(popup,mint);
            Text(new Rect(popup.x+18,popup.y+10,346,25),T("音量","Volume"),17,Color.white,true);
            PracticeVolumeRow(new Rect(popup.x+18,popup.y+43,346,28),"Music",Audio.BackingGain,true);
            PracticeVolumeRow(new Rect(popup.x+18,popup.y+82,346,28),"Drums",Audio.DrumGain,false);
            if(Event.current.type==EventType.MouseUp)FlushPracticeVolume();
        }
        if(Audio.Preparing||!string.IsNullOrEmpty(Audio.PlaybackError))FittedText(new Rect(242,y-23,670,22),Audio.Preparing?T("音源を準備中…","Preparing audio…"):T("音源の準備に失敗しました。再生し直してください","Audio preparation failed. Press Play to retry."),12,muted);
    }
    void PracticeVolumeRow(Rect r,string caption,float value,bool music){
        Text(new Rect(r.x,r.y,66,r.height),caption,14,muted);
        GUI.SetNextControlName(music?"practice-music-volume":"practice-drums-volume");
        float next=GUI.HorizontalSlider(new Rect(r.x+70,r.y+7,212,18),value,0,1);
        Text(new Rect(r.x+294,r.y,52,r.height),Mathf.RoundToInt(next*100)+"%",14,mint);
        if(Math.Abs(next-value)>.0001)SetPracticeVolume(music,next);
    }
    void SetPracticeVolume(bool music,float value){
        value=Mathf.Clamp01(value);if(music)Audio.BackingGain=value;else Audio.DrumGain=value;
        PlayerPrefs.SetFloat(music?"musicVolume":"drumsVolume",value);practiceVolumeDirty=true;
    }
    void FlushPracticeVolume(){if(!practiceVolumeDirty)return;PlayerPrefs.Save();practiceVolumeDirty=false;}
    void ClosePracticeVolume(){FlushPracticeVolume();practiceVolumeOpen=false;}
}
}
