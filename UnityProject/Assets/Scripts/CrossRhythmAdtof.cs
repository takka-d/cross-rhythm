using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    const string AdtofUrl="http://127.0.0.1:8765";
    string adtofState="";bool adtofChecking;
    UnityWebRequest adtofRequest;
    static JObject AdtofResponse(UnityWebRequest request){
        JObject result=null;try{result=JObject.Parse(request.downloadHandler.text);}catch{}
        if(request.result!=UnityWebRequest.Result.Success)throw new Exception((string)result?["error"]??request.error);
        if(result==null)throw new Exception("Invalid companion response");return result;
    }
    IEnumerator CheckAdtof(){
        if(adtofChecking)yield break;adtofChecking=true;adtofState=T("接続確認中…","Checking…");
        using(var request=UnityWebRequest.Get(AdtofUrl+"/health")){
            request.timeout=4;yield return request.SendWebRequest();
            try{var r=AdtofResponse(request);adtofState=(bool?)r["adtof_available"]==true&&((int?)r["protocol"]??0)==1?T("接続済み","Connected"):T("Setup ADTOFを実行してください","Run Setup ADTOF");}
            catch{adtofState=T("未接続: Setup / Start ADTOFを実行","Offline: run Setup / Start ADTOF");}
        }adtofChecking=false;
    }
    void AdtofSetup(){
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.OpenURL(new Uri(new Uri(Application.absoluteURL),"../adtof.html").AbsoluteUri);
#else
        string root=Path.GetFullPath(Path.Combine(Application.dataPath,"..","ADTOF"));
        if(Application.isEditor)root=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Tools","ADTOF"));
        var script=Path.Combine(root,"Start ADTOF.cmd");
        if(File.Exists(script)){try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(script){UseShellExecute=true,WorkingDirectory=root});status=T("ADTOFのセットアップ/起動を開きました","Opened ADTOF setup / start");}catch(Exception ex){status=ex.Message;}}
        else Application.OpenURL("https://takka-d.github.io/cross-rhythm/dev/0.3.13/adtof.html");
#endif
    }
    IEnumerator GenerateAdtof(){
        if(draftRunning||busy||!Project.Files.TryGetValue(Project.AudioPath,out var bytes)||bytes.Length==0)yield break;
        if(bytes.Length>256*1024*1024){status=T("ADTOFの音源は256MB以下にしてください","ADTOF audio must be 256 MB or smaller");yield break;}
        Audio.Pause();ClearDraft();draftProject=Project;draftSnapshot=new ProjectSaveSnapshot(Project);draftRunning=true;draftCancel=false;draftProgress=0;
        string error=null,id=null;JObject response=null;double started=Time.realtimeSinceStartupAsDouble;bool complete=false;
        using(var request=new UnityWebRequest(AdtofUrl+"/jobs?name="+UnityWebRequest.EscapeURL(Path.GetFileName(Project.AudioPath)),"POST")){
            adtofRequest=request;request.uploadHandler=new UploadHandlerRaw(bytes);request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/octet-stream");request.timeout=90;
            var operation=request.SendWebRequest();
            // Finish the local upload before cancelling, so its job ID can always be cleaned up.
            while(!operation.isDone){draftProgress=.04f*Mathf.Max(0,request.uploadProgress);yield return null;}
            try{response=AdtofResponse(request);id=(string)response["id"];if(string.IsNullOrEmpty(id)||id.Length!=32)throw new Exception("Invalid job ID");}catch(Exception ex){error=ex.Message;}
            adtofRequest=null;
        }
        while(error==null&&!draftCancel&&!complete){
            if(Time.realtimeSinceStartupAsDouble-started>1800){error=T("解析が時間制限を超えました","Analysis timed out");break;}
            using(var request=UnityWebRequest.Get(AdtofUrl+"/jobs/"+id)){
                adtofRequest=request;request.timeout=15;yield return request.SendWebRequest();
                try{response=AdtofResponse(request);string state=(string)response["status"];if(state=="error")throw new Exception((string)response["error"]);if(state=="cancelled"){draftCancel=true;break;}complete=state=="done";draftProgress=Mathf.Clamp01((float?)response["progress"]??.95f);}catch(Exception ex){error=ex.Message;}adtofRequest=null;
            }
            if(!complete&&!draftCancel&&error==null){double until=Time.realtimeSinceStartupAsDouble+.5;while(Time.realtimeSinceStartupAsDouble<until&&!draftCancel)yield return null;}
        }
        if(error==null&&!draftCancel&&complete){try{draftResult=AdtofDraft.Convert(draftProject,response["events"] as JArray,(double?)response["duration"]??0,draftExtend);}catch(Exception ex){error=ex.Message;}}
        if(!string.IsNullOrEmpty(id))using(var cleanup=UnityWebRequest.Delete(AdtofUrl+"/jobs/"+id)){cleanup.timeout=5;yield return cleanup.SendWebRequest();}
        adtofRequest=null;draftRunning=false;
        if(draftCancel){ClearDraft();status=T("解析を中止しました。譜面は変更していません","Analysis cancelled. Chart unchanged");}
        else if(error!=null){ClearDraft();status=T("ADTOF: ","ADTOF: ")+error+T(" — Setup / Startで接続を確認"," — Check Setup / Start and the connection");}
        else{adtofState=T("接続済み","Connected");status=T("ADTOFの下書きができました。Applyで適用できます","ADTOF draft ready. Apply it from Analyze");}
    }
}
}
