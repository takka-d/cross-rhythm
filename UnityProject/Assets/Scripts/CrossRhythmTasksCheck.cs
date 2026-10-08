using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator TasksCheck(){
        string root=workspaceCheckRoot;Directory.CreateDirectory(root);var checks=new List<string>();
        bool hadPreview=PlayerPrefs.HasKey("songPreview");int originalPreview=PlayerPrefs.GetInt("songPreview",0);
        void Check(bool ok,string message){if(!ok){File.WriteAllText(Path.Combine(root,"failed.txt"),message);allowApplicationQuit=true;Application.Quit(1);throw new Exception(message);}checks.Add(message);Debug.Log("PASS tasks: "+message);}
        IEnumerator Ready(){double end=Time.realtimeSinceStartupAsDouble+30;while((busy||!loaded)&&Time.realtimeSinceStartupAsDouble<end)yield return null;Check(loaded&&!busy,"audio finished loading");}
        try{
            yield return Ready();
            SetSongPreview(false);NavigateNow(Page.Songs);yield return Ready();
            Check(!Audio.Backing.isPlaying&&PlayerPrefs.GetInt("songPreview",-1)==0,"preview OFF persists and does not autoplay");
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--test-audio");Check(at>=0&&at+1<args.Length,"synthetic audio fixture supplied");
            var bytes=File.ReadAllBytes(args[at+1]);
            ChartProject AudioProject(string title){var p=ChartProject.Demo();p.SetSongInfo(title,"");p.Files["audio/test.wav"]=bytes;p.Manifest["audio"]=new JObject{{"path","audio/test.wav"}};p.Dirty=false;return p;}
            Library.Add(AudioProject("Preview A"));Library.Add(AudioProject("Preview B"));
            FocusSong(1);yield return new WaitForSeconds(.22f);Check(busy,"background decode is in progress");
            FocusSong(2);Check(selected==2&&Project.Title=="Preview B","song selection changes while previous audio is loading");yield return Ready();
            Check(Audio.Project==Project&&Audio.Song!=null&&!Audio.Backing.isPlaying,"obsolete audio cannot start; preview remains OFF after decode");
            SetSongPreview(true);yield return new WaitForSeconds(.35f);Check(Audio.Backing.isPlaying&&!Audio.Running,"preview ON plays only the focused song");
            SetSongPreview(false);Check(!Audio.Backing.isPlaying&&PlayerPrefs.GetInt("songPreview",-1)==0,"preview OFF immediately stops playback and persists");
            FocusSong(1);Check(busy&&SongActionsAvailable,"Songs actions remain available before preview audio loads");
            Begin(false);Check(Current==Page.Play&&ReferenceEquals(pendingPerformance,Project)&&!Audio.Running,"Start immediately enters Play and waits without advancing chart");
            yield return Ready();Check(pendingPerformance==null&&Audio.Running&&Audio.AnchorBeat==CountIn.Start(Project),"queued Start begins count-in once selected audio is ready");
            NavigateNow(Page.Songs);FocusSong(2);Begin(true);
            Check(Current==Page.Practice&&ReferenceEquals(pendingPerformance,Project),"Practice accepts action during background audio load");
            NavigateNow(Page.Songs);yield return Ready();Check(Current==Page.Songs&&!Audio.Running&&pendingPerformance==null,"Back cancels queued performance; late audio does not launch it");
            FocusSong(1);Check(busy,"Edit test begins with audio pending");EditSelectedSong();
            Check(Current==Page.Edit&&Project.Title=="Preview A"&&!ReferenceEquals(Project,Library[1]),"Edit opens selected chart during preview loading and keeps independent data");
            yield return Ready();Check(Audio.Project==Project,"Edit receives its own audio after old preview load is cancelled");
            Begin(true);Audio.Pause();pendingPracticeSpeed=.37;UpdatePracticeSpeed();
            Check(Math.Abs(practiceSpeed-.37)<1e-8&&Math.Abs(Audio.Rate-.37)<1e-8&&!Audio.Running,"released speed slider accepts hundredths without starting paused playback");
            ChangePractice(1,pro);NavigateNow(Page.Songs);
            var p=EmptyEditorProject();string path=Path.Combine(root,"save-check.crproj");p.SaveNative(path,false);OpenEditorProject(p);yield return Ready();
            p.SetSongInfo("Unsaved title","Test artist");QueueEditorRecovery();Navigate(Page.Songs);
            Check(discardPrompt&&Current==Page.Edit&&p.Dirty,"navigation opens unsaved prompt and stays in Edit");
            CancelLeaving();Check(!discardPrompt&&Current==Page.Edit&&p.Title=="Unsaved title"&&p.Dirty,"Cancel retains edits and stays in Edit");
            foreach(var destination in new[]{Page.Songs,Page.Config,Page.Title}){
                editorFileOpen=true;Editor.ContextOpen=true;Navigate(destination);
                Check(discardPrompt&&Current==Page.Edit&&!editorFileOpen&&!Editor.ContextOpen,"unsaved guard closes menus before navigating to "+destination);CancelLeaving();
            }
            Navigate(Page.Title);SaveBeforeLeaving();
            Check(Current==Page.Title&&!discardPrompt&&!p.Dirty&&ChartProject.Read(File.ReadAllBytes(path),"check").Title=="Unsaved title","Save and Continue waits for verified save and navigates");
            NavigateNow(Page.Edit);yield return Ready();p.SetSongInfo("Newer unsaved title","");File.AppendAllText(path,"external change");Navigate(Page.Songs);SaveBeforeLeaving();
            Check(discardPrompt&&Current==Page.Edit&&p.Dirty&&saveState==SaveState.Error,"save conflict keeps the prompt open and does not navigate");CancelLeaving();
            Check(!ConfirmApplicationQuit()&&discardPrompt&&Current==Page.Edit,"window close is blocked by unsaved prompt");CancelLeaving();
            Check(p.Title=="Newer unsaved title"&&p.Dirty,"cancelling close preserves changes");
            NewProject();Check(discardPrompt&&ReferenceEquals(Project,p),"New Project asks before replacing a dirty project");
            CancelLeaving();Check(Project.Title=="Newer unsaved title","New Project Cancel retains data");
            string diskBefore=ChartProject.Hash(File.ReadAllBytes(path));NewProject();CompleteLeaving();yield return Ready();
            Check(Current==Page.Edit&&Project.Title=="Untitled"&&Project.Notes.Count==0&&!Project.Dirty&&undo.Count==0&&redo.Count==0,"New Project Leave creates a clean empty document");
            Check(ChartProject.Hash(File.ReadAllBytes(path))==diskBefore,"discard does not change the original file");
            Check(!editorDocuments.Values.Contains(p)&&!editorSessions.ContainsKey(p),"discard removes old document and undo session");
            Project.SetSongInfo("Disposable draft","");Edited();Navigate(Page.Config);CompleteLeaving();
            Check(Current==Page.Config&&!editorProject.Dirty&&editorProject.Notes.Count==0&&editorProject.Title=="Untitled","Leave discards instead of retaining the draft");
            var recovery=JObject.Parse(File.ReadAllText(EditorRecoveryPath));var recovered=ChartProject.Read(Convert.FromBase64String((string)recovery["bytes"]),"recovered");
            Check(recovered.Title=="Untitled"&&recovered.Notes.Count==0&&!(bool)recovery["dirty"],"restart recovery cannot bring back discarded edits");
            NavigateNow(Page.Edit);yield return Ready();p=EmptyEditorProject();p.SaveNative(Path.Combine(root,"new-after-save.crproj"),false);OpenEditorProject(p);yield return Ready();p.SetSongInfo("Saved before new","");NewProject();SaveBeforeLeaving();yield return Ready();
            Check(Project.Title=="Untitled"&&!Project.Dirty&&ChartProject.Read(File.ReadAllBytes(p.FilePath),"saved").Title=="Saved before new","New Project Save and Continue saves before creating empty document");
            NavigateNow(Page.Songs);yield return Ready();Begin(false);Check(Current==Page.Play&&Audio.AnchorBeat==-8,"Play starts one eight-beat count-in");Audio.Stop();Begin(true);Check(Current==Page.Practice&&Audio.AnchorBeat==-8,"Practice uses the same count-in");
            Audio.Stop();var odd=EmptyEditorProject();odd.SetMeter(0,7,8);Library.Add(odd);NavigateNow(Page.Songs);FocusSong(Library.Count-1);yield return Ready();Begin(true);
            Check(Audio.AnchorBeat==-7&&Project.BarAt(-6.9)==-2&&Math.Abs(ReferencePosition(-3.5)+1)<1e-9,"7/8 count-in clock and displayed bars match");Seek(-100);Check(Audio.AnchorBeat==-7,"practice seek begins at the meter-specific count-in");Audio.Stop();
            int midiAt=Array.IndexOf(args,"--tuplet-midi");Check(midiAt>=0&&midiAt+1<args.Length,"tuplet MIDI fixture supplied");
            OpenEditorProject(EmptyEditorProject());yield return Ready();ImportMidi(File.ReadAllBytes(args[midiAt+1]),"Tuplet Check.mid");
            Check(Project.Notes.Count==14&&Project.Grid==.25,"app imports seven-tuples without replacing the placement grid");
            var importedChart=(JObject)Project.Chart.DeepClone();var note=Project.Notes[1];double onset=note.Beat;var point=Editor.NoteRect(note).center;
            Editor.Down(point,0,1,false,false);Editor.Up(point+new Vector2(0,Editor.RowHeight*3));
            Check(Project.Notes.Any(n=>n.Id==note.Id&&n.Beat==onset&&n.Instrument=="HT"),"app vertical drag keeps imported tuplet tick");
            Restore(false);Check(JToken.DeepEquals(Project.Chart,importedChart),"Undo restores complete MIDI chart after drag");
            var file=Path.Combine(root,"Tuplet Check.crproj");Project.SaveNative(file,false);var saved=ChartProject.Read(File.ReadAllBytes(file),Path.GetFileName(file));
            Check(JToken.DeepEquals(saved.Chart,importedChart),"saved MIDI project reopens with exact notes and grid metadata");
            Library.Add(saved);NavigateNow(Page.Songs);FocusSong(Library.Count-1);yield return Ready();bool oldPro=pro;pro=false;
            try{
                Begin(false);Audio.Stop();Audio.AnchorBeat=0;
                foreach(var n in Project.Notes)HandleKey(UnityEngine.InputSystem.Key.Digit9,n.Beat);
                Check(Records.Count==14&&Records.All(r=>r.Judge=="JUST"&&Math.Abs(r.Ms)<1e-9),"Play judges all imported tuplet onsets at zero error");
                Begin(true);
                foreach(var n in Project.Notes)Check(Math.Abs(Audio.BeatAt(Audio.DSPAt(n.Beat))-n.Beat)<1e-8,"Practice audio clock preserves tuplet onset "+n.Index);
                Audio.Stop();
            }finally{pro=oldPro;}
            yield return TempoRuntimeCheck(Check,bytes);
            yield return PitchRuntimeCheck(Check);
            yield return MidiWorkflowCheck(Check);
            yield return ArrangementRuntimeCheck(Check);
            yield return MixerRuntimeCheck(Check);
            File.WriteAllText(Path.Combine(root,"passed.json"),new JObject{{"version","0.3.23"},{"checks",new JArray(checks)}}.ToString());
            Debug.Log("CROSS_RHYTHM_TASKS_CHECK_PASS");
        }finally{if(hadPreview)PlayerPrefs.SetInt("songPreview",originalPreview);else PlayerPrefs.DeleteKey("songPreview");PlayerPrefs.Save();allowApplicationQuit=true;}
        Application.Quit();
    }
}
}
