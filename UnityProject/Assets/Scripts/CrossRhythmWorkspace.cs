using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CrossRhythm {
public partial class CrossRhythmApp {
    ChartProject editorProject;
    readonly Dictionary<string,ChartProject> editorDocuments=new Dictionary<string,ChartProject>(StringComparer.OrdinalIgnoreCase);
    bool editorRecoveryPending,editorOpenedByUser;
    double editorRecoveryDue;
    string playSelection="";
    static ChartProject EmptyEditorProject(){var p=ChartProject.Demo();p.SetSongInfo("Untitled","");p.Chart["events"]=new JArray();p.Chart["measures"]=new JArray(4,4,4,4);p.FileName="Untitled.crproj";p.Rebuild();p.Dirty=false;return p;}
    void ActivateProject(ChartProject p){
        if(ReferenceEquals(Project,p))return;
        EndSongInfoEdit();CancelWaveMove();RememberEditorSession();pendingPerformance=null;stageLoadError="";Audio.Stop();Project=p;busy=true;loaded=false;Records.Clear();judged.Clear();ResetScheduled();pendingSongAudio=null;
        if(Current==Page.Songs){pendingSongAudio=p;RequestSongPreview();}else Audio.Load(p);
        editScroll=Vector2.zero;Editor.Reset();editMeasure=0;ResetEditorFields();RestoreEditorSession();
    }
    public static ChartProject CopyForEditor(ChartProject p){
        var copy=new ChartProject{Chart=(JObject)p.Chart.DeepClone(),Manifest=(JObject)p.Manifest.DeepClone(),Files=new Dictionary<string,byte[]>(p.Files),FileName=p.FileName,FilePath=p.FilePath,Baseline=p.Baseline,Dirty=p.Dirty};
        copy.Rebuild();return copy;
    }
    void EditSelectedSong(){
        if(Current!=Page.Songs||!SongActionsAvailable||selected<0||selected>=Library.Count)return;
        OpenEditorProject(CopyForEditor(Library[selected]));
    }
    void OpenEditorProject(ChartProject p,bool restoring=false,JObject metadata=null){
        if(restoring&&editorOpenedByUser)return;
        if(!restoring){RememberEditorSession();PersistEditorSession();editorOpenedByUser=true;if(!string.IsNullOrEmpty(p.FilePath)&&editorDocuments.TryGetValue(p.FilePath,out var existing)&&existing.Dirty)p=existing;}
        if(!string.IsNullOrEmpty(p.FilePath))editorDocuments[p.FilePath]=p;
        editorProject=p;
        if(metadata!=null){p.Baseline=(string)metadata["baseline"]??p.Baseline;p.Dirty=(bool?)metadata["dirty"]??false;editorSessions[p]=new EditorSession{Undo=new Stack<Snapshot>(),Redo=new Stack<Snapshot>(),Beat=(double?)metadata["beat"]??0,Scroll=new Vector2((float?)metadata["scrollX"]??0,(float?)metadata["scrollY"]??0)};zoom=Mathf.Clamp((float?)metadata["zoom"]??1,.25f,8);}
        if(Current==Page.Edit)ActivateProject(p);
        if(!restoring){NavigateNow(Page.Edit);QueueEditorRecovery();}
    }
    void QueueEditorRecovery(){editorRecoveryPending=true;editorRecoveryDue=Time.realtimeSinceStartupAsDouble+.75;}
    void UpdateEditorRecovery(){if(editorRecoveryPending&&!busy&&!Audio.Running&&Time.realtimeSinceStartupAsDouble>=editorRecoveryDue&&!(Current==Page.Edit&&(Editor.Capturing||waveCaptured||editTextFocused)))PersistEditorSession();}
    string EditorRecoveryPath=>Path.Combine(workspaceCheckRoot??Application.persistentDataPath,"editor-session-v2.json");
    void PersistEditorSession(){
        if(editorProject==null||!editorRecoveryPending)return;
        try{
            RememberEditorSession();editorSessions.TryGetValue(editorProject,out var state);
            var meta=new JObject{{"fileName",editorProject.FileName},{"filePath",editorProject.FilePath},{"baseline",editorProject.Baseline},{"dirty",editorProject.Dirty},{"beat",state?.Beat??0},{"scrollX",state?.Scroll.x??0},{"scrollY",state?.Scroll.y??0},{"zoom",zoom}};
            var bytes=editorProject.Write();
#if UNITY_WEBGL && !UNITY_EDITOR
            PlatformFiles.CRSaveEditorSession(bytes,bytes.Length,meta.ToString(Formatting.None));
#else
            meta["bytes"]=Convert.ToBase64String(bytes);var path=EditorRecoveryPath;Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,meta.ToString(Formatting.None));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
#endif
            editorRecoveryPending=false;
        }catch(Exception e){editorRecoveryPending=false;status=T("編集の復元用保存に失敗: ","Editor recovery save failed: ")+e.Message;}
    }
    void RestoreEditorNative(){
        try{if(!File.Exists(EditorRecoveryPath))return;var meta=JObject.Parse(File.ReadAllText(EditorRecoveryPath));var p=ChartProject.Read(Convert.FromBase64String((string)meta["bytes"]),(string)meta["fileName"],(string)meta["filePath"]??"");OpenEditorProject(p,true,meta);}catch(Exception e){status=T("編集中ファイルの復元に失敗: ","Editor restore failed: ")+e.Message;}
    }
    void SavePlaySelection(){if(selected<0||selected>=Library.Count)return;playSelection=Library[selected].FilePath;if(string.IsNullOrEmpty(workspaceCheckRoot)){PlayerPrefs.SetString("playSelection",playSelection);PlayerPrefs.Save();}}
    void SelectRestoredPlay(){int index=Library.FindIndex(p=>p.FilePath==playSelection);SelectProject(index>=0?index:0);}
    void OnApplicationQuit(){PersistEditorSession();}
    public void OnEditorRecoveryError(string message){status=T("編集の復元用保存に失敗: ","Editor recovery save failed: ")+message;}
}
}
