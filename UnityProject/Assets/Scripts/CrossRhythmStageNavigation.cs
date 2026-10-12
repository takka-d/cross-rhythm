using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool stageExitPrompt,stageWasRunning;
    int stageExitChoice;
    void RequestStageExit(){
        if(InputBlocked||stageExitPrompt||(Current!=Page.Play&&Current!=Page.Practice))return;
        stageWasRunning=Audio.Running;Audio.Pause();SetPedal(false,Audio.Beat,false);ReleaseInputs();
        stageExitPrompt=true;stageExitChoice=0;ResetMenuFocus();GUI.FocusControl(null);
    }
    void ResumeStage(){
        if(!stageExitPrompt)return;
        stageExitPrompt=false;ResetMenuFocus();
        if(stageWasRunning){ResetScheduled();lastClick=Math.Floor(Audio.Beat)-1;Audio.Play(Audio.Beat);}
    }
    void QuitStage(){stageExitPrompt=false;Navigate(Page.Songs);}
    void PracticeFromStage(){
        if(!stageExitPrompt||Current!=Page.Play)return;
        double beat=Audio.Beat;stageExitPrompt=false;
        Transition(()=>{Begin(true);Seek(beat);},"Practice");
    }
    void StageExitKeys(){
        var e=Event.current;
        if(!stageExitPrompt){
            if((Current==Page.Play||Current==Page.Practice)&&e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){RequestStageExit();e.Use();}
            return;
        }
        if(e.type==EventType.Repaint&&padMenu.Count>0)e=new Event{type=EventType.KeyDown,keyCode=padMenu.Dequeue()};
        if(e.type!=EventType.KeyDown)return;
        int count=Current==Page.Play?3:2;
        switch(e.keyCode){
            case KeyCode.Escape:ResumeStage();break;
            case KeyCode.Tab:case KeyCode.RightArrow:case KeyCode.DownArrow:stageExitChoice=(stageExitChoice+(e.shift?count-1:1))%count;break;
            case KeyCode.LeftArrow:case KeyCode.UpArrow:stageExitChoice=(stageExitChoice+count-1)%count;break;
            case KeyCode.Return:case KeyCode.KeypadEnter:
                if(stageExitChoice==0)ResumeStage();else if(stageExitChoice==1&&Current==Page.Play)PracticeFromStage();else QuitStage();break;
        }
        e.Use();
    }
    void StageExitDialog(){
        if(!stageExitPrompt)return;
        RectFill(new Rect(0,0,W,H),new Color(0,0,0,.72f));var r=new Rect(W/2-330,H/2-125,660,250);RoundFill(r,panel,10);RoundBorder(r,mint,1,10);
        Text(new Rect(r.x+26,r.y+24,608,38),T("演奏を中断しますか？","Quit this performance?"),27,Color.white,true);
        Text(new Rect(r.x+26,r.y+81,608,30),Current==Page.Play?T("再開するか、同じ位置からPracticeへ移れます。","Resume, or practice from this position." ):T("Resumeで、今の位置から練習を続けます。","Resume continues practice from this position."),16,muted);
        bool play=Current==Page.Play;float width=play?194:296;
        string[] labels=play?new[]{"Resume","Practice","Quit"}:new[]{"Resume","Quit"};
        for(int i=0;i<labels.Length;i++){
            var buttonRect=new Rect(r.x+26+i*(width+13),r.y+170,width,48);
            if(i==stageExitChoice)RoundBorder(new Rect(buttonRect.x-3,buttonRect.y-3,buttonRect.width+6,buttonRect.height+6),Color.white,1,7);
            if(Button(buttonRect,labels[i],i==0,true,18,"stage-exit:"+i)){
                if(i==0)ResumeStage();else if(i==1&&play)PracticeFromStage();else QuitStage();
            }
        }
        var e=Event.current;if(e.isKey||e.isMouse||e.type==EventType.ScrollWheel)e.Use();
    }
    void RequestPracticeEdit(){
        if(Current!=Page.Practice||InputBlocked||stageExitPrompt)return;
        var playing=Project;double beat=Math.Max(0,Audio.Beat);
        Transition(()=>{OpenEditorProject(CopyForEditor(playing));EditorSeek(Math.Min(beat,Project.Length));},"Edit");
    }
}
}
