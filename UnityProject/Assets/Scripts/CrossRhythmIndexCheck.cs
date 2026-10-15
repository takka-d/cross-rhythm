using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator IndexAndLaneRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();p.Chart["measures"]=new JArray(Enumerable.Repeat(4,512));p.Chart["events"]=new JArray();
        string[] instruments={"HH","SN","RD","BD"};
        for(int bar=0;bar<512;bar++){
            for(int i=0;i<16;i++)p.Events.Add(new JObject{{"id","perf-"+bar+"-"+i},{"measure",bar},{"beat",i/4.0},{"instrument",instruments[i%4]},{"velocity",i%6}});
            if(bar%2==0)p.Events.Add(new JObject{{"id","pedal-"+bar},{"measure",bar},{"beat",1},{"instrument","HHSTATE"},{"durationBeats",1.5}});
        }
        // A hold and buzz starting outside the viewport must still be found inside it.
        p.Events.Add(new JObject{{"id","long-pedal"},{"measure",0},{"beat",0},{"instrument","HHSTATE"},{"durationBeats",12}});
        p.Events.Add(new JObject{{"id","long-buzz"},{"measure",0},{"beat",0},{"instrument","SN"},{"articulation","buzz"},{"durationBeats",15}});
        p.Rebuild();
        bool bars=true,steps=true,ranges=true,overlaps=true,closedMatch=true,pedalMatch=true;
        for(int bar=0;bar<512;bar+=7){
            var expected=p.Notes.Where(n=>n.Measure==bar).ToArray();bars&=p.NotesInBar(bar).SequenceEqual(expected);
            steps&=p.DisplayStep(bar)==expected.Select(n=>n.Step).Append(.25).Min();
            double from=bar*4+.3,to=from+1.1;
            ranges&=p.NotesBetween(from,to).SequenceEqual(p.Notes.Where(n=>n.Beat>=from&&n.Beat<=to));
            overlaps&=p.NotesOverlapping(from,to).SequenceEqual(p.Notes.Where(n=>n.Beat<=to&&n.Beat+Math.Max(n.DisplaySpan,.25)>=from));
            pedalMatch&=p.PedalsOverlapping(from,to).SequenceEqual(p.Pedals.Where(n=>n.End>from&&n.Start<to));
        }
        foreach(var pedal in p.Pedals)foreach(double beat in new[]{pedal.Start-2e-8,pedal.Start-1e-8,pedal.Start,pedal.End-2e-8,pedal.End-1e-8,pedal.End,pedal.End+1})
            closedMatch&=p.ClosedAt(beat)==p.Pedals.Any(r=>beat>=r.Start-1e-8&&beat<r.End-1e-8);
        check(bars&&steps,"indexed bar queries and display spacing equal the original full-chart calculation");
        check(ranges&&overlaps,"indexed time and overlap queries return every matching note in original order");
        check(pedalMatch&&closedMatch,"pedal lookup preserves merged ranges and exact open/close boundary tolerance");
        check(p.Pedals.All(r=>r.HasKick==p.Notes.Any(n=>n.Instrument=="BD"&&n.Beat>=r.Start&&n.Beat<r.End)),"cached pedal kick lanes equal original assignments");
        check(p.NotesOverlapping(8,9).Any(n=>n.Id=="long-pedal")&&p.NotesOverlapping(8,9).Any(n=>n.Id=="long-buzz"),"viewport retains long pedal and buzz notes starting several bars earlier");
        check(Instruments.All(i=>p.InstrumentCount(i)==p.Notes.Count(n=>n.Instrument==i))&&p.Notes.All(n=>p.NoteAtIndex(n.Index)==n),"lane counts and selection lookup match original chart");
        int revision=p.Revision;p.Events.RemoveAt(0);p.InsertMeasure(10);p.SetGrid(12);
        check(p.Revision>revision&&p.NotesInBar(12).SequenceEqual(p.Notes.Where(n=>n.Measure==12))&&p.NoteAtIndex(p.Notes[0].Index)==p.Notes[0],"timing indices rebuild after delete, meter insertion and grid change");
        p.SaveNative(Path.Combine(workspaceCheckRoot,"large-chart.crproj"),false);
        var reopened=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName);
        check(reopened.NotesBetween(100,110).Select(n=>n.Id).SequenceEqual(p.NotesBetween(100,110).Select(n=>n.Id)),"save and reopen recreate timing indices without adding metadata to the chart");
        double Baseline(){double result=0;for(int bar=250;bar<256;bar++)foreach(var n in p.Notes.Where(n=>n.Measure==bar)){double step=p.Notes.Where(x=>x.Measure==bar).Select(x=>x.Step).Append(.25).Min();result+=220+(float)((n.Local+Math.Min(step,p.Measures[bar]-n.Local)/2)/p.Measures[bar])*970;}return result;}
        double Indexed(){double result=0;for(int bar=250;bar<256;bar++)foreach(var n in p.NotesInBar(bar))result+=ChartVisuals.CellX(p,bar,n.Local,220,970);return result;}
        double a=Baseline(),b=Indexed();check(Math.Abs(a-b)<.01,"indexed stage positions equal original full scans");
        var timer=System.Diagnostics.Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<40;i++)Baseline();timer.Stop();double oldMs=timer.Elapsed.TotalMilliseconds;long oldBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        timer.Restart();bytes=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<40;i++)Indexed();timer.Stop();double newMs=timer.Elapsed.TotalMilliseconds;long newBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
        File.WriteAllText(Path.Combine(workspaceCheckRoot,"render-query-performance.json"),new JObject{{"notes",p.Notes.Count},{"iterations",40},{"visibleBars",6},{"legacyMs",oldMs},{"indexedMs",newMs},{"allocationCounterAvailable",oldBytes>0},{"legacyAllocatedBytes",oldBytes>0?(JToken)oldBytes:JValue.CreateNull()},{"indexedAllocatedBytes",oldBytes>0?(JToken)newBytes:JValue.CreateNull()},{"sameCoordinates",true},{"scope","Stage note-position queries only; not total frame time or every freeze cause"}}.ToString());
        check(Enumerable.Range(250,6).Sum(bar=>p.NotesInBar(bar).Count())<p.Notes.Count/10,"stage-position queries visit only the six displayed bars");
        yield return null;
        var lane=ChartProject.Demo();lane.SetSongInfo("Lane Order QA","Synthetic fixture");
        for(int m=0;m<16;m++)lane.Events.Add(new JObject{{"id","lane-pedal-"+m},{"measure",m},{"beat",0},{"instrument","HHSTATE"},{"durationBeats",1.5}});
        lane.Rebuild();lane.SaveNative(Path.Combine(workspaceCheckRoot,"lane-order.crproj"),false);Library.Add(lane);NavigateNow(Page.Songs);FocusSong(Library.Count-1);yield return WaitForOperations();
        double readyDeadline=Time.realtimeSinceStartupAsDouble+30;
        while((busy||!loaded||pendingSongAudio!=null)&&Time.realtimeSinceStartupAsDouble<readyDeadline)yield return null;
        check(ReferenceEquals(Project,lane)&&loaded&&!busy,"lane-order fixture finishes preparation before performance checks");
        bool oldFeet=feetAbove,oldPro=pro;bool had=PlayerPrefs.HasKey("feetAbove");int saved=PlayerPrefs.GetInt("feetAbove",0);
        string chart=lane.Chart.ToString();
        try{
            var positions=new Dictionary<string,float>();SetFeetAbove(false);foreach(string name in new[]{"H1","H2","HANY","F1","F2","FANY"})positions[name]=LaneY(name,100,42);
            SetFeetAbove(true);check(positions.All(kv=>Math.Abs(LaneY(kv.Key,100,42)-kv.Value-(kv.Key.StartsWith("H")?84:-84))<.001),"feet-above swaps complete hand and foot groups while preserving left/right positions");
            check(PlayerPrefs.GetInt("feetAbove",0)==1,"lane order is saved in platform preferences");
            foreach(bool mode in new[]{false,true}){
                pro=mode;var baseline=new Dictionary<string,bool>();var keys=new[]{Key.Digit4,Key.Digit9,Key.R,Key.O,Key.E,Key.P,Key.Digit3,Key.Digit0,Key.V,Key.M,Key.C,Key.Comma};
                SetFeetAbove(false);foreach(var n in lane.Notes.Take(30))foreach(var key in keys)baseline[n.Id+key]=Matches(n,key);
                SetFeetAbove(true);check(lane.Notes.Take(30).All(n=>keys.All(key=>Matches(n,key)==baseline[n.Id+key])),(pro?"Pro":"Normal")+" lane reversal preserves every input mapping");
                Begin(true);Audio.Stop();Seek(0);Audio.Stop();check(feetAbove&&Current==Page.Practice,"Practice retains lane order after start and seek");
            }
            check(lane.Chart.ToString()==chart&&!lane.Dirty,"lane display preference never changes project data or difficulty");
            pro=false;Begin(false);Audio.Stop();Audio.AnchorBeat=0;var note=lane.Notes.First(n=>!n.Pedal&&n.Instrument=="HH");HandleKey(Key.Digit9,note.Beat);
            check(Records.Count==1&&Records[0].Judge=="JUST","Play still judges the reversed hand lane at the original note time");
        }finally{feetAbove=oldFeet;pro=oldPro;if(had)PlayerPrefs.SetInt("feetAbove",saved);else PlayerPrefs.DeleteKey("feetAbove");PlayerPrefs.Save();}
        NavigateNow(Page.Songs);yield return WaitForOperations();
    }
}
}
