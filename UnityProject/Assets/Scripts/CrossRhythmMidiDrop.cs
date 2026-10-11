using System;
using System.IO;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    MidiFileDrop midiFileDrop;
    bool CanDropMidi=>!InputBlocked&&CanImportMidi&&!editorFileOpen&&!gridMenuOpen&&laneTypeRow<0&&!Editor.ContextOpen;
    void InitializeMidiDrop(){midiFileDrop=new MidiFileDrop();}
    void DisposeMidiDrop(){midiFileDrop?.Dispose();}
    void UpdateMidiDrop(){
#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CRMidiDropState(gameObject.name,CanDropMidi?1:0,english?1:0);
#else
        midiFileDrop.Update(CanDropMidi);
        while(midiFileDrop.TryRead(out var paths))ImportDroppedMidi(paths);
#endif
    }
    void ImportDroppedMidi(string[] paths){
        if(!CanDropMidi){status=T("Editの読込完了後にMIDI・音声をドロップしてください","Drop MIDI or audio after Edit is ready");return;}
        if(paths==null||paths.Length!=1||(!MidiDropFiles.IsMidi(paths[0])&&!MidiDropFiles.IsAudio(paths[0]))){status=T("MIDIまたは音声ファイルを1つドロップしてください","Drop one MIDI or audio file");return;}
        try{
            var info=new FileInfo(paths[0]);if(!info.Exists||info.Length<=0||info.Length>(MidiDropFiles.IsMidi(paths[0])?MidiDropFiles.MaxBytes:MidiDropFiles.MaxAudioBytes)){status=T("空でないファイルを選択: MIDI 32 MB / 音声128 MB以下","Choose a nonempty file: MIDI up to 32 MB / audio up to 128 MB");return;}
            if(MidiDropFiles.IsAudio(paths[0]))StartCoroutine(LoadDroppedAudio(paths[0]));else ImportMidi(File.ReadAllBytes(paths[0]),Path.GetFileName(paths[0]));
        }catch(Exception e){status=T("ファイル読込エラー: ","File import error: ")+e.Message;}
    }
    public void OnMidiDropError(string message){status=message;}
}
}
