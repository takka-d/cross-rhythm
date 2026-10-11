using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator AudioImportRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();p.Chart["measures"]=new JArray(3);p.Chart["timeSignatures"]=new JArray(new JObject{{"numerator",6},{"denominator",8}});p.Rebuild();
        var unchanged=(JObject)p.Chart.DeepClone();
        check(p.ExtendToAudio(1.5)==0&&JToken.DeepEquals(unchanged,p.Chart),"audio exactly at bar end does not add a bar");
        check(p.ExtendToAudio(9.25)==6&&p.Measures.Length==7&&p.Meter(6).Item2==8,"audio coverage appends complete 6/8 bars using last meter");
        check(p.DurationSeconds>=9.25&&p.SecondsAtBeat(p.Starts.Last())<9.25,"audio ends inside the last appended bar");
        check(p.ExtendToAudio(2)==0&&p.Measures.Length==7,"shorter audio never deletes existing bars");
        p.Chart["measures"]=new JArray(4,3);p.Chart["timeSignatures"]=new JArray(new JObject{{"numerator",4},{"denominator",4}},new JObject{{"numerator",6},{"denominator",8}});p.Chart["audioOffsetSec"]=-1;p.Rebuild();p.SetTempo(1,0,60);
        var tempi=p.Chart["tempoChanges"].DeepClone();check(p.ExtendToAudio(9)==2&&p.Length==13&&p.DurationSeconds==11,"coverage follows tempo changes and negative source offset");
        check(JToken.DeepEquals(p.Chart["tempoChanges"],tempi)&&p.Meter(3).Item2==8,"coverage retains existing tempo points and meter");
        p.Chart["audioOffsetSec"]=3;check(p.BarsToCoverAudio(9)==0,"positive source offset excludes audio before chart start");
        check(p.BarsToCoverAudio(double.NaN)==0&&p.BarsToCoverAudio(double.PositiveInfinity)==0,"invalid duration cannot extend the chart");
        p=EmptyEditorProject();p.Events.Add(new JObject{{"instrument","SN"},{"measure",0},{"beat",1.0/3},{"gridStepBeats",1.0/3},{"velocity",3}});p.Rebuild();OpenEditorProject(p);yield return WaitForOperations();
        var original=(JObject)p.Chart.DeepClone();var history=undo.Count;var libraryCount=Library.Count;
        string wav=Path.Combine(workspaceCheckRoot,"..","audio-import-0328.wav");
        ImportDroppedMidi(new[]{wav});check(InputBlocked,"native audio drop immediately blocks overlapping imports");yield return WaitForOperations();
        check(loaded&&Audio.Song!=null&&p.AudioPath.EndsWith("audio-import-0328.wav"),"dropped WAV is decoded through the normal audio import path");
        check(p.Measures.Length==5&&p.DurationSeconds>=9.25&&p.Notes.Count==1&&Math.Abs(p.Notes[0].Beat-1.0/3)<1e-10,"new audio adds bars without moving existing tuplet notes");
        check(undo.Count==history+1&&Library.Count==libraryCount,"audio replacement plus extension is one undo and never adds a Songs entry");
        Restore(false);yield return WaitForOperations();check(JToken.DeepEquals(p.Chart,original)&&Audio.Song==null&&p.AudioPath=="","one Undo restores chart and original audio");
        Restore(true);yield return WaitForOperations();check(Audio.Song!=null&&p.Measures.Length==5,"Redo restores decoded audio and complete coverage");
        int before=undo.Count;var good=p.Write();bool dirty=p.Dirty;SetAudio(new byte[]{1,2,3,4},"broken.wav");yield return WaitForOperations();
        check(loaded&&Audio.Song!=null&&ChartProject.Hash(good)==ChartProject.Hash(p.Write())&&p.Dirty==dirty,"failed decoding restores original audio, chart and dirty state");
        check(undo.Count==before&&status.Contains(T("元の編集内容を保持","Previous edits retained")),"failed audio import preserves undo history and explains recovery");
        gridMenuOpen=true;ImportDroppedMidi(new[]{wav});check(!InputBlocked&&undo.Count==before,"drop cannot enter through grid popup");gridMenuOpen=false;
        ImportDroppedMidi(new[]{wav,wav});check(undo.Count==before,"multiple audio files are rejected");
        foreach(string ext in new[]{"WAV","mp3","m4a","ogg","flac","aac"})check(MidiDropFiles.IsAudio("track."+ext),"audio drop accepts documented extension "+ext);
        check(!MidiDropFiles.IsAudio("song.crproj")&&MidiDropFiles.IsMidi("track.MIDI"),"audio extension support does not change MIDI or project routing");
        SelectMeasure(4);RemoveEditorMeasure();check(p.Measures.Length==4,"empty trailing bar can still be removed");ExtendEditorToAudio();check(p.Measures.Length==5,"Extend to Audio restores missing tail without reimport");Restore(false);check(p.Measures.Length==4,"manual audio coverage is undoable");Restore(true);
        SelectMeasure(0);int count=p.Measures.Length;RemoveEditorMeasure();check(p.Measures.Length==count,"Remove Bar protects bars that contain notes");
        Audio.Play(0);yield return null;Audio.EditorMonitoring=true;
        float music=Audio.BackingGain;ToggleMonitor("music");check(Audio.Running&&Audio.EffectiveBackingGain==0&&Audio.BackingGain==music,"Music Mute keeps transport running and preserves its fader");
        ToggleMonitor("music");check(Audio.EffectiveBackingGain==music&&Audio.Running,"Music unmute restores level without seeking or restarting");
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var voices=(List<AudioSource>)typeof(RhythmAudio).GetField("voices",flags).GetValue(Audio);
        int VoiceIndex()=>(int)typeof(RhythmAudio).GetField("voice",flags).GetValue(Audio);
        var snare=new ChartNote{Instrument="SN",Articulation="buzz",Velocity=4,Beat=0,Duration=4};
        int next=VoiceIndex();Audio.Drum(snare,true);var sounding=voices[next%voices.Count];ToggleMonitor("SN");
        check(!sounding.isPlaying&&Audio.Running&&p.InstrumentGain("SN")==1,"instrument mute stops existing tail without changing fader or transport");
        next=VoiceIndex();Audio.Drum(snare,true);check(VoiceIndex()==next,"muted instrument cannot schedule new notes");
        Audio.Click(true,RhythmAudio.Clock+.05);check(VoiceIndex()==next+1,"snare mute does not silence the metronome");
        ToggleMonitor("drums");next=VoiceIndex();Audio.Drum(new ChartNote{Instrument="BD",Velocity=4},false);Audio.Pedal();check(VoiceIndex()==next,"Drums Mute includes kick and pedal");
        Audio.Click(false,RhythmAudio.Clock+.5);var click=voices[next%voices.Count];check(VoiceIndex()==next+1,"Drums Mute leaves click independent");SetMetronome(false);check(!click.isPlaying,"metronome OFF cancels already scheduled clicks");
        Audio.AnchorBeat=7;SetMetronome(true);check(lastClick>6.99,"metronome ON starts at current location without replaying earlier beats");SetMetronome(false);Audio.Stop();
        var saved=ChartProject.Read(p.Write(),"monitor.crproj");check(saved.MonitorMuted("SN")&&saved.MonitorMuted("drums")&&saved.InstrumentGain("SN")==1,"monitor switches persist separately from instrument levels");
        Audio.EditorMonitoring=false;next=VoiceIndex();Audio.Drum(snare,true);check(VoiceIndex()==next+1&&Audio.EffectiveBackingGain==music,"Edit monitoring does not mute performance playback");Audio.Stop();
        p.SetMonitorMuted("drums",false);p.SetMonitorMuted("SN",false);Audio.EditorMonitoring=true;ToggleMonitor("HH");check(Audio.MonitorSilenced("HH")&&Audio.MonitorSilenced("HHSTATE")&&Audio.MonitorSilenced("OHH"),"HH monitor includes open/closed hat and pedal");Restore(false);yield return WaitForOperations();check(!p.MonitorMuted("HH"),"Undo restores monitor switch");
        Audio.Stop();Audio.AnchorBeat=0;Audio.Play(0);scheduled.Add(p.Notes[0].Index);previewCursor=p.Notes.Count;
        ToggleMonitor("SN");ToggleMonitor("SN");check(previewCursor==0&&!scheduled.Contains(p.Notes[0].Index),"unmute requeues future notes instead of losing lookahead onsets");
        next=VoiceIndex();Audio.Click(true,RhythmAudio.Clock+1);var independentClick=voices[next%voices.Count];Audio.CancelFutureDrums();check(independentClick.isPlaying,"drum rescheduling does not cancel independent future clicks");Audio.Stop();
        p.SaveNative(Path.Combine(workspaceCheckRoot,"audio-import-saved.crproj"),false);var reread=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName);check(reread.Measures.Length==5&&reread.Files.ContainsKey(reread.AudioPath),"saved project contains audio and extended bars");
        NavigateNow(Page.Songs);yield return WaitForOperations();
    }
}
}
