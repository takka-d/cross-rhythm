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
    IEnumerator MeterSoundRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();p.SetSongInfo("Meter and Sound QA","Synthetic fixture");
        p.Chart["measures"]=new JArray(3,1.5,3);
        p.Chart["timeSignatures"]=new JArray(new JObject{{"numerator",6},{"denominator",8}},new JObject{{"numerator",3},{"denominator",8}},new JObject{{"numerator",6},{"denominator",8}});
        p.Chart["events"]=new JArray(new JObject{{"id","early"},{"measure",0},{"beat",.5},{"instrument","SN"},{"velocity",3}},new JObject{{"id","overflow"},{"measure",0},{"beat",2.5},{"instrument","RD"},{"velocity",3}},new JObject{{"id","hold"},{"measure",0},{"beat",1},{"instrument","HHSTATE"},{"durationBeats",4}},new JObject{{"id","last"},{"measure",2},{"beat",2.5},{"instrument","HH"},{"velocity",3},{"durationBeats",.25}});
        p.Events.Add(new JObject{{"id","triplet"},{"measure",1},{"beat",1.0/3},{"instrument","SN"},{"velocity",2}});
        p.Chart["tempoChanges"]=new JArray(new JObject{{"measure",0},{"beat",2},{"bpm",144}},new JObject{{"measure",2},{"beat",1},{"bpm",90}});p.Rebuild();
        OpenEditorProject(p);double deadline=Time.realtimeSinceStartupAsDouble+30;while((busy||!loaded||InputBlocked||pendingSongAudio!=null)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        check(loaded&&!busy,"meter and sound fixture loaded");Audio.Stop();
        var times=p.Notes.Select(n=>(n.Id,n.Beat,n.Duration,n.Step,p.SecondsAtBeat(n.Beat))).ToArray();
        var tempos=p.Tempo.Points.Select(t=>(t.Beat,t.BPM,t.Seconds)).ToArray();var closedAt=Enumerable.Range(0,75).Select(i=>p.ClosedAt(i*.1)).ToArray();
        string original=p.Chart.ToString();OpenMeterPanel();SelectMeasure(0);meterNumerator="3";meterDenominator="8";ApplyMeter();
        check(meterKeepTiming&&p.Meter(0).Item1==3&&p.Meter(0).Item2==8,"occupied bar can be shortened to 3/8 with default keep timing mode");
        check(p.Notes.Select(n=>(n.Id,n.Beat,n.Duration,n.Step,p.SecondsAtBeat(n.Beat))).SequenceEqual(times),"rebar preserves every note onset, duration and audible time");
        check(p.Notes.Single(n=>n.Id=="overflow").Measure==1&&p.Notes.Count==times.Length,"overflow notes carry into following bar without loss or duplication");
        check(p.Tempo.Points.Select(t=>(t.Beat,t.BPM,t.Seconds)).SequenceEqual(tempos)&&Enumerable.Range(0,75).Select(i=>p.ClosedAt(i*.1)).SequenceEqual(closedAt),"rebar preserves tempo points and pedal closure across bar boundaries");
        check(p.Length>=7.5&&p.Notes.Any(n=>n.Id=="last"),"shortening appends bars as needed to retain the end of the chart");
        string changed=p.Chart.ToString();Restore(false);check(p.Chart.ToString()==original,"meter Undo restores exact occupied chart");Restore(true);check(p.Chart.ToString()==changed,"meter Redo restores exact rebarring");
        SelectMeasure(1);meterNumerator="6";meterDenominator="8";ApplyMeter();
        check(p.Notes.Select(n=>(n.Id,n.Beat,n.Duration,n.Step,p.SecondsAtBeat(n.Beat))).SequenceEqual(times),"expanding adjacent bar keeps all note times when moving a meter boundary");
        string valid=p.Chart.ToString();SelectMeasure(0);meterNumerator="0";ApplyMeter();check(p.Chart.ToString()==valid,"invalid meter cannot mutate chart");
        showMeterPanel=false;p.SaveNative(Path.Combine(workspaceCheckRoot,"meter-sound.crproj"),false);
        var reopened=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName);check(JToken.DeepEquals(reopened.Chart,p.Chart)&&reopened.Notes.Select(n=>(n.Id,n.Beat,n.Duration)).SequenceEqual(p.Notes.Select(n=>(n.Id,n.Beat,n.Duration))),"rebarring survives actual Windows save and reopen");
        string[] expected={"Crash","Ride","Hi-hat","Snare","High tom","Mid tom","Floor tom","Kick","Pedal"};
        foreach(bool language in new[]{false,true}){english=language;for(int row=0;row<Instruments.Length;row++)check(EditorNoteTypes.Label(Instruments[row],Types[row][0])==expected[row],"default Type uses instrument name: "+(language?"English ":"Japanese ")+Instruments[row]);}
        english=false;check(EditorNoteTypes.Label("RD","crash")=="Ride crash"&&EditorNoteTypes.Label("SN","rim_closed")=="Closed rimshot"&&EditorNoteTypes.Label("HH","tambourine")=="Tambourine","specific articulations remain distinguishable");
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var audioType=typeof(RhythmAudio);var voiceList=(List<AudioSource>)audioType.GetField("voices",flags).GetValue(Audio);var rawClips=(Dictionary<string,AudioClip>)audioType.GetField("clips",flags).GetValue(Audio);float previousGain=Audio.DrumGain;Audio.DrumGain=.8f;
        var evidence=new JArray();
        foreach(string inst in new[]{"HH","RD","CUP"}){
            var levels=new List<double>();var rawData=new float[rawClips[inst].samples*rawClips[inst].channels];rawClips[inst].GetData(rawData,0);float rawPeak=rawData.Max(x=>Math.Abs(x));var kitPeaks=new JObject();
            foreach(string kit in new[]{"studio","bright","punch","dry"}){float requested=1.27f*ChartProject.MaxInstrumentGain*KitPreset.Get(kit,inst).x*RhythmAudio.SampleGainFor(inst),preparedPeak=rawPeak*RhythmAudio.SafePreparationScale(Math.Max(1,requested),rawPeak);kitPeaks[kit]=preparedPeak;check(preparedPeak<=1.00001f,"prepared PCM peak remains in range for "+kit+" "+inst);}
            double actualPreparedPeak=0;
            for(int v=0;v<=5;v++){
                string instrument=inst=="CUP"?"RD":inst,articulation=inst=="HH"?"auto":inst=="CUP"?"cup":"ride";
                int next=(int)audioType.GetField("voice",flags).GetValue(Audio);Audio.Drum(new ChartNote{Instrument=instrument,Articulation=articulation,Velocity=v},true);
                var voice=voiceList[next%voiceList.Count];var data=new float[voice.clip.samples*voice.clip.channels];voice.clip.GetData(data,0);int count=Math.Min(data.Length,voice.clip.frequency*voice.clip.channels/10);
                double rms=Math.Sqrt(data.Take(count).Sum(x=>(double)x*x)/count)*voice.volume;levels.Add(rms);actualPreparedPeak=Math.Max(actualPreparedPeak,data.Max(x=>Math.Abs(x)));
                check(voice.volume<=1&&voice.volume>=0,"calibrated sample voice remains in supported gain range: "+inst+" V"+v);
            }
            check(actualPreparedPeak<=1.00001,"actual prepared clip peak remains in range: "+inst);
            check(levels[0]==0&&levels[3]>.027&&levels[1]<levels[2]&&levels[2]<levels[3]&&levels[3]<levels[4]&&levels[4]<levels[5],"balanced PCM output preserves all strengths and audible medium hits: "+inst);
            evidence.Add(new JObject{{"instrument",inst},{"rawPeak",rawPeak},{"preparedPeak",actualPreparedPeak},{"safePreparedPeakByKit",kitPeaks},{"rms100msByStrength",new JArray(levels)}});
        }
        Audio.DrumGain=previousGain;Audio.Stop();File.WriteAllText(Path.Combine(workspaceCheckRoot,"meter-sound-evidence.json"),new JObject{{"checks","note/tempo timing, save/reopen, undo/redo, labels, actual decoded voice RMS"},{"samples",evidence}}.ToString());
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--snow-midi");
        if(at>=0&&at+1<args.Length){
            var imported=MidiImport.Read(File.ReadAllBytes(args[at+1]),EmptyEditorProject(),"Private MIDI fixture",MidiImport.ZeroMode.Auto);var actual=new ChartProject{Chart=imported.Chart,Manifest=new JObject()};actual.Rebuild();
            check(actual.Starts[51]==153&&actual.Meter(51).Item1==3&&actual.Meter(51).Item2==8&&actual.Starts[52]==154.5,"private MIDI raw tick 73440 / PPQ 480 maps to bar 52 at beat 153 and back to 6/8 at 154.5");
            check(actual.Notes.Count(n=>n.Instrument=="HH"&&n.Velocity==3)==240&&actual.Notes.Count(n=>n.Instrument=="RD"&&n.Velocity==3)==107,"private MIDI hi-hat and ride medium strengths are imported without loss");
        }
        p.Dirty=false;NavigateNow(Page.Songs);yield return null;
    }
}
}
