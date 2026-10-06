using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Diagnostics;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool draftRunning,draftCancel,draftReplace=true,draftExtend=true;
    float draftProgress;
    AudioDraft draftResult;ProjectSaveSnapshot draftSnapshot;ChartProject draftProject;
    void ClearDraft(){draftResult=null;draftSnapshot=null;draftProject=null;}
    void DraftControls(float x,float y,float cw){
        FittedText(new Rect(x,y,cw-12,24),Audio.Song==null?T("File → Audioから音源を追加","Add music with File → Audio"):System.IO.Path.GetFileName(Project.AudioPath),14,mint);
        if(Button(new Rect(x,y+32,96,34),"Audio",false,!busy,14))PickAudio();
        if(Button(new Rect(x+108,y+32,126,34),"Auto Draft",true,Audio.Song!=null&&loaded&&!busy,14))StartCoroutine(GenerateDraft());
        if(Button(new Rect(x+246,y+32,116,34),"ADTOF",false,Audio.Song!=null&&loaded&&!busy,14))StartCoroutine(GenerateAdtofDraft());
        Text(new Rect(x,y+80,cw,23),T("BPM・開始位置はSong、間隔はGridで設定","Set BPM / offset in Song, and spacing in Grid"),12,muted);
        Text(new Rect(x,y+105,cw,23),T("音を解析してBD・SN・HHの下書きを生成","Analyze audio into a BD / SN / HH draft"),12,muted);
        x+=cw+12;
        if(Button(new Rect(x,y,cw-16,32),T("既存ノーツを置換: ","Replace notes: ")+(draftReplace?"ON":"OFF"),draftReplace,true,13))draftReplace=!draftReplace;
        if(Button(new Rect(x,y+42,cw-16,32),T("曲の長さまで小節を追加: ","Extend bars to audio: ")+(draftExtend?"ON":"OFF"),draftExtend,true,13))draftExtend=!draftExtend;
        Text(new Rect(x,y+88,cw,22),T("下書きです。適用後はUndoで戻せます","A rough draft. Undo restores the previous chart"),12,muted);
        x+=cw+12;bool ready=draftResult!=null&&draftProject==Project&&draftSnapshot.Matches(Project);
        Text(new Rect(x,y,cw,26),ready?draftResult.Events.Count(n=>(string)n["instrument"]!="HHSTATE")+" notes · "+draftResult.Layout.Measures.Length+" bars":T("生成結果は適用前に確認できます","Generate first, then apply the result"),14,mint);
        if(Button(new Rect(x,y+36,130,34),"Apply",true,ready,14))ApplyDraft();
        if(Button(new Rect(x+144,y+36,130,34),"Discard",false,draftResult!=null,14))ClearDraft();
        Text(new Rect(x,y+88,cw,22),T("Auto Draftは端末内解析 / ADTOFはlocalhost:8765","Auto Draft is local / ADTOF uses localhost:8765"),12,muted);
        Text(new Rect(x,y+111,cw,22),T("Windowsは同梱ADTOFサーバーを自動起動します","Windows can start the bundled ADTOF server"),12,muted);
    }
    IEnumerator GenerateDraft(){
        if(draftRunning||Audio.Song==null||busy)yield break;
        Audio.Pause();ClearDraft();draftProject=Project;draftSnapshot=new ProjectSaveSnapshot(Project);draftRunning=true;draftCancel=false;draftProgress=0;
        var clip=Audio.Song;var source=Project;var job=new AudioDraft();string error=null;float[] mono=null;System.Collections.Generic.IEnumerator<float> steps=null;
        try{mono=new float[clip.samples];}catch(Exception ex){error=ex.Message;}
        if(error==null){
            int frames=16384;var block=new float[frames*clip.channels];
            for(int offset=0;offset<clip.samples&&!draftCancel;offset+=frames){
                bool ok=clip.GetData(block,offset);if(!ok){error="Cannot read decoded audio";break;}
                int count=Math.Min(frames,clip.samples-offset);for(int i=0;i<count;i++){double sum=0;for(int ch=0;ch<clip.channels;ch++)sum+=block[i*clip.channels+ch];mono[offset+i]=(float)(sum/clip.channels);}
                draftProgress=.1f*offset/clip.samples;yield return null;
            }
        }
        if(error==null&&!draftCancel){
            steps=job.Run(mono,clip.frequency,source,draftExtend).GetEnumerator();
            while(!draftCancel){bool more=false;try{more=steps.MoveNext();if(more)draftProgress=.1f+.9f*steps.Current;}catch(Exception ex){error=ex.Message;}if(!more||error!=null)break;yield return null;}
            steps.Dispose();
        }
        draftRunning=false;
        if(draftCancel){ClearDraft();status=T("解析を中止しました。譜面は変更していません","Analysis cancelled. Chart unchanged");yield break;}
        if(error!=null){ClearDraft();status=T("解析エラー: ","Analysis error: ")+error;yield break;}
        draftResult=job;status=T("下書きを生成しました。AnalyzeのApplyで適用できます","Draft ready. Apply it from Analyze");
    }
    IEnumerator GenerateAdtofDraft(){
        if(draftRunning||Audio.Song==null||busy)yield break;
        Audio.Pause();ClearDraft();draftProject=Project;draftSnapshot=new ProjectSaveSnapshot(Project);draftRunning=true;draftCancel=false;draftProgress=0;
        string error=null;JObject health=null;UnityWebRequest request=null;
        yield return AdtofHealth((ok,message)=>{health=ok;error=message;});
#if !UNITY_WEBGL || UNITY_EDITOR
        if(health==null&&!draftCancel){TryStartAdtofCompanion();error=null;for(int retry=0;retry<20&&health==null&&!draftCancel;retry++){yield return new WaitForSecondsRealtime(.25f);yield return AdtofHealth((ok,message)=>{health=ok;error=message;});}}
#endif
        if(draftCancel){draftRunning=false;ClearDraft();status=T("解析を中止しました。譜面は変更していません","Analysis cancelled. Chart unchanged");yield break;}
        if(health==null||!((bool?)health["adtof_available"]??false)){draftRunning=false;ClearDraft();status=T("ADTOFサーバーへ接続できません: ","Cannot connect to ADTOF server: ")+(error??T("ADTOF-pytorchを確認してください","Check ADTOF-pytorch"));yield break;}
        if(!Project.Files.TryGetValue(Project.AudioPath,out var bytes)){draftRunning=false;ClearDraft();status=T("音源データがありません","Audio data is missing");yield break;}
        var form=new WWWForm();form.AddBinaryData("file",bytes,Path.GetFileName(Project.AudioPath),"application/octet-stream");form.AddField("bpm",Project.BPM.ToString(System.Globalization.CultureInfo.InvariantCulture));form.AddField("offset_sec",Project.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture));form.AddField("measures",Project.Chart["measures"].ToString(Newtonsoft.Json.Formatting.None));
        request=UnityWebRequest.Post("http://127.0.0.1:8765/transcribe",form);var op=request.SendWebRequest();
        while(!op.isDone&&!draftCancel){draftProgress=.05f+.9f*Mathf.Max(request.uploadProgress,request.downloadProgress);yield return null;}
        if(draftCancel){request.Abort();request.Dispose();draftRunning=false;ClearDraft();status=T("解析を中止しました。譜面は変更していません","Analysis cancelled. Chart unchanged");yield break;}
        if(request.result!=UnityWebRequest.Result.Success)error=request.error+" "+request.downloadHandler.text;
        else try{var result=JObject.Parse(request.downloadHandler.text);draftResult=AudioDraft.FromAdtof(result["events"] as JArray??new JArray(),Project,Audio.Song.length,draftExtend);}catch(Exception ex){error=ex.Message;}
        request.Dispose();draftRunning=false;draftProgress=1;
        if(error!=null){ClearDraft();status=T("ADTOF解析エラー: ","ADTOF analysis error: ")+error;yield break;}
        status=T("ADTOF下書きを生成しました。AnalyzeのApplyで適用できます","ADTOF draft ready. Apply it from Analyze");
    }
    IEnumerator AdtofHealth(Action<JObject,string> done){
        using(var request=UnityWebRequest.Get("http://127.0.0.1:8765/health")){var op=request.SendWebRequest();while(!op.isDone&&!draftCancel)yield return null;if(draftCancel){request.Abort();done(null,"cancelled");yield break;}if(request.result!=UnityWebRequest.Result.Success){done(null,request.error);yield break;}try{done(JObject.Parse(request.downloadHandler.text),null);}catch(Exception e){done(null,e.Message);}}
    }
#if !UNITY_WEBGL || UNITY_EDITOR
    void TryStartAdtofCompanion(){
        try{
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"..","ADTOF")),server=Path.Combine(root,"cross_rhythm_transcriber_server.py");if(!File.Exists(server))return;
            foreach(string exe in new[]{"pythonw.exe","python.exe","py.exe"})try{var info=new ProcessStartInfo(exe,'"'+server+'"'){WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true};Process.Start(info);return;}catch{}
        }catch{}
    }
#endif
    void ApplyDraft(){
        if(draftResult==null||draftProject!=Project||!draftSnapshot.Matches(Project)){status=T("編集内容が変わりました。再解析してください","Chart changed. Generate a new draft");return;}
        var chart=draftResult.Apply(Project,draftReplace);Audio.Pause();PushUndo();Project.Chart=chart;Edited();Editor.Reset();editScroll=Vector2.zero;Audio.AnchorBeat=0;ResetEditorFields();ClearDraft();status=T("下書きを適用しました。Undoで戻せます。Saveで保存してください","Draft applied. Undo to restore; Save to keep it");
    }
}
}
