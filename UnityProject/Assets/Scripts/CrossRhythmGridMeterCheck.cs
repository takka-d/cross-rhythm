using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator GridMeterRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();p.SetSongInfo("Grid and Meter QA","Synthetic fixture");
        p.Chart["measures"]=new JArray(3,3.5,4);p.Chart["timeSignatures"]=new JArray(new JObject{{"numerator",6},{"denominator",8}},new JObject{{"numerator",7},{"denominator",8}},new JObject{{"numerator",4},{"denominator",4}});
        p.Chart["quantize"]=1;
        p.Chart["events"]=new JArray(
            new JObject{{"id","legacy-sn"},{"measure",0},{"beat",1},{"instrument","SN"}},
            new JObject{{"id","legacy-pedal"},{"measure",0},{"beat",2},{"instrument","HHSTATE"}},
            new JObject{{"id","long-sn"},{"measure",1},{"beat",1.0/3},{"instrument","SN"},{"durationBeats",1.25},{"gridStepBeats",1.0/3}},
            new JObject{{"id","buzz-sn"},{"measure",1},{"beat",1.5},{"instrument","SN"},{"articulation","buzz"},{"durationBeats",1.25},{"gridStepBeats",.25}},
            new JObject{{"id","late-hh"},{"measure",1},{"beat",3},{"instrument","HH"},{"velocity",2}});
        p.Chart["tempoChanges"]=new JArray(new JObject{{"measure",1},{"beat",2},{"bpm",144}});p.Rebuild();
        p.SaveNative(Path.Combine(workspaceCheckRoot,"grid-meter.crproj"),false);
        OpenEditorProject(p);yield return WaitForOperations();
        string before=p.Chart.ToString();
        var timing=p.Notes.Select(n=>(n.Beat,n.Duration,n.Step)).ToArray();
        var widths=p.Notes.Select(n=>ChartVisuals.EditRect(p,n,0,56,32).width).ToArray();
        var pedal=p.Pedals.Select(n=>(n.Start,n.End)).ToArray();
        var legacySn=p.Notes.Single(n=>n.Id=="legacy-sn");var staleSn=p.Notes.Single(n=>n.Id=="long-sn");var buzz=p.Notes.Single(n=>n.Id=="buzz-sn");
        check(!legacySn.Sustained&&legacySn.Source["durationBeats"]==null&&Math.Abs(legacySn.Duration-.25)<1e-9&&Math.Abs(ChartVisuals.EditRect(p,legacySn,0,56,32).width-.25*.7*56)<.001,"whole-note Grid does not give a legacy hit a whole-note body");
        check(!staleSn.Sustained&&Math.Abs(staleSn.Duration-.25)<1e-9&&buzz.Sustained&&Math.Abs(buzz.Duration-1.25)<1e-9,"only Pedal and Buzz use saved duration metadata");
        foreach(int q in new[]{4,6,8,12,24,10,20,14,28,1024,16}){
            SetEditorGrid(q);
            check(p.Notes.Select(n=>(n.Beat,n.Duration,n.Step)).SequenceEqual(timing),"grid "+q+" preserves legacy and explicit note onset, duration and step");
            check(p.Notes.Select(n=>ChartVisuals.EditRect(p,n,0,56,32).width).SequenceEqual(widths)&&p.Pedals.Select(n=>(n.Start,n.End)).SequenceEqual(pedal),"grid "+q+" preserves drawn widths and pedal release times");
        }
        for(int i=0;i<11;i++)Restore(false);check(p.Chart.ToString()==before,"grid Undo restores exact chart without injecting hit durations");
        for(int i=0;i<11;i++)Restore(true);check(p.Notes.Select(n=>(n.Beat,n.Duration,n.Step)).SequenceEqual(timing),"grid Redo retains original note timing");
        bool oldEnglish=english;english=false;check(GridLabel(12)=="8分3連"&&GridLabel(24)=="16分3連"&&GridLabel(20)=="16分5連"&&GridLabel(28)=="16分7連","Japanese grid names identify note value and tuplet count");
        english=true;check(GridLabel(12)=="1/8 triplet"&&GridLabel(24)=="1/16 triplet"&&GridLabel(20)=="1/16 · 5-tuplet"&&GridLabel(28)=="1/16 · 7-tuplet","English grid names identify note value and tuplet count");english=oldEnglish;
        meterKeepTiming=false;string gridChart=p.Chart.ToString();SelectMeasure(0);InsertEditorMeasure();
        check(p.Measures.Length==4&&p.Meter(1).Item1==6&&p.Meter(1).Item2==8&&p.Measures[1]==3,"insert after 6/8 inherits 6/8 and its exact length");
        check(Math.Abs(p.Notes.Single(n=>n.Id=="long-sn").Beat-(timing[2].Beat+3))<1e-9&&(int)p.TempoChanges.First()["measure"]==2,"insert shifts later notes and tempo together");
        Restore(false);check(p.Chart.ToString()==gridChart,"bar insertion Undo restores exact chart");Restore(true);
        meterBarField="2";GoToTypedMeterMeasure();check(editMeasure==1&&Math.Abs(Audio.Beat-p.Starts[1])<1e-8,"typed bar number selects and seeks the exact bar for meter editing");
        string beforeTypedMeter=p.Chart.ToString();var firstMeter=p.Meter(0);meterKeepTiming=false;meterNumerator="5";meterDenominator="8";ApplyMeter();
        check(p.Meter(1).Equals(Tuple.Create(5,8))&&p.Meter(0).Equals(firstMeter),"typed bar meter action changes only the requested bar");Restore(false);check(p.Chart.ToString()==beforeTypedMeter,"typed bar meter change has one exact Undo step");
        SelectMeasure(2);InsertEditorMeasure();check(p.Meter(3).Item1==7&&p.Meter(3).Item2==8&&p.Measures[3]==3.5,"insert after 7/8 inherits 7/8 in mixed meter chart");
        SelectMeasure(1);meterNumerator="5";meterDenominator="8";ApplyMeter();check(p.Measures[1]==2.5&&p.Meter(1).Item1==5,"meter action changes inserted bar to 5/8");
        SelectMeasure(0);meterNumerator="7";meterDenominator="8";ApplyMeter();check(p.Measures[0]==3.5&&p.Meter(0).Item1==7,"meter action changes original bar to 7/8");
        string changed=p.Chart.ToString();meterKeepTiming=false;SelectMeasure(2);meterNumerator="3";meterDenominator="8";ApplyMeter();check(p.Chart.ToString()==changed,"shortening past a note is rejected without modifying the chart");
        p.SaveNative(Path.Combine(workspaceCheckRoot,"grid-meter-saved.crproj"),false);
        var reopened=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName);check(JToken.DeepEquals(reopened.Chart,p.Chart)&&reopened.Notes.Select(n=>n.Duration).SequenceEqual(p.Notes.Select(n=>n.Duration)),"grid lengths and mixed meters survive native save and reopen");
        meterKeepTiming=true;p.Dirty=false;NavigateNow(Page.Songs);yield return WaitForOperations();
    }
}
}
