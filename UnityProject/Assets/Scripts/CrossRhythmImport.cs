using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool importBatch,restoreBatch;int importedIndex=-1,importedCount;
    readonly List<string> importErrors=new List<string>();
    sealed class EditorSession {public Stack<Snapshot> Undo,Redo;public double Beat;public Vector2 Scroll;}
    readonly Dictionary<ChartProject,EditorSession> editorSessions=new Dictionary<ChartProject,EditorSession>();
    void RememberEditorSession(){if(Project!=null&&ReferenceEquals(Project,editorProject))editorSessions[Project]=new EditorSession{Undo=undo,Redo=redo,Beat=Math.Max(0,Audio.Beat),Scroll=editScroll};}
    void RestoreEditorSession(){undo=new Stack<Snapshot>();redo=new Stack<Snapshot>();if(editorSessions.TryGetValue(Project,out var state)){undo=state.Undo;redo=state.Redo;editScroll=state.Scroll;Audio.AnchorBeat=state.Beat;}if(Current==Page.Songs)RequestSongPreview();}
    System.Collections.IEnumerator PickFolderNative(){
        if(nativePickerOpen)yield break;nativePickerOpen=true;
        var task=NativeFolderPicker.PickAsync();while(!task.IsCompleted)yield return null;
        nativePickerOpen=false;if(task.IsFaulted){status=task.Exception.GetBaseException().Message;yield break;}if(task.Result==null)yield break;
        projectPath=task.Result;PlayerPrefs.SetString("projects",projectPath);PlayerPrefs.Save();
        OnImportBatch("open");var paths=System.IO.Directory.EnumerateFiles(projectPath,"*.crproj",System.IO.SearchOption.AllDirectories).GetEnumerator();
        while(true){string path=null;try{if(paths.MoveNext())path=paths.Current;}catch(Exception e){importErrors.Add(e.Message);}if(path==null)break;ImportNative(path);yield return null;}
        paths.Dispose();OnLibrarySource(new JObject{{"kind","folder"},{"name",projectPath}}.ToString());
    }
    bool nativePickerOpen;
    public void OnImportProgress(string json){try{var p=JObject.Parse(json);status=T("読込中: ","Loading: ")+(string)p["path"]+"  "+(string)p["count"];}catch{}}
    public void OnImportBatch(string mode){importBatch=true;restoreBatch=mode=="restore";importedIndex=-1;importedCount=0;importErrors.Clear();if(Current!=Page.Edit)Audio.Pause();Library.Clear();Library.Add(ChartProject.Demo());selected=0;}
    public void OnLibrarySource(string json){
        var info=JObject.Parse(json);bool restoring=restoreBatch;importBatch=false;restoreBatch=false;
        if(info["errors"] is JArray errors)foreach(var error in errors)importErrors.Add((string)error);
        if((string)info["kind"]=="folder"){lastSource=info;if(string.IsNullOrEmpty(workspaceCheckRoot)){PlayerPrefs.SetString("projectSource",info.ToString(Newtonsoft.Json.Formatting.None));PlayerPrefs.Save();}}
        if(restoring)SelectRestoredPlay();else SelectProject(importedIndex>=0?importedIndex:0);
        string result=T("読込: ","Opened: ")+importedCount+T("曲"," tracks");
        if(importErrors.Count>0)result+=" / "+T("読込失敗: ","Failed: ")+string.Join("; ",importErrors);
        if(!restoring||importErrors.Count>0)status=result;
    }
}
}
