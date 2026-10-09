using System;
using System.Collections;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    int loadingPaints;
    bool transitioning,nativeProjectLoading,webPickerOpen;
    string transitionLabel="",loadingFile="";
    bool InputBlocked=>savingProject!=null||transitioning||nativeProjectLoading||nativePickerOpen||webPickerOpen||importBatch||incoming!=null||pendingPerformance!=null||(Current==Page.Edit&&busy);

    public void OnPickerState(string state){webPickerOpen=state=="open";if(webPickerOpen)PrepareExclusiveOperation();}
    void PrepareExclusiveOperation(){
        Audio.Pause();ReleaseInputs();EndSongInfoEdit();CancelWaveMove();interaction?.Cancel();
        if(interaction!=null)interaction.ContextOpen=false;
        editorFileOpen=false;laneTypeRow=-1;menuActivate="";padMenu.Clear();GUI.FocusControl(null);
    }
    void Transition(Action action,string destination){
        if(InputBlocked)return;
        PrepareExclusiveOperation();transitioning=true;transitionLabel=destination;
        StartCoroutine(TransitionRoutine(action));
    }
    IEnumerator TransitionRoutine(Action action){
        // Let the loading page reach a rendered frame before preparing the destination.
        yield return null;yield return null;
        try{action();}catch(Exception e){status=e.Message;}
        finally{transitioning=false;transitionLabel="";}
    }
    void RequestBegin(bool practice){Transition(()=>Begin(practice),practice?"Practice":"Play");}
    void RequestEditSelected(){Transition(EditSelectedSong,"Edit");}

    void LoadingPage(){
        if(Event.current.type==EventType.Repaint)loadingPaints++;
#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CRProjectButtonsBegin();PlatformFiles.CRProjectButtonsEnd();
        PlatformFiles.CRKeyboardFileMode(-1);PlatformFiles.CREditorKeys(0,0);
        PlatformFiles.CRDisplayLayout(0,0,0,0,english?1:0,0);
#endif
        menuItems.Clear();menuActivate="";
        var box=new Rect((W-620)/2,(H-236)/2,620,236);
        Text(new Rect(box.x,box.y-56,620,36),"CROSS RHYTHM",23,mint,true);
        RectFill(box,panel);Border(box,line);
        bool save=savingProject!=null;
        string heading=save?T("保存中…","Saving…"):nativePickerOpen?T("フォルダーを選択してください","Choose a folder"):T("読み込み中…","Loading…");
        Text(new Rect(box.x+28,box.y+25,564,38),heading,30,Color.white,true);
        string detail=save?savingProject.FileName:transitioning?transitionLabel:incoming!=null?incomingName:importBatch?T("プロジェクトフォルダー","Project folder")+" · "+importedCount:!string.IsNullOrEmpty(loadingFile)?loadingFile:Project.FileName;
        FittedText(new Rect(box.x+28,box.y+78,564,30),detail,18,mint);
        string message=save?T("書き込みの確認が終わるまでお待ちください。","Please wait while the file is saved and verified."):pendingPerformance!=null?T("準備が終わるとカウントを開始します。","Count-in starts when the audio is ready."):T("準備が終わるまでお待ちください。","Please wait while the content is prepared.");
        Text(new Rect(box.x+28,box.y+123,564,26),message,16,muted);
        var bar=new Rect(box.x+28,box.y+175,564,4);RectFill(bar,line);
        float phase=(float)(Time.realtimeSinceStartupAsDouble*.6%1);
        RectFill(new Rect(bar.x+phase*(bar.width-112),bar.y,112,4),mint);
        if(pendingPerformance!=null){
            if(Button(new Rect(box.x+210,box.yMax+24,200,44),"Back"))NavigateNow(Page.Songs);
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape)NavigateNow(Page.Songs);
        }
        if(Event.current.isKey||Event.current.isMouse||Event.current.type==EventType.ScrollWheel||Event.current.type==EventType.ContextClick)Event.current.Use();
    }
}
}
