using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public sealed class TempoPoint {
    public readonly int Measure;
    public readonly double Local,Beat,BPM,Seconds;
    public TempoPoint(int measure,double local,double beat,double bpm,double seconds){Measure=measure;Local=local;Beat=beat;BPM=bpm;Seconds=seconds;}
}
// Chart beats are quarter notes. Only the mapping to elapsed seconds changes.
// Negative beats use the initial tempo for count-in; the final tempo extends
// beyond the chart so audio analysis can safely append measures.
public sealed class TempoMap {
    public readonly TempoPoint[] Points;
    readonly double[] beats,seconds;
    public TempoMap(double initial,JToken changes,double[] starts,double[] measures){
        if(!ValidBPM(initial))throw new ArgumentException("BPM must be 20-600");
        if(changes!=null&&changes.Type!=JTokenType.Null&&!(changes is JArray))throw new ArgumentException("Invalid tempo changes");
        var entries=new SortedDictionary<double,TempoPoint>{{0,new TempoPoint(0,0,0,initial,0)}};
        if(changes is JArray array)foreach(var token in array){
            if(!(token is JObject e))throw new ArgumentException("Invalid tempo point");
            int m=(int?)e["measure"]??-1;double b=(double?)e["beat"]??double.NaN,v=(double?)e["bpm"]??double.NaN;
            if(m<0||m>=measures.Length||!double.IsFinite(b)||b<0||b>=measures[m]||!ValidBPM(v))throw new ArgumentException("Invalid tempo position or BPM");
            double absolute=starts[m]+b;entries[absolute]=new TempoPoint(m,b,absolute,v,0);
        }
        var result=new List<TempoPoint>();double elapsed=0;
        foreach(var e in entries.Values){if(result.Count>0){var last=result[result.Count-1];elapsed+=(e.Beat-last.Beat)*60/last.BPM;}result.Add(new TempoPoint(e.Measure,e.Local,e.Beat,e.BPM,elapsed));}
        Points=result.ToArray();beats=Points.Select(p=>p.Beat).ToArray();seconds=Points.Select(p=>p.Seconds).ToArray();
    }
    public static bool ValidBPM(double bpm)=>double.IsFinite(bpm)&&bpm>=20&&bpm<=600;
    static int Segment(double[] values,double value){int i=Array.BinarySearch(values,value);return i>=0?i:Math.Max(0,~i-1);}
    public double SecondsAt(double beat){var p=Points[Segment(beats,beat)];return p.Seconds+(beat-p.Beat)*60/p.BPM;}
    public double BeatAt(double sec){var p=Points[Segment(seconds,sec)];return p.Beat+(sec-p.Seconds)*p.BPM/60;}
    public double BPMAt(double beat)=>Points[Segment(beats,beat)].BPM;
}
}
