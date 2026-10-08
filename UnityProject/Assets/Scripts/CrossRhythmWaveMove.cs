using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool waveCaptured,waveMoved;
    Vector2 waveOrigin;
    double waveStartBeat,waveStartOffset,wavePreviewOffset;
    double EditorAudioOffset=>waveCaptured&&waveMoved?wavePreviewOffset:Project.Offset;
    void CancelWaveMove(){waveCaptured=waveMoved=false;}
    void MoveWave(Vector2 local){
        if(!waveMoved&&Math.Abs(local.x-waveOrigin.x)<4)return;
        if(!waveMoved){waveMoved=true;Audio.Pause();}
        wavePreviewOffset=EditorInteraction.AudioOffsetForDrag(Project,waveStartOffset,waveStartBeat,local.x/(EditorPPB*zoom));
    }
    void CommitWaveMove(){
        double offset=wavePreviewOffset;bool change=waveMoved&&Math.Abs(offset-Project.Offset)>1e-9;
        CancelWaveMove();
        if(!change)return;
        PushUndo();Project.Chart["audioOffsetSec"]=offset;Edited();
        offsetField=offset.ToString("0.######",System.Globalization.CultureInfo.InvariantCulture);
        status=T("音源の位置を変更しました","Audio position updated");
    }
    bool WavePointerInput(Rect viewport,float notesTop,Vector2 local){
        var ev=Event.current;
        if(waveCaptured){
            if(ev.type==EventType.KeyDown&&ev.keyCode==KeyCode.Escape){CancelWaveMove();GUIUtility.hotControl=0;ev.Use();return true;}
            if(ev.type==EventType.MouseDrag||ev.type==EventType.MouseUp){
                MoveWave(local);
                if(ev.type==EventType.MouseUp){bool click=!waveMoved;CommitWaveMove();if(click)EditorSeek(EditorInteraction.PlacementBeat(Project,local.x/(EditorPPB*zoom)));GUIUtility.hotControl=0;}
                ev.Use();return true;
            }
            return false;
        }
        if(ev.type!=EventType.MouseDown||ev.button!=0||Audio.Waveform==null||busy||ev.shift||ev.control||ev.command)return false;
        var wave=new Rect(viewport.x,viewport.y+24,viewport.width-16,notesTop-24);
        if(!wave.Contains(ev.mousePosition))return false;
        waveCaptured=true;waveMoved=false;waveOrigin=local;waveStartBeat=local.x/(EditorPPB*zoom);waveStartOffset=wavePreviewOffset=Project.Offset;
        GUI.FocusControl(null);GUIUtility.hotControl=GUIUtility.GetControlID(73518,FocusType.Passive);ev.Use();return true;
    }
}
}
