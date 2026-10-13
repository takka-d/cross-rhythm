using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CrossRhythm {
public static class StabilityTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception("STABILITY: "+message);UnityEngine.Debug.Log("PASS stability: "+message);}
    static string Arg(string name,string fallback){var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,name);return at>=0&&at+1<args.Length?args[at+1]:fallback;}
    static int InstrumentOrder(string instrument){switch(instrument){case "BD":return 0;case "HH":return 1;case "SN":return 2;case "HT":return 3;case "MT":return 4;case "FT":return 5;case "CR":return 6;case "RD":return 7;default:return 8;}}

    public static void Run(){
        var p=ChartProject.Demo();p.Chart["measures"]=new JArray(Enumerable.Repeat(4,512));p.Chart["events"]=new JArray();
        string[] instruments={"HH","SN","RD","BD"};
        for(int bar=0;bar<512;bar++)for(int i=0;i<16;i++)p.Events.Add(new JObject{{"id","long-"+bar+"-"+i},{"measure",bar},{"beat",i/4.0},{"instrument",instruments[i%4]},{"velocity",i%6}});
        p.Rebuild();Check(p.Notes.Count==8192,"long chart fixture contains 8192 notes");

        const int frames=10000;long legacyVisits=0;var watch=Stopwatch.StartNew();
        for(int frame=0;frame<frames;frame++)foreach(var note in p.Notes)if(note.Beat<.1)legacyVisits++;
        watch.Stop();double legacyMs=watch.Elapsed.TotalMilliseconds;
        var cursor=new PlaybackJudgementCursor();long cursorVisits=0;watch.Restart();
        for(int frame=0;frame<frames;frame++)if(cursor.TakeNextDue(p,0,.1,false,1)!=null)cursorVisits++;
        watch.Stop();double cursorMs=watch.Elapsed.TotalMilliseconds;
        Check(legacyVisits>0&&cursorVisits==0&&cursor.Index==0,"stationary playback cursor does not consume a future note");
        Check(cursorMs<legacyMs,"cursor avoids the former full-chart frame scan");

        int due=0;cursor.Reset();
        for(double beat=0;beat<=p.Length+1;beat+=.125){ChartNote note;while((note=cursor.TakeNextDue(p,0,beat,false,1))!=null)due++;}
        Check(due==p.PlayableNoteCount&&cursor.Index==p.Notes.Count,"long-run cursor judges every playable note once");
        cursor.Reset();int automatic=0;ChartNote autoNote;while((autoNote=cursor.TakeNextDue(p,0,p.Length,true,1))!=null)automatic++;
        Check(automatic==p.PlayableNoteCount,"Auto uses the same bounded cursor without losing notes");

        var visible=p.Notes.Take(96).ToList();visible.AddRange(new[]{
            new ChartNote{Index=10001,Id="z",Lane="HANY",Beat=100,Instrument="SN"},
            new ChartNote{Index=10002,Id="a",Lane="HANY",Beat=100,Instrument="HH"},
            new ChartNote{Index=10003,Id="b",Lane="HANY",Beat=100,Instrument="SN"},
            new ChartNote{Index=10004,Id="c",Lane="HANY",Beat=100,Instrument="BD"}
        });
        var offsets=new Dictionary<int,float>();var order=new List<ChartNote>();ChartVisuals.FillSimultaneousOffsets(visible,offsets,order);
        var expected=new Dictionary<int,float>();foreach(var group in visible.GroupBy(n=>n.Lane+"|"+Math.Round(n.Beat,3).ToString(System.Globalization.CultureInfo.InvariantCulture))){var sorted=group.OrderBy(n=>InstrumentOrder(n.Instrument)).ThenBy(n=>n.Id,StringComparer.Ordinal).ToArray();for(int i=0;i<sorted.Length;i++)expected[sorted[i].Index]=(i-(sorted.Length-1)*.5f)*16;}
        Check(offsets.Count==expected.Count&&offsets.All(kv=>expected.TryGetValue(kv.Key,out float value)&&Math.Abs(value-kv.Value)<1e-6),"reused stage offsets exactly match the former stable grouping");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<10000;i++)ChartVisuals.FillSimultaneousOffsets(visible,offsets,order);long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(allocated<4096,"steady stage offset calculation does not create per-frame garbage");

        var meter=p.Meter(0);Check(ReferenceEquals(meter,p.Meter(0)),"meter lookup reuses the rebuilt immutable signature");
        bool meterStable=true;before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100000;i++)meterStable&=ReferenceEquals(meter,p.Meter(0));long meterAllocated=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(meterStable,"meter cache remains stable across repeated reads");
        Check(meterAllocated<4096,"steady meter lookup does not create per-frame garbage");

        var report=new JObject{{"notes",p.Notes.Count},{"simulatedFrames",frames},{"legacyNoteVisits",p.Notes.Count*(long)frames},{"legacyMilliseconds",legacyMs},{"cursorMilliseconds",cursorMs},{"cursorIndexAfterLongRun",cursor.Index},{"offsetIterations",10000},{"offsetAllocatedBytes",allocated},{"meterIterations",100000},{"meterAllocatedBytes",meterAllocated}};
        string path=Arg("-stabilityReport",Path.Combine(Application.temporaryCachePath,"cross-rhythm-stability.json"));Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,report.ToString());
        UnityEngine.Debug.Log("CROSS_RHYTHM_STABILITY_TESTS_PASS "+path);
    }
}
}
