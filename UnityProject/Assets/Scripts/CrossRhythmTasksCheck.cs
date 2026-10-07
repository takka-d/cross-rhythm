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
            var p=EmptyEditorProject();string path=Path.Combine(root,"save-check.crproj");p.SaveNative(path,false);OpenEditorProject(p);yield return Ready();
            p.SetSongInfo("Unsaved title","Test artist");QueueEditorRecovery();Navigate(Page.Songs);
            Check(discardPrompt&&Current==Page.Edit&&p.Dirty,"navigation opens unsaved prompt and stays in Edit");
            CancelLeaving();Check(!discardPrompt&&Current==Page.Edit&&p.Title=="Unsaved title"&&p.Dirty,"Cancel retains edits and stays in Edit");
            Navigate(Page.Songs);CompleteLeaving();Check(Current==Page.Songs&&editorProject==p&&p.Dirty&&File.Exists(EditorRecoveryPath),"Leave keeps the editor draft and recovery file");
            NavigateNow(Page.Edit);yield return Ready();Navigate(Page.Title);SaveBeforeLeaving();
            Check(Current==Page.Title&&!discardPrompt&&!p.Dirty&&ChartProject.Read(File.ReadAllBytes(path),"check").Title=="Unsaved title","Save and Continue waits for verified save and navigates");
            NavigateNow(Page.Edit);yield return Ready();p.SetSongInfo("Newer unsaved title","");File.AppendAllText(path,"external change");Navigate(Page.Songs);SaveBeforeLeaving();
            Check(discardPrompt&&Current==Page.Edit&&p.Dirty&&saveState==SaveState.Error,"save conflict keeps the prompt open and does not navigate");CancelLeaving();
            Check(!ConfirmApplicationQuit()&&discardPrompt&&Current==Page.Edit,"window close is blocked by unsaved prompt");CancelLeaving();
            Check(p.Title=="Newer unsaved title"&&p.Dirty,"cancelling close preserves changes");
            NavigateNow(Page.Songs);yield return Ready();Begin(false);Check(Current==Page.Play&&Audio.AnchorBeat==-8,"Play starts one eight-beat count-in");Audio.Stop();Begin(true);Check(Current==Page.Practice&&Audio.AnchorBeat==-8,"Practice uses the same count-in");
            File.WriteAllText(Path.Combine(root,"passed.json"),new JObject{{"version","0.3.14"},{"checks",new JArray(checks)}}.ToString());
            Debug.Log("CROSS_RHYTHM_TASKS_CHECK_PASS");
        }finally{if(hadPreview)PlayerPrefs.SetInt("songPreview",originalPreview);else PlayerPrefs.DeleteKey("songPreview");PlayerPrefs.Save();allowApplicationQuit=true;}
        Application.Quit();
    }
}
}
