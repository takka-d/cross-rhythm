using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator MidiWorkflowCheck(Action<bool,string> Check){
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--overlap-midi");Check(at>=0&&at+1<args.Length,"overlap MIDI supplied");
        string file=args[at+1];byte[] bytes=File.ReadAllBytes(file);OpenEditorProject(EmptyEditorProject());while(busy||!loaded)yield return null;
        var original=(JObject)Project.Chart.DeepClone();bool dirty=Project.Dirty;int history=undo.Count;midiZeroMode=MidiImport.ZeroMode.NoteOff;
        ImportDroppedMidi(new[]{file});Check(MidiPromptOpen&&pendingMidi.Preview.Overlaps.Length==4,"drop route opens per-instrument overlap prompt");
        Check(JToken.DeepEquals(original,Project.Chart)&&dirty==Project.Dirty&&undo.Count==history,"import preview preserves chart dirty flag and undo history");
        var prompt=pendingMidi;ImportDroppedMidi(new[]{file});Check(ReferenceEquals(prompt,pendingMidi),"second drop cannot replace an active choice");
        Navigate(Page.Songs);OnEditorShortcut("SelectAll");Check(Current==Page.Edit&&selection.Count==0,"modal blocks navigation and editor shortcuts");
        CancelMidi();Check(!MidiPromptOpen&&JToken.DeepEquals(original,Project.Chart)&&dirty==Project.Dirty,"Cancel changes no chart data");
        ImportDroppedMidi(new[]{file,file});Check(!MidiPromptOpen&&JToken.DeepEquals(original,Project.Chart),"multiple dropped files cannot overwrite sequentially");
        ImportDroppedMidi(new[]{file+".txt"});Check(!MidiPromptOpen&&JToken.DeepEquals(original,Project.Chart),"non-MIDI drop is rejected");
        ImportMidi(bytes,Path.GetFileName(file));Check(MidiPromptOpen,"Import MIDI uses the same overlap review");
        Check(MidiRemaining==5,"overlap dialog initially requires five concrete choices");
        ApplyPendingMidi();Check(MidiPromptOpen&&JToken.DeepEquals(original,Project.Chart),"incomplete choices cannot replace the chart");
        foreach(var overlap in pendingMidi.Preview.Overlaps)foreach(var c in overlap.Collisions)pendingMidi.Choices[overlap.Instrument].Selected[c.Tick]=c.Candidates[0].Id;
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
