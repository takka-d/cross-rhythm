using System;
using System.IO;
using System.Collections;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool exportingMidi;
    string midiExportSummary="";
    void MidiExportButton(Rect r){
        bool enabled=GUI.enabled&&!InputBlocked;
#if UNITY_WEBGL && !UNITY_EDITOR
        Button(r,"",false,enabled,15);
        if(Event.current.type==EventType.Repaint)PlatformFiles.CRProjectButton(fileButtonCount++,r.x/W,r.y/H,r.width/W,r.height/H,6,english?1:0,enabled?1:0,0,15*scale);
#else
        if(Button(r,"Export MIDI",false,enabled,15))OnMidiExportButton("");
#endif
    }
    public void OnMidiExportButton(string unused){
        if(Current!=Page.Edit||InputBlocked||MidiPromptOpen||discardPrompt||draftRunning||LaneTypeOpen||gridMenuOpen)return;
        try{
            PrepareExclusiveOperation();var result=MidiExport.Write(Project);string name=MidiExport.FileName(Project.Title);
            midiExportSummary=result.Hits+" notes"+(result.Muted>0?T(" / 無音除外 "," / Muted omitted ")+result.Muted:"")+(result.Approximate>0?T(" / 標準ドラム音へ置換 "," / GM substitutions ")+result.Approximate:"");
            exportingMidi=true;loadingFile=name;
#if UNITY_WEBGL && !UNITY_EDITOR
            PlatformFiles.CRExportMidi(result.Bytes,result.Bytes.Length,name,gameObject.name);
#else
            StartCoroutine(ExportMidiNative(result.Bytes,name));
#endif
        }catch(Exception e){OnMidiExported(new JObject{{"error",e.Message}}.ToString());}
    }
#if !UNITY_WEBGL || UNITY_EDITOR
    IEnumerator ExportMidiNative(byte[] bytes,string name){
        yield return null;yield return null;
        string path=null,error=null;try{path=PlatformFiles.Pick(true,name,false,true);}catch(Exception e){error=e.Message;}
        if(error!=null||path==null){OnMidiExported(new JObject{{"error",error??"Cancelled"}}.ToString());yield break;}
        var task=Task.Run(()=>MidiExport.SaveNative(path,bytes));while(!task.IsCompleted)yield return null;
        OnMidiExported(task.IsFaulted?new JObject{{"error",task.Exception.GetBaseException().Message}}.ToString():new JObject{{"saved",true},{"name",Path.GetFileName(path)}}.ToString());
    }
#endif
    public void OnMidiExported(string json){
        exportingMidi=false;loadingFile="";
        var o=JObject.Parse(json);string error=(string)o["error"];
        if(error!=null){status=error=="Cancelled"?T("MIDI書き出しをキャンセルしました","MIDI export cancelled"):T("MIDI書き出し失敗: ","MIDI export failed: ")+error;return;}
        status=((bool?)o["saved"]==true?T("MIDI書き出し済み: ","MIDI exported: "):T("MIDIダウンロード開始。保存先を確認してください: ","MIDI download started; check your downloads: "))+(string)o["name"]+" · "+midiExportSummary;
        // MIDI is a separate export, never a saved project or a library replacement.
    }
}
}
