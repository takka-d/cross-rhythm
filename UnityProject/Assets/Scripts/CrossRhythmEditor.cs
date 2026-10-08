using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    static readonly string[] Instruments={"CR","RD","HH","SN","HT","MT","FT","BD","HHSTATE"};
    static readonly string[][] Types={new[]{"crash","splash","china"},new[]{"ride","cup","crash"},new[]{"auto"},new[]{"center","rim_closed","rim_open","buzz"},new[]{"center","high","rimshot"},new[]{"center","high","rimshot"},new[]{"center","high","rimshot"},new[]{"normal"},new[]{"closed"}};
    static readonly int[] Grids={4,8,16,32,64,6,12,24,48,10,20,40,14,28,56,36,44,52,60};
    sealed class Snapshot {public JObject Chart,Manifest;public Dictionary<string,byte[]> Files;}
    Stack<Snapshot> undo=new Stack<Snapshot>(),redo=new Stack<Snapshot>();
    HashSet<int> selection=new HashSet<int>();
    Vector2 editScroll;
    float zoom=1;
    int editMeasure;
    string meterNumerator="4",meterDenominator="4";
    MidiImport.ZeroMode midiZeroMode;
    void PickMidi(){
        if(!CanImportMidi)return;
#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CRPickMidi(gameObject.name);
#else
        string path=PlatformFiles.Pick(false,"",false,true);if(path!=null)ImportMidi(System.IO.File.ReadAllBytes(path),System.IO.Path.GetFileName(path));
#endif
    }
    int instrument=3,kind=0,velocity=4;
    double duration=.25,pasteBeat;
    string bpmField="",gridField="",durationField="0.25",offsetField="0";
    void ResetEditorFields(){bpmField=Project.BPM.ToString(System.Globalization.CultureInfo.InvariantCulture);gridField=((int?)Project.Chart["quantize"]??16).ToString();offsetField=Project.Offset.ToString("0.######",System.Globalization.CultureInfo.InvariantCulture);SelectMeasure(editMeasure);ResetTempoFields();}
    void PushUndo(){mixerEditing="";EndSongInfoEdit();undo.Push(new Snapshot{Chart=(JObject)Project.Chart.DeepClone(),Manifest=(JObject)Project.Manifest.DeepClone(),Files=new Dictionary<string,byte[]>(Project.Files)});redo.Clear();}
    void Restore(bool reverse){mixerEditing="";EndSongInfoEdit();CancelWaveMove();var src=reverse?redo:undo;var dst=reverse?undo:redo;if(src.Count==0)return;dst.Push(new Snapshot{Chart=(JObject)Project.Chart.DeepClone(),Manifest=(JObject)Project.Manifest.DeepClone(),Files=new Dictionary<string,byte[]>(Project.Files)});var s=src.Pop();Audio.Pause();bool audioChanged=!ReferenceEquals(Project.Files.TryGetValue(Project.AudioPath,out var oldAudio)?oldAudio:null,s.Files.TryGetValue((string)s.Manifest["audio"]?["path"]??"",out var newAudio)?newAudio:null);Project.Chart=s.Chart;Project.Manifest=s.Manifest;Project.Files=s.Files;Project.Dirty=true;QueueEditorRecovery();Project.Rebuild();Editor.Reset();Audio.Pause();Audio.BPM=Project.BPM;Audio.Offset=Project.Offset;Audio.AnchorBeat=Math.Min(Audio.Beat,Project.Length);ResetScheduled();if(audioChanged){busy=true;loaded=false;Audio.Load(Project);}ResetEditorFields();}
    void Edited(){Project.Dirty=true;QueueEditorRecovery();Project.Rebuild();stageGrids.Clear();Audio.BPM=Project.BPM;Audio.Offset=Project.Offset;Audio.CancelFutureDrums();ResetScheduled();if(Audio.Running)foreach(var n in Project.Notes)if(n.Beat<Audio.Beat)scheduled.Add(n.Index);editMeasure=Math.Min(editMeasure,Project.Measures.Length-1);}
    void SelectMeasure(int m,bool scroll=false){int next=Math.Max(0,Math.Min(Project.Measures.Length-1,m));if(Audio.Running&&!scroll&&next==editMeasure)return;editMeasure=Math.Max(0,Math.Min(Project.Measures.Length-1,m));var sig=Project.Meter(editMeasure);meterNumerator=sig.Item1.ToString();meterDenominator=sig.Item2.ToString();if(scroll)editScroll.x=(float)Project.Starts[editMeasure]*EditorPPB*zoom;}
    void ApplyMeter(){
        if(!int.TryParse(meterNumerator,out int n)||!int.TryParse(meterDenominator,out int d)||!Project.CanSetMeter(editMeasure,n,d)){status=T("拍子を確認してください。短くする範囲のノーツ・テンポ変更を先に移動・削除してください。","Check the meter. Move or erase notes and tempo changes beyond the new bar end first.");return;}
        Audio.Pause();PushUndo();Project.SetMeter(editMeasure,n,d);Edited();SelectMeasure(editMeasure);Audio.AnchorBeat=Math.Min(Audio.Beat,Project.Length);status=T("拍子を変更しました","Meter updated");
    }
    void InsertEditorMeasure(){Audio.Pause();PushUndo();Project.InsertMeasure(editMeasure);Edited();SelectMeasure(editMeasure+1,true);Editor.Reset();Audio.AnchorBeat=Project.Starts[editMeasure];}
    void RemoveEditorMeasure(){if(!Project.CanRemoveMeasure(editMeasure)){status=T("ノーツ・テンポ変更のない小節を選んでください","Select a bar without notes or tempo changes");return;}Audio.Pause();PushUndo();Project.RemoveMeasure(editMeasure);Edited();SelectMeasure(editMeasure,true);Editor.Reset();Audio.AnchorBeat=Project.Starts[editMeasure];}
    void ChangeSelected(Action<JObject> change){if(selection.Count==0)return;PushUndo();foreach(int ix in selection){if(ix<Project.Events.Count)change((JObject)Project.Events[ix]);}Edited();}
    void DeleteSelected(){Editor.Delete();}
    void Copy(){Editor.Copy();status=T("コピーしました。選択を解除しました","Copied. Selection cleared");}
    void Paste(){Editor.Paste(Math.Max(0,Audio.Beat));SyncEditorNote();}
    void NewProject(){if(editorProject.Dirty){AskBeforeLeaving(NewProjectNow);return;}NewProjectNow();}
    void NewProjectNow(){ClearEditorDocument();NavigateNow(Page.Edit);status=T("新規プロジェクトを作成しました","Created a new project");}
}
}
