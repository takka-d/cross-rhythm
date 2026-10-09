using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CrossRhythm {
public partial class CrossRhythmApp {
    string workspaceCheckRoot;
    IEnumerator WorkspaceCheck(){
        var lines=new List<string>();bool failed=false;
        void Check(bool ok,string name){lines.Add((ok?"PASS ":"FAIL ")+name);failed|=!ok;}
        Directory.CreateDirectory(workspaceCheckRoot);
        string first=Path.Combine(workspaceCheckRoot,"SongsA"),second=Path.Combine(workspaceCheckRoot,"SongsB");Directory.CreateDirectory(first);Directory.CreateDirectory(second);
        string aPath=Path.Combine(first,"A.crproj"),bPath=Path.Combine(first,"B.crproj"),cPath=Path.Combine(workspaceCheckRoot,"EditOnly.crproj"),dPath=Path.Combine(second,"D.crproj");
        foreach(var item in new[]{(aPath,"Track A"),(bPath,"Track B"),(cPath,"Editor only"),(dPath,"Track D")}){var p=ChartProject.Demo();p.SetSongInfo(item.Item2,"Test artist");p.SaveNative(item.Item1,false);}
        projectPath=first;Scan();yield return new WaitForSeconds(.5f);
        Check(Library.Count==3&&Library.Any(p=>p.FileName=="A.crproj")&&Library.Any(p=>p.FileName=="B.crproj"),"folder loads all charts");
        ImportNative(cPath);yield return new WaitForSeconds(.5f);var editing=editorProject;
        Check(Current==Page.Edit&&Project==editing&&!Library.Any(p=>p.FileName=="EditOnly.crproj"),"Edit open never adds to Play");
        NavigateNow(Page.Songs);FocusSong(Library.FindIndex(p=>p.FileName=="B.crproj"));yield return new WaitForSeconds(.5f);
        Check(Project.Title=="Track B"&&editorProject==editing,"Play selection preserves Edit file");
        NavigateNow(Page.Edit);yield return new WaitForSeconds(.5f);Check(Project==editing&&Project.Title=="Editor only","Edit returns to its own file");
        ImportNative(aPath);yield return new WaitForSeconds(.5f);var playA=Library.Single(p=>p.FileName=="A.crproj");var originalHash=ChartProject.Hash(File.ReadAllBytes(aPath));
        PushUndo();Project.SetSongInfo("Draft A",Project.Artist);Edited();Audio.AnchorBeat=6;editScroll=new Vector2(120,0);PersistEditorSession();
        Check(Project!=playA&&playA.Title=="Track A"&&!playA.Dirty,"same file uses separate Play and Edit objects");
        Check(File.Exists(EditorRecoveryPath)&&ChartProject.Hash(File.ReadAllBytes(aPath))==originalHash,"recovery saved without overwriting original");
        editorOpenedByUser=false;editorProject=EmptyEditorProject();NavigateNow(Page.Title);RestoreEditorNative();NavigateNow(Page.Edit);yield return new WaitForSeconds(.5f);
        Check(Project.Title=="Draft A"&&Project.Dirty&&Project.FilePath==aPath&&Project.Baseline==originalHash,"dirty edit and baseline restored");
        Check(Math.Abs(Audio.Beat-6)<.001&&Math.Abs(editScroll.x-120)<.001,"editor position restored");
        var retained=Project;projectPath=second;Scan();yield return new WaitForSeconds(.5f);
        Check(Project==retained&&Library.Count==2&&Library.Any(p=>p.FileName=="D.crproj")&&!Library.Any(p=>p.FileName=="A.crproj"||p.FileName=="B.crproj"),"folder replacement removes old list and preserves Edit");
        NavigateNow(Page.Songs);FocusSong(1);yield return new WaitForSeconds(.5f);Check(Project.Title=="Track D","new Play folder selected independently");
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(workspaceCheckRoot,"Play.png"));yield return new WaitForSeconds(.2f);
        NavigateNow(Page.Edit);yield return new WaitForSeconds(.5f);Check(Project==retained&&Project.Title=="Draft A","Edit survives folder change");
        Save(false);yield return WaitForOperations();Check(!Project.Dirty&&ChartProject.Read(File.ReadAllBytes(aPath),"A.crproj").Title=="Draft A","explicit Save writes editor file");
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(workspaceCheckRoot,"Edit.png"));yield return new WaitForSeconds(.2f);
        NewProject();Check(Project==editorProject&&Project.Title=="Untitled"&&Library.Count==2,"New Project stays outside Play list");
        lines.Add(failed?"WORKSPACE_RUNTIME_FAIL":"WORKSPACE_RUNTIME_PASS");File.WriteAllLines(Path.Combine(workspaceCheckRoot,"workspace-check.txt"),lines);Application.Quit(failed?1:0);
    }
}
}
