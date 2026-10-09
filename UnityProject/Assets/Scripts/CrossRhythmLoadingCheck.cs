using System;
using System.IO;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator WaitForOperations(){
        double deadline=Time.realtimeSinceStartupAsDouble+30;
        while(InputBlocked&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        if(InputBlocked)throw new Exception("Operation did not finish within 30 seconds");
    }
    IEnumerator LoadingRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();p.SaveNative(Path.Combine(workspaceCheckRoot,"blocking-save.crproj"),false);
        OpenEditorProject(p);yield return WaitForOperations();p.SetSongInfo("Verified asynchronous save","Artist");
        p.Files["qa-padding.bin"]=new byte[4*1024*1024];p.Dirty=true;
        int before=Time.frameCount;OnSaveButton("Save");
        check(InputBlocked&&savingProject==p&&saveState==SaveState.Saving,"Save blocks immediately before writing starts");
        OnEditorShortcut("Undo");OnEditorShortcut("Paste");Navigate(Page.Songs);NavigateNow(Page.Config);OnSaveButton("SaveAs");
        check(Project==p&&Current==Page.Edit&&p.SongTitle=="Verified asynchronous save"&&savingProject==p,"in-flight save rejects shortcuts, navigation and duplicate Save");
        check(!ConfirmApplicationQuit()&&!discardPrompt,"window close cannot interrupt an in-flight save");
        yield return WaitForOperations();
        check(Time.frameCount>=before+2,"save yields frames before writing so a visible window can repaint");
        var saved=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName);
        check(saveState==SaveState.Saved&&!p.Dirty&&saved.SongTitle==p.SongTitle&&saved.Files["qa-padding.bin"].Length==4*1024*1024,"save confirmation follows archive write and readback");
        check(!InputBlocked&&savingProject==null,"successful save releases the operation block");
        string path=p.FilePath;File.AppendAllText(path,"outside edit");p.SetSongInfo("Keep conflict draft","");Save(false);yield return WaitForOperations();
        check(saveState==SaveState.Error&&p.Dirty&&p.SongTitle=="Keep conflict draft"&&!InputBlocked,"external-file conflict preserves draft and releases controls");
        check(File.ReadAllText(path).EndsWith("outside edit"),"failed conflict save does not overwrite external change");
        p.FilePath=Path.Combine(workspaceCheckRoot,"missing-parent-0325","failed.crproj");Save(false);yield return WaitForOperations();
        check(saveState==SaveState.Error&&p.Dirty&&savingProject==null,"native write failure keeps edits and releases the block");
        p.FilePath=path;savingProject=p;SetSaveState(SaveState.Saving);OnFileError("Save cancelled");
        check(saveState==SaveState.Cancelled&&!InputBlocked&&p.Dirty,"cancelled save returns to the retained draft");
        ClearEditorDocument();yield return WaitForOperations();
        before=Time.frameCount;Navigate(Page.Config);
        check(transitioning&&Current==Page.Edit&&InputBlocked,"screen transition blocks before preparing destination");
        Navigate(Page.Songs);yield return WaitForOperations();
        check(Current==Page.Config&&Time.frameCount>=before+2&&!transitioning,"transition yields for loading view and ignores a duplicate navigation");
        var read=EmptyEditorProject();string readPath=Path.Combine(workspaceCheckRoot,"async-open.crproj");read.SetSongInfo("Async project read","");read.SaveNative(readPath,false);
        before=Time.frameCount;StartCoroutine(LoadNativeProject(readPath));
        check(nativeProjectLoading&&InputBlocked,"existing project open blocks during asynchronous read");
        yield return WaitForOperations();
        check(Current==Page.Edit&&Project.SongTitle=="Async project read"&&Time.frameCount>=before+2,"existing project open yields for loading and activates parsed project");
        StartCoroutine(LoadNativeProject(readPath+".missing"));yield return WaitForOperations();
        check(!nativeProjectLoading&&!InputBlocked&&Project.SongTitle=="Async project read","failed project read retains current document and releases controls");
        OnImportStart(new JObject{{"name","Transfer.crproj"},{"token","qa-transfer"},{"size",100},{"kind","editor"}}.ToString());
        check(InputBlocked&&incoming!=null,"browser project transfer blocks editor while bytes arrive");OnFileError("Transfer failed");
        check(!InputBlocked&&incoming==null,"failed browser transfer releases memory and controls");
        OnPickerState("open");check(InputBlocked,"browser picker state blocks underlying canvas");OnPickerState("closed");check(!InputBlocked,"cancelled browser picker releases canvas");
        File.WriteAllText(Path.Combine(workspaceCheckRoot,"loading-render-evidence.json"),new JObject{{"nativeHiddenWindow",true},{"observedRepaints",loadingPaints},{"visualVerification","separate browser checks"}}.ToString());
        NavigateNow(Page.Songs);FocusSong(1);
        check(!InputBlocked&&SongActionsAvailable,"background Songs preview does not show blocking loading screen");
        yield return null;
    }
}
}
