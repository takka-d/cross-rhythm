using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator TempoRuntimeCheck(Action<bool,string> Check,byte[] backing){
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--tempo-midi");Check(at>=0&&at+1<args.Length,"tempo MIDI supplied");
        OpenEditorProject(EmptyEditorProject());while(busy||!loaded)yield return null;
        ImportMidi(File.ReadAllBytes(args[at+1]),"Tempo Check.mid");Check(Project.Tempo.Points.Length==4,"tempo changes imported into app");
        var original=(JObject)Project.Chart.DeepClone();tempoBar="3";tempoBeat="2";tempoBPM="150";ApplyTempo(false);
        Check(Project.Tempo.BPMAt(8)==150&&Project.Dirty,"Edit sets inside-bar tempo");Restore(false);Check(JToken.DeepEquals(original,Project.Chart),"Undo restores exact tempo chart");Restore(true);Check(Project.Tempo.BPMAt(8)==150,"Redo restores tempo change");
        ApplyTempo(true);Check(!Project.HasTempoChange(2,1),"Edit removes selected tempo change");Restore(false);Restore(false);Check(JToken.DeepEquals(original,Project.Chart),"undo chain restores imported tempo chart");
        SetAudio(backing,"tempo-test.wav");while(busy||!loaded)yield return null;
        string path=Path.Combine(workspaceCheckRoot,"Tempo Check.crproj");Project.SaveNative(path,false);var saved=ChartProject.Read(File.ReadAllBytes(path),"Tempo Check.crproj");Check(JToken.DeepEquals(saved.Chart,Project.Chart),"tempo and audio project saved and reopened");
        Library.Add(saved);NavigateNow(Page.Songs);FocusSong(Library.Count-1);while(busy||!loaded)yield return null;
        bool previousPro=pro;pro=false;
        try {
            Begin(false);Audio.Stop();Audio.AnchorBeat=0;
            int i=0;foreach(var n in Project.Notes){double ms=(i++%3-1)*80;HandleKey(UnityEngine.InputSystem.Key.Digit9,Project.BeatAtSeconds(Project.SecondsAtBeat(n.Beat)+ms/1000));}
            Check(Records.Count==Project.Notes.Count,"Play judges each tempo fixture note");i=0;foreach(var record in Records)Check(Math.Abs(record.Ms-(i++%3-1)*80)<.00001,"Play timing error uses seconds across tempo changes");
            Begin(true);Audio.Stop();
            foreach(double rate in new[]{.25,1,2}){
                ChangePractice(rate,false);Seek(4.45);Audio.Play(4.45);
                foreach(var n in Project.Notes)Check(Math.Abs(Audio.BeatAt(Audio.DSPAt(n.Beat))-n.Beat)<1e-8,"tempo DSP inverse at speed "+rate);
                double onset=Audio.DSPAt(4.5);yield return new WaitForSeconds(.8f);
                Check(Audio.Beat>4.5&&AudioSettings.dspTime>onset,"live Practice passes tempo boundary at speed "+rate);
                double sample=Audio.Backing.timeSamples/(double)Audio.Song.frequency,expected=Project.Offset+Project.SecondsAtBeat(Audio.Beat);
                Check(Audio.Backing.isPlaying&&Math.Abs(sample-expected)<.10,"backing sample position follows tempo clock at speed "+rate);
                Audio.Pause();double beat=Audio.Beat;yield return new WaitForSeconds(.1f);Check(Audio.Beat==beat,"pause holds tempo position");
            }
            ChangePractice(1,false);NavigateNow(Page.Edit);while(busy||!loaded)yield return null;OpenEditorProject(saved);while(busy||!loaded)yield return null;editorPanel=3;Audio.AnchorBeat=0;SelectTempoPosition(0);
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(workspaceCheckRoot,"Tempo-Edit.png"));yield return new WaitForSeconds(.2f);
        }finally{pro=previousPro;}
    }
}
}
