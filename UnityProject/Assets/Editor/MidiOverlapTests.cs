using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public static class MidiOverlapTests {
    static int count;
    static void Check(bool ok,string message){if(!ok)throw new Exception("MIDI OVERLAP TEST FAILED: "+message);count++;}
    static string Arg(string name){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:"";}
    public static void Run(){
        count=0;string path=Arg("-overlapFixture");if(path=="")throw new Exception("Supply -overlapFixture");
        byte[] bytes=File.ReadAllBytes(path);var source=ChartProject.Demo();source.Chart["title"]="Untitled";var before=(JObject)source.Chart.DeepClone();
        var all=MidiImport.Read(bytes,source,"Overlap.mid",MidiImport.ZeroMode.NoteOff);
        Check(all.Hits==16,"baseline keeps all 16 MIDI hits");
        Check(all.Overlaps.Length==4&&all.Overlaps.Sum(o=>o.Positions)==5&&all.Overlaps.Sum(o=>o.Extra)==6,"groups by exact tick and mapped instrument");
        Check(all.Overlaps.Single(o=>o.Instrument=="SN").Pitches.SequenceEqual(new[]{37,38,40}),"different MIDI pitches offered for shared drum");
        var choices=all.Overlaps.ToDictionary(o=>o.Instrument,o=>new MidiImport.OverlapChoice());
        ChartProject Project(MidiImport.Result r){var p=new ChartProject{Chart=r.Chart,Manifest=(JObject)source.Manifest.DeepClone()};p.Rebuild();return p;}
        var strong=MidiImport.Read(bytes,source,"Overlap.mid",MidiImport.ZeroMode.NoteOff,choices);var p=Project(strong);
        Check(strong.Hits==10&&strong.OverlapsRemoved==6,"stronger policy removes only redundant hits");
        Check(p.Notes.Count(n=>n.Beat==0&&!n.Pedal)==3,"simultaneous SN BD and HH retained");
        Check(p.Notes.Single(n=>n.Instrument=="SN"&&n.Beat==0).Velocity==5,"strongest raw velocity retained");
        Check(p.Notes.Any(n=>n.Instrument=="SN"&&Math.Abs(n.Beat-1.0/960)<1e-12),"one tick apart is never merged");
        Check(p.Notes.Where(n=>n.Instrument=="RD").Select(n=>n.Beat).SequenceEqual(new[]{1.0/3,2.0/3}),"tuplet ticks remain exact");
        Check(!p.ClosedAt(0),"open HH winner releases pedal state");
        Check(p.Tempo.Points.Length==2&&Math.Abs(p.Tempo.BPMAt(1.5)-90)<.001,"tempo changes retained");
        var roundtrip=ChartProject.Read(p.Write(),"roundtrip.crproj");Check(JToken.DeepEquals(p.Chart,roundtrip.Chart),"resolved chart saves exactly");
        choices["SN"].PreferredPitch=37;choices["HH"].PreferredPitch=42;choices["BD"].Softest=true;choices["CR"].KeepAll=true;
        var preferred=MidiImport.Read(bytes,source,"Overlap.mid",MidiImport.ZeroMode.NoteOff,choices);p=Project(preferred);
        Check(p.Notes.Single(n=>n.Instrument=="SN"&&n.Beat==0).Articulation=="rim_closed","explicit type preferred over higher velocity");
        Check(p.Notes.Single(n=>n.Instrument=="SN"&&n.Beat==1).Velocity==4,"missing preferred type falls back to stronger");
        Check(p.Notes.Single(n=>n.Instrument=="BD"&&n.Beat==0).Velocity==1,"softer applies independently to kick");
        Check(p.ClosedAt(0)&&p.Pedals.Count>0,"chosen closed HH creates pedal state");
        Check(p.Notes.Count(n=>n.Instrument=="CR"&&n.Beat==1)==2,"keep all preserves intentional cymbal layering");
        foreach(var c in choices.Values)c.KeepAll=true;
        var keep=MidiImport.Read(bytes,source,"Overlap.mid",MidiImport.ZeroMode.NoteOff,choices);Check(JToken.DeepEquals(all.Chart,keep.Chart)&&keep.OverlapsRemoved==0,"keep all preserves complete original MIDI chart");
        Check(JToken.DeepEquals(source.Chart,before),"preview and resolution never mutate source");
        Check(MidiDropFiles.IsMidi("日本語.MID")&&MidiDropFiles.IsMidi("a.midi")&&!MidiDropFiles.IsMidi("song.mid.exe")&&!MidiDropFiles.IsMidi("song.crproj"),"drop file names use exact MIDI extension");
        var broken=bytes.Take(bytes.Length-7).ToArray();bool rejected=false;try{MidiImport.Read(broken,source,"broken.mid",MidiImport.ZeroMode.NoteOff,choices);}catch(InvalidDataException){rejected=true;}Check(rejected&&JToken.DeepEquals(source.Chart,before),"invalid MIDI leaves original unchanged");
        Debug.Log("CROSS_RHYTHM_MIDI_OVERLAP_TESTS_PASS "+count);
    }
}
}
