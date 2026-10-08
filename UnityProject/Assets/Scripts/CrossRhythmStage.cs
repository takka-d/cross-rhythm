using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    readonly Dictionary<string,Texture2D> glyphTextures=new Dictionary<string,Texture2D>();
    readonly Dictionary<int,double[]> stageGrids=new Dictionary<int,double[]>();
    ChartProject gridProject;string gridRevision="";
    static Color C(string hex)=>ChartVisuals.Hex(hex);
    Color InstrumentColor(string inst)=>ChartVisuals.PlayColor(inst);
    void RoundFill(Rect r,Color c,float radius=6){GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,0,radius);}
    void RoundBorder(Rect r,Color c,float width=1,float radius=6){GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,c,width,radius);}
    void Glyph(ChartNote n,float x,float y,float alpha=1,bool outline=false,float sizeScale=1,bool missed=false){
        string name=n.Instrument+"-"+(outline?"outline":missed?"miss":"normal");
        if(!glyphTextures.TryGetValue(name,out var texture)){texture=Resources.Load<Texture2D>("NoteGlyphs/"+name);glyphTextures[name]=texture;}
        if(texture==null)return;
        float sy=n.Pedal?1:ChartProject.Heights[n.Velocity]/.5f;var old=GUI.color;GUI.color=new Color(1,1,1,alpha);
        GUI.DrawTexture(new Rect(x-40*sizeScale,y-40*sizeScale*sy,80*sizeScale,80*sizeScale*sy),texture);GUI.color=old;
    }
    float LaneY(string lane,float top,float rh)=>top+(lane=="H1"?.5f:lane=="H2"?1.5f:lane=="HANY"?1:lane=="F1"?2.5f:lane=="F2"?3.5f:3)*rh;
    bool NoteVisible(ChartNote n)=>!judged.TryGetValue(n.Index,out var hit)||hit.Judge=="MISS";
    float BarAlpha(float y,float rh,float baseY,float z){float distance=Math.Abs(y+rh*2-baseY)/z;return distance<125?1:distance<360?.46f:.16f;}
    double[] StageGrid(int bar){if(!stageGrids.TryGetValue(bar,out var points)){points=ChartVisuals.GridPoints(Project,bar);stageGrids[bar]=points;}return points;}
    void Stage(){
        bool practice=Current==Page.Practice;double b=Audio.Beat;
        RoundFill(new Rect(8,8,W-16,58),panel);RoundBorder(new Rect(8,8,W-16,58),line);
        Text(new Rect(22,18,W*.32f,36),Project.Title,19,Color.white,true);
        Text(new Rect(W*.35f,24,175,28),$"{Project.Tempo.BPMAt(Audio.Beat):0.##} BPM · "+(pro?"PRO":"NORMAL"),13,muted);
        if(!practice){var live=RhythmScore.Calculate(Project.Notes,Records);Text(new Rect(W*.51f,16,210,40),$"Score {live.Score:0.0}",24,mint,true);if(Button(new Rect(W-178,16,100,42),"Back"))NavigateNow(Page.Songs);}
        else{if(Button(new Rect(W-438,16,118,42),Audio.Running?"Pause":"Play",true,loaded&&!busy)){if(Audio.Running)Audio.Pause();else {ResetScheduled();lastClick=Math.Floor(Audio.Beat)-1;Audio.Play(Audio.Beat);}}if(Button(new Rect(W-308,16,118,42),"Auto "+(auto?"ON":"OFF"))){auto=!auto;ResetScheduled();}if(Button(new Rect(W-178,16,100,42),"Back"))NavigateNow(Page.Songs);}
        string revision=Project.Events.Count+":"+Project.Measures.Length+":"+Project.Grid;
        if(gridProject!=Project||gridRevision!=revision){stageGrids.Clear();gridProject=Project;gridRevision=revision;}
        Rect area=new Rect(8,74,W-16,H-74-(practice?124:8));RoundFill(area,C("#090d13"));RoundBorder(area,line);
        ReferencePlayfield(area,b);
        if(pendingPerformance!=null||!string.IsNullOrEmpty(stageLoadError)){
            var wait=new Rect(W/2-260,H/2-70,520,140);RectFill(wait,panel);Border(wait,mint);
            Text(new Rect(wait.x+22,wait.y+18,476,36),pendingPerformance!=null?T("音源を準備中…","Preparing audio…"):T("音源を読み込めませんでした","Audio could not load"),22,mint);
            if(pendingPerformance!=null)Text(new Rect(wait.x+22,wait.y+66,476,28),T("準備が終わるとカウントを開始します。Backで戻れます。","Count-in starts when ready. Back returns to Songs."),14,muted);
            else {FittedText(new Rect(wait.x+22,wait.y+57,476,25),stageLoadError,14,muted);if(Button(new Rect(wait.x+180,wait.y+91,160,34),"Retry",true))Begin(practice);}
        }
        if(practice){PracticeControls();RectFill(new Rect(12,H-67,W-24,53),panel);Text(new Rect(28,H-56,180,28),$"{Project.SecondsAtBeat(Math.Max(0,b)):0.0} / {Project.DurationSeconds:0.0}s",16,muted);float next=GUI.HorizontalSlider(new Rect(230,H-48,W-550,20),(float)b,(float)CountIn.Start(Project),(float)Project.Length);if(Math.Abs(next-b)>.01)Seek(next);Text(new Rect(W-295,H-56,270,30),"Wheel: ±4 beats · Space",14,muted);if(Event.current.type==EventType.ScrollWheel){Seek(b+Event.current.delta.y*4);Event.current.Use();}}
    }
    void DrawPedalRanges(int bar,double start,double length,double b,float y,float rh,float left,float width,float z,float alpha){
        foreach(var p in Project.Pedals){
            double begin=Math.Max(p.Start,Audio.AnchorBeat),end=p.End;if(end<=begin)continue;
            var remaining=new List<PedalRange>{new PedalRange{Start=begin,End=end}};
            var consumed=auto?new[]{new PedalRange{Start=begin,End=b}}:heldPedal.Concat(pedalStart.HasValue?new[]{new PedalRange{Start=pedalStart.Value,End=b}}:Array.Empty<PedalRange>());
            foreach(var hold in consumed){var next=new List<PedalRange>();foreach(var r in remaining){if(hold.End<=r.Start||hold.Start>=r.End){next.Add(r);continue;}if(hold.Start>r.Start)next.Add(new PedalRange{Start=r.Start,End=hold.Start});if(hold.End<r.End)next.Add(new PedalRange{Start=hold.End,End=r.End});}remaining=next;}
            bool kick=Project.Notes.Any(n=>n.Instrument=="BD"&&n.Beat>=p.Start&&n.Beat<p.End);float yy=LaneY(kick?"F2":"FANY",y,rh);
            foreach(var r in remaining){double s=Math.Max(start,r.Start),e=Math.Min(start+length,r.End);if(e<=s)continue;bool head=Math.Abs(s-p.Start)<1e-8;float hx=ChartVisuals.CellX(Project,bar,Math.Max(0,s-start),left,width),x=Math.Max(left+4*z,head?hx-13*z:left+(float)((s-start)/length)*width),x2=Math.Max(x+8*z,left+(float)((e-start)/length)*width);ReferencePedalBar(x/z,yy/z,(x2-x)/z,z,alpha);if(head)ReferenceGlyph(new ChartNote{Instrument="HHSTATE",Velocity=4},hx/z,yy/z,z,.98f*alpha);}
        }
    }
}
}
