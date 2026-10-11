using System;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public sealed partial class ChartProject {
    public int BarsToCoverAudio(double seconds){
        if(!double.IsFinite(seconds)||seconds<=0)return 0;
        double end=BeatAtSeconds(Math.Max(0,seconds-Offset));
        return (int)Math.Min(100000,Math.Max(0,Math.Ceiling((end-Length)/Measures[Measures.Length-1]-1e-6)));
    }
    public int ExtendToAudio(double seconds){
        int count=BarsToCoverAudio(seconds);if(count==0)return 0;
        if(Measures.Length+count>100000)throw new InvalidOperationException("Audio needs too many bars");
        var bars=(JArray)Chart["measures"];var meters=MeterArray();var meter=Meter(Measures.Length-1);double length=Measures[Measures.Length-1];
        for(int i=0;i<count;i++){bars.Add(length);meters.Add(new JObject{{"numerator",meter.Item1},{"denominator",meter.Item2}});}
        Chart["timeSignatures"]=meters;Dirty=true;Rebuild();return count;
    }
    public bool MonitorMuted(string channel)=>(bool?)Chart["editorMonitor"]?[channel]??false;
    public void SetMonitorMuted(string channel,bool muted){
        if(!(Chart["editorMonitor"] is JObject monitor))Chart["editorMonitor"]=monitor=new JObject();
        monitor[channel]=muted;Dirty=true;
    }
}
}
