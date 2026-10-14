using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator WorkflowRuntimeCheck(Action<bool,string> check){
        var p=ChartProject.Read(File.ReadAllBytes(Path.Combine(workspaceCheckRoot,"practice-visual.crproj")),"Workflow.crproj");
        p.SaveNative(Path.Combine(workspaceCheckRoot,"workflow.crproj"),false);Library.Add(p);NavigateNow(Page.Songs);FocusSong(Library.Count-1);
        Begin(false);yield return WaitForOperations();
        Seek(.5);Judge(p.Notes.First(n=>!n.Pedal),4,"JUST");
        int records=Records.Count,judgementCount=judged.Count;RequestStageExit();double stopped=Audio.Beat;
        check(stageExitPrompt&&!Audio.Running&&Current==Page.Play,"Stop opens a confirmation over the paused performance");
        yield return new WaitForSeconds(.2f);check(Audio.Beat==stopped&&Records.Count==records&&judged.Count==judgementCount,"Stop confirmation freezes clock and retains score and judgements");
        Navigate(Page.Title);check(Current==Page.Play&&stageExitPrompt,"underlying navigation cannot bypass the confirmation");
        ResumeStage();check(Audio.Running&&!stageExitPrompt&&Audio.AnchorBeat==stopped&&Records.Count==records&&judged.Count==judgementCount,"Resume continues the same performance without resetting score");
        RequestStageExit();stopped=Audio.Beat;PracticeFromStage();yield return WaitForOperations();
        check(Current==Page.Practice&&!Audio.Running&&Audio.Beat==stopped&&Records.Count==0&&judged.Count==0,"Stop dialog Practice opens paused at the current position without carrying score");
        PlayFromPractice();yield return WaitForOperations();
        check(Current==Page.Play&&Audio.Running&&Audio.AnchorBeat==CountIn.Start(p)&&Records.Count==0&&judged.Count==0,"Practice Start opens a fresh scored Play with the normal count-in");
        RequestStageExit();stopped=Audio.Beat;PracticeFromStage();yield return WaitForOperations();
        check(Current==Page.Practice&&!Audio.Running&&Audio.Beat==stopped,"Play can return to Practice at the same position after the new Start route");
        Seek(7.25);RequestPracticeEdit();yield return WaitForOperations();
        check(Current==Page.Edit&&!ReferenceEquals(Project,p)&&Audio.Beat==7.25,"Practice Edit opens an independent document at the selected beat");
        var draft=Project;PushUndo();draft.SetSongInfo("Kept editor draft","QA artist");
        check(p.Title!="Kept editor draft","Practice-to-Edit changes never mutate the Songs project");
        int undoCount=undo.Count;NavigateNow(Page.Songs);FocusSong(Library.IndexOf(p));Begin(true);yield return WaitForOperations();Seek(2);RequestPracticeEdit();yield return WaitForOperations();
        check(ReferenceEquals(Project,draft)&&draft.Title=="Kept editor draft"&&undo.Count==undoCount&&Audio.Beat==2,"Practice Edit reuses an existing dirty document and its history");
        int frame=Time.frameCount,historyPaints=loadingPaints;RequestRestore(false);
        check(restoringHistory&&InputBlocked&&LoadingIsDialog&&draft.Title=="Kept editor draft","Undo blocks with the retained editor before history processing");
        RequestRestore(false);NavigateNow(Page.Songs);OnEditorShortcut("Paste");
        check(Current==Page.Edit&&draft.Title=="Kept editor draft"&&undo.Count==undoCount,"duplicate Undo, navigation and editing cannot race history restore");
        yield return WaitForOperations();check(Time.frameCount>=frame+2&&(!HistoryPaintRequired||loadingPaints>historyPaints)&&draft.Title==p.Title&&redo.Count==1&&!restoringHistory,"visible Undo paints progress before restoring one complete history entry");
        OnEditorShortcut("Redo");check(restoringHistory&&InputBlocked,"browser Redo shortcut uses the progress operation");yield return WaitForOperations();
        check(draft.Title=="Kept editor draft"&&undo.Count==undoCount&&redo.Count==0,"Redo restores the same editor metadata and history");
        var chart=draft.Chart.ToString();Audio.Play(0);EditorBoundary(true);
        check(!Audio.Running&&Audio.Beat==draft.Length&&editMeasure==draft.Measures.Length-1,"end transport stops and moves to the final bar end");
        EditorBoundary(false);check(!Audio.Running&&Audio.Beat==0&&editMeasure==0&&editScroll.x==0&&draft.Chart.ToString()==chart,"start transport returns to bar one without changing chart or history");
        check(EditorTransportIcon(false)=="|◀"&&EditorTransportIcon(true)=="▶|","editor boundary buttons preserve the requested boundary icon shape");
        draft.Dirty=false;NavigateNow(Page.Songs);FocusSong(Library.IndexOf(p));Begin(true);yield return WaitForOperations();RequestStageExit();
        check(!stageExitPrompt&&transitioning&&Current==Page.Practice,"Practice Songs leaves directly without opening the Play stop dialog");yield return WaitForOperations();
        check(Current==Page.Songs&&!stageExitPrompt&&!Audio.Running,"Practice Songs returns directly to Songs");
        bool oldFeet=feetAbove,oldEnglish=english;try{
            float songsX=(W-1160)/2;var laneOrderRect=SongsLaneOrderRect(songsX);var previewRect=SongsPreviewRect(songsX);
            check(Math.Abs(laneOrderRect.y-previewRect.y)<.001&&laneOrderRect.xMax<previewRect.x,"Songs note layout is on the Preview row instead of the song-detail panel");
            english=false;check(LaneOrderOption(false)=="手で叩くノーツが上"&&LaneOrderOption(true)=="足で踏むノーツが上","Japanese lane choices explain which playable notes move above");
            english=true;check(LaneOrderOption(false)=="Hand Notes on Top"&&LaneOrderOption(true)=="Foot Notes on Top","English lane choices explain which playable notes move above");
            english=oldEnglish;SetFeetAbove(!oldFeet);Begin(true);yield return WaitForOperations();check(feetAbove!=oldFeet,"Songs lane order is inherited in Practice");SetFeetAbove(oldFeet);NavigateNow(Page.Config);check(feetAbove==oldFeet,"Practice lane order is shared with Config");
        }finally{english=oldEnglish;SetFeetAbove(oldFeet);}
        NavigateNow(Page.Songs);yield return WaitForOperations();
    }
}
}
