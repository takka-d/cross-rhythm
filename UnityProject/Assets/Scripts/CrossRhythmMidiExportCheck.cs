using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator MidiExportRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();OpenEditorProject(p);while(busy||!loaded)yield return null;
        SetLaneInputType(2,"tambourine");var point=new Vector2(EditorPPB,EditorRowHeight*2.5f);Editor.Down(point,0,1,false,false);Editor.Up(point);
        var n=p.Notes.Single();check(n.Articulation=="tambourine"&&ChartVisuals.TypeCode(n)=="TB","HH pointer placement uses Tambourine with TB label");
        check(RhythmAudio.KeyFor(n,true)=="TAMBOURINE"&&RhythmAudio.KeyFor(n,false)=="TAMBOURINE","Tambourine does not switch sound with pedal position");
        var clip=Resources.Load<AudioClip>("Drums/TAMBOURINE");check(clip!=null&&clip.samples>1000,"licensed Tambourine recording included in player");
        var pcm=new float[clip.samples*clip.channels];check(clip.GetData(pcm,0)&&pcm.Any(v=>Math.Abs(v)>.01),"Tambourine recording decodes to non-silent PCM");
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var voices=(List<AudioSource>)typeof(RhythmAudio).GetField("voices",flags).GetValue(Audio);
        int next=(int)typeof(RhythmAudio).GetField("voice",flags).GetValue(Audio);Audio.Drum(n,true);
        var voice=voices[next%voices.Count];check(voice.clip.name.StartsWith("TAMBOURINE"),"editor and performance Drum route selects Tambourine clip");
        float normal=voice.volume;p.SetInstrumentGain("HH",.25f);next=(int)typeof(RhythmAudio).GetField("voice",flags).GetValue(Audio);Audio.Drum(n,false);
        check(Math.Abs(voices[next%voices.Count].volume/normal-.25)<.0001,"HH mixer applies to Tambourine");
        Audio.Stop();Restore(false);check(p.Notes.Count==0,"Tambourine placement Undo removes the note");Restore(true);check(p.Notes.Single().Articulation=="tambourine","Tambourine placement Redo restores the type");
        p.SaveNative(Path.Combine(workspaceCheckRoot,"tambourine.crproj"),false);var saved=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName);
        check(saved.Notes.Single().Articulation=="tambourine"&&EditorNoteTypes.Get(saved,2)=="tambourine","Tambourine project and input type survive disk save");
        p.SetSongInfo("MIDI Export QA","Artist");p.Chart["measures"]=new JArray(3,3.5,4);p.Chart["timeSignatures"]=new JArray(new JObject{{"numerator",6},{"denominator",8}},new JObject{{"numerator",7},{"denominator",8}},new JObject{{"numerator",4},{"denominator",4}});p.Chart["events"]=new JArray();p.Rebuild();p.SetTempo(1,.5,173);p.SetTempo(2,1,91);
        foreach(int div in new[]{3,5,7,11,13})p.Events.Add(new JObject{{"measure",0},{"beat",1.0/div},{"instrument","HH"},{"articulation","tambourine"},{"velocity",div==3?1:div==5?2:div==7?3:div==11?4:5}});
        p.Events.Add(new JObject{{"measure",1},{"beat",1},{"instrument","HHSTATE"},{"durationBeats",1.5},{"velocity",3}});
        p.Events.Add(new JObject{{"measure",1},{"beat",1},{"instrument","HH"},{"velocity",4}});
        p.Events.Add(new JObject{{"measure",2},{"beat",.5},{"instrument","HH"},{"velocity",4}});
        p.Events.Add(new JObject{{"measure",2},{"beat",1},{"instrument","SN"},{"articulation","buzz"},{"durationBeats",2},{"velocity",4}});
        p.Events.Add(new JObject{{"measure",2},{"beat",2},{"instrument","SN"},{"velocity",0}});p.Rebuild();
        var original=p.Chart.ToString();string baseline=p.Baseline,path=p.FilePath;bool dirty=p.Dirty;int history=undo.Count,library=Library.Count;
        var output=MidiExport.Write(p);check(output.Hits==9&&output.Muted==1&&output.Approximate==1,"MIDI exports sounding notes and reports muted notes and GM substitution");
        var target=Path.Combine(workspaceCheckRoot,"export-check.mid");MidiExport.SaveNative(target,output.Bytes);
        check(File.ReadAllBytes(target).SequenceEqual(output.Bytes),"MIDI disk write verified");
        var imported=MidiImport.Read(output.Bytes,EmptyEditorProject(),"check.mid",MidiImport.ZeroMode.NoteOff);var restored=new ChartProject{Chart=imported.Chart,Manifest=p.Manifest};restored.Rebuild();
        check(imported.Hits==8&&restored.Notes.Count(n=>n.Articulation=="tambourine")==5,"exported MIDI reimports all 8 strikes including Tambourine");
        foreach(var hit in p.Notes.Where(x=>x.Articulation=="tambourine"))check(restored.Notes.Any(x=>x.Articulation=="tambourine"&&Math.Abs(x.Beat-hit.Beat)<=.500001/MidiExport.PPQ&&x.Velocity==hit.Velocity),"MIDI retains tuplet position and strength "+hit.Velocity);
        check(restored.Tempo.Points.Length==3&&Math.Abs(restored.SecondsAtBeat(8)-p.SecondsAtBeat(8))<.0001,"MIDI tempo changes retain seconds mapping");
        check(restored.Meter(0).Equals(Tuple.Create(6,8))&&restored.Meter(1).Equals(Tuple.Create(7,8)),"MIDI retains 6/8 and 7/8 time signatures");
        exportingMidi=true;Navigate(Page.Songs);check(Current==Page.Edit&&!ConfirmApplicationQuit(),"MIDI write blocks navigation and app quit");
        OnMidiExported("{\"error\":\"Cancelled\"}");check(!exportingMidi&&status.Contains("MIDI"),"MIDI cancellation releases operation");
        OnMidiExported("{\"saved\":true,\"name\":\"export-check.mid\"}");
        check(p.Chart.ToString()==original&&p.Dirty==dirty&&p.Baseline==baseline&&p.FilePath==path&&undo.Count==history&&Library.Count==library,"MIDI export does not mark project saved or modify chart, history or Songs");
        bool refused=false;try{MidiExport.SaveNative(Path.Combine(workspaceCheckRoot,"not-midi.crproj"),output.Bytes);}catch(IOException){refused=true;}check(refused,"MIDI export rejects a project extension");
        File.WriteAllText(Path.Combine(workspaceCheckRoot,"export-check.json"),new JObject{{"ppq",MidiExport.PPQ},{"hits",output.Hits},{"muted",output.Muted},{"approximate",output.Approximate},{"length",p.Length},{"chart",p.Chart.DeepClone()}}.ToString());
        p.Dirty=false;NavigateNow(Page.Songs);yield return null;
    }
}
}
