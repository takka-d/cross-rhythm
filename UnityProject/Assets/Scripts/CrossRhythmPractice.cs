using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    double practiceSpeed=1;
    void ChangePractice(double speed,bool nextPro){
        speed=Math.Round(Math.Max(.25,Math.Min(2,speed)),2);
        double beat=Audio.Beat;bool running=Audio.Running;
        Audio.Pause();pro=nextPro;practiceSpeed=speed;Audio.Rate=speed;
        Seek(beat);if(running)Audio.Play(beat);
        PlayerPrefs.SetInt("pro",pro?1:0);PlayerPrefs.Save();
    }
    void PracticeControls(){
        float y=H-114;RectFill(new Rect(12,y,W-24,46),panel);
        if(Button(new Rect(28,y+6,104,32),"Normal",!pro,true,14))ChangePractice(practiceSpeed,false);
        if(Button(new Rect(140,y+6,80,32),"Pro",pro,true,14))ChangePractice(practiceSpeed,true);
        Text(new Rect(242,y+11,132,26),$"Speed ×{practiceSpeed:0.00}",15,mint);
        if(Button(new Rect(390,y+6,76,32),"−0.05",false,practiceSpeed>.25,14))ChangePractice(practiceSpeed-.05,pro);
        if(Button(new Rect(476,y+6,68,32),"×1",false,true,14))ChangePractice(1,pro);
        if(Button(new Rect(554,y+6,76,32),"+0.05",false,practiceSpeed<2,14))ChangePractice(practiceSpeed+.05,pro);
        Text(new Rect(650,y+13,W-672,24),T("原曲の音程も速度に応じて変化します","Music pitch changes with speed"),12,muted);
    }
}
}
