using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public static class TempoTests {
    static int checks;
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception("TEMPO TEST: "+message);}
    static void Near(double a,double b,string message,double tolerance=1e-8)=>Check(Math.Abs(a-b)<=tolerance,message+" "+a+" vs "+b);
    static ChartProject Empty(){var p=ChartProject.Demo();p.Chart["events"]=new JArray();p.Chart["measures"]=new JArray(4,4,4);p.Rebuild();return p;}
    static void Reject(Action action,string message){bool rejected=false;try{action();}catch(ArgumentException){rejected=true;}Check(rejected,message);}
    public static void Run(){
        checks=0;var p=Empty();var original=(JObject)p.Chart.DeepClone();
        foreach(double beat in new[]{-8,-.5,0,.3333333333333,3.5,4,8,12,20}){Near(p.SecondsAtBeat(beat),beat*.5,"legacy beat time");Near(p.BeatAtSeconds(beat*.5),beat,"legacy time beat");}
        Check(JToken.DeepEquals(original,p.Chart),"reading timing does not rewrite legacy projects");
        p.SetTempo(0,2,60);p.SetTempo(1,.5,180);p.SetTempo(2,0,90);
        Near(p.SecondsAtBeat(2),1,"first boundary");Near(p.SecondsAtBeat(4.5),3.5,"inside-bar boundary");Near(p.SecondsAtBeat(8),3.5+3.5/3,"third boundary");Near(p.DurationSeconds,3.5+3.5/3+4*2.0/3,"chart duration");
        foreach(double beat in new[]{-8,0,1.999999,2,2.000001,3.5,4.499999,4.5,4.500001,7.999999,8,8.000001,12,40})Near(p.BeatAtSeconds(p.SecondsAtBeat(beat)),beat,"inverse across boundaries");
        foreach(var t in p.Tempo.Points)foreach(double offset in new[]{-.12,-.00001,0,.00001,.12})Near(p.SecondsAtBeat(p.BeatAtSeconds(t.Seconds+offset)),t.Seconds+offset,"time inverse across tempo point");
        var copied=ChartProject.Read(p.Write(),"tempo.crproj");Check(JToken.DeepEquals(p.Chart,copied.Chart),"tempo project lossless round trip");Near(copied.DurationSeconds,p.DurationSeconds,"saved duration");
        p.SetTempo(1,.5,240);Check(p.Tempo.Points.Length==4&&p.Tempo.BPMAt(4.5)==240,"replace instead of duplicate");Check(p.RemoveTempo(1,.5)&&p.Tempo.BPMAt(4.5)==60,"remove inherits previous tempo");Check(!p.RemoveTempo(0,0),"initial tempo cannot be removed");
        p.SetTempo(0,0,100);Near(p.SecondsAtBeat(-4),-2.4,"count-in uses initial tempo");
        var before=(JObject)p.Chart.DeepClone();double beforeSeconds=p.DurationSeconds;p.InsertMeasure(0);Check(p.Tempo.Points.Last().Measure==3,"insert shifts tempo with notes");Check(p.RemoveMeasure(1)&&JToken.DeepEquals(before["tempoChanges"],p.Chart["tempoChanges"])&&JToken.DeepEquals(before["measures"],p.Chart["measures"]),"remove inserted empty bar restores exact tempo map");Near(p.DurationSeconds,beforeSeconds,"insert/remove retains elapsed time");
        Check(!p.RemoveMeasure(2),"tempo-only bar cannot be silently deleted");Check(!p.SetMeter(0,1,4),"meter shortening cannot remove tempo point");Check(p.SetMeter(1,7,8),"meter change before tempo");Near(p.Tempo.Points.Last().Beat,7.5,"tempo follows changed meter");
        foreach(double bad in new[]{0,19,601,double.NaN,double.PositiveInfinity})Reject(()=>p.SetTempo(0,1,bad),"reject invalid BPM");
        Reject(()=>p.SetTempo(0,double.NaN,120),"reject invalid beat");Reject(()=>p.SetTempo(0,4,120),"reject end of bar");Reject(()=>p.SetTempo(-1,0,120),"reject invalid measure");
        p=Empty();p.Chart["tempoChanges"]=new JArray(new JObject{{"measure",0},{"beat",2},{"bpm",60}},new JObject{{"measure",0},{"beat",2},{"bpm",90}});p.Rebuild();Check(p.Tempo.Points.Length==2&&p.Tempo.BPMAt(2)==90,"last point at identical time wins");
        p.Chart["tempoChanges"]=new JArray(new JObject{{"measure",0},{"beat",double.PositiveInfinity},{"bpm",120}});Reject(p.Rebuild,"malformed saved tempo rejected");
        p=Empty();for(int i=0;i<12;i++)p.Events.Add(new JObject{{"measure",i/4},{"beat",i%4},{"instrument","RD"}});p.Rebuild();Near(p.NotesPerSecond,2,"fixed tempo difficulty");p.SetTempo(1,0,60);Near(p.NotesPerSecond,1.2,"difficulty uses actual elapsed time");
        var detected=AdtofDraft.Convert(p,new JArray(new JObject{{"time",3.0},{"instrument","SN"},{"confidence",1}}),10,false);Near((double)detected.Events[0]["beat"],1,"ADTOF seconds map through tempo changes");Check((int)detected.Events[0]["measure"]==1,"ADTOF correct bar");
        var pcm=new float[48000*4];for(int i=0;i<2400;i++)pcm[3*48000+i]=(float)(Math.Sin(i*.1)*Math.Exp(-i/500.0));var draft=new AudioDraft();foreach(var progress in draft.Run(pcm,48000,p,false)){}Check(draft.Events.OfType<JObject>().Any(e=>(int)e["measure"]==1&&Math.Abs((double)e["beat"]-1)<1e-6),"local draft detects onset using tempo map");
        var fixture=MidiFixture();p=Empty();p.SetTempo(2,0,80);var midi=MidiImport.Read(fixture,p,"Tempo Check.mid",MidiImport.ZeroMode.Auto);var imported=new ChartProject{Chart=midi.Chart,Manifest=p.Manifest};imported.Rebuild();
        Check(imported.Tempo.Points.Length==4&&midi.TempoChanges==3,"all MIDI changes retained, old map replaced");Near(imported.BPM,120,"MIDI initial BPM");Near(imported.Tempo.Points[2].Beat,4.5,"MIDI inside-bar tempo");Near(imported.SecondsAtBeat(7),1+2.5+2.5*.333333,"MIDI microsecond integration");
        Check(imported.Meter(0).Item2==8&&imported.Tempo.Points[3].Measure==2&&imported.Tempo.Points[3].Local==0,"MIDI meter and tempo at same tick");
        var ticks=new[]{0,160,960,1680,2160,2400,3200,3360,3840,4320,4800};Check(imported.Notes.Select(n=>n.Beat).SequenceEqual(ticks.Select(t=>t/480.0)),"MIDI note ticks unchanged");
        var noInitial=MidiImport.Read(MidiFixture(false),Empty(),"late-tempo.mid",MidiImport.ZeroMode.Auto);Near((double)noInitial.Chart["bpm"],120,"MIDI defaults to 120 before first event");
        copied=ChartProject.Read(imported.Write(),"midi-tempo.crproj");foreach(var note in copied.Notes)Near(copied.SecondsAtBeat(note.Beat),imported.SecondsAtBeat(note.Beat),"MIDI saved timing exact");
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-tempoFixture");if(at>=0&&at+1<args.Length){File.WriteAllBytes(args[at+1],fixture);File.WriteAllBytes(Path.ChangeExtension(args[at+1],"crproj"),imported.Write());}
        Debug.Log("CROSS_RHYTHM_TEMPO_TESTS_PASS "+checks);
    }
    public static byte[] MidiFixture(bool initial=true){
        byte[] Number(int n,int count){var b=new byte[count];for(int i=count-1;i>=0;i--){b[i]=(byte)(n&255);n>>=8;}return b;}
        byte[] Variable(int n){var b=new List<byte>{(byte)(n&127)};while((n>>=7)>0)b.Insert(0,(byte)(128|(n&127)));return b.ToArray();}
        byte[] Track(List<(int tick,byte[] data)> events){var b=new List<byte>();int last=0;foreach(var e in events){b.AddRange(Variable(e.tick-last));b.AddRange(e.data);last=e.tick;}b.AddRange(new byte[]{0,255,47,0});return System.Text.Encoding.ASCII.GetBytes("MTrk").Concat(Number(b.Count,4)).Concat(b).ToArray();}
        byte[] Tempo(int us)=>new byte[]{255,81,3,(byte)(us>>16),(byte)(us>>8),(byte)us};
        var meta=new List<(int,byte[])>{(0,new byte[]{255,88,4,7,3,24,8})};if(initial){meta.Add((0,Tempo(600000)));meta.Add((0,Tempo(500000)));}
        meta.Add((960,Tempo(1000000)));meta.Add((2160,Tempo(333333)));meta.Add((3360,Tempo(666667)));meta.Add((3360,new byte[]{255,88,4,4,2,24,8}));
        var notes=new List<(int,byte[])>();foreach(int tick in new[]{0,160,960,1680,2160,2400,3200,3360,3840,4320,4800})notes.Add((tick,new byte[]{153,51,100}));
        return System.Text.Encoding.ASCII.GetBytes("MThd").Concat(Number(6,4)).Concat(Number(1,2)).Concat(Number(2,2)).Concat(Number(480,2)).Concat(Track(meta)).Concat(Track(notes)).ToArray();
    }
}
}
