using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    // Read-only diagnostics for the isolated browser regression harness.
    public void OnPlaybackProbe(string tag){
        if(!Application.absoluteURL.Contains("qa=1"))return;
        Debug.Log("CR_PLAYBACK_PROBE "+new JObject{{"tag",tag},{"page",Current.ToString()},{"rate",Audio.Rate},{"running",Audio.Running},{"preparing",Audio.Preparing},{"error",Audio.PlaybackError},{"sourceSecond",Audio.BackingTimelineSeconds},{"chartSecond",Project.Offset+Project.SecondsAtBeat(Audio.Beat)},{"segments",Audio.StretchSegmentCount},{"prepared",Audio.SegmentsPrepared},{"maxPreparationSeconds",Audio.MaxSegmentPreparationSeconds},{"anchor",Audio.AnchorDSP}}.ToString(Newtonsoft.Json.Formatting.None));
    }
}
}
