using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool libraryRefreshing;string libraryRefreshFailure="";
    void RefreshSongFolder(){
        if(libraryRefreshing||importBatch)return;
#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CRReloadLibrary(gameObject.name);
#else
        if(!string.IsNullOrEmpty(projectPath))StartCoroutine(ReloadLibraryNative(projectPath));
#endif
    }
    public void OnLibraryReloadState(string state){libraryRefreshing=state=="start";if(libraryRefreshing){libraryRefreshFailure="";Audio.Pause();loadingFile=SourceLine();}else loadingFile="";}
    public void OnLibraryReloadUnavailable(string reason){libraryRefreshFailure=reason;status=LibraryRefreshMessage();}
    public void OnLibraryCacheError(string reason){libraryRefreshFailure="cache:"+reason;status=LibraryRefreshMessage();}
    string LibraryRefreshMessage(){string reason=libraryRefreshFailure;return reason.StartsWith("cache:")?T("一覧は更新しましたがブラウザー内の保存に失敗しました: ","Tracks refreshed, but browser storage failed: ")+reason.Substring(6):reason=="permission"?T("フォルダーの再読込にはOpen Folderでアクセスを許可してください。","Use Open Folder to grant access and refresh tracks."):reason=="unsupported"?T("このブラウザーでは再読込時にOpen Folderで同じフォルダーを選び直してください。","This browser requires selecting the same folder with Open Folder to refresh tracks."):T("フォルダーの更新に失敗しました。一覧を保持しています: ","Folder refresh failed. Previous list retained: ")+reason;}
    IEnumerator ReloadLibraryNative(string path){
        if(libraryRefreshing)yield break;OnLibraryReloadState("start");yield return null;yield return null;
        // Build a complete replacement off the UI thread; failed reads retain the old list.
        var work=Task.Run(()=>Directory.GetFiles(path,"*",SearchOption.AllDirectories).Where(f=>string.Equals(Path.GetExtension(f),".crproj",StringComparison.OrdinalIgnoreCase)).OrderBy(f=>f,StringComparer.OrdinalIgnoreCase).Select(f=>ChartProject.Read(File.ReadAllBytes(f),Path.GetFileName(f),f)).ToList());
        while(!work.IsCompleted)yield return null;
        try{
            if(work.IsFaulted)throw work.Exception.GetBaseException();
            var next=new List<ChartProject>{ChartProject.Demo()};foreach(var item in work.Result)ProjectLibrary.Upsert(next,item);
            Library=next;SelectRestoredPlay();status=T("更新: ","Refreshed: ")+work.Result.Count+T("曲"," tracks");
        }catch(Exception e){OnLibraryReloadUnavailable(e.Message);}
        finally{OnLibraryReloadState("done");}
    }
}
}
