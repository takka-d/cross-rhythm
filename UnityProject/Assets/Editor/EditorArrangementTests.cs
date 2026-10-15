using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public static class EditorArrangementTests {
    static void Check(bool ok,string name){if(!ok)throw new Exception("ARRANGEMENT: "+name);Debug.Log("PASS arrangement: "+name);}
    public static void Run(){
        var p=ChartProject.Demo();p.Chart["events"]=new JArray();p.Rebuild();
        Check(p.SnapToGrid,"older projects default to Grid");
        var e=new EditorInteraction(new HashSet<int>()){Project=p,PPB=100};int undos=0;e.BeforeEdit=()=>undos++;
        var point=new Vector2(137,4.5f*46);e.Down(point,0,1,false,false);e.Up(point);
        Check(p.Notes.Single().Beat==1.25,"Grid placement retains cell floor");
        p.Chart["editorSnapToGrid"]=false;
        point=new Vector2(217,5.5f*46);e.Down(point,0,1,false,false);e.Up(point);
        var note=p.Notes.Single(n=>n.Instrument=="MT");
        Check(Math.Abs(note.Beat-2.17)<1e-6,"Free placement keeps pointer position");
        var at=e.NoteRect(note).center;int before=undos;e.Down(at,0,1,false,false);e.Move(at+new Vector2(13,0));e.Up(at+new Vector2(13,0));
        note=p.Notes.Single(n=>n.Instrument=="MT");Check(Math.Abs(note.Beat-2.30)<1e-6&&undos==before+1,"Free drag uses exact delta with one undo");
        at=e.NoteRect(note).center;e.Down(at,0,1,false,false);e.Up(at+new Vector2(0,46));
        Check(Math.Abs(p.Notes.Single(n=>n.Instrument=="FT").Beat-2.30)<1e-6,"Free vertical move preserves onset");
        e.Copy();e.Paste(4.123);Check(p.Notes.Any(n=>Math.Abs(n.Beat-4.123)<1e-8),"Free paste keeps requested position");
        point=new Vector2(631,8.5f*46);e.Down(point,0,1,false,false);e.Up(new Vector2(687,point.y));
        var pedal=p.Notes.Single(n=>n.Pedal);Check(Math.Abs(pedal.Beat-6.31)<1e-6&&Math.Abs(pedal.Duration-.56)<1e-6,"Free pedal drag keeps fractional endpoints");
        var edge=e.NoteRect(pedal);e.Down(new Vector2(edge.xMax-1,edge.center.y),0,1,false,false);e.Up(new Vector2(717,edge.center.y));
        Check(Math.Abs(p.Notes.Single(n=>n.Pedal).Duration-.86)<1e-6,"Free pedal resize does not snap");
        var saved=ChartProject.Read(p.Write(),"free.crproj");Check(!saved.SnapToGrid&&JToken.DeepEquals(p.Chart,saved.Chart),"Free mode and onsets persist through project save");
        Check(Math.Abs(EditorInteraction.AudioOffsetForDrag(p,.5,1,2)+.0)<1e-9,"moving wave right delays audio without moving notes");
        p.SetTempo(0,2,60);
        double shifted=EditorInteraction.AudioOffsetForDrag(p,.75,1,3.123);
        Check(Math.Abs(shifted+p.SecondsAtBeat(3.123)-(.75+p.SecondsAtBeat(1)))<1e-9,"wave grab stays attached across tempo changes");
        p.Chart["editorSnapToGrid"]=true;
        shifted=EditorInteraction.AudioOffsetForDrag(p,.75,1,1.37);
        Check(Math.Abs(shifted-.625)<1e-9,"Grid wave movement snaps beat delta");
        Check(PlayerSettingCheck(),"Windows launcher owns single instance handling");
        Debug.Log("CROSS_RHYTHM_ARRANGEMENT_TESTS_PASS");
    }
    static bool PlayerSettingCheck(){
        string launcher=System.IO.Path.GetFullPath("Tools/Launcher/CrossRhythmLauncher.cs");
        string startup=System.IO.Path.GetFullPath("Assets/Scripts/CrossRhythmStartup.cs");
        if(UnityEditor.PlayerSettings.forceSingleInstance||!System.IO.File.Exists(launcher)||!System.IO.File.Exists(startup))return false;
        string source=System.IO.File.ReadAllText(launcher),startupSource=System.IO.File.ReadAllText(startup);
        const string startupWait="while(!nativeStartupCancelled&&busy)yield return null;";
        int restoreAt=startupSource.IndexOf("RestoreEditorNative();",StringComparison.Ordinal);
        int requestedAt=startupSource.IndexOf("ImportNative(args[at+1]);",StringComparison.Ordinal);
        int waitAt=startupSource.IndexOf(startupWait,StringComparison.Ordinal);
        return source.Contains("PackageKey(AppDomain.CurrentDomain.BaseDirectory)")
            &&source.Contains("new Mutex(true,mutexName,out first)")
            &&source.Contains("FormBorderStyle=FormBorderStyle.None")
            &&source.Contains("visiblePaints>0")
            &&source.Contains("FindPlayer(path)")
            &&!source.Contains("TryActivatePlayer")
            &&!source.Contains("AutoEllipsis=true")
            &&source.Contains("--startup-ready-event")
            &&startupSource.Contains("EventWaitHandle.OpenExisting(nativeStartupReadyEvent)")
            &&startupSource.Contains("if(nativeStartupExternalOverlay)")
            &&restoreAt>=0&&requestedAt>restoreAt&&waitAt>requestedAt
            &&startupSource.IndexOf(startupWait,waitAt+startupWait.Length,StringComparison.Ordinal)<0;
    }
}
}
