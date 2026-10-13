using System.Collections.Generic;
namespace CrossRhythm {
public sealed partial class RhythmAudio {
    public bool EditorMonitoring;
    readonly Dictionary<UnityEngine.AudioSource,string> voiceChannels=new Dictionary<UnityEngine.AudioSource,string>();
    bool hatsMuted;
    public float EffectiveBackingGain=>EditorMonitoring&&Project!=null&&Project.MonitorMuted("music")?0:BackingGain;
    public bool MonitorSilenced(string instrument){if(instrument=="HHSTATE"||instrument=="HH_PEDAL"||instrument=="OHH")instrument="HH";return EditorMonitoring&&Project!=null&&(Project.MonitorMuted("drums")||Project.MonitorMuted(instrument));}
    public void ApplyMonitor(){
        if(Backing!=null)Backing.volume=EffectiveBackingGain;
        foreach(var item in voiceChannels)if(item.Value!="click"&&item.Value!="audition"&&MonitorSilenced(item.Value))item.Key.Stop();
        bool next=MonitorSilenced("HH");if(next&&!hatsMuted)StopHats();hatsMuted=next;UpdateStretch();
    }
    public void StopClicks(){foreach(var item in voiceChannels)if(item.Value=="click")item.Key.Stop();}
}
}
