using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    Snapshot audioImportBefore;
    ChartProject audioImportProject;
    bool audioImportWasDirty;
    double audioImportBeat;
    string audioImportFailure="";
    bool CanImportAudio=>Current==Page.Edit&&ReferenceEquals(Project,editorProject)&&!busy&&!draftRunning&&!discardPrompt&&!MidiPromptOpen&&!showMeterPanel&&savingProject==null&&!exportingMidi;
    public void SetAudio(byte[] bytes,string name){
        if(!CanImportAudio||audioImportBefore!=null)return;
        if(bytes==null||bytes.Length==0||bytes.Length>MidiDropFiles.MaxAudioBytes||!MidiDropFiles.IsAudio(name)){status=T("対応する音声ファイルを選んでください(128 MB以下)","Choose a supported audio file up to 128 MB");return;}
        PrepareExclusiveOperation();ClearDraft();audioImportFailure="";
        audioImportProject=Project;audioImportWasDirty=Project.Dirty;audioImportBeat=Math.Max(0,Audio.Beat);
        audioImportBefore=new Snapshot{Chart=(JObject)Project.Chart.DeepClone(),Manifest=(JObject)Project.Manifest.DeepClone(),Files=new Dictionary<string,byte[]>(Project.Files)};
        string audioPath="audio/"+Path.GetFileName(name),oldPath=Project.AudioPath;
        if(oldPath!=audioPath&&!string.IsNullOrEmpty(oldPath))Project.Files.Remove(oldPath);
        Project.Files[audioPath]=bytes;Project.Manifest["audio"]=new JObject{{"path",audioPath},{"originalName",name},{"size",bytes.Length}};
        busy=true;loaded=false;loadingFile=name;Audio.Load(Project);
    }
    bool FinishAudioImport(string error){
        if(audioImportBefore==null||!ReferenceEquals(Project,audioImportProject))return false;
        var before=audioImportBefore;audioImportBefore=null;audioImportProject=null;
        if(string.IsNullOrEmpty(error)&&Audio.Song==null)error="Audio contains no playable samples";
        if(string.IsNullOrEmpty(error)){
            try{Project.ExtendToAudio(Audio.Song.length);}catch(Exception e){error=e.Message;}
        }
        if(!string.IsNullOrEmpty(error)){
            Project.Chart=before.Chart;Project.Manifest=before.Manifest;Project.Files=before.Files;Project.Dirty=audioImportWasDirty;Project.Rebuild();
            audioImportFailure=T("音源の読込に失敗しました。元の編集内容を保持: ","Audio import failed. Previous edits retained: ")+error;
            busy=true;loaded=false;Audio.Load(Project);return true;
        }
        undo.Push(before);redo.Clear();Edited();Audio.AnchorBeat=Math.Min(audioImportBeat,Project.Length);ResetEditorFields();
        loadingFile="";editorPanel=1;return false;
    }
    IEnumerator LoadDroppedAudio(string path){
        if(!CanImportAudio||InputBlocked)yield break;
        var target=Project;PrepareExclusiveOperation();nativeProjectLoading=true;loadingFile=Path.GetFileName(path);
        yield return null;yield return null;
        var read=Task.Run(()=>{var info=new FileInfo(path);if(info.Length<=0||info.Length>MidiDropFiles.MaxAudioBytes)throw new Exception("Choose audio up to 128 MB");return File.ReadAllBytes(path);});while(!read.IsCompleted)yield return null;
        nativeProjectLoading=false;loadingFile="";
        if(read.IsFaulted){status=T("音源読込エラー: ","Audio import error: ")+read.Exception.GetBaseException().Message;yield break;}
        if(ReferenceEquals(Project,target)&&CanImportAudio)SetAudio(read.Result,Path.GetFileName(path));
    }
    void ExtendEditorToAudio(){
        if(Audio.Song==null)return;
        if(Project.BarsToCoverAudio(Audio.Song.length)==0){status=T("音源の終わりまで小節があります","Bars already cover the audio");return;}
        Audio.Pause();PushUndo();int added=Project.ExtendToAudio(Audio.Song.length);Edited();status=T("小節を追加しました: ","Bars added: ")+added;
    }
}
}
