using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    sealed class PendingMidi {
        public byte[] Bytes;public string Name;public ChartProject Source;public MidiImport.Result Preview;public MidiImport.ZeroMode ZeroMode;
        public readonly Dictionary<string,MidiImport.OverlapChoice> Choices=new Dictionary<string,MidiImport.OverlapChoice>();
    }
    PendingMidi pendingMidi;
    Vector2 midiOverlapScroll;
    bool MidiPromptOpen=>pendingMidi!=null;
    bool CanImportMidi=>Current==Page.Edit&&Project!=null&&ReferenceEquals(Project,editorProject)&&loaded&&!busy&&!draftRunning&&!discardPrompt&&!showMeterPanel&&!bindingsOpen&&!MidiPromptOpen&&savingProject==null;
    void ImportMidi(byte[] bytes,string name){
        if(!CanImportMidi){status=T("Editの読込完了後にMIDIを入れてください","Import MIDI after Edit is ready");return;}
        try{
            var imported=MidiImport.Read(bytes,Project,name,midiZeroMode);
            Audio.Pause();editorFileOpen=false;Editor.Reset();GUI.FocusControl(null);
            if(imported.Overlaps.Length==0){CommitMidi(imported);return;}
            pendingMidi=new PendingMidi{Bytes=bytes,Name=name,Source=Project,Preview=imported,ZeroMode=midiZeroMode};
            foreach(var overlap in imported.Overlaps)pendingMidi.Choices[overlap.Instrument]=new MidiImport.OverlapChoice();
            midiOverlapScroll=Vector2.zero;ResetMenuFocus();
        }catch(Exception e){status=T("MIDI読込エラー: ","MIDI import error: ")+e.Message;}
    }
    void CommitMidi(MidiImport.Result imported){
        Audio.Pause();PushUndo();Project.Chart=imported.Chart;Project.Manifest["title"]=Project.Title;Edited();Editor.Reset();editScroll=Vector2.zero;editMeasure=0;ResetEditorFields();Audio.AnchorBeat=0;
        status=T("MIDI読込: ","MIDI imported: ")+imported.Hits+" notes · V0 +"+imported.ZeroKept+" / −"+imported.ZeroDropped+(imported.TempoChanges>0?T(" / テンポ変更: "," / Tempo changes: ")+imported.TempoChanges:"")+(imported.OverlapsRemoved>0?T(" / 重複整理: "," / Overlaps removed: ")+imported.OverlapsRemoved:"");
    }
    void CancelMidi(){pendingMidi=null;ResetMenuFocus();status=T("MIDI読込をキャンセルしました。譜面は変更していません","MIDI import cancelled; chart unchanged");}
    void ApplyPendingMidi(){
        var p=pendingMidi;if(p==null)return;
        if(Current!=Page.Edit||!ReferenceEquals(Project,p.Source)){CancelMidi();return;}
        try{var result=MidiImport.Read(p.Bytes,p.Source,p.Name,p.ZeroMode,p.Choices);pendingMidi=null;CommitMidi(result);ResetMenuFocus();}
        catch(Exception e){status=T("MIDI読込エラー: ","MIDI import error: ")+e.Message;}
    }
    void MidiOverlapPrompt(){
        if(pendingMidi==null)return;var p=pendingMidi;
        RectFill(new Rect(0,0,W,H),new Color(0,0,0,.7f));
        float width=Math.Min(960,W-48),height=Math.Min(H-100,228+p.Preview.Overlaps.Length*86),x=(W-width)/2,y=(H-height)/2;
        RectFill(new Rect(x,y,width,height),panel);Border(new Rect(x,y,width,height),mint);
        Text(new Rect(x+24,y+17,width-48,34),T("MIDIの重複ノーツ","MIDI overlapping notes"),24,Color.white,true);
        FittedText(new Rect(x+24,y+55,width-48,24),p.Name,14,muted);
        Text(new Rect(x+24,y+86,width-48,26),T("同じ楽器・同じ時刻のノーツを整理します。異なる時刻と別の楽器は残ります","Choose notes at the same time on each instrument. Other hits stay unchanged."),13,muted);
        float listHeight=height-210;
        midiOverlapScroll=GUI.BeginScrollView(new Rect(x+20,y+121,width-40,listHeight),midiOverlapScroll,new Rect(0,0,width-64,p.Preview.Overlaps.Length*86),false,false);
        for(int i=0;i<p.Preview.Overlaps.Length;i++){
            var overlap=p.Preview.Overlaps[i];var choice=p.Choices[overlap.Instrument];float row=i*86;
            Text(new Rect(6,row,width-76,26),overlap.Instrument+"  ·  "+overlap.Positions+T("箇所 / 余分なノーツ "," positions / extra notes ")+overlap.Extra,15,mint,true);
            float typeWidth=(width-100)*.46f,velocityWidth=(width-100)*.30f;
            string type=choice.PreferredPitch<0?T("種別: Auto","Type: Auto"):MidiImport.PitchLabel(choice.PreferredPitch);
            if(Button(new Rect(6,row+31,typeWidth,33),type,false,!choice.KeepAll,13)){
                int at=Array.IndexOf(overlap.Pitches,choice.PreferredPitch);choice.PreferredPitch=at+1<overlap.Pitches.Length?overlap.Pitches[at+1]:-1;
            }
            if(Button(new Rect(18+typeWidth,row+31,velocityWidth,33),choice.Softest?T("弱い方を残す","Keep softer"):T("強い方を残す","Keep stronger"),false,!choice.KeepAll,13))choice.Softest=!choice.Softest;
            if(Button(new Rect(30+typeWidth+velocityWidth,row+31,width-94-typeWidth-velocityWidth,33),T("両方残す","Keep all"),choice.KeepAll,true,13))choice.KeepAll=!choice.KeepAll;
        }
        GUI.EndScrollView();
        int removed=p.Preview.Overlaps.Where(o=>!p.Choices[o.Instrument].KeepAll).Sum(o=>o.Extra);
        Text(new Rect(x+24,y+height-80,width-48,24),T("選んだ種別を優先し、同種別は強弱で選択。整理するノーツ: ","Prefer the selected type, then resolve by velocity. Notes to remove: ")+removed,12,muted);
        if(Button(new Rect(x+width-282,y+height-45,116,32),"Cancel",false,true,14)){CancelMidi();return;}
        if(Button(new Rect(x+width-152,y+height-45,128,32),"Import",true,true,14)){ApplyPendingMidi();return;}
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){CancelMidi();Event.current.Use();}
    }
}
}
