using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    double practiceSpeed=1;
    double? pendingPracticeSpeed;
    void UpdatePracticeSpeed(){
        if(Current!=Page.Practice){pendingPracticeSpeed=null;return;}
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
    void PracticeControls(){
        float y=H-114;RectFill(new Rect(12,y,W-24,46),panel);
        if(Button(new Rect(28,y+6,104,32),"Normal",!pro,true,14))ChangePractice(practiceSpeed,false);
        if(Button(new Rect(140,y+6,80,32),"Pro",pro,true,14))ChangePractice(practiceSpeed,true);
        Text(new Rect(242,y+11,132,26),$"Speed ×{(pendingPracticeSpeed??practiceSpeed):0.00}",15,mint);
        if(Button(new Rect(390,y+6,68,32),"×1",false,true,14)){pendingPracticeSpeed=null;ChangePractice(1,pro);}
        GUI.SetNextControlName("practice-speed");
        Rect slider=new Rect(480,y+17,430,20);
        float speed=GUI.HorizontalSlider(slider,(float)(pendingPracticeSpeed??practiceSpeed),.25f,2f);
        if(Math.Abs(speed-(pendingPracticeSpeed??practiceSpeed))>.0001)pendingPracticeSpeed=Math.Round(speed,2);
        float thumb=GUI.skin.horizontalSliderThumb.fixedWidth;
        float normal=slider.x+thumb/2+(slider.width-thumb)*(1f-.25f)/(2f-.25f);
        RectFill(new Rect(normal,y+8,1,7),mint);RectFill(new Rect(normal,y+28,1,7),mint);
        if(Audio.Preparing||!string.IsNullOrEmpty(Audio.PlaybackError))Text(new Rect(940,y+13,W-962,24),Audio.Preparing?T("音源を準備中…","Preparing audio…"):T("音源の準備に失敗しました。再生し直してください","Audio preparation failed. Press Play to retry."),12,muted);
    }
}
}
