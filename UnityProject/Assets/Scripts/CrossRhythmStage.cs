using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    readonly Dictionary<string,Texture2D> glyphTextures=new Dictionary<string,Texture2D>();
    readonly Dictionary<int,double[]> stageGrids=new Dictionary<int,double[]>();
    ChartProject gridProject;int gridRevision=-1;
    ChartProject liveScoreProject;HitRecord liveScoreLastRecord;int liveScoreRevision=-1,liveScoreRecordCount=-1,liveScoreJudgedCount=-1;ScoreResult liveScore;
    ScoreResult LiveStageScore(){var last=Records.Count==0?null:Records[Records.Count-1];if(!ReferenceEquals(liveScoreProject,Project)||liveScoreRevision!=Project.Revision||liveScoreRecordCount!=Records.Count||!ReferenceEquals(liveScoreLastRecord,last)||liveScoreJudgedCount!=judged.Count){liveScore=RhythmScore.Calculate(Project.Notes,Records);liveScoreProject=Project;liveScoreRevision=Project.Revision;liveScoreRecordCount=Records.Count;liveScoreLastRecord=last;liveScoreJudgedCount=judged.Count;}return liveScore;}
    struct DrawPedalRange {public double Start,End;public DrawPedalRange(double start,double end){Start=start;End=end;}}
    static readonly ChartNote PedalHeadNote=new ChartNote{Instrument="HHSTATE",Velocity=4};
    List<DrawPedalRange> drawPedalRanges=new List<DrawPedalRange>(8),drawPedalScratch=new List<DrawPedalRange>(8);
    void SubtractDrawPedalRange(double start,double end){drawPedalScratch.Clear();foreach(var r in drawPedalRanges){if(end<=r.Start||start>=r.End){drawPedalScratch.Add(r);continue;}if(start>r.Start)drawPedalScratch.Add(new DrawPedalRange(r.Start,start));if(end<r.End)drawPedalScratch.Add(new DrawPedalRange(end,r.End));}var swap=drawPedalRanges;drawPedalRanges=drawPedalScratch;drawPedalScratch=swap;}
    static Color C(string hex)=>ChartVisuals.Hex(hex);
    Color InstrumentColor(string inst)=>ChartVisuals.PlayColor(inst);
    void RoundFill(Rect r,Color c,float radius=6){GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,radius);}
    void RoundBorder(Rect r,Color c,float width=1,float radius=6){GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,width,radius);}
    void Glyph(ChartNote n,float x,float y,float alpha=1,bool outline=false,float sizeScale=1,bool missed=false){
        if(n.Instrument=="RD"){var tint=GUI.color;GUI.color=new Color(1,1,1,alpha);GUI.DrawTexture(new Rect(x-40*sizeScale,y-40*sizeScale,80*sizeScale,80*sizeScale),CymbalVisuals.Ride(n.Velocity,outline,missed));GUI.color=tint;return;}
        if((n.Instrument=="HH"&&n.Articulation!="tambourine")||DrumNoteVisuals.IsTom(n.Instrument)){
            var t=n.Instrument=="HH"?DrumNoteVisuals.HiHat(n.Velocity,Project.ClosedAt(n.Beat),outline,missed):DrumNoteVisuals.Tom(n.Instrument,n.Velocity,outline,missed);
            var tint=GUI.color;GUI.color=new Color(1,1,1,alpha);GUI.DrawTexture(new Rect(x-40*sizeScale,y-40*sizeScale,80*sizeScale,80*sizeScale),t);GUI.color=tint;return;
        }
        string name=n.Instrument+"-"+(outline?"outline":missed?"miss":"normal");
        if(!glyphTextures.TryGetValue(name,out var texture)){texture=Resources.Load<Texture2D>("NoteGlyphs/"+name);glyphTextures[name]=texture;}
        if(texture==null)return;
        float sy=n.Pedal?1:ChartProject.Heights[n.Velocity]/.5f;var old=GUI.color;GUI.color=new Color(1,1,1,alpha);
        GUI.DrawTexture(new Rect(x-40*sizeScale,y-40*sizeScale*sy,80*sizeScale,80*sizeScale*sy),texture);GUI.color=old;
    }
    bool feetAbove;
    void LaneOrderControls(Rect r,int size=16){
        float width=(r.width-10)/2;
        if(Button(new Rect(r.x,r.y,width,r.height),T("上段: 手","Top: Hands"),!feetAbove,true,size,kind:ControlKind.Choice))SetFeetAbove(false);
        if(Button(new Rect(r.x+width+10,r.y,width,r.height),T("上段: 足","Top: Feet"),feetAbove,true,size,kind:ControlKind.Choice))SetFeetAbove(true);
    }
    void SetFeetAbove(bool value){feetAbove=value;stageEffects.Clear();PlayerPrefs.SetInt("feetAbove",value?1:0);PlayerPrefs.Save();}
    float LaneY(string lane,float top,float rh){float row=lane=="H1"?.5f:lane=="H2"?1.5f:lane=="HANY"?1:lane=="F1"?2.5f:lane=="F2"?3.5f:3;return top+(feetAbove?(row<2?row+2:row-2):row)*rh;}
    bool NoteVisible(ChartNote n)=>!judged.TryGetValue(n.Index,out var hit)||hit.Judge=="MISS";
    float BarAlpha(float y,float rh,float baseY,float z){float distance=Math.Abs(y+rh*2-baseY)/z;return distance<125?1:distance<360?.46f:.16f;}
    double[] StageGrid(int bar){if(!stageGrids.TryGetValue(bar,out var points)){points=ChartVisuals.GridPoints(Project,bar);stageGrids[bar]=points;}return points;}
    void Stage(){
        bool practice=Current==Page.Practice;double b=Audio.Beat;
        RoundFill(new Rect(8,8,W-16,58),panel);RoundBorder(new Rect(8,8,W-16,58),line);
        Text(new Rect(22,18,W*.32f,36),Project.Title,19,Color.white,true);
        Text(new Rect(W*.35f,24,175,28),$"{Project.Tempo.BPMAt(Audio.Beat):0.##} BPM · "+(pro?"PRO":"NORMAL"),13,muted);
        if(!practice){var live=LiveStageScore();Text(new Rect(W*.51f,16,210,40),$"Score {live.Score:0.0}",24,mint,true);if(Button(new Rect(W-178,16,100,42),"Stop",kind:ControlKind.Dialog))RequestStageExit();}
        else{if(Button(new Rect(W-634,16,96,42),"Start",false,loaded&&!busy))PlayFromPractice();if(Button(new Rect(W-526,16,88,42),"Edit",false,loaded&&!busy))RequestPracticeEdit();if(Button(new Rect(W-426,16,106,42),Audio.Running?"Pause":"Play",true,loaded&&!busy)){if(Audio.Running)Audio.Pause();else {ResetScheduled();lastClick=Math.Floor(Audio.Beat)-1;Audio.Play(Audio.Beat);}}if(Button(new Rect(W-308,16,106,42),"Auto",auto,kind:ControlKind.Toggle)){auto=!auto;ResetScheduled();}if(Button(new Rect(W-190,16,102,42),"Songs"))RequestStageExit();}
        int revision=Project.Revision;
        if(gridProject!=Project||gridRevision!=revision){stageGrids.Clear();gridProject=Project;gridRevision=revision;}
        Rect area=new Rect(8,74,W-16-(practice?27:0),H-74-(practice?72:8));RoundFill(area,C("#090d13"));RoundBorder(area,line);
        if(Event.current.type==EventType.Repaint)ReferencePlayfield(area,b);
        if(pendingPerformance!=null||!string.IsNullOrEmpty(stageLoadError)){
            var wait=new Rect(W/2-260,H/2-70,520,140);RectFill(wait,panel);Border(wait,mint);
            Text(new Rect(wait.x+22,wait.y+18,476,36),pendingPerformance!=null?T("音源を準備中…","Preparing audio…"):T("音源を読み込めませんでした","Audio could not load"),22,mint);
            if(pendingPerformance!=null)Text(new Rect(wait.x+22,wait.y+66,476,28),practice?T("準備後、Playで開始します。Songsで戻れます。","Press Play when ready. Songs returns to the list."):T("準備が終わるとカウントを開始します。Stopで中断できます。","Count-in starts when ready. Stop opens exit options."),14,muted);
            else {FittedText(new Rect(wait.x+22,wait.y+57,476,25),stageLoadError,14,muted);if(Button(new Rect(wait.x+180,wait.y+91,160,34),"Retry",true))Begin(practice);}
        }
        if(practice){PracticeControls();PracticeSeekBar(b,area);if(GUI.enabled&&Event.current.type==EventType.ScrollWheel){Seek(b+Event.current.delta.y*4);Event.current.Use();}if(GUI.enabled&&Event.current.type==EventType.Repaint&&ControllerScrollActive(area))Seek(b+controllerWheel.y*8*Time.unscaledDeltaTime);}
    }
    void DrawPedalRanges(int bar,double start,double length,double b,float y,float rh,float left,float width,float z,float alpha){
        int pedalAt=Project.PedalLowerBound(start),pedalEnd=Project.Pedals.Count;
        for(;pedalAt<pedalEnd&&Project.Pedals[pedalAt].Start<start+length;pedalAt++){var p=Project.Pedals[pedalAt];
            double begin=Math.Max(p.Start,Audio.AnchorBeat),end=p.End;if(end<=begin)continue;
            drawPedalRanges.Clear();drawPedalRanges.Add(new DrawPedalRange(begin,end));
            if(auto)SubtractDrawPedalRange(begin,b);else {foreach(var hold in heldPedal)SubtractDrawPedalRange(hold.Start,hold.End);if(pedalStart.HasValue)SubtractDrawPedalRange(pedalStart.Value,b);}
            bool kick=p.HasKick;float yy=LaneY(kick?"F2":"FANY",y,rh);
            foreach(var r in drawPedalRanges){double s=Math.Max(start,r.Start),e=Math.Min(start+length,r.End);if(e<=s)continue;bool head=Math.Abs(s-p.Start)<1e-8;float hx=ChartVisuals.CellX(Project,bar,Math.Max(0,s-start),left,width),x=Math.Max(left+4*z,head?hx-13*z:left+(float)((s-start)/length)*width),x2=Math.Max(x+8*z,left+(float)((e-start)/length)*width);ReferencePedalBar(x/z,yy/z,(x2-x)/z,z,alpha);if(head)ReferenceGlyph(PedalHeadNote,hx/z,yy/z,z,.98f*alpha);}
        }
    }
}
}
