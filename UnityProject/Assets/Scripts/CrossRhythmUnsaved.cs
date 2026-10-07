using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    Action pendingLeave;
    bool leaveAfterSave,allowApplicationQuit;
    int browserDirty=-1;
    void AskBeforeLeaving(Action next){
        if(discardPrompt)return;
        Audio.Pause();interaction?.Cancel();editorFileOpen=false;if(Event.current!=null)GUIUtility.hotControl=0;
        pendingLeave=next;leaveAfterSave=false;discardPrompt=true;
    }
    void CancelLeaving(){discardPrompt=false;pendingLeave=null;leaveAfterSave=false;}
    void CompleteLeaving(){
        var next=pendingLeave;CancelLeaving();QueueEditorRecovery();PersistEditorSession();next?.Invoke();
    }
    void SaveBeforeLeaving(){leaveAfterSave=true;Save(false);TryFinishLeaving();}
    void TryFinishLeaving(){if(leaveAfterSave&&discardPrompt&&savingProject==null&&!editorProject.Dirty)CompleteLeaving();}
    bool ConfirmApplicationQuit(){
        if(allowApplicationQuit||editorProject==null||!editorProject.Dirty)return true;
        if(Current!=Page.Edit)NavigateNow(Page.Edit);
        AskBeforeLeaving(()=>{allowApplicationQuit=true;Application.Quit();});return false;
    }
    void UpdateUnsavedBrowserGuard(){
#if UNITY_WEBGL && !UNITY_EDITOR
        int dirty=editorProject!=null&&editorProject.Dirty?1:0;
        if(dirty!=browserDirty){browserDirty=dirty;PlatformFiles.CRUnsaved(dirty);}
#endif
    }
    void UnsavedPrompt(){
        if(!discardPrompt)return;
        RectFill(new Rect(0,0,W,H),new Color(0,0,0,.8f));
        var box=new Rect(W/2-330,H/2-142,660,284);RectFill(box,panel);Border(box,mint);
        Text(new Rect(box.x+24,box.y+22,610,34),T("未保存の編集があります","You have unsaved changes"),24);
        FittedText(new Rect(box.x+24,box.y+66,610,28),editorProject.FileName,16,mint);
        Text(new Rect(box.x+24,box.y+100,610,26),T("保存して続けますか？ Leaveでも復元用の編集データは保持します。","Save before leaving? Leave also keeps a recovery draft."),14,muted);
        SaveStatusLine(new Rect(box.x+24,box.y+140,612,36));
        bool available=savingProject==null;
        if(Button(new Rect(box.x+24,box.y+204,168,48),"Cancel",false,available,16))CancelLeaving();
        if(Button(new Rect(box.x+204,box.y+204,168,48),"Leave",false,available,16))CompleteLeaving();
        var save=new Rect(box.x+384,box.y+204,252,48);
#if UNITY_WEBGL && !UNITY_EDITOR
        Button(save,"",true,available,16);
        if(Event.current.type==EventType.Repaint)PlatformFiles.CRProjectButton(fileButtonCount++,save.x/W,save.y/H,save.width/W,save.height/H,5,english?1:0,available?1:0,1,16*scale);
#else
        if(Button(save,"Save & Continue",true,available,16))SaveBeforeLeaving();
#endif
        var e=Event.current;
        if(e.type==EventType.KeyDown&&available&&e.keyCode==KeyCode.Escape){CancelLeaving();e.Use();}
    }
}
}
