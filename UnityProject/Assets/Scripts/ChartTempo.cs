using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public sealed partial class ChartProject {
    public TempoMap Tempo {get;private set;}
    public IEnumerable<JObject> TempoChanges => (Chart["tempoChanges"] as JArray)?.OfType<JObject>()??Enumerable.Empty<JObject>();
    public double SecondsAtBeat(double beat)=>Tempo.SecondsAt(beat);
    public double BeatAtSeconds(double seconds)=>Tempo.BeatAt(seconds);
    public double DurationSeconds=>SecondsAtBeat(Length);
    public string TempoLabel {get{double min=Tempo.Points.Min(t=>t.BPM),max=Tempo.Points.Max(t=>t.BPM);return Math.Abs(max-min)<.000001?$"{min:0.##}":$"{min:0.##}–{max:0.##}";}}
    bool SameTempoPosition(JObject e,int bar,double beat)=>(int?)e["measure"]==bar&&Math.Abs(((double?)e["beat"]??0)-beat)<1e-9;
    public bool HasTempoChange(int bar,double beat)=>(bar!=0||beat>1e-9)&&TempoChanges.Any(e=>SameTempoPosition(e,bar,beat));
    public void SetTempo(int bar,double beat,double bpm){
        if(bar<0||bar>=Measures.Length||!double.IsFinite(beat)||beat<0||beat>=Measures[bar]||!TempoMap.ValidBPM(bpm))throw new ArgumentException("Invalid tempo position or BPM");
        foreach(var e in TempoChanges.Where(e=>SameTempoPosition(e,bar,beat)).ToArray())e.Remove();
        if(bar==0&&beat==0)Chart["bpm"]=bpm;
        else {if(!(Chart["tempoChanges"] is JArray))Chart["tempoChanges"]=new JArray();((JArray)Chart["tempoChanges"]).Add(new JObject{{"measure",bar},{"beat",beat},{"bpm",bpm}});}
        Dirty=true;Rebuild();
    }
    public bool RemoveTempo(int bar,double beat){
        if(!HasTempoChange(bar,beat))return false;
        foreach(var e in TempoChanges.Where(e=>SameTempoPosition(e,bar,beat)).ToArray())e.Remove();Dirty=true;Rebuild();return true;
    }
}
}
