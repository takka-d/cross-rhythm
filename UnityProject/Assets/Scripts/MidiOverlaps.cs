using System;
using System.Linq;
using System.Collections.Generic;
namespace CrossRhythm {
public static partial class MidiImport {
    public sealed class Overlap {
        public string Instrument;
        public int Positions,Extra;
        public int[] Pitches;
    }
    public sealed class OverlapChoice {
        public bool KeepAll,Softest;
        public int PreferredPitch=-1;
    }
    static Overlap[] FindOverlaps(Note[] notes)=>notes.Where(n=>n.Instrument!="HHSTATE")
        .GroupBy(n=>(n.Instrument,n.Tick)).Where(g=>g.Count()>1).GroupBy(g=>g.Key.Instrument)
        .Select(g=>new Overlap{Instrument=g.Key,Positions=g.Count(),Extra=g.Sum(x=>x.Count()-1),Pitches=g.SelectMany(x=>x).Select(n=>n.Pitch).Distinct().OrderBy(x=>x).ToArray()}).ToArray();
    static Note[] ResolveOverlaps(Note[] notes,IReadOnlyDictionary<string,OverlapChoice> choices,out int removed){
        removed=0;if(choices==null)return notes;
        var result=new List<Note>();
        foreach(var group in notes.GroupBy(n=>(n.Instrument,n.Tick))){
            var choice=choices.TryGetValue(group.Key.Instrument,out var selected)?selected:new OverlapChoice();
            if(group.Key.Instrument=="HHSTATE"||group.Count()==1||choice.KeepAll){result.AddRange(group);continue;}
            var candidates=group.AsEnumerable();
            if(choice.PreferredPitch>=0&&group.Any(n=>n.Pitch==choice.PreferredPitch))candidates=group.Where(n=>n.Pitch==choice.PreferredPitch);
            result.Add((choice.Softest?candidates.OrderBy(n=>n.Velocity):candidates.OrderByDescending(n=>n.Velocity)).ThenBy(n=>n.Track).ThenBy(n=>n.Pitch).First());
            removed+=group.Count()-1;
        }
        return result.OrderBy(n=>n.Tick).ThenBy(n=>n.Pitch).ToArray();
    }
    public static string PitchLabel(int pitch){
        var n=new Note{Pitch=pitch};Map(n);
        string label=n.Hat=="closed"?"Closed":n.Hat=="open"?"Open":n.Type=="center"?"Normal":n.Type=="rim_closed"?"Closed rimshot":n.Type=="rim_open"?"Open rimshot":n.Type=="cup"?"Cup":n.Type=="ride"?"Ride":n.Type=="high"?"High":n.Type=="normal"?"Normal":n.Type=="crash"?"Crash":n.Type=="china"?"China":n.Type=="splash"?"Splash":n.Instrument??"Note";
        return pitch+" · "+label;
    }
}
}
