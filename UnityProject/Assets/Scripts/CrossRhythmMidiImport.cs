using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    sealed class PendingMidi {
        public byte[] Bytes;public string Name;public ChartProject Source;public MidiImport.Result Preview;public MidiImport.ZeroMode ZeroMode;
        public ChartProject Chart;
        public readonly Dictionary<string,MidiImport.OverlapChoice> Choices=new Dictionary<string,MidiImport.OverlapChoice>();
    }
    PendingMidi pendingMidi;
    Vector2 midiOverlapScroll;int midiOverlapInstrument;
    bool MidiPromptOpen=>pendingMidi!=null;
    bool CanImportMidi=>Current==Page.Edit&&Project!=null&&ReferenceEquals(Project,editorProject)&&loaded&&!busy&&!draftRunning&&!discardPrompt&&!showMeterPanel&&!bindingsOpen&&!MidiPromptOpen&&!LaneTypeOpen&&!gridMenuOpen&&savingProject==null;
    void ImportMidi(byte[] bytes,string name){
        if(!CanImportMidi){status=T("Editの読込完了後にMIDIを入れてください","Import MIDI after Edit is ready");return;}
        try{
            var imported=MidiImport.Read(bytes,Project,name,midiZeroMode);
            Audio.Pause();editorFileOpen=false;Editor.Reset();GUI.FocusControl(null);
            if(imported.Overlaps.Length==0){CommitMidi(imported);return;}
            pendingMidi=new PendingMidi{Bytes=bytes,Name=name,Source=Project,Preview=imported,ZeroMode=midiZeroMode};
            foreach(var overlap in imported.Overlaps)pendingMidi.Choices[overlap.Instrument]=new MidiImport.OverlapChoice();
            pendingMidi.Chart=new ChartProject{Chart=imported.Chart,Manifest=Project.Manifest};pendingMidi.Chart.Rebuild();
            midiOverlapInstrument=0;midiOverlapScroll=Vector2.zero;ResetMenuFocus();
        }catch(Exception e){status=T("MIDI読込エラー: ","MIDI import error: ")+e.Message;}
    }
    void CommitMidi(MidiImport.Result imported){
        Audio.Pause();PushUndo();Project.Chart=imported.Chart;Project.Manifest["title"]=Project.Title;Edited();Editor.Reset();editScroll=Vector2.zero;editMeasure=0;ResetEditorFields();Audio.AnchorBeat=0;
        status=T("MIDI読込: ","MIDI imported: ")+imported.Hits+" notes · V0 +"+imported.ZeroKept+" / −"+imported.ZeroDropped+(imported.TempoChanges>0?T(" / テンポ変更: "," / Tempo changes: ")+imported.TempoChanges:"")+(imported.LeadingBarsAligned>0?T(" / 音源位置に合わせた先頭小節: "," / Leading bars aligned to audio: ")+imported.LeadingBarsAligned:"")+(imported.OverlapsRemoved>0?T(" / 重複整理: "," / Overlaps removed: ")+imported.OverlapsRemoved:"");
    }
    void CancelMidi(){pendingMidi=null;ResetMenuFocus();status=T("MIDI読込をキャンセルしました。譜面は変更していません","MIDI import cancelled; chart unchanged");}
    void ApplyPendingMidi(){
        var p=pendingMidi;if(p==null)return;
        if(Current!=Page.Edit||!ReferenceEquals(Project,p.Source)){CancelMidi();return;}
        try{var result=MidiImport.Read(p.Bytes,p.Source,p.Name,p.ZeroMode,p.Choices);pendingMidi=null;CommitMidi(result);ResetMenuFocus();}
        catch(Exception e){status=T("MIDI読込エラー: ","MIDI import error: ")+e.Message;}
    }
    void PreviewMidiCandidate(MidiImport.Candidate candidate){
        var note=MidiImport.PreviewNote(candidate.Pitch,candidate.Velocity,out bool closed);if(note==null)return;
        Audio.Audition(note,closed);status=T("ゲーム内の変換音を試聴: ","Previewing target game sound: ")+MidiImport.GameSoundLabel(candidate.Pitch);
    }
    int MidiRemaining=>pendingMidi==null?0:pendingMidi.Preview.Overlaps.Sum(o=>o.Collisions.Count(c=>pendingMidi.Choices[o.Instrument].SelectedId(c)<0));
    void MidiOverlapPrompt(){
        if(pendingMidi==null)return;var p=pendingMidi;
        RectFill(new Rect(0,0,W,H),new Color(0,0,0,.8f));
        float width=Math.Min(1120,W-48),height=H-64,x=(W-width)/2,y=32;
        RectFill(new Rect(x,y,width,height),panel);Border(new Rect(x,y,width,height),mint);
        Text(new Rect(x+24,y+16,width-48,32),T("MIDI: 残すノーツを選択","MIDI: choose the note to keep"),24,Color.white,true);
        FittedText(new Rect(x+24,y+51,width-48,23),p.Name,14,muted);
        Text(new Rect(x+24,y+80,width-48,25),T("同じ時刻・同じ変換先になるMIDIノーツがあります。各時刻で実際に残す1音を選びます。","Some MIDI notes share the same time and target. Choose the one hit to keep at each time."),13,muted);
        float tabWidth=(width-48)/p.Preview.Overlaps.Length;
        for(int i=0;i<p.Preview.Overlaps.Length;i++){
            var o=p.Preview.Overlaps[i];int left=o.Collisions.Count(c=>p.Choices[o.Instrument].SelectedId(c)<0);
            if(Button(new Rect(x+24+i*tabWidth,y+115,tabWidth-8,34),MidiImport.InstrumentLabel(o.Instrument)+" · "+(o.Positions-left)+"/"+o.Positions,midiOverlapInstrument==i,true,14)){midiOverlapInstrument=i;midiOverlapScroll=Vector2.zero;ResetMenuFocus();}
        }
        var overlap=p.Preview.Overlaps[midiOverlapInstrument];var choice=p.Choices[overlap.Instrument];
        Text(new Rect(x+24,y+158,width-48,25),T("時刻ごとに、実際に残す1音を「この音を残す」で選びます。試聴で変換後の音を確認できます。","At each time, choose the one hit to keep. Preview plays the converted in-game sound."),13,mint);
        float listTop=y+190;
        float listHeight=y+height-80-listTop,totalHeight=overlap.Collisions.Sum(c=>40+c.Candidates.Length*82);
        var midiView=new Rect(x+20,listTop,width-40,listHeight);midiOverlapScroll=ControllerScrollView(midiView,midiOverlapScroll,width-62,totalHeight);midiOverlapScroll=GUI.BeginScrollView(midiView,midiOverlapScroll,new Rect(0,0,width-62,totalHeight),false,false);
        float row=0;
        foreach(var collision in overlap.Collisions){
            float rowHeight=40+collision.Candidates.Length*82;
            if(row+rowHeight>=midiOverlapScroll.y&&row<=midiOverlapScroll.y+listHeight){
                int bar=p.Chart.BarAt(collision.Beat),selected=choice.SelectedId(collision);double beat=(collision.Beat-p.Chart.Starts[bar])/ChartVisuals.BeatUnit(p.Chart,bar)+1;
                RectFill(new Rect(0,row,width-64,30),bg);
                Text(new Rect(8,row+3,width-90,24),$"M{bar+1} · Beat {beat:0.######} · {p.Chart.SecondsAtBeat(collision.Beat):0.000} s · Tick {collision.Tick}"+(selected<0?T("  未選択","  Choose one"):""),13,selected<0?Color.white:mint);
                for(int i=0;i<collision.Candidates.Length;i++){var c=collision.Candidates[i];float cy=row+34+i*82;bool kept=selected==c.Id;
                    Rect candidateRect=new Rect(8,cy,width-82,76);RectFill(candidateRect,kept?new Color(.08f,.20f,.18f):new Color(.04f,.075f,.095f));Border(candidateRect,kept?mint:line);
                    FittedText(new Rect(candidateRect.x+14,cy+6,candidateRect.width-330,20),T("元MIDI音: ","Source MIDI sound: ")+MidiImport.SourceSoundLabel(c.Pitch)+" ["+c.Pitch+"]",13,Color.white,true);
                    FittedText(new Rect(candidateRect.x+14,cy+28,candidateRect.width-330,19),$"Track {c.Track+1} · Channel {c.Channel+1} · Velocity {c.Velocity}/127 · "+T("強弱 ","Strength ")+MidiImport.Strength(c.Velocity),12,muted);
                    FittedText(new Rect(candidateRect.x+14,cy+50,candidateRect.width-330,19),T("変換先: ","Target: ")+MidiImport.TargetInstrumentLabel(c.Pitch)+" / "+T("種別 ","Type ")+MidiImport.TargetTypeLabel(c.Pitch),12,mint,true);
                    if(Button(new Rect(width-382,cy+18,164,40),kept?T("選択中","Selected"):T("この音を残す","Keep this hit"),kept,true,12,"midi-candidate-"+c.Id,ControlKind.Choice))choice.SelectCandidate(collision,c.Id);
                    if(Button(new Rect(width-206,cy+18,112,40),T("試聴","Preview"),false,loaded&&!busy&&!Audio.Preparing,13,"midi-preview-"+c.Id))PreviewMidiCandidate(c);
                }
            }row+=rowHeight;
        }
        GUI.EndScrollView();
        int remaining=MidiRemaining;
        Text(new Rect(x+24,y+height-66,width-346,50),remaining>0?T("未選択: ","Still to choose: ")+remaining:T("各位置に1つ残して読み込みます","Ready: one hit at each position"),15,mint);
        if(Button(new Rect(x+width-282,y+height-54,116,34),"Cancel",false,true,14)){CancelMidi();return;}
        if(Button(new Rect(x+width-152,y+height-54,128,34),"Import",true,remaining==0,14)){ApplyPendingMidi();return;}
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){CancelMidi();Event.current.Use();}
    }
}
}
