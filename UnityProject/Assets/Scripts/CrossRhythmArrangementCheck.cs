using System;
using System.Linq;
using System.IO;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator ArrangementRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();p.Chart["measures"]=new JArray(Enumerable.Repeat(4,200));
        for(int i=0;i<6000;i++)p.Events.Add(new JObject{{"measure",i/32},{"beat",(i%32)*.125},{"instrument","HH"},{"id","perf-"+i},{"velocity",3}});
        p.Rebuild();OpenEditorProject(p);double end=Time.realtimeSinceStartupAsDouble+30;
        while((busy||!loaded)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
        check(loaded&&!busy,"arrangement test editor ready");
        var watch=System.Diagnostics.Stopwatch.StartNew();for(int i=0;i<40;i++){var oldSnapshot=p.Chart.DeepClone();}watch.Stop();double previous=watch.Elapsed.TotalMilliseconds;
        int before=undo.Count;var first=p.Notes[0];watch.Restart();
        for(int i=1;i<=40;i++)SetSongInfoField("title",new string('a',i));watch.Stop();double grouped=watch.Elapsed.TotalMilliseconds;
        check(undo.Count==before+1&&p.SongTitle.Length==40&&ReferenceEquals(p.Notes[0],first),"40 title edits create one snapshot without rebuilding notes");
        SetSongInfoField("artist","Artist with spaces 日本語");check(undo.Count==before+2,"changing metadata field begins a separate undo group");
        Restore(false);check(p.Artist==""&&p.SongTitle.Length==40,"artist undo preserves title group");
        Restore(false);check(p.SongTitle=="Untitled","title undo restores complete prior value");
        Restore(true);Restore(true);check(p.Artist=="Artist with spaces 日本語","metadata redo restores text");
        QueueEditorRecovery();editorRecoveryDue=0;editTextFocused=true;UpdateEditorRecovery();
        check(editorRecoveryPending,"recovery archive is deferred while typing");
        editTextFocused=false;UpdateEditorRecovery();check(!editorRecoveryPending&&File.Exists(EditorRecoveryPath),"recovery saves when typing focus leaves");
        string notes=p.Events.ToString();p.Chart["editorSnapToGrid"]=false;waveCaptured=waveMoved=true;wavePreviewOffset=-.731;CommitWaveMove();
        check(p.Offset==-.731&&Audio.Offset==-.731&&p.Events.ToString()==notes,"wave drop updates playback offset without altering notes");
        Restore(false);check(p.Offset==0&&Audio.Offset==0,"wave move undo restores playback timing");Restore(true);
        var saved=ChartProject.Read(p.Write(),"arrangement.crproj");check(saved.Offset==-.731&&!saved.SnapToGrid,"audio placement and Free mode survive save reload");
        int snapshots=undo.Count;waveCaptured=waveMoved=true;wavePreviewOffset=3;CancelWaveMove();
        check(undo.Count==snapshots&&p.Offset==-.731,"cancelled wave preview does not modify history or timing");
        File.WriteAllText(Path.Combine(workspaceCheckRoot,"metadata-performance.json"),new JObject{{"notes",6000},{"characters",40},{"previousSnapshotMs",previous},{"groupedEditMs",grouped},{"undoGroups",1}}.ToString());
        p.SaveNative(Path.Combine(workspaceCheckRoot,"arrangement-saved.crproj"),false);
        int librarySize=Library.Count,selectedBefore=selected;Library.Add(CopyForEditor(p));int savedIndex=Library.Count-1;
        string previousTitle=Library[savedIndex].SongTitle;
        SetSongInfoField("title","Saved song title");SetSongInfoField("artist","Saved artist");
        check(Library[savedIndex].SongTitle==previousTitle,"unsaved metadata remains isolated from Songs");
        OnSaveButton("Save");
        var onDisk=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName,p.FilePath);
        check(saveState==SaveState.Saved&&!p.Dirty&&onDisk.SongTitle=="Saved song title"&&onDisk.Artist=="Saved artist","Save button writes title and artist to the existing Windows file");
        check(Library.Count==librarySize+1&&selected==selectedBefore&&Library[savedIndex].SongTitle==onDisk.SongTitle&&Library[savedIndex].Artist==onDisk.Artist,"verified save refreshes existing Songs entry without changing membership or selection");
        check(!ReferenceEquals(Library[savedIndex],p),"saved Songs entry stays independent of Edit document");
        byte[] written=p.Write();SetSongInfoField("artist","Unsaved later edit");RefreshSavedLibrary(p,written);
        check(Library[savedIndex].Artist=="Saved artist"&&p.Artist=="Unsaved later edit"&&p.Dirty,"asynchronous save refresh uses written snapshot not later unsaved edits");
        var unrelated=CopyForEditor(p);unrelated.FilePath=Path.Combine(workspaceCheckRoot,"not-in-songs.crproj");unrelated.SetSongInfo("Unlisted edit","");RefreshSavedLibrary(unrelated);
        check(Library.Count==librarySize+1&&Library[savedIndex].SongTitle=="Saved song title","saving unrelated Edit file does not add or replace Songs entry");
        Restore(false);selected=savedIndex;NavigateNow(Page.Songs);end=Time.realtimeSinceStartupAsDouble+30;while((busy||!loaded)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
        check(Project.SongTitle=="Saved song title"&&Project.Artist=="Saved artist","returning to Songs immediately shows saved metadata");
        NavigateNow(Page.Edit);end=Time.realtimeSinceStartupAsDouble+30;while((busy||!loaded)&&Time.realtimeSinceStartupAsDouble<end)yield return null;
        check(ReferenceEquals(Project,p)&&p.SongTitle=="Saved song title","returning to Edit preserves its document after Songs refresh");
        NavigateNow(Page.Songs);yield return null;
    }
}
}
