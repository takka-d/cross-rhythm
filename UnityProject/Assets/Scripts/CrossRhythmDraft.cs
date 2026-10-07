using System;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool draftRunning,draftCancel,draftReplace=true,draftExtend=true;
    float draftProgress;
    AudioDraft draftResult;ProjectSaveSnapshot draftSnapshot;ChartProject draftProject;
    void ClearDraft(){draftResult=null;draftSnapshot=null;draftProject=null;}
    void DraftControls(float x,float y,float cw){
        FittedText(new Rect(x,y,cw-12,24),Audio.Song==null?T("File → Audioから音源を追加","Add music with File → Audio"):System.IO.Path.GetFileName(Project.AudioPath),14,mint);
        if(Button(new Rect(x,y+32,142,34),"Auto Draft",false,Audio.Song!=null&&loaded&&!busy,14))StartCoroutine(GenerateDraft());
        if(Button(new Rect(x+154,y+32,142,34),"ADTOF",true,Audio.Song!=null&&loaded&&!busy,14))StartCoroutine(GenerateAdtof());
        if(Button(new Rect(x,y+78,142,30),"Setup / Start",false,true,13))AdtofSetup();
        if(Button(new Rect(x+154,y+78,142,30),"Check",false,!adtofChecking,13))StartCoroutine(CheckAdtof());
        FittedText(new Rect(x,y+113,cw-8,22),string.IsNullOrEmpty(adtofState)?"ADTOF · 127.0.0.1:8765":adtofState,12,muted);
        x+=cw+12;
        if(Button(new Rect(x,y,cw-16,32),T("既存ノーツを置換: ","Replace notes: ")+(draftReplace?"ON":"OFF"),draftReplace,true,13))draftReplace=!draftReplace;
        if(Button(new Rect(x,y+42,cw-16,32),T("曲の長さまで小節を追加: ","Extend bars to audio: ")+(draftExtend?"ON":"OFF"),draftExtend,true,13))draftExtend=!draftExtend;
        Text(new Rect(x,y+88,cw,22),T("下書きです。適用後はUndoで戻せます","A rough draft. Undo restores the previous chart"),12,muted);
        x+=cw+12;bool ready=draftResult!=null&&draftProject==Project&&draftSnapshot.Matches(Project);
        Text(new Rect(x,y,cw,26),ready?draftResult.Events.Count(n=>(string)n["instrument"]!="HHSTATE")+" notes · "+draftResult.Layout.Measures.Length+" bars":T("生成結果は適用前に確認できます","Generate first, then apply the result"),14,mint);
        if(Button(new Rect(x,y+36,130,34),"Apply",true,ready,14))ApplyDraft();
        if(Button(new Rect(x+144,y+36,130,34),"Discard",false,draftResult!=null,14))ClearDraft();
        Text(new Rect(x,y+88,cw,22),T("解析はこの端末内で行います","Audio is analyzed locally on this device"),12,muted);
        Text(new Rect(x,y+111,cw,22),T("ADTOF: 5系統。強弱・開閉は手動調整","ADTOF: 5 classes; edit dynamics / open hats"),12,muted);
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
    void ApplyDraft(){
        if(draftResult==null||draftProject!=Project||!draftSnapshot.Matches(Project)){status=T("編集内容が変わりました。再解析してください","Chart changed. Generate a new draft");return;}
        var chart=draftResult.Apply(Project,draftReplace);Audio.Pause();PushUndo();Project.Chart=chart;Edited();Editor.Reset();editScroll=Vector2.zero;Audio.AnchorBeat=0;ResetEditorFields();ClearDraft();status=T("下書きを適用しました。Undoで戻せます。Saveで保存してください","Draft applied. Undo to restore; Save to keep it");
    }
}
}
