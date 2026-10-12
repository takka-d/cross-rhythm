using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator PedalCymbalRuntimeCheck(Action<bool,string> check){
        PedalImportChecks.Run(check);
        var p=EmptyEditorProject();OpenEditorProject(p);yield return WaitForOperations();
        string before=p.Chart.ToString();ImportMidi(PedalImportChecks.Fixture(),"Pedal QA.mid");
        check(p.Notes.Count(n=>n.Pedal&&n.Beat<2)==2&&p.Notes.First().Duration==.125,"Edit import applies short pedal strikes");
        Restore(false);check(p.Chart.ToString()==before,"MIDI pedal import Undo restores previous chart");Restore(true);
        check(p.Notes.First().Duration==.125,"MIDI pedal import Redo restores exact duration");
        p.SaveNative(Path.Combine(workspaceCheckRoot,"pedal-import.crproj"),false);File.WriteAllBytes(Path.Combine(workspaceCheckRoot,"pedal-import.mid"),PedalImportChecks.Fixture());
        bool oldMetro=metronome;metronome=false;
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var voices=(List<AudioSource>)typeof(RhythmAudio).GetField("voices",flags).GetValue(Audio);var field=typeof(RhythmAudio).GetField("voice",flags);
        p.Chart["events"]=new JArray(new JObject{{"measure",0},{"beat",0},{"instrument","HHSTATE"},{"durationBeats",.125},{"velocity",4}},new JObject{{"measure",0},{"beat",.125},{"instrument","HHSTATE"},{"durationBeats",.125},{"velocity",2}},new JObject{{"measure",0},{"beat",.125},{"instrument","HHSTATE"},{"durationBeats",.125},{"velocity",2}});p.Rebuild();
        Audio.Stop();ResetScheduled();int start=(int)field.GetValue(Audio);Audio.Play(0);yield return new WaitForSeconds(.2f);
        int end=(int)field.GetValue(Audio);check(end-start==2,"adjacent pedal strikes sound once each; same-onset duplicate does not double");
        check(voices[start%voices.Count].clip.name.StartsWith("HH_PEDAL")&&voices[(start+1)%voices.Count].clip.name.StartsWith("HH_PEDAL"),"Edit scheduled pedal strikes use pedal hi-hat samples");
        check(Math.Abs(voices[(start+1)%voices.Count].volume/voices[start%voices.Count].volume-.5)<.001,"scheduled pedal preview retains individual velocity");
        double deadline=Time.realtimeSinceStartupAsDouble+3;while(Audio.Beat<.3&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;yield return null;
        check(!closed&&!p.ClosedAt(Audio.Beat),"preview releases closed state after a short pedal strike");Audio.Stop();
        start=(int)field.GetValue(Audio);closed=false;SetPedal(true,0);check((int)field.GetValue(Audio)==start+1,"manual pedal press retains its immediate sound");SetPedal(false,.1);Audio.Stop();metronome=oldMetro;
        p=EmptyEditorProject();p.SetSongInfo("Cymbal and Pedal QA","Synthetic fixture");p.Chart["measures"]=new JArray(8,8,8);p.Rebuild();
        for(int bar=0;bar<3;bar++)for(int v=0;v<6;v++)foreach(string inst in new[]{"CR","RD"})p.Events.Add(new JObject{{"id",inst+bar+v},{"measure",bar},{"beat",v+1},{"instrument",inst},{"articulation",inst=="CR"?"crash":v==2?"cup":v==4?"crash":"ride"},{"velocity",v}});
        p.Rebuild();string chart=p.Chart.ToString();float maxHeight=0;
        for(int v=0;v<6;v++){
            var t=CymbalVisuals.Ride(v);var colors=t.GetPixels32();var occupied=Enumerable.Range(0,colors.Length).Where(i=>colors[i].a>100).ToArray();int height=occupied.Max(i=>i/t.width)-occupied.Min(i=>i/t.width);
            check(occupied.Length>0&&height>maxHeight,"ride velocity keeps increasing silhouette height "+v);maxHeight=height;
            check(colors.Any(c=>c.a>200&&c.r>c.b*1.3&&c.g>c.b*1.2)&&!colors.Take(t.width).Any(c=>c.a>0),"ride uses the cymbal gold palette and unclipped bounds "+v);
            var outline=CymbalVisuals.Ride(v,true);var miss=CymbalVisuals.Ride(v,false,true);check(outline!=t&&miss!=t&&CymbalVisuals.Ride(v)==t,"normal outline miss use cached distinct ride textures "+v);
            File.WriteAllBytes(Path.Combine(workspaceCheckRoot,"ride-normal-"+v+".png"),t.EncodeToPNG());
        }
        check(chart==p.Chart.ToString()&&p.Notes.Count(n=>n.Instrument=="RD")==18,"cymbal rendering preserves chart data and note count");
        check(RhythmAudio.KeyFor(new ChartNote{Instrument="RD",Articulation="crash"},false)=="RIDE_CRASH"&&RhythmAudio.KeyFor(new ChartNote{Instrument="RD",Articulation="cup"},false)=="CUP","ride visual changes retain ride-crash and bell sound mapping");
        p.SaveNative(Path.Combine(workspaceCheckRoot,"cymbal-visual.crproj"),false);OpenEditorProject(p);yield return WaitForOperations();Begin(true);yield return WaitForOperations();Seek(0);Audio.Pause();
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(workspaceCheckRoot,"cymbal-practice.png"));yield return new WaitForSeconds(.2f);
        p.Dirty=false;NavigateNow(Page.Songs);yield return WaitForOperations();
    }
}
}
