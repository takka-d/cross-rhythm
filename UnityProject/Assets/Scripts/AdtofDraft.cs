using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public static class AdtofDraft {
    public static AudioDraft Convert(ChartProject source,JArray detections,double duration,bool extend){
        if(detections==null||detections.Count>250000||!double.IsFinite(duration)||duration<0||duration>1800)throw new ArgumentException("Invalid ADTOF result");
        var layout=new ChartProject{Chart=(JObject)source.Chart.DeepClone(),Manifest=(JObject)source.Manifest.DeepClone()};layout.Rebuild();
        double end=Math.Max(0,layout.BeatAtSeconds(duration-layout.Offset));
        if(extend&&end>layout.Length){
            var bars=(JArray)layout.Chart["measures"];var meters=new JArray(Enumerable.Range(0,bars.Count).Select(i=>{var m=layout.Meter(i);return new JObject{{"numerator",m.Item1},{"denominator",m.Item2}};}));
            double length=layout.Measures.Last(),total=layout.Length;
            while(total<end){if(bars.Count>=20000)throw new ArgumentException("Too many bars");bars.Add(length);meters.Add(meters.Last.DeepClone());total+=length;}
            layout.Chart["timeSignatures"]=meters;layout.Rebuild();
        }
        var result=new AudioDraft{Layout=layout};var seen=new HashSet<string>();
        foreach(JObject detection in detections){
            double t=(double?)detection["time"]??double.NaN;if(!double.IsFinite(t)||t<0||t>duration)continue;
            string inst=(string)detection["instrument"];if(!new[]{"BD","SN","MT","HH","CR"}.Contains(inst))continue;
            double beat=layout.BeatAtSeconds(t-layout.Offset);if(beat<0||beat>=layout.Length)continue;
            beat=layout.SnapBeat(beat);int bar=Array.BinarySearch(layout.Starts,beat);if(bar<0)bar=~bar-1;bar=Math.Max(0,Math.Min(bar,layout.Measures.Length-1));
            double local=beat-layout.Starts[bar];string key=bar+":"+Math.Round(local/layout.Grid)+":"+inst;if(!seen.Add(key))continue;
            JObject Note(string instrument)=>new JObject{{"id",Guid.NewGuid().ToString("N")},{"measure",bar},{"beat",local},{"instrument",instrument},{"velocity",4},{"gridStepBeats",layout.Grid},{"source","adtof"},{"confidence",Math.Max(0,Math.Min(1,(double?)detection["confidence"]??0))}};
            var note=Note(inst);if(inst=="SN"){note["articulation"]="center";note["durationBeats"]=layout.Grid;}result.Events.Add(note);
            if(inst=="HH"){var pedal=Note("HHSTATE");pedal["durationBeats"]=layout.Grid;result.Events.Add(pedal);}
        }
        return result;
    }
}
}
