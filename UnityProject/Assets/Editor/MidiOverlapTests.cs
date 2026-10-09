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
        bool incomplete=false;try{MidiImport.Read(bytes,source,"Overlap.mid",MidiImport.ZeroMode.NoteOff,choices);}catch(InvalidDataException){incomplete=true;}
        Check(incomplete,"unselected collisions cannot import through an implicit policy");
        foreach(var overlap in all.Overlaps)foreach(var c in overlap.Collisions)choices[overlap.Instrument].Selected[c.Tick]=c.Candidates[0].Id;
        var sn=all.Overlaps.Single(o=>o.Instrument=="SN");var hh=all.Overlaps.Single(o=>o.Instrument=="HH");
        choices["SN"].SelectSource(sn,sn.Collisions[0].Candidates.First(c=>c.Pitch==37).Track,37);
        choices["HH"].SelectSource(hh,hh.Collisions[0].Candidates.First(c=>c.Pitch==46).Track,46);
        var resolved=MidiImport.Read(bytes,source,"Overlap.mid",MidiImport.ZeroMode.NoteOff,choices);var p=Project(resolved);
        Check(resolved.Hits==10&&resolved.OverlapsRemoved==6,"one explicit candidate remains at every collision");
        Check(p.Notes.Count(n=>n.Beat==0&&!n.Pedal)==3,"simultaneous SN BD and HH retained");
        Check(p.Notes.Single(n=>n.Instrument=="SN"&&n.Beat==0).Articulation=="rim_closed","selected source type wins independently of strength");
        Check(p.Notes.Any(n=>n.Instrument=="SN"&&Math.Abs(n.Beat-1.0/960)<1e-12),"one tick apart is never merged");
        Check(p.Notes.Where(n=>n.Instrument=="RD").Select(n=>n.Beat).SequenceEqual(new[]{1.0/3,2.0/3}),"tuplet ticks remain exact");
        Check(!p.ClosedAt(0),"open HH winner releases pedal state");
        Check(p.Tempo.Points.Length==2&&Math.Abs(p.Tempo.BPMAt(1.5)-90)<.001,"tempo changes retained");
        var roundtrip=ChartProject.Read(p.Write(),"roundtrip.crproj");Check(JToken.DeepEquals(p.Chart,roundtrip.Chart),"resolved chart saves exactly");
        choices["HH"].SelectSource(hh,hh.Collisions[0].Candidates.First(c=>c.Pitch==42).Track,42);
        var preferred=MidiImport.Read(bytes,source,"Overlap.mid",MidiImport.ZeroMode.NoteOff,choices);p=Project(preferred);
        Check(p.ClosedAt(0)&&p.Pedals.Count>0,"chosen closed HH creates pedal state");
        Check(p.Notes.Count(n=>n.Instrument=="CR"&&n.Beat==1)==1,"cymbal collision also keeps only one");
        var bulk=new MidiImport.OverlapChoice();bulk.SelectSource(sn,sn.Collisions[0].Candidates.First(c=>c.Pitch==37).Track,37);
        Check(bulk.Selected.Count==1&&bulk.SelectedId(sn.Collisions[1])<0,"bulk source does not guess at positions where the source is missing");
        var same=new MidiImport.Collision{Tick=9,Candidates=new[]{new MidiImport.Candidate{Id=500,Track=2,Pitch=38,Velocity=15},new MidiImport.Candidate{Id=501,Track=2,Pitch=38,Velocity=120}}};
        bulk=new MidiImport.OverlapChoice();bulk.SelectSource(new MidiImport.Overlap{Collisions=new[]{same}},2,38);
        Check(bulk.SelectedId(same)<0,"identical source pitches remain individually selectable instead of using velocity");
        bulk.Selected[9]=501;Check(bulk.SelectedId(same)==501,"exact event identity chooses a duplicate within one track");
        choices["SN"].Selected[sn.Collisions[0].Tick]=-99;bool invalid=false;try{MidiImport.Read(bytes,source,"Overlap.mid",MidiImport.ZeroMode.NoteOff,choices);}catch(InvalidDataException){invalid=true;}Check(invalid,"invalid candidate is rejected without silent fallback");
        Check(MidiImport.PitchLabel(38)!=MidiImport.PitchLabel(40)&&MidiImport.PitchLabel(37).Contains("Closed rimshot"),"labels identify MIDI source sounds and mapped Type");
        Check(JToken.DeepEquals(source.Chart,before),"preview and resolution never mutate source");
        Check(MidiDropFiles.IsMidi("日本語.MID")&&MidiDropFiles.IsMidi("a.midi")&&!MidiDropFiles.IsMidi("song.mid.exe")&&!MidiDropFiles.IsMidi("song.crproj"),"drop file names use exact MIDI extension");
        var broken=bytes.Take(bytes.Length-7).ToArray();bool rejected=false;try{MidiImport.Read(broken,source,"broken.mid",MidiImport.ZeroMode.NoteOff,choices);}catch(InvalidDataException){rejected=true;}Check(rejected&&JToken.DeepEquals(source.Chart,before),"invalid MIDI leaves original unchanged");
        Debug.Log("CROSS_RHYTHM_MIDI_OVERLAP_TESTS_PASS "+count);
    }
}
}
