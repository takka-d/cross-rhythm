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
        File.WriteAllText(Path.Combine(workspaceCheckRoot,"arrangement.crproj"),"");
        p.SaveNative(Path.Combine(workspaceCheckRoot,"arrangement-saved.crproj"),false);
        NavigateNow(Page.Songs);yield return null;
    }
}
}
