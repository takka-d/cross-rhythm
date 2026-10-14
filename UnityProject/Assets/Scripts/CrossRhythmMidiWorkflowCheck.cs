using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator MidiWorkflowCheck(Action<bool,string> Check){
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--overlap-midi");Check(at>=0&&at+1<args.Length,"overlap MIDI supplied");
        string file=args[at+1];byte[] bytes=File.ReadAllBytes(file);OpenEditorProject(EmptyEditorProject());while(busy||!loaded)yield return null;
        var original=(JObject)Project.Chart.DeepClone();bool dirty=Project.Dirty;int history=undo.Count;midiZeroMode=MidiImport.ZeroMode.NoteOff;
        ImportDroppedMidi(new[]{file});Check(MidiPromptOpen&&pendingMidi.Preview.Overlaps.Length==4,"drop route opens per-instrument overlap prompt");
        var rim=MidiImport.PreviewNote(37,96,out bool rimClosed);Check(rim!=null&&rim.Instrument=="SN"&&rim.Articulation=="rim_closed"&&rim.Velocity==4&&!rimClosed&&RhythmAudio.KeyFor(rim,rimClosed)=="SIDE","MIDI candidate preview maps source sound and game strength exactly");
        var openHat=MidiImport.PreviewNote(46,64,out bool hatClosed);Check(openHat!=null&&openHat.Instrument=="HH"&&openHat.Velocity==3&&!hatClosed&&RhythmAudio.KeyFor(openHat,hatClosed)=="OHH","MIDI candidate preview distinguishes open hi-hat audio");
        Check(MidiImport.SourceSoundLabel(37)=="Side Stick"&&MidiImport.GameSoundLabel(37)=="Closed rimshot"&&MidiImport.GameSoundLabel(46)=="Open hi-hat"&&MidiImport.InstrumentLabel("SN")=="Snare","MIDI duplicate UI exposes source sound, target game sound and full instrument name");
        Check(MidiImport.TargetInstrumentLabel(37)=="Snare"&&MidiImport.TargetTypeLabel(37)=="Closed rimshot"&&MidiImport.TargetInstrumentLabel(46)=="Hi-hat"&&MidiImport.TargetTypeLabel(46)=="Open","MIDI duplicate UI shows target instrument and Type as separate fields");
        Check(JToken.DeepEquals(original,Project.Chart)&&dirty==Project.Dirty&&undo.Count==history,"import preview preserves chart dirty flag and undo history");
        var audioType=typeof(RhythmAudio);var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var voices=(List<AudioSource>)audioType.GetField("voices",flags).GetValue(Audio);var channels=(Dictionary<AudioSource,string>)audioType.GetField("voiceChannels",flags).GetValue(Audio);var gainScales=(Dictionary<string,float>)audioType.GetField("gainScales",flags).GetValue(Audio);var hatVoices=(List<HiHatEnvelope>)audioType.GetField("hatVoices",flags).GetValue(Audio);
        Func<string> choiceState=()=>string.Join("|",pendingMidi.Choices.OrderBy(x=>x.Key).SelectMany(x=>x.Value.Selected.OrderBy(y=>y.Key).Select(y=>x.Key+":"+y.Key+":"+y.Value)));string choicesBefore=choiceState();
        var beforeMute=(JObject)Project.Chart.DeepClone();bool beforeMuteDirty=Project.Dirty,beforeEditorMonitoring=Audio.EditorMonitoring;float beforeDrumGain=Audio.DrumGain;Audio.DrumGain=.43f;Project.SetMonitorMuted("drums",true);Audio.EditorMonitoring=true;Audio.ApplyMonitor();int mutedVoice=(int)audioType.GetField("voice",flags).GetValue(Audio);Audio.Drum(rim,rimClosed);
        Check((int)audioType.GetField("voice",flags).GetValue(Audio)==mutedVoice,"normal editor drum output remains silent under the Drums monitor mute");Audio.Audition(rim,rimClosed);var audition=voices[mutedVoice%voices.Count];Audio.ApplyMonitor();
        float sideScale=gainScales.TryGetValue("SIDE",out float preparedSide)?preparedSide:1,expectedAudition=Mathf.Clamp01(Audio.DrumGain*ChartProject.Gains[rim.Velocity]*Project.InstrumentGain("SN")*KitPreset.Get((string)Project.Chart["drumKit"]??"studio","SIDE").x*RhythmAudio.SampleGainFor("SIDE")/sideScale);
        Check((int)audioType.GetField("voice",flags).GetValue(Audio)==mutedVoice+1&&channels[audition]=="audition"&&audition.clip!=null&&Math.Abs(audition.volume-expectedAudition)<.0001&&audition.isPlaying,"MIDI candidate audition bypasses editor monitor mute while retaining game volume and sample gain");audition.Stop();
        float openScale=gainScales.TryGetValue("OHH",out float preparedOpen)?preparedOpen:1,expectedOpenAudition=Mathf.Clamp01(Audio.DrumGain*ChartProject.Gains[openHat.Velocity]*Project.InstrumentGain("HH")*KitPreset.Get((string)Project.Chart["drumKit"]??"studio","OHH").x*RhythmAudio.SampleGainFor("OHH")/openScale);
        foreach(string muteChannel in new[]{"drums","HH"}){
            Project.Chart=(JObject)beforeMute.DeepClone();Project.Dirty=beforeMuteDirty;Project.Rebuild();Audio.EditorMonitoring=true;Audio.ApplyMonitor();Project.SetMonitorMuted(muteChannel,true);Audio.ApplyMonitor();
            var mutedChart=(JObject)Project.Chart.DeepClone();bool mutedDirty=Project.Dirty;int openVoice=(int)audioType.GetField("voice",flags).GetValue(Audio);Audio.Audition(openHat,hatClosed);var openAudition=voices[openVoice%voices.Count];Audio.ApplyMonitor();
            Check((int)audioType.GetField("voice",flags).GetValue(Audio)==openVoice+1&&channels.TryGetValue(openAudition,out string openChannel)&&openChannel=="audition"&&openAudition.clip!=null&&Math.Abs(openAudition.volume-expectedOpenAudition)<.0001&&openAudition.isPlaying,"open hi-hat audition remains audible with game volume and sample gain on the audition channel under "+muteChannel+" monitor mute");
            Check(JToken.DeepEquals(mutedChart,Project.Chart)&&mutedDirty==Project.Dirty&&undo.Count==history&&choiceState()==choicesBefore&&MidiRemaining==5,"open hi-hat audition changes no chart, undo history or overlap choice under "+muteChannel+" monitor mute");openAudition.Stop();
        }
        Project.Chart=(JObject)beforeMute.DeepClone();Project.Dirty=beforeMuteDirty;Project.Rebuild();Audio.EditorMonitoring=true;Audio.ApplyMonitor();int openHatIndex=(int)audioType.GetField("hatVoice",flags).GetValue(Audio);Audio.Drum(openHat,hatClosed);var normalOpenHat=hatVoices[openHatIndex%hatVoices.Count];yield return null;
        int sideEffectVoice=(int)audioType.GetField("voice",flags).GetValue(Audio);var closedHat=MidiImport.PreviewNote(42,64,out bool closedHatState);Audio.Audition(closedHat,closedHatState);Audio.Audition(new ChartNote{Instrument="HHSTATE",Velocity=3},false);
        Check(normalOpenHat.Source.isPlaying&&channels[voices[sideEffectVoice%voices.Count]]=="audition"&&channels[voices[(sideEffectVoice+1)%voices.Count]]=="audition","closed and pedal hi-hat audition do not choke an existing normal open hi-hat or enter normal channels");normalOpenHat.Source.Stop();voices[sideEffectVoice%voices.Count].Stop();voices[(sideEffectVoice+1)%voices.Count].Stop();
        Project.Chart=beforeMute;Project.Dirty=beforeMuteDirty;Project.Rebuild();Audio.DrumGain=beforeDrumGain;Audio.EditorMonitoring=beforeEditorMonitoring;Audio.ApplyMonitor();
        Check(JToken.DeepEquals(original,Project.Chart)&&dirty==Project.Dirty&&undo.Count==history&&MidiRemaining==5,"MIDI candidate audition changes no chart, undo history or overlap choice");
        var prompt=pendingMidi;ImportDroppedMidi(new[]{file});Check(ReferenceEquals(prompt,pendingMidi),"second drop cannot replace an active choice");
        Navigate(Page.Songs);OnEditorShortcut("SelectAll");Check(Current==Page.Edit&&selection.Count==0,"modal blocks navigation and editor shortcuts");
        CancelMidi();Check(!MidiPromptOpen&&JToken.DeepEquals(original,Project.Chart)&&dirty==Project.Dirty,"Cancel changes no chart data");
        ImportDroppedMidi(new[]{file,file});Check(!MidiPromptOpen&&JToken.DeepEquals(original,Project.Chart),"multiple dropped files cannot overwrite sequentially");
        ImportDroppedMidi(new[]{file+".txt"});Check(!MidiPromptOpen&&JToken.DeepEquals(original,Project.Chart),"non-MIDI drop is rejected");
        ImportMidi(bytes,Path.GetFileName(file));Check(MidiPromptOpen,"Import MIDI uses the same overlap review");
        Check(MidiRemaining==5,"overlap dialog initially requires five concrete choices");
        ApplyPendingMidi();Check(MidiPromptOpen&&JToken.DeepEquals(original,Project.Chart),"incomplete choices cannot replace the chart");
        foreach(var overlap in pendingMidi.Preview.Overlaps)foreach(var c in overlap.Collisions)Check(pendingMidi.Choices[overlap.Instrument].SelectCandidate(c,c.Candidates[0].Id),"displayed MIDI candidate can be selected directly");
        var sn=pendingMidi.Preview.Overlaps.Single(o=>o.Instrument=="SN");pendingMidi.Choices["SN"].SelectSource(sn,sn.Collisions[0].Candidates.First(c=>c.Pitch==37).Track,37);
        Check(MidiRemaining==0,"each exact candidate resolves one position");ApplyPendingMidi();
        Check(!MidiPromptOpen&&Project.PlayableNoteCount==10&&Project.Dirty,"confirmed policies apply exactly once");
        Check(Project.Notes.Single(n=>n.Instrument=="SN"&&n.Beat==0).Articulation=="rim_closed","app imports selected type");
        var imported=(JObject)Project.Chart.DeepClone();Restore(false);Check(JToken.DeepEquals(original,Project.Chart),"single Undo restores the entire pre-import chart");Restore(true);Check(JToken.DeepEquals(imported,Project.Chart),"Redo restores resolved notes and tempo");
        string path=Path.Combine(workspaceCheckRoot,"MIDI Overlap Check.crproj");Project.SaveNative(path,false);Check(JToken.DeepEquals(ChartProject.Read(File.ReadAllBytes(path),"saved").Chart,imported),"resolved import survives native save and reopen");
        var originalPage=Current;Current=Page.Songs;ImportDroppedMidi(new[]{file});Check(!MidiPromptOpen&&JToken.DeepEquals(imported,Project.Chart),"drop outside Edit leaves chart unchanged");Current=originalPage;
        ImportMidi(bytes,Path.GetFileName(file));yield return null;
        Check(midiFileDrop.Attached,"Windows native drop receiver attached to game window");
        CancelMidi();
    }
}
}
