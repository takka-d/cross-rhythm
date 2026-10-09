using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;

namespace CrossRhythm {
public partial class CrossRhythmApp : MonoBehaviour {
    public enum Page {Title,Songs,Play,Practice,Result,Config,Edit}
    public Page Current=Page.Title;
    public ChartProject Project;
    public RhythmAudio Audio;
    public List<ChartProject> Library=new List<ChartProject>();
    public List<HitRecord> Records=new List<HitRecord>();
    public ScoreResult Result=new ScoreResult();
    Dictionary<int,HitRecord> judged=new Dictionary<int,HitRecord>();
    int previewCursor;
    void ResetScheduled(){scheduled.Clear();previewCursor=0;}
    HashSet<int> scheduled=new HashSet<int>();
    HashSet<Key> held=new HashSet<Key>();
    Dictionary<Key,string> footRoles=new Dictionary<Key,string>();
    List<PedalRange> heldPedal=new List<PedalRange>();
    double? pedalStart;
    bool closed,auto,pro,loaded,busy,english,metronome,discardPrompt;
    Page pendingPage;
    string status="",projectPath="",lastJudge="";
    double lastJudgeTime,lastClick=-999,inputOffset;
    Vector2 songScroll;
    int selected;
    float W,H,scale;
    Font font;
    GUIStyle label,button,field,small,title,big,center;
    
    readonly Color bg=new Color(.027f,.045f,.063f),panel=new Color(.055f,.087f,.116f),line=new Color(.15f,.21f,.25f),mint=new Color(.56f,.9f,.76f),muted=new Color(.57f,.66f,.72f);
    MemoryStream incoming;
    string incomingName,incomingToken,incomingKind;
    ChartProject incomingMidiProject;
    int expectedBytes;
    ChartProject savingProject;byte[] savingBytes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(FindAnyObjectByType<CrossRhythmApp>()==null)new GameObject("CrossRhythm").AddComponent<CrossRhythmApp>();}
    void Awake(){
        gameObject.name="CrossRhythm";Application.targetFrameRate=120;Application.runInBackground=false;
        font=Resources.Load<Font>("Fonts/NotoSansJP");
        uiFeedback=gameObject.AddComponent<UiFeedback>();Audio=gameObject.AddComponent<RhythmAudio>();Audio.OnReady=ProjectAudioReady;
        english=PlayerPrefs.GetInt("language",0)==1;pro=PlayerPrefs.GetInt("pro",0)==1;inputOffset=PlayerPrefs.GetFloat("offset",0);projectPath=PlayerPrefs.GetString("projects","");songPreviewEnabled=PlayerPrefs.GetInt("songPreview",0)==1;
        Application.wantsToQuit+=ConfirmApplicationQuit;
        try{lastSource=JObject.Parse(PlayerPrefs.GetString("projectSource","{}"));if(!lastSource.HasValues)lastSource=null;}catch{lastSource=null;}
        #if !UNITY_WEBGL || UNITY_EDITOR
        var workspaceArgs=Environment.GetCommandLineArgs();int workspaceAt=Array.IndexOf(workspaceArgs,"--workspace-check");if(workspaceAt>=0&&workspaceAt+1<workspaceArgs.Length){workspaceCheckRoot=workspaceArgs[workspaceAt+1];projectPath="";}
#endif
        LoadBindings();InputSystem.onDeviceChange+=InputDeviceChanged;InputSystem.pollingFrequency=120;
        editorProject=EmptyEditorProject();playSelection=PlayerPrefs.GetString("playSelection","");Library.Add(ChartProject.Demo());ActivateProject(Library[0]);
        InputSystem.onEvent+=OnInput;
        InitializeMidiDrop();

#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CRInit();
        PlatformFiles.CRRestore(gameObject.name);
#else
        RestoreLibrary();RestoreEditorNative();
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"--project-file");if(at>=0&&at+1<args.Length)ImportNative(args[at+1]);
        if(!string.IsNullOrEmpty(workspaceCheckRoot)){Application.runInBackground=true;if(!args.Contains("--interactive-check"))StartCoroutine(args.Contains("--tasks-check")?TasksCheck():args.Contains("--practice-draft-check")?PracticeDraftCheck():WorkspaceCheck());}
        if(args.Contains("--revision-check")){Application.runInBackground=true;StartCoroutine(RevisionCheck());}
        if(args.Contains("--editor-audio-check")){Application.runInBackground=true;StartCoroutine(EditorAudioCheck());}
        if(args.Contains("--smoke-test")){Application.runInBackground=true;StartCoroutine(SmokeTest());}
#endif
    }
    void OnDestroy(){DisposeMidiDrop();InputSystem.onEvent-=OnInput;InputSystem.onDeviceChange-=InputDeviceChanged;Application.wantsToQuit-=ConfirmApplicationQuit;}
    string T(string jp,string en)=>english?en:jp;
    void SelectProject(int index){if(index<0||index>=Library.Count)return;selected=index;SavePlaySelection();if(Current!=Page.Edit&&!ReferenceEquals(Project,editorProject))ActivateProject(Library[index]);}
    void AddProject(byte[] bytes,string name,string path){
        var p=ChartProject.Read(bytes,name,path);int ix=ProjectLibrary.Upsert(Library,p);
        if(importBatch){if(importedIndex<0)importedIndex=ix;importedCount++;return;}
        if(ReferenceEquals(Project,Library[ix])){status=T("このプロジェクトは開いています","This project is already open");return;}
        SelectProject(ix);
    }
    void ImportNative(string path){try{var bytes=File.ReadAllBytes(path);if(importBatch)AddProject(bytes,Path.GetFileName(path),path);else OpenEditorProject(ChartProject.Read(bytes,Path.GetFileName(path),path));}catch(Exception e){status=e.Message;busy=false;}}
    void RestoreLibrary(){if(!Directory.Exists(projectPath))return;Scan();}
    void Scan(){if(!Directory.Exists(projectPath)){status=T("フォルダーが見つかりません","Folder not found");return;}if(string.IsNullOrEmpty(workspaceCheckRoot)){PlayerPrefs.SetString("projects",projectPath);PlayerPrefs.Save();}OnImportBatch("restore");foreach(string path in Directory.GetFiles(projectPath,"*.crproj",SearchOption.AllDirectories))ImportNative(path);OnLibrarySource(new JObject{{"kind","folder"},{"name",projectPath}}.ToString());}
    void PickProject(bool folder,bool editor=false){
        if(InputBlocked)return;
        if(editor)Audio.Pause();
#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CRPick(gameObject.name,folder?1:editor?2:0);
#else
        if(folder){StartCoroutine(PickFolderNative());return;}string path=PlatformFiles.Pick(false);if(path==null)return;StartCoroutine(LoadNativeProject(path));
#endif
    }
    public void OnImportStart(string json){var p=JObject.Parse(json);incomingName=(string)p["name"];incomingToken=(string)p["token"];incomingKind=(string)p["kind"]??"project";incomingSource=p["source"] as JObject;expectedBytes=(int)p["size"];if(expectedBytes>256*1024*1024)throw new Exception("Project too large");incoming=new MemoryStream();incomingMidiProject=(incomingKind=="midi"||incomingKind=="midi-drop")&&CanImportMidi?Project:null;}
    public void OnImportChunk(string data){byte[] b=Convert.FromBase64String(data);incoming.Write(b,0,b.Length);}
    public void OnImportEnd(string _){try{if(incoming.Length!=expectedBytes)throw new Exception("Incomplete project transfer");byte[] bytes=incoming.ToArray();incoming.Dispose();incoming=null;if(incomingKind=="audio"){SetAudio(bytes,incomingName);}else if(incomingKind=="midi"||incomingKind=="midi-drop"){if(!ReferenceEquals(Project,incomingMidiProject)||!CanImportMidi){status=T("Editで読込をやり直してください","Import again in Edit");return;}ImportMidi(bytes,incomingName);}else{if(incomingSource!=null)projectOrigins[incomingToken]=incomingSource;if(incomingKind=="editor"||incomingKind=="editor-restore"){var p=ChartProject.Read(bytes,incomingName,incomingToken);OpenEditorProject(p,incomingKind=="editor-restore",incomingSource?["session"] as JObject);}else AddProject(bytes,incomingName,incomingToken);}}catch(Exception e){status=e.Message;busy=false;if(importBatch)importErrors.Add(incomingName+": "+e.Message);}}
    void Navigate(Page page){if(InputBlocked||MidiPromptOpen||page==Current)return;if(Current==Page.Edit&&editorProject.Dirty){AskBeforeLeaving(()=>Transition(()=>NavigateNow(page),page.ToString()));return;}Transition(()=>NavigateNow(page),page.ToString());}
    void NavigateNow(Page page){if(savingProject!=null||draftRunning||MidiPromptOpen)return;EndSongInfoEdit();CancelWaveMove();laneTypeRow=-1;editorFileOpen=false;interaction?.Reset();pendingPerformance=null;pendingPracticeSpeed=null;stageLoadError="";previewRequested=false;Audio.Pause();Audio.Rate=1;ReleaseInputs();closed=false;if(ReferenceEquals(Project,editorProject)){RememberEditorSession();QueueEditorRecovery();PersistEditorSession();}Current=page;if(page==Page.Edit)ActivateProject(editorProject);else if(page==Page.Songs)ActivateProject(Library[Mathf.Clamp(selected,0,Library.Count-1)]);if(page==Page.Edit&&Audio.AnchorBeat<0)Audio.AnchorBeat=0;ResetMenuFocus();if(page==Page.Songs)RequestSongPreview();GUI.FocusControl(null);}
    void Begin(bool practice){
        if(savingProject!=null||Project==null||importBatch||incoming!=null)return;
        bool ready=loaded&&!busy&&ReferenceEquals(Audio.Project,Project);
        Current=practice?Page.Practice:Page.Play;previewRequested=false;stageLoadError="";pendingPracticeSpeed=null;
        stageGrids.Clear();Records.Clear();judged.Clear();ResetScheduled();stageEffects.Clear();ReleaseInputs();heldPedal.Clear();pedalStart=null;closed=false;auto=false;lastClick=-999;lastJudge="";
        Audio.Stop();Audio.AnchorBeat=CountIn.Start(Project);
        if(ready){StartPreparedPerformance();return;}
        pendingPerformance=Project;
        if(pendingSongAudio!=null||!ReferenceEquals(Audio.Project,Project)||!busy){pendingSongAudio=null;busy=true;loaded=false;Audio.Load(Project);}
    }
    void Seek(double beat){beat=Math.Max(CountIn.Start(Project),Math.Min(Project.Length,beat));Audio.Seek(beat);stageEffects.Clear();Records.Clear();judged.Clear();ResetScheduled();heldPedal.Clear();pedalStart=null;ReleaseInputs();closed=false;lastClick=Math.Floor(beat)-1;}
    bool Matches(ChartNote n,Key key){
        if(n.Pedal)return false;
        if(!pro){if(key==Key.Digit9)return n.Lane=="H1"||n.Lane=="HANY";if(key==Key.Digit4)return n.Lane=="H2"||n.Lane=="HANY";return (key==Key.V||key==Key.M)&&n.Instrument=="BD"&&!footRoles.ContainsKey(key);}
        return (key==Key.R||key==Key.O)?n.Instrument=="SN":(key==Key.E||key==Key.P)?new[]{"HT","MT","FT"}.Contains(n.Instrument):(key==Key.Digit4||key==Key.Digit9)?n.Instrument=="HH":(key==Key.Digit3||key==Key.Digit0)?n.Instrument=="CR"||n.Instrument=="RD":(key==Key.V||key==Key.M)&&n.Instrument=="BD";
    }
    double NoteSeconds(double from,double to)=>(Project.SecondsAtBeat(to)-Project.SecondsAtBeat(from))/Audio.Rate;
    double BeatAfter(double beat,double seconds)=>Project.BeatAtSeconds(Project.SecondsAtBeat(beat)+seconds*Audio.Rate);
    void HandleKey(Key key,double beat){
        if(auto)return;
        if(pro&&(key==Key.C||key==Key.Comma)){SetPedal(true,beat);return;}
        var n=Project.Notes.Where(n=>Matches(n,key)&&!judged.ContainsKey(n.Index)&&Math.Abs(NoteSeconds(n.Beat,beat))<=.12).OrderBy(n=>Math.Abs(NoteSeconds(n.Beat,beat))).FirstOrDefault();
        if(!pro&&(key==Key.V||key==Key.M)){
            if(n==null&&Project.ClosedAt(beat)){footRoles[key]="pedal";SetPedal(true,beat);return;}
            if(n!=null)footRoles[key]="kick";
        }
        if(n==null)return;
        double ms=NoteSeconds(n.Beat,beat)*1000;Judge(n,ms,Math.Abs(ms)<=45?"JUST":ms<0?"FAST":"LATE");Audio.Drum(n,closed);
    }
    void SetPedal(bool value,double beat){if(value==closed)return;closed=value;if(value){pedalStart=beat;Audio.Pedal();}else if(pedalStart.HasValue){heldPedal.Add(new PedalRange{Start=pedalStart.Value,End=beat});pedalStart=null;}}
    void Judge(ChartNote n,double ms,string kind){if(judged.ContainsKey(n.Index))return;var r=new HitRecord{Index=n.Index,Beat=n.Beat,Instrument=n.Instrument,Ms=ms,Judge=kind};judged[n.Index]=r;AddReferenceEffect(n,kind);if(Current==Page.Play)Records.Add(r);lastJudge=kind+(kind=="MISS"?"":$"  {ms:+0.0;-0.0;0.0} ms");lastJudgeTime=Time.realtimeSinceStartupAsDouble;}
    void Update(){
        if(Project==null)return;
        UpdateMidiDrop();
        if(InputBlocked){UpdateUnsavedBrowserGuard();return;}
        UpdateSongPreview();UpdatePracticeSpeed();UpdateEditorRecovery();UpdateUnsavedBrowserGuard();
        UpdateDisplayKeys();
        EditorFrame();
        if(Current==Page.Practice&&Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame&&loaded&&!busy&&!discardPrompt&&!showMeterPanel){if(Audio.Running)Audio.Pause();else {ResetScheduled();lastClick=Math.Floor(Audio.Beat)-1;Audio.Play(Audio.Beat);}}
        if(Current!=Page.Play&&Current!=Page.Practice&&Current!=Page.Edit)return;
        if(!Audio.Running||Audio.Preparing)return;
        double b=Audio.Beat;
        bool preview=Current==Page.Edit||auto;
        if(preview){
            bool nextClosed=Project.ClosedAt(b);SetPedal(nextClosed,b);
            double until=BeatAfter(b,.35);
            while(previewCursor<Project.Notes.Count){var n=Project.Notes[previewCursor];if(n.Beat>until)break;previewCursor++;if(n.Pedal||n.Beat<Audio.AnchorBeat-1e-8||scheduled.Contains(n.Index))continue;scheduled.Add(n.Index);Audio.Drum(n,Project.ClosedAt(n.Beat),Audio.DSPAt(n.Beat));}
        }
        if(Current!=Page.Edit){foreach(var n in Project.Notes){if(n.Pedal||judged.ContainsKey(n.Index)||n.Beat<Audio.AnchorBeat-1e-8)continue;if(auto&&n.Beat<=b)Judge(n,0,"JUST");else if(!auto&&NoteSeconds(n.Beat,b)>.12)Judge(n,double.NaN,"MISS");}}
        if(!pro&&!preview&&Project.ClosedAt(b)&&!closed){var releasedKick=footRoles.FirstOrDefault(kv=>kv.Value=="kick"&&held.Contains(kv.Key));if(!releasedKick.Equals(default(KeyValuePair<Key,string>))){bool near=Project.Notes.Any(n=>n.Instrument=="BD"&&Math.Abs(NoteSeconds(n.Beat,b))<.12);if(!near){footRoles[releasedKick.Key]="pedal";SetPedal(true,b);}}}
        if(metronome||b<0){
            double from=Math.Max(Audio.AnchorBeat,lastClick+0.000001),until=BeatAfter(b,.12);
            foreach(double q in CountIn.Between(Project,from,until)){Audio.Click(CountIn.Accent(Project,q),Audio.DSPAt(q));lastClick=q;}
            if(metronome&&until>=0)foreach(double q in Project.Pulses(Math.Max(0,from),until)){Audio.Click(Project.IsBarStart(q),Audio.DSPAt(q));lastClick=q;}
        }
        if(NoteSeconds(Project.Length,b)>.3){Audio.Stop();Audio.AnchorBeat=Project.Length;if(Current==Page.Play){Result=RhythmScore.Calculate(Project.Notes,Records);SaveResult();Current=Page.Result;}}
    }
    void SaveResult(){string key="best:"+Project.Title;float old=PlayerPrefs.GetFloat(key,-1);if(Result.Score>old)PlayerPrefs.SetFloat(key,(float)Result.Score);PlayerPrefs.Save();}
    public void SetAudio(byte[] bytes,string name){editorPanel=2;ClearDraft();PushUndo();string audioPath="audio/"+Path.GetFileName(name);Project.Files[audioPath]=bytes;Project.Manifest["audio"]=new JObject{{"path",audioPath},{"originalName",name},{"size",bytes.Length}};Project.Dirty=true;QueueEditorRecovery();busy=true;loaded=false;Audio.Load(Project);}
    void PickAudio(){editorFileOpen=false;editorPanel=2;
#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CRPickAudio(gameObject.name);
#else
        string path=PlatformFiles.Pick(false,"",true);if(path!=null)SetAudio(File.ReadAllBytes(path),Path.GetFileName(path));
#endif
    }
}
}
