using System;
using System.IO;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    MidiFileDrop midiFileDrop;
    bool CanDropMidi=>CanImportMidi&&!editorFileOpen&&!Editor.ContextOpen;
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
        if(!CanDropMidi){status=T("Editの読込完了後にMIDIをドロップしてください","Drop MIDI after Edit is ready");return;}
        if(paths==null||paths.Length!=1||!MidiDropFiles.IsMidi(paths[0])){status=T("MIDIファイル(.mid / .midi)を1つドロップしてください","Drop one MIDI file (.mid / .midi)");return;}
        try{
            var info=new FileInfo(paths[0]);if(!info.Exists||info.Length<=0||info.Length>MidiDropFiles.MaxBytes){status=T("MIDIは空でない32 MB以下のファイルを選んでください","Choose a nonempty MIDI file up to 32 MB");return;}
            ImportMidi(File.ReadAllBytes(paths[0]),Path.GetFileName(paths[0]));
        }catch(Exception e){status=T("MIDI読込エラー: ","MIDI import error: ")+e.Message;}
    }
    public void OnMidiDropError(string message){status=message;}
}
}
