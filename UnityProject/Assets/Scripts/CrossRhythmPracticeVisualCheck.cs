using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator PracticeVisualRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();p.SetSongInfo("Practice Visual QA","Synthetic fixture");
        p.Chart["measures"]=new JArray(4,3,4,4,4,4);
        p.Chart["timeSignatures"]=new JArray(new JObject{{"numerator",4},{"denominator",4}},new JObject{{"numerator",6},{"denominator",8}},new JObject{{"numerator",4},{"denominator",4}},new JObject{{"numerator",4},{"denominator",4}},new JObject{{"numerator",4},{"denominator",4}},new JObject{{"numerator",4},{"denominator",4}});
        p.Chart["events"]=new JArray();
        void Note(string id,int bar,double beat,string instrument,int velocity=4,double step=.25){p.Events.Add(new JObject{{"id",id},{"measure",bar},{"beat",beat},{"instrument",instrument},{"velocity",velocity},{"gridStepBeats",step}});}
        p.Events.Add(new JObject{{"id","pedal"},{"measure",0},{"beat",0},{"instrument","HHSTATE"},{"durationBeats",1}});
        Note("closed",0,.5,"HH");Note("open",0,1.5,"HH");Note("high",0,2,"HT");Note("mid",0,2.5,"MT");Note("floor",0,3,"FT");
        for(int i=0;i<3;i++)Note("triplet"+i,1,i/3.0,"HH",4,1.0/3);
        for(int i=0;i<5;i++)Note("fifth"+i,2,i/5.0,"SN",3,1.0/5);
        for(int i=0;i<7;i++)Note("seventh"+i,3,i/7.0,"HH",3,1.0/7);
        for(int i=0;i<6;i++){Note("closed-v"+i,4,i*.5,"HH",i);Note("open-v"+i,5,i*.5,"HH",i);}
        p.Events.Add(new JObject{{"id","pedal-strength"},{"measure",4},{"beat",0},{"instrument","HHSTATE"},{"durationBeats",3.25}});p.Rebuild();
        string chart=p.Chart.ToString();var timing=p.Notes.Select(n=>(n.Id,n.Beat,n.Step,n.Duration)).ToArray();
        var triplets=ChartVisuals.GridPoints(p,1);check(triplets.Any(x=>Math.Abs(x-1.0/3)<1e-8)&&triplets.Any(x=>Math.Abs(x-2.0/3)<1e-8),"triplet guides cover the occupied group");
        check(!triplets.Any(x=>Math.Abs(x-4.0/3)<1e-8)&&triplets.Contains(.25),"triplet guides stay local and preserve sixteenth lines");
        foreach(int bar in new[]{2,3})check(ChartVisuals.GridPoints(p,bar).All(x=>Math.Abs(x*4-Math.Round(x*4))<1e-8),"quintuplet or septuplet keeps ordinary sixteenth guide lines: bar "+bar);
        check(triplets.Where(x=>x<3&&ChartVisuals.IsBeatLine(p,1,x)).SequenceEqual(new[]{0,.5,1,1.5,2,2.5}),"6/8 retains all six eighth-note beat lines with triplet guides");
        check(ChartVisuals.PlayColor("HT")!=ChartVisuals.PlayColor("MT")&&ChartVisuals.PlayColor("MT")!=ChartVisuals.PlayColor("FT"),"three tom pitches use distinct colours");
        int Height(Texture2D texture){var pixels=texture.GetPixels32();int lo=texture.height,hi=-1;for(int y=0;y<texture.height;y++)for(int x=0;x<texture.width;x++)if(pixels[y*texture.width+x].a>127){lo=Math.Min(lo,y);hi=Math.Max(hi,y);}return hi-lo+1;}
        foreach(bool closedHat in new[]{false,true}){int previous=0;for(int v=0;v<=5;v++){
            var normal=DrumNoteVisuals.HiHat(v,closedHat);int height=Height(normal);check(height>previous,"hi-hat strength changes silhouette height: "+closedHat+" V"+v);previous=height;
            var outline=DrumNoteVisuals.HiHat(v,closedHat,true);var miss=DrumNoteVisuals.HiHat(v,closedHat,false,true);
            check(normal!=outline&&normal!=miss&&normal==DrumNoteVisuals.HiHat(v,closedHat),"hi-hat states are distinct cached textures: "+closedHat+" V"+v);
        }}
        var open=DrumNoteVisuals.HiHat(4,false).GetPixels32();var closedPixels=DrumNoteVisuals.HiHat(4,true).GetPixels32();
        check(open[120*240+120].a==0&&closedPixels[120*240+120].a>127,"open hat has a central gap while closed hat touches");
        foreach(int v in Enumerable.Range(0,6))foreach(string state in new[]{"normal","outline","miss"}){
            var original=new Texture2D(2,2);original.LoadImage(Resources.Load<TextAsset>("Stage175/HH-"+state+"-"+v).bytes,false);
            check(original.GetPixels32().SequenceEqual(DrumNoteVisuals.HiHat(v,true,state=="outline",state=="miss").GetPixels32()),"closed hi-hat retains every v175 pixel: "+state+" "+v);
            UnityEngine.Object.Destroy(original);
        }
        foreach(string state in new[]{"normal","outline","miss"}){var ht=DrumNoteVisuals.Tom("HT",4,state=="outline",state=="miss");var mt=DrumNoteVisuals.Tom("MT",4,state=="outline",state=="miss");var ft=DrumNoteVisuals.Tom("FT",4,state=="outline",state=="miss");
            check(ht!=null&&mt!=null&&ft!=null,"all three tom textures render in "+state);
            if(state!="miss")check(!ht.GetPixels32().SequenceEqual(mt.GetPixels32())&&!mt.GetPixels32().SequenceEqual(ft.GetPixels32()),"tom colours distinguish pitch in "+state);
        }
        check(p.ClosedAt(.5)&&!p.ClosedAt(1.5)&&RhythmAudio.KeyFor(p.Notes.Single(n=>n.Id=="closed"),p.ClosedAt(.5))=="HH"&&RhythmAudio.KeyFor(p.Notes.Single(n=>n.Id=="open"),p.ClosedAt(1.5))=="OHH","hi-hat drawing uses the same pedal state as scheduled audio");
        check(chart==p.Chart.ToString()&&timing.SequenceEqual(p.Notes.Select(n=>(n.Id,n.Beat,n.Step,n.Duration))),"new stage visuals never mutate tuplet timing, strength or chart");
        p.SaveNative(Path.Combine(workspaceCheckRoot,"practice-visual.crproj"),false);Library.Add(p);NavigateNow(Page.Songs);FocusSong(Library.Count-1);Begin(true);
        double deadline=Time.realtimeSinceStartupAsDouble+30;while((!loaded||busy||pendingPerformance!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        check(Current==Page.Practice&&loaded&&!Audio.Running&&!Audio.BackingIsPlaying,"Practice remains stopped after asynchronous preparation");
        double stopped=Audio.Beat;yield return new WaitForSeconds(.3f);check(Audio.Beat==stopped,"entering Practice does not advance chart or count-in");
        Begin(true);check(!Audio.Running&&Audio.Beat==CountIn.Start(p),"already prepared Practice also starts stopped");
        Seek(4.5);check(!Audio.Running&&Audio.Beat==4.5&&PracticePosition(4.5)=="M2\n2 / 6","paused seek reports bar and denominator-based beat in 6/8");
        Audio.Play(Audio.Beat);yield return new WaitForSeconds(.2f);check(Audio.Running&&Audio.Beat>4.5,"explicit Play starts at selected Practice position");
        Audio.Pause();Seek(p.Length);check(PracticePosition(Audio.Beat)=="M6\n4 / 4","song-end position stays within the last meter beat");Seek(0);bool oldFeet=feetAbove;feetAbove=false;
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(workspaceCheckRoot,"practice-visual.png"));feetAbove=oldFeet;
        Begin(false);check(Audio.Running&&Audio.AnchorBeat==CountIn.Start(p),"normal Play still starts its count-in automatically");Audio.Stop();
        p.Dirty=false;NavigateNow(Page.Songs);yield return WaitForOperations();
    }
}
}
