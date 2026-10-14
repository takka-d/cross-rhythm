using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool nativeStartup,nativeStartupCancelled,nativeStartupCompleted,nativeStartupPaintRequired;
    bool nativeStartupExternalOverlay;
    int nativeStartupPaints,nativeStartupRequestFrame=-1,nativeStartupRestoreFrame=-1,nativeStartupPaintsBeforeRestore;
    string nativeStartupDetail="",nativeStartupReadyEvent="",nativeStartupStatusPath="";

    IEnumerator NativeStartupRoutine(string[] args){
        bool previousRunInBackground=Application.runInBackground;
        Application.runInBackground=true;
        nativeStartupReadyEvent=StartupArgument(args,"--startup-ready-event");
        nativeStartupStatusPath=StartupArgument(args,"--startup-status-file");
        nativeStartupExternalOverlay=!string.IsNullOrEmpty(nativeStartupReadyEvent);
        nativeStartup=true;nativeStartupCancelled=false;nativeStartupCompleted=false;nativeStartupPaints=0;
        nativeStartupPaintRequired=string.IsNullOrEmpty(workspaceCheckRoot)&&!Application.isBatchMode;
        nativeStartupRequestFrame=Time.frameCount;nativeStartupRestoreFrame=-1;nativeStartupPaintsBeforeRestore=0;
        ReportNativeStartup(T("画面を準備しています","Preparing the window"));

        // Awake must finish before any saved project is read. This gives the native
        // window a real repaint opportunity instead of appearing frozen at launch.
        yield return null;
        if(nativeStartupPaintRequired){
            while(!nativeStartupCancelled&&nativeStartupPaints==0)yield return null;
        }else{
            int paintDeadline=nativeStartupRequestFrame+4;
            while(!nativeStartupCancelled&&nativeStartupPaints==0&&Time.frameCount<paintDeadline)yield return null;
        }
        if(nativeStartupCancelled)yield break;

        nativeStartupRestoreFrame=Time.frameCount;
        nativeStartupPaintsBeforeRestore=nativeStartupPaints;
        ReportNativeStartup(T("プロジェクトフォルダーを復元しています","Restoring the project folder"));
        yield return RestoreLibraryAtStartup();
        if(nativeStartupCancelled)yield break;

        ReportNativeStartup(T("編集中のファイルを復元しています","Restoring the editor file"));
        yield return null;
        if(nativeStartupCancelled)yield break;
        RestoreEditorNative();

        int at=Array.IndexOf(args,"--project-file");
        if(at>=0&&at+1<args.Length){
            ReportNativeStartup(T("指定されたプロジェクトを開いています","Opening the requested project"));
            yield return null;
            if(nativeStartupCancelled)yield break;
            ImportNative(args[at+1]);
        }

        // Restoring or explicitly opening an editor project can start
        // asynchronous audio decoding. Keep the external startup surface in
        // front until every startup load has completed, so it cannot hand off
        // to a second Unity loading dialog. Close remains responsive through
        // the launcher while the player is busy.
        while(!nativeStartupCancelled&&busy)yield return null;
        if(nativeStartupCancelled)yield break;

        ReportNativeStartup(T("準備が完了しました","Ready"));
        nativeStartupCompleted=true;nativeStartup=false;
        // Paint the ready destination behind the external startup screen first.
        // The launcher only disappears after this frame, so no second loading
        // page or blank Unity window flashes between startup and the title.
        yield return null;
        SignalNativeStartupReady();
        Application.runInBackground=previousRunInBackground;
        StartNativeChecks(args);
    }

    static string StartupArgument(string[] args,string name){
        int at=Array.IndexOf(args,name);
        return at>=0&&at+1<args.Length?args[at+1]:"";
    }

    void ReportNativeStartup(string value){
        nativeStartupDetail=value;
        if(string.IsNullOrEmpty(nativeStartupStatusPath))return;
        try{File.WriteAllText(nativeStartupStatusPath,value,Encoding.UTF8);}catch(Exception){}
    }

    void SignalNativeStartupReady(){
        if(string.IsNullOrEmpty(nativeStartupReadyEvent))return;
        try{using(var ready=EventWaitHandle.OpenExisting(nativeStartupReadyEvent))ready.Set();}catch(Exception){}
    }

    IEnumerator RestoreLibraryAtStartup(){
        if(!Directory.Exists(projectPath))yield break;
        if(string.IsNullOrEmpty(workspaceCheckRoot)){PlayerPrefs.SetString("projects",projectPath);PlayerPrefs.Save();}
        OnImportBatch("restore");
        IEnumerable<string> files;
        try{files=Directory.EnumerateFiles(projectPath,"*.crproj",SearchOption.AllDirectories);}
        catch(Exception e){importErrors.Add(e.Message);OnLibrarySource(new JObject{{"kind","folder"},{"name",projectPath}}.ToString());yield break;}

        using(var paths=files.GetEnumerator()){
            while(!nativeStartupCancelled){
                bool hasNext;
                try{hasNext=paths.MoveNext();}
                catch(Exception e){importErrors.Add(e.Message);break;}
                if(!hasNext)break;
                ReportNativeStartup(T("読込中: ","Loading: ")+Path.GetFileName(paths.Current));
                ImportNative(paths.Current);
                // Keep the startup window responsive and make Close effective between files.
                yield return null;
            }
        }
        if(!nativeStartupCancelled)OnLibrarySource(new JObject{{"kind","folder"},{"name",projectPath}}.ToString());
    }

    void StartNativeChecks(string[] args){
#if !UNITY_WEBGL || UNITY_EDITOR
        if(!string.IsNullOrEmpty(workspaceCheckRoot)){Application.runInBackground=true;if(!args.Contains("--interactive-check"))StartCoroutine(args.Contains("--tasks-check")?TasksCheck():args.Contains("--practice-draft-check")?PracticeDraftCheck():WorkspaceCheck());}
        if(args.Contains("--revision-check")){Application.runInBackground=true;StartCoroutine(RevisionCheck());}
        if(args.Contains("--editor-audio-check")){Application.runInBackground=true;StartCoroutine(EditorAudioCheck());}
        if(args.Contains("--smoke-test")){Application.runInBackground=true;StartCoroutine(SmokeTest());}
#endif
    }

    void CancelNativeStartup(){
        if(!nativeStartup)return;
        nativeStartupCancelled=true;allowApplicationQuit=true;Application.Quit();
    }

    void NativeStartupPage(){
        if(Event.current.type==EventType.Repaint)nativeStartupPaints++;
        menuItems.Clear();menuActivate="";
        if(nativeStartupExternalOverlay){
            var blocked=Event.current;
            if(blocked.isKey||blocked.isMouse||blocked.type==EventType.ScrollWheel||blocked.type==EventType.ContextClick)blocked.Use();
            return;
        }
        var box=new Rect((W-680)/2,(H-304)/2,680,304);
        Text(new Rect(box.x,box.y-62,680,42),"CROSS RHYTHM",28,mint,true);
        RectFill(box,panel);Border(box,line);
        Text(new Rect(box.x+32,box.y+30,616,42),T("Cross Rhythm を起動中…","Starting Cross Rhythm..."),30,Color.white,true);
        FittedText(new Rect(box.x+32,box.y+91,616,30),nativeStartupDetail,17,mint);
        Text(new Rect(box.x+32,box.y+132,616,25),T("保存した状態を確認しています。","Checking your saved workspace."),15,muted);
        var bar=new Rect(box.x+32,box.y+180,616,4);RectFill(bar,line);
        float phase=(float)(Time.realtimeSinceStartupAsDouble*.6%1);
        RectFill(new Rect(bar.x+phase*(bar.width-120),bar.y,120,4),mint);
        if(Button(new Rect(box.xMax-188,box.yMax-72,156,42),"Close"))CancelNativeStartup();
        var e=Event.current;
        if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){CancelNativeStartup();e.Use();}
        else if(e.isKey||e.isMouse||e.type==EventType.ScrollWheel||e.type==EventType.ContextClick)e.Use();
    }
}
}
