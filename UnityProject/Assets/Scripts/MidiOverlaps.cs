using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
namespace CrossRhythm {
public static partial class MidiImport {
    public sealed class Candidate {public int Id,Track,Channel,Pitch,Velocity;}
    public sealed class Collision {public long Tick;public double Beat;public Candidate[] Candidates;}
    public sealed class Overlap {
        public string Instrument;public int Positions,Extra;public int[] Pitches;public Collision[] Collisions;
    }
    public sealed class OverlapChoice {
        // Select an exact source event. Identical pitches from different tracks
        // (or repeated within one track) are still individually selectable.
        public readonly Dictionary<long,int> Selected=new Dictionary<long,int>();
        public int SelectedId(Collision collision)=>Selected.TryGetValue(collision.Tick,out int id)&&collision.Candidates.Any(c=>c.Id==id)?id:-1;
        public void SelectSource(Overlap overlap,int track,int pitch){
            foreach(var collision in overlap.Collisions){var matches=collision.Candidates.Where(c=>c.Track==track&&c.Pitch==pitch).ToArray();if(matches.Length==1)Selected[collision.Tick]=matches[0].Id;}
        }
    }
    static Overlap[] FindOverlaps(Note[] notes)=>notes.Where(n=>n.Instrument!="HHSTATE")
        .GroupBy(n=>(n.Instrument,n.Tick)).Where(g=>g.Count()>1).GroupBy(g=>g.Key.Instrument)
        .Select(g=>new Overlap{Instrument=g.Key,Positions=g.Count(),Extra=g.Sum(x=>x.Count()-1),Pitches=g.SelectMany(x=>x).Select(n=>n.Pitch).Distinct().OrderBy(x=>x).ToArray(),
            Collisions=g.Select(x=>new Collision{Tick=x.Key.Tick,Beat=x.First().Beat,Candidates=x.Select(n=>new Candidate{Id=n.Id,Track=n.Track,Channel=n.Channel,Pitch=n.Pitch,Velocity=n.Velocity}).ToArray()}).ToArray()}).ToArray();
    static Note[] ResolveOverlaps(Note[] notes,IReadOnlyDictionary<string,OverlapChoice> choices,out int removed){
        removed=0;if(choices==null)return notes;
        var result=new List<Note>();
        foreach(var group in notes.GroupBy(n=>(n.Instrument,n.Tick))){
            if(group.Key.Instrument=="HHSTATE"||group.Count()==1){result.AddRange(group);continue;}
            if(!choices.TryGetValue(group.Key.Instrument,out var choice)||!choice.Selected.TryGetValue(group.Key.Tick,out int id))throw new InvalidDataException("Select one note at each overlapping position");
            var note=group.FirstOrDefault(n=>n.Id==id);if(note==null)throw new InvalidDataException("The selected MIDI note is not a candidate at this position");
            result.Add(note);removed+=group.Count()-1;
        }
        return result.OrderBy(n=>n.Tick).ThenBy(n=>n.Pitch).ToArray();
    }
    public static string PitchLabel(int pitch){
        string source=pitch switch {35=>"Acoustic Bass Drum",36=>"Bass Drum 1",37=>"Side Stick",38=>"Acoustic Snare",39=>"Hand Clap",40=>"Electric Snare",42=>"Closed Hi-Hat",46=>"Open Hi-Hat",49=>"Crash Cymbal 1",57=>"Crash Cymbal 2",55=>"Splash Cymbal",52=>"Chinese Cymbal",51=>"Ride Cymbal 1",59=>"Ride Cymbal 2",53=>"Ride Bell",54=>"Tambourine",50=>"High Tom",48=>"Hi-Mid Tom",47=>"Low-Mid Tom",45=>"Low Tom",43=>"High Floor Tom",41=>"Low Floor Tom",_=>"MIDI "+pitch};
        var n=new Note{Pitch=pitch};Map(n);
        string type=n.Type=="tambourine"?"Tambourine":n.Hat=="closed"?"Closed":n.Hat=="open"?"Open":n.Type=="center"?"Normal":n.Type=="rim_closed"?"Closed rimshot":n.Type=="rim_open"?"Open rimshot":n.Type=="cup"?"Cup":n.Type=="ride"?"Ride":n.Type=="high"?"High":n.Type=="crash"?"Crash":n.Type=="china"?"China":n.Type=="splash"?"Splash":"Normal";
        return source+" ["+pitch+"] → "+type;
    }
}
}
