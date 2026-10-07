using System;
using UnityEngine;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    enum SaveState {None,Saving,Saved,Download,Cancelled,Error}
    SaveState saveState;
    ChartProject saveStatusProject;
    ProjectSaveSnapshot savingSnapshot;
    string saveDetail="",saveTime="";
    void SetSaveState(SaveState state,string detail=""){saveState=state;saveStatusProject=savingProject??Project;saveDetail=detail;saveTime=DateTime.Now.ToString("HH:mm:ss");}
    void ClearSave(){savingProject=null;savingBytes=null;savingSnapshot=null;}
    public void OnSaveButton(string mode){if(mode=="Continue"&&discardPrompt){SaveBeforeLeaving();return;}if(Current!=Page.Edit||showMeterPanel||Editor.ContextOpen||discardPrompt)return;Save(mode=="SaveAs");}
    void SaveButton(Rect r,bool saveAs){
        string caption=saveAs?"Save As":"Save";bool enabled=GUI.enabled&&savingProject==null&&!busy;
#if UNITY_WEBGL && !UNITY_EDITOR
        Button(r,"",!saveAs,enabled,16);
        if(Event.current.type==EventType.Repaint)PlatformFiles.CRProjectButton(fileButtonCount++,r.x/W,r.y/H,r.width/W,r.height/H,saveAs?4:3,english?1:0,enabled?1:0,saveAs?0:1,16*scale);
#else
        if(Button(r,caption,!saveAs,enabled,16))Save(saveAs);
#endif
    }
    void SaveStatusLine(Rect r){
        bool relevant=saveStatusProject==Project;string message;Color color=muted;
        if(relevant&&saveState==SaveState.Saving){message=T("保存中… 完了までお待ちください","Saving… Please wait for confirmation");color=mint;}
        else if(relevant&&saveState==SaveState.Error){message=T("保存失敗: ","Save failed: ")+saveDetail;color=C("#ff9e9e");}
        else if(relevant&&saveState==SaveState.Download){message=T("コピーのダウンロード開始。元ファイルは未更新。保存先で.crprojを確認してください。","Copy download started. Original not updated. Check the .crproj in your downloads.");color=C("#edc779");}
        else if(relevant&&saveState==SaveState.Cancelled){message=T("保存をキャンセルしました。編集内容は保持しています。","Save cancelled. Your edits are still here.");color=C("#edc779");}
        else if(Project.Dirty){message=T("未保存の変更があります","Unsaved changes")+(relevant&&saveState==SaveState.Saved?T(" · 前回保存 "," · Last saved ")+saveTime:"");color=C("#edc779");}
        else if(relevant&&saveState==SaveState.Saved){message=T("保存済み ","Saved ")+saveTime+" · "+Project.FileName+T(" · 書き込み確認済み"," · Write verified");color=mint;}
        else message=T("変更なし","No unsaved changes")+" · "+Project.FileName;
        RectFill(r,panel);FittedText(new Rect(r.x+10,r.y,r.width-20,r.height),message,14,color);
    }
    public void OnFileError(string error){
        status=error=="Cancelled"?T("キャンセルしました","Cancelled"):error;
        if(savingProject!=null){SetSaveState(error=="Save cancelled"?SaveState.Cancelled:SaveState.Error,error);ClearSave();}
    }
    public void OnSaved(string json){
        if(savingProject==null)return;
        try{
            var r=JObject.Parse(json);
            if((string)r["status"]=="saved"){
                savingProject.Baseline=ChartProject.Hash(savingBytes);
                savingProject.FilePath=(string)r["token"]??savingProject.FilePath;
                savingProject.FileName=(string)r["name"]??savingProject.FileName;
                savingProject.Dirty=!savingSnapshot.Matches(savingProject);
                SetSaveState(SaveState.Saved);QueueEditorRecovery();PersistEditorSession();status=T("保存しました: ","Saved: ")+savingProject.FileName;
            }else{SetSaveState(SaveState.Download);status=T("ダウンロード先で保存を確認してください。元ファイルは未更新です。","Check the downloaded file. The original has not been overwritten.");}
        }catch(Exception e){SetSaveState(SaveState.Error,e.Message);status=e.Message;}
        finally{ClearSave();TryFinishLeaving();}
    }
    void Save(bool saveAs){
        if(savingProject!=null)return;
        try{
            savingProject=Project;SetSaveState(SaveState.Saving);status=T("保存中…","Saving…");
#if UNITY_WEBGL && !UNITY_EDITOR
            savingSnapshot=new ProjectSaveSnapshot(Project);savingBytes=Project.Write();
            PlatformFiles.CRSave(savingBytes,savingBytes.Length,Project.FileName,Project.FilePath,Project.Baseline,gameObject.name,saveAs?1:0);
#else
            string path=Project.FilePath;
            if(saveAs||string.IsNullOrEmpty(path))path=PlatformFiles.Pick(true,Project.FileName);
            if(path==null){SetSaveState(SaveState.Cancelled);ClearSave();return;}
            Project.SaveNative(path,!saveAs);QueueEditorRecovery();PersistEditorSession();SetSaveState(SaveState.Saved);status=T("保存しました: ","Saved: ")+Project.FileName;ClearSave();
#endif
        }catch(Exception e){SetSaveState(SaveState.Error,e.Message);status=T("保存エラー: ","Save error: ")+e.Message;ClearSave();}
    }
}
}
