using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator RevisionCheck(){
        var args=Environment.GetCommandLineArgs();string folder=args[Array.IndexOf(args,"--smoke-output")+1];Directory.CreateDirectory(folder);AudioListener.volume=0;
        string fixtures=args[Array.IndexOf(args,"--fixture-folder")+1];yield return new WaitForSeconds(2);
        var reports=new System.Collections.Generic.List<string>();bool ok=true;
        void Check(bool value,string name){ok&=value;reports.Add((value?"PASS ":"FAIL ")+name);}
        var oldAudioProject=Audio.Project;OnImportBatch("open");
        foreach(var path in Directory.GetFiles(fixtures,"*.crproj")){
            byte[] bytes=File.ReadAllBytes(path);OnImportStart(new JObject{{"name",Path.GetFileName(path)},{"token",path},{"size",bytes.Length},{"kind","project"}}.ToString());
            for(int i=0;i<bytes.Length;i+=49152)OnImportChunk(Convert.ToBase64String(bytes,i,Math.Min(49152,bytes.Length-i)));OnImportEnd("");
        }
        Check(ReferenceEquals(Audio.Project,oldAudioProject),"batch registers every chart without decoding every song");
        OnLibrarySource(new JObject{{"kind","folder"},{"name",fixtures}}.ToString());yield return new WaitForSeconds(2);
        Check(Library.Any(p=>p.FileName=="black_market_blues.crproj")&&Library.Any(p=>p.FileName.Contains("上海ハニー")),"both named tracks loaded");
        Current=Page.Edit;var editing=Project;PushUndo();Project.SetSongInfo(Project.Title+" - test",Project.Artist);int undoCount=undo.Count;
        int other=Library.FindIndex(p=>p!=editing&&!string.IsNullOrEmpty(p.FilePath));ImportNative(Library[other].FilePath);yield return new WaitForSeconds(2);
        Check(Project!=editing&&editing.Dirty,"Open Project switches while retaining previous unsaved edits");SelectProject(Library.IndexOf(editing));yield return new WaitForSeconds(2);
        Check(undo.Count==undoCount&&Project.Title.EndsWith(" - test"),"return restores edits and undo history");Restore(false);
        NavigateNow(Page.Songs);FocusSong(Library.FindIndex(p=>p.FileName=="black_market_blues.crproj"));yield return new WaitForSeconds(3);
        Check(Audio.Backing.isPlaying&&!Audio.Running&&Audio.Project==Project,"focused song previews backing without game judgment");
        FocusSong(Library.FindIndex(p=>p.FileName.Contains("上海ハニー")));yield return new WaitForSeconds(3);
        Check(Audio.Backing.isPlaying&&Audio.Project==Project&&Project.FileName.Contains("上海ハニー"),"preview follows focus to second song");
        NavigateNow(Page.Config);Check(!Audio.Backing.isPlaying,"leaving track selection stops preview");
        var savedNormal=normalBindings;var savedPro=proBindings;bool savedMode=pro;
        // This CLI check runs hidden. Keep virtual devices enabled; real play still
        // uses normal focus handling and ReleaseOnFocusLoss.
        var focusBehavior=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        normalBindings=ControlBindings.Defaults(false);proBindings=ControlBindings.Defaults(true);pro=true;Begin(false);
        var testPad=InputSystem.AddDevice<Gamepad>();var testKeyboard=InputSystem.AddDevice<Keyboard>();
        InputSystem.EnableDevice(testPad);InputSystem.EnableDevice(testKeyboard);
        reports.Add("STATE running="+Audio.Running+" loaded="+loaded+" busy="+busy+" page="+Current+" focus="+Application.isFocused+" pad="+testPad.enabled+" keyboard="+testKeyboard.enabled);
        InputSystem.QueueStateEvent(testPad,new GamepadState(GamepadButton.East){leftTrigger=1});yield return null;
        Check(held.Contains(Key.O)&&held.Contains(Key.C)&&closed,"gamepad Notion SN and pedal simultaneous press");
        InputSystem.QueueStateEvent(testPad,new GamepadState());yield return null;
        Check(held.Count==0&&!closed,"gamepad release clears logical keys and pedal");
        proBindings.BindKey(0,Key.Z);InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Z));yield return null;
        Check(held.Contains(Key.R)&&!held.Contains(Key.Z),"remapped keyboard reaches v175 logical action");
        InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());yield return null;
        Check(held.Count==0,"remapped keyboard release clears action");
        InputSystem.QueueStateEvent(testPad,new GamepadState{rightTrigger=1});yield return null;
        Check(closed,"second gamepad pedal closes hi-hat");InputSystem.RemoveDevice(testPad);
        Check(!closed&&held.Count==0,"controller disconnect releases held pedal");
        InputSystem.RemoveDevice(testKeyboard);InputSystem.settings.backgroundBehavior=focusBehavior;normalBindings=savedNormal;proBindings=savedPro;pro=savedMode;NavigateNow(Page.Config);
        File.WriteAllLines(Path.Combine(folder,"revision-check.txt"),reports);Application.Quit(ok?0:2);
    }
}
}
