using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator RefreshRuntimeCheck(Action<bool,string> check){
        OpenEditorProject(EmptyEditorProject());yield return WaitForOperations();
        var p=Project;var original=(JObject)p.Chart.DeepClone();
        foreach(int q in new[]{12,20,28,36,44,52,60,17,1024}){
            gridMenuOpen=true;SetEditorGrid(q);check(!gridMenuOpen&&Math.Abs(p.Grid-4.0/q)<1e-10,"grid dropdown selection sets exact interval 1/"+q);
            var at=new Vector2((float)(4.0/q*EditorPPB*1.5),EditorRowHeight*3.5f);Editor.Down(at,0,1,false,false);Editor.Up(at);
            check(p.Notes.Any(n=>Math.Abs(n.Beat-4.0/q)<1e-9),"grid selection drives pointer placement 1/"+q);
            Restore(false);Restore(false);check(JToken.DeepEquals(p.Chart,original),"grid change and note placement undo independently 1/"+q);
        }
        SetEditorGrid(0);SetEditorGrid(1025);check(JToken.DeepEquals(p.Chart,original),"invalid custom spacing cannot change chart");
        gridMenuOpen=true;OnEditorShortcut("Paste");check(p.Notes.Count==0,"grid popup blocks editor shortcuts");gridMenuOpen=false;
        savingProject=p;check(LoadingIsDialog,"Save retains the source page behind a dialog");savingProject=null;
        nativeProjectLoading=true;check(LoadingIsDialog,"project loading retains the source page behind a dialog");nativeProjectLoading=false;
        transitioning=true;check(!LoadingIsDialog,"navigation uses the full loading screen");transitioning=false;
        string previousPath=projectPath,previousSelection=playSelection;
        string folder=Path.Combine(workspaceCheckRoot,"reload-library"),a=Path.Combine(folder,"A.crproj"),b=Path.Combine(folder,"B.CRPROJ"),broken=Path.Combine(folder,"Broken.crproj");Directory.CreateDirectory(folder);
        try{
            var first=EmptyEditorProject();first.SetSongInfo("Track A","");first.SaveNative(a,false);
            projectPath=folder;NavigateNow(Page.Songs);check(libraryRefreshing&&InputBlocked&&LoadingIsDialog,"entering Songs starts a blocking folder refresh dialog");yield return WaitForOperations();
            check(Library.Count==2&&Library.Any(v=>v.Title=="Track A"),"Songs discovers files from its own project folder");FocusSong(1);yield return WaitForOperations();
            NavigateNow(Page.Edit);yield return WaitForOperations();var editing=editorProject;editing.SetSongInfo("Unrelated editor draft","");var chart=(JObject)editing.Chart.DeepClone();var history=undo;
            var added=EmptyEditorProject();added.SetSongInfo("Newly saved B","");added.SaveNative(b,false);
            first.SetSongInfo("A updated outside Songs","");first.SaveNative(a,false);
            NavigateNow(Page.Songs);yield return WaitForOperations();
            check(Library.Count==3&&Library.Any(v=>v.Title=="Newly saved B"),"every Songs entry discovers newly saved and uppercase-extension projects");
            check(Project.Title=="A updated outside Songs"&&SameProjectFile(Project.FilePath,a),"refresh updates selected file while preserving its selection");
            check(editorProject==editing&&editing.Dirty&&JToken.DeepEquals(editing.Chart,chart),"folder refresh preserves independent dirty Edit document");
            NavigateNow(Page.Edit);yield return WaitForOperations();check(undo==history&&Project==editing,"folder refresh preserves editor undo session");
            File.Delete(a);NavigateNow(Page.Songs);yield return WaitForOperations();check(Library.Count==2&&!Library.Any(v=>SameProjectFile(v.FilePath,a)),"refresh removes deleted projects without stale cards");
            File.WriteAllText(broken,"invalid archive");var retained=Library;NavigateNow(Page.Config);NavigateNow(Page.Songs);yield return WaitForOperations();
            check(ReferenceEquals(Library,retained)&&status.Contains(T("一覧を保持","Previous list retained")),"failed folder read retains previous list and reports failure");ProjectAudioReady("");check(libraryRefreshFailure!=""&&LibraryRefreshMessage().Contains(T("一覧を保持","Previous list retained")),"audio-ready callback cannot erase the folder failure notice");File.Delete(broken);
            NavigateNow(Page.Config);NavigateNow(Page.Songs);yield return WaitForOperations();check(Library.Count==2&&!InputBlocked&&libraryRefreshFailure=="","refresh recovers on next visit without duplicate cards");
            File.Delete(b);NavigateNow(Page.Config);NavigateNow(Page.Songs);yield return WaitForOperations();check(Library.Count==1&&Project.FilePath=="","empty folder refresh leaves only the test track");
            OnImportBatch("refresh");AddProject(first.Write(),"A.crproj","web-A");OnLibrarySource(new JObject{{"kind","folder"},{"name","QA"},{"errors",new JArray("read failed")}}.ToString());
            check(Library.Count==1&&!importBatch&&!refreshBatch,"Web refresh failure discards partial replacement");
            OnImportBatch("refresh");AddProject(first.Write(),"A.crproj","web-A");OnLibrarySource(new JObject{{"kind","folder"},{"name","QA"}}.ToString());
            check(Library.Count==2&&Library.Any(v=>v.FilePath=="web-A"),"Web refresh commits a complete replacement batch");
        }finally{projectPath=previousPath;playSelection=previousSelection;}
    }
}
}
