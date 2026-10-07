using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using Newtonsoft.Json.Linq;
#if UNITY_EDITOR_WIN
using NAudio.Wave;
#endif
namespace CrossRhythm {
public static class CrossRhythmBuild {
    static string Arg(string name,string fallback=""){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0&&i+1<a.Length?a[i+1]:fallback;}
    public static void Prepare(){
        PlayerSettings.companyName="TakKa";PlayerSettings.productName="Cross Rhythm";PlayerSettings.bundleVersion="0.3.13";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=false;PlayerSettings.colorSpace=ColorSpace.Gamma;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone,ApiCompatibilityLevel.NET_Standard);
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=2;settings.ApplyModifiedPropertiesWithoutUndo();}
        Directory.CreateDirectory("Assets/Scenes");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=new GameObject("Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.027f,.045f,.063f);new GameObject("AudioListener").AddComponent<AudioListener>();EditorSceneManager.SaveScene(scene,"Assets/Scenes/CrossRhythm.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/CrossRhythm.unity",true)};
        string samples=Arg("-sampleSource");
#if UNITY_EDITOR_WIN
        if(samples!="")foreach(string key in RhythmAudio.SampleKeys){string inputFile=Path.Combine(samples,key+".flac"),output="Assets/Resources/Drums/"+key+".wav";if(!File.Exists(output)){using(var reader=new MediaFoundationReader(inputFile))WaveFileWriter.CreateWaveFile(output,reader);}}
#else
        if(samples!="")throw new Exception("sampleSource conversion requires a Windows Unity Editor");
#endif
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string path in Directory.GetFiles("Assets/Resources/NoteGlyphs","*.png")){var importer=AssetImporter.GetAtPath(path) as TextureImporter;importer.textureType=TextureImporterType.Default;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=256;importer.SaveAndReimport();}
        foreach(string path in Directory.GetFiles("Assets/Resources/Stage175","*.png")){var importer=AssetImporter.GetAtPath(path) as TextureImporter;importer.textureType=TextureImporterType.Default;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=4096;importer.SaveAndReimport();}
        foreach(string path in Directory.GetFiles("Assets/Resources/Drums","*.wav")){var importer=AssetImporter.GetAtPath(path) as AudioImporter;var sample=importer.defaultSampleSettings;sample.loadType=AudioClipLoadType.DecompressOnLoad;sample.compressionFormat=AudioCompressionFormat.Vorbis;sample.quality=.85f;sample.preloadAudioData=true;importer.defaultSampleSettings=sample;importer.SaveAndReimport();}
        AssetDatabase.SaveAssets();Debug.Log("CROSS_RHYTHM_PREPARED");
    }
    public static void Windows(){Prepare();Tests();string output=Arg("-buildOutput",Path.GetFullPath("../CrossRhythm-Windows/CrossRhythm.exe"));Directory.CreateDirectory(Path.GetDirectoryName(output));var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,output,BuildTarget.StandaloneWindows64,BuildOptions.None);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed: "+report.summary.result);CopyAdtofTools(Path.GetDirectoryName(output));Debug.Log("CROSS_RHYTHM_WINDOWS_BUILD_PASS "+output);}
    static void CopyAdtofTools(string buildRoot){string source=Path.GetFullPath("Tools/ADTOF");if(!Directory.Exists(source))return;string target=Path.Combine(buildRoot,"ADTOF");Directory.CreateDirectory(target);foreach(string file in Directory.GetFiles(source)){File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);}}
    public static void Web(){Prepare();Tests();PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Disabled;PlayerSettings.WebGL.template="PROJECT:CrossRhythm";PlayerSettings.WebGL.initialMemorySize=256;PlayerSettings.WebGL.maximumMemorySize=2048;PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL,ScriptingImplementation.IL2CPP);string output=Arg("-buildOutput",Path.GetFullPath("../CrossRhythm-Web"));Directory.CreateDirectory(output);var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes,output,BuildTarget.WebGL,BuildOptions.None);if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Web build failed: "+report.summary.result);Debug.Log("CROSS_RHYTHM_WEB_BUILD_PASS "+output);}
    static void Check(bool ok,string message){if(!ok)throw new Exception("TEST FAILED: "+message);Debug.Log("PASS "+message);}
    public static void Tests(){
        ControlBindingsTests.Run();
        PracticeDraftTests.Run();
        AdtofTests.Run();
        LibraryTests();
        MetadataDifficultyTests();
        SaveSnapshotTests();
        VisualTests();
        EditorTests();
        EditorInteractionTests.Run();
        MidiTests();
        var demo=ChartProject.Demo();var notes=demo.Notes.Where(n=>!n.Pedal).ToArray();var perfect=notes.Select(n=>new HitRecord{Index=n.Index,Beat=n.Beat,Instrument=n.Instrument,Ms=0,Judge="JUST"}).ToArray();Check(RhythmScore.Calculate(notes,perfect).Score==100,"perfect score 100");Check(RhythmScore.Calculate(notes,perfect.Take(perfect.Length/2)).Score<=50.2,"miss coverage penalty");
        var stable=perfect.Select(r=>new HitRecord{Index=r.Index,Beat=r.Beat,Ms=60,Judge="LATE"}).ToArray();Check(Math.Abs(RhythmScore.Calculate(notes,stable).Score-92)<.01,"stable late score 92");Check(RhythmScore.Calculate(notes,new HitRecord[0]).Score==0,"all missed score 0");
        foreach(int v in Enumerable.Range(0,6)){Check(ChartProject.Strength(new JValue(v))==v,"literal strength "+v);}
        foreach(int q in new[]{12,20,28,52}){demo.Chart["quantize"]=q;demo.Events.Add(new JObject{{"measure",0},{"beat",4.0/q},{"instrument","SN"},{"velocity",2},{"durationBeats",4.0/q},{"gridStepBeats",4.0/q}});demo.Rebuild();var reopened=ChartProject.Read(demo.Write(),"test.crproj");Check(JToken.DeepEquals(demo.Chart,reopened.Chart),"tuplet roundtrip "+q);}
        string fixture=Arg("-fixture");if(fixture!=""){
            byte[] bytes=File.ReadAllBytes(fixture);var project=ChartProject.Read(bytes,Path.GetFileName(fixture));var copy=ChartProject.Read(project.Write(),"roundtrip.crproj");Check(JToken.DeepEquals(project.Chart,copy.Chart),"fixture chart lossless roundtrip");foreach(var kv in project.Files.Where(kv=>!kv.Key.EndsWith(".json")))Check(kv.Value.SequenceEqual(copy.Files[kv.Key]),"asset unchanged "+kv.Key);
            string reference=Arg("-referenceNotes");if(reference!=""){
                var expected=JArray.Parse(File.ReadAllText(reference));int checkedNotes=0;
                foreach(JObject n in expected){var match=project.Notes.FirstOrDefault(x=>!x.Pedal&&x.Measure==(int)n["measure"]&&Math.Abs(x.Local-(double)n["local"])<1e-8&&x.Instrument==(string)n["inst"]&&x.Id==(string)n["id"]);if(match==null||match.Lane!=(string)n["lane"]||Math.Abs(ChartVisuals.CellX(project,match.Measure,match.Local,220,970)-(double)n["x"])>.001)throw new Exception("v175 reference mismatch: "+n);checkedNotes++;}
                Check(checkedNotes==project.Notes.Count(n=>!n.Pedal),"v175 full chart lane and X match: "+checkedNotes);
            }
            if(project.Title.Contains("上海")){Check(project.Difficulty=="B","Shanghai actual project level B");Check(project.Notes.Count(n=>n.Instrument=="HH")==736,"Shanghai HH 736");Check(project.Notes.Count(n=>n.Instrument=="HH"&&n.Velocity==2)==322,"Shanghai HH soft 322");Check(project.Notes.Count(n=>n.Instrument=="HH"&&n.Velocity==3)==170,"Shanghai HH medium 170");Check(project.Notes.Count(n=>n.Instrument=="HH"&&n.Velocity==4)==244,"Shanghai HH normal 244");}
            string temp=Path.Combine(Application.temporaryCachePath,"cr-unity-roundtrip-"+Guid.NewGuid().ToString("N")+".crproj");copy.SaveNative(temp,false);copy.Chart["title"]="Save test";copy.Dirty=true;copy.SaveNative(temp,true);Check(ChartProject.Read(File.ReadAllBytes(temp),"test").Title=="Save test","overwrite read-back");File.AppendAllText(temp,"changed");bool conflict=false;try{copy.SaveNative(temp,true);}catch{conflict=true;}Check(conflict,"external change blocks overwrite");File.Delete(temp);
            if(project.Files.ContainsKey(project.AudioPath)){string audio=Path.Combine(Application.temporaryCachePath,"cr-test"+Path.GetExtension(project.AudioPath));File.WriteAllBytes(audio,project.Files[project.AudioPath]);float[] pcm;int ch,rate;RhythmAudio.DecodeNative(audio,out pcm,out ch,out rate);Check(pcm.Length>rate*ch*10&&pcm.Any(f=>Math.Abs(f)>.01),"fixture audio decoded "+rate+" Hz, "+ch+" channels");}
        }
        string log=Arg("-testReport");if(log!="")File.WriteAllText(log,"PASS: scoring, literal strength, tuplets, project roundtrip, audio decode, verified overwrite and external-change guard.\n"+DateTime.UtcNow.ToString("O"));Debug.Log("CROSS_RHYTHM_CORE_TESTS_PASS");
    }
    static void LibraryTests(){
        var bytes=ChartProject.Demo().Write();var library=new List<ChartProject>();
        var original=ChartProject.Read(bytes,"Song.crproj","token-1");Check(ProjectLibrary.Upsert(library,original)==0,"first project added");
        var repeat=ChartProject.Read(bytes,"Song.crproj","token-2");Check(ProjectLibrary.Upsert(library,repeat)==0&&library.Count==1&&ReferenceEquals(library[0],original),"new browser token does not duplicate identical file or reset edit state");
        var different=ChartProject.Read(bytes,"Song.crproj","token-3");different.SetSongInfo("Different content","");different=ChartProject.Read(different.Write(),"Song.crproj","token-3");Check(ProjectLibrary.Upsert(library,different)==1&&library.Count==2,"same filename with different bytes stays separate");
        original.SetSongInfo("Unsaved edit","Artist");ProjectLibrary.Upsert(library,ChartProject.Read(bytes,"Song.crproj","token-2"));Check(original.Title=="Unsaved edit"&&original.Dirty,"reimport cannot replace unsaved edits");
        var updated=ChartProject.Read(different.Write(),"Song.crproj","token-3");updated.SetSongInfo("Updated disk file","");updated=ChartProject.Read(updated.Write(),"Song.crproj","token-3");Check(ProjectLibrary.Upsert(library,updated)==1&&library.Count==2&&library[1].Title=="Updated disk file","changed file at same identity refreshes in place");
        Check(ProjectLibrary.Upsert(library,ChartProject.Read(bytes,"Other.crproj","token-4"))==2,"different filename is not deduplicated by bytes alone");
        Debug.Log("CROSS_RHYTHM_LIBRARY_TESTS_PASS");
    }
    static void SaveSnapshotTests(){
        var p=ChartProject.Demo();var snapshot=new ProjectSaveSnapshot(p);
        Check(snapshot.Matches(p),"save snapshot initially matches");
        p.SetSongInfo("Edited during save","");Check(!snapshot.Matches(p),"late save completion keeps newer metadata dirty");
        snapshot=new ProjectSaveSnapshot(p);p.Events.RemoveAt(0);Check(!snapshot.Matches(p),"late save completion keeps newer notes dirty");
        snapshot=new ProjectSaveSnapshot(p);p.Files["audio/new.wav"]=new byte[]{1,2};Check(!snapshot.Matches(p),"late save completion keeps new audio dirty");
    }
    static void MetadataDifficultyTests(){
        var p=ChartProject.Demo();p.SetSongInfo("保存した曲名","Artist テスト");
        var copy=ChartProject.Read(p.Write(),"different-name.crproj");
        Check(copy.Title=="保存した曲名"&&copy.Artist=="Artist テスト"&&(string)copy.Manifest["artist"]==copy.Artist&&(string)copy.Manifest["title"]==copy.Title,"title and artist survive save under another filename");
        p.Chart.Remove("title");p.Chart.Remove("artist");Check(p.Title=="保存した曲名"&&p.Artist=="Artist テスト","legacy manifest metadata fallback");
        p.Manifest.Remove("title");p.Manifest.Remove("artist");p.FileName="Legacy Song.crproj";
        var raw=p.Chart.ToString();Check(p.SongTitle==""&&p.Title=="Legacy Song"&&p.Artist==""&&p.Chart.ToString()==raw,"legacy filename fallback does not rewrite metadata");
        p.SetSongInfo("","");Check(p.SongTitle==""&&p.Title=="Legacy Song","empty title remains editable with display fallback");
        p.Chart["measures"]=new JArray(4);p.Chart["events"]=new JArray();p.Chart["bpm"]=120;p.Rebuild();Check(p.Difficulty=="C"&&p.NotesPerSecond==0,"empty chart has zero density and level C");
        for(int i=0;i<6;i++)p.Events.Add(new JObject{{"id","density"+i},{"measure",0},{"beat",i/2.0},{"instrument",i%2==0?"HH":"SN"},{"velocity",4}});
        p.Rebuild();Check(p.NotesPerSecond==3&&p.Difficulty=="B","three actual hits per second is level B");
        p.Chart["bpm"]=240;p.Rebuild();Check(p.NotesPerSecond==6&&p.Difficulty=="B","doubling BPM doubles actual density");
        p.Chart["bpm"]=360;p.Rebuild();Check(p.NotesPerSecond==9&&p.Difficulty=="A","nine hits per second is level A");
        p.Events.Add(new JObject{{"measure",0},{"beat",0},{"instrument","HHSTATE"},{"durationBeats",4}});p.Rebuild();Check(p.PlayableNoteCount==6&&p.NotesPerSecond==9,"pedal holds do not affect difficulty");
        p.Chart["quantize"]=28;p.Chart["audioOffsetSec"]=5;((JObject)p.Events[0])["durationBeats"]=3;p.Rebuild();Check(p.NotesPerSecond==9,"grid offset and sustain duration do not add hits");
        p.Events.RemoveAt(0);p.Rebuild();Check(p.Difficulty=="B"&&p.NotesPerSecond==7.5,"deleting a note recomputes the level");
        p.Events.Add(new JObject{{"measure",0},{"beat",.5},{"instrument","BD"},{"velocity",4}});p.Rebuild();Check(p.NotesPerSecond==9,"simultaneous hand and kick count individually");
        var saved=ChartProject.Read(p.Write(),"density.crproj");Check(saved.NotesPerSecond==p.NotesPerSecond&&saved.Difficulty==p.Difficulty,"computed level survives project roundtrip");
        p.Chart["bpm"]=320;p.Rebuild();Check(p.NotesPerSecond==8&&p.Difficulty=="A","A boundary at eight notes per second");
        p.Chart["bpm"]=440;p.Rebuild();Check(p.NotesPerSecond==11&&p.Difficulty=="S","S boundary at eleven notes per second");
        string other=Arg("-secondFixture");if(other!=""){var track=ChartProject.Read(File.ReadAllBytes(other),Path.GetFileName(other));Check(track.Difficulty=="A","Black Market Blues actual project level A");}
        Debug.Log("CROSS_RHYTHM_METADATA_DIFFICULTY_TESTS_PASS");
    }
    static void VisualTests(){
        var p=ChartProject.Demo();p.Chart["events"]=new JArray();p.Chart["measures"]=new JArray(4);p.Rebuild();
        var e=new JObject{{"id","triplet"},{"measure",0},{"beat",1.0/3},{"instrument","SN"},{"velocity",4},{"articulation","center"},{"gridStepBeats",1.0/3}};p.Events.Add(e);p.Rebuild();
        var n=p.Notes.Single();Rect r=ChartVisuals.EditRect(p,n,3,56,46);Check(Math.Abs(r.x-(1.0/3+.25*.15)*56)<.0001&&Math.Abs(r.width-.25*.7*56)<.0001,"editor uses original cell inset and width");Check(r.Contains(new Vector2(r.center.x,r.center.y)),"note center pickable");
        Rect zoom=ChartVisuals.EditRect(p,n,3,112,46);Check(Math.Abs(zoom.width-2*r.width)<.0001&&zoom.y==r.y&&zoom.height==r.height,"editor horizontal zoom preserves lane and note height");
        e["articulation"]="buzz";e["durationBeats"]=2;p.Rebuild();r=ChartVisuals.EditRect(p,p.Notes.Single(),3,56,46);Check(Math.Abs(r.width-108)<.0001&&ChartVisuals.TypeCode(p.Notes.Single())=="BZ","buzz duration is visible and pickable across full bar");
        p.Events.Add(new JObject{{"id","fifth"},{"measure",0},{"beat",.2},{"instrument","HH"},{"velocity",2},{"gridStepBeats",.2}});p.Rebuild();var points=ChartVisuals.GridPoints(p,0);Check(points.Any(x=>Math.Abs(x-1.0/3)<1e-8)&&points.Contains(.2)&&points.Contains(.25),"mixed triplet fifth and straight grids coexist");
        var before=p.Chart.ToString();ChartVisuals.EditRect(p,p.Notes[0],0,56,46);ChartVisuals.GridPoints(p,0);Check(p.Chart.ToString()==before,"rendering never mutates chart");
        var same=new[]{new ChartNote{Index=1,Id="a",Instrument="SN",Lane="HANY",Beat=1},new ChartNote{Index=2,Id="b",Instrument="HH",Lane="HANY",Beat=1}};var offsets=ChartVisuals.SimultaneousOffsets(same);Check(Math.Abs(offsets[1]-offsets[2])==16,"same-lane simultaneous notes stay separately visible");
        foreach(string inst in new[]{"HH","SN","HT","MT","FT","BD","CR","RD","HHSTATE"})foreach(string state in new[]{"normal","outline","miss"}){var texture=Resources.Load<Texture2D>("NoteGlyphs/"+inst+"-"+state);Check(texture!=null&&texture.width==240&&texture.height==240,"original glyph texture "+inst+" "+state);}
    }
    static void EditorTests(){
        var p=ChartProject.Demo();p.Chart["events"]=new JArray();p.Chart["measures"]=new JArray(4,4,4);p.Chart["quantize"]=12;p.Rebuild();
        Check(p.SetMeter(0,7,8),"set 7/8 meter");Check(Math.Abs(p.Starts[1]-3.5)<1e-8,"7/8 moves following bar to 3.5");
        p.Events.Add(new JObject{{"id","triplet"},{"measure",1},{"beat",1.0/3},{"instrument","HH"},{"velocity",2},{"gridStepBeats",1.0/3},{"custom","keep"}});p.Rebuild();
        Check(Math.Abs(p.SnapBeat(3.5)-3.5)<1e-8&&Math.Abs(p.SnapBeat(3.84)-(3.5+1.0/3))<1e-8,"triplets use local bar origin after 7/8");
        Check(Math.Abs(p.SnapBeat(3.49)-10.0/3)<1e-8,"snap never places a note past the bar end");
        var before=ChartProject.Read(p.Write(),"before.crproj");p.InsertMeasure(0);Check(p.Notes.Single().Measure==2&&p.Meter(0).Item2==8,"insert shifts note indexes, keeps signature");
        Check(p.RemoveMeasure(1)&&JToken.DeepEquals(before.Chart,p.Chart),"remove inserted empty bar restores exact chart");
        Check(!p.SetMeter(1,1,16)&&JToken.DeepEquals(before.Chart,p.Chart),"shortening across a note is rejected without loss");
        Check(!p.RemoveMeasure(1),"nonempty measure cannot silently remove notes");
        Check(p.SetMeter(0,6,8),"set 6/8 meter");var copy=ChartProject.Read(p.Write(),"meter.crproj");Check(copy.Meter(0).Item1==6&&copy.Meter(0).Item2==8&&copy.Measures[0]==3,"save preserves 6/8 versus 3/4");
        Check(copy.Pulses(0,2.9).SequenceEqual(new double[]{0,.5,1,1.5,2,2.5})&&copy.IsBarStart(3),"meter-aligned click pulses");
        var pcm=new float[1024];pcm[0]=.75f;pcm[1]=-.6f;pcm[700]=.9f;var wave=new AudioWaveform(pcm,2,512);wave.Range(0,.25,out var min,out var max);Check(Math.Abs(min+.6)<1e-6&&Math.Abs(max-.75)<1e-6,"waveform retains opposing stereo peaks");wave.Range(2,3,out min,out max);Check(min==0&&max==0,"waveform beyond audio is empty");
        Debug.Log("CROSS_RHYTHM_EDITOR_TESTS_PASS");
    }
    static void MidiTests(){
        byte[] Number(int n,int count){var b=new byte[count];for(int i=count-1;i>=0;i--){b[i]=(byte)(n&255);n>>=8;}return b;}
        byte[] Variable(int n){var b=new List<byte>{(byte)(n&127)};while((n>>=7)>0)b.Insert(0,(byte)(128|(n&127)));return b.ToArray();}
        byte[] Track(params Tuple<int,byte[]>[] events){var b=new List<byte>();int last=0;foreach(var e in events){b.AddRange(Variable(e.Item1-last));b.AddRange(e.Item2);last=e.Item1;}b.AddRange(new byte[]{0,255,47,0});return System.Text.Encoding.ASCII.GetBytes("MTrk").Concat(Number(b.Count,4)).Concat(b).ToArray();}
        Tuple<int,byte[]> E(int tick,params byte[] data)=>Tuple.Create(tick,data);
        var meta=Track(E(0,255,81,3,7,161,32),E(0,255,88,4,7,3,24,8),E(3360,255,88,4,4,2,24,8));
        var notes=Track(E(0,153,42,25),E(320,42,50),E(640,153,46,75),E(800,137,46,0),E(960,153,38,0),E(1280,153,51,127),E(3360,153,38,100));
        var bytes=System.Text.Encoding.ASCII.GetBytes("MThd").Concat(Number(6,4)).Concat(Number(1,2)).Concat(Number(2,2)).Concat(Number(960,2)).Concat(meta).Concat(notes).ToArray();
        var source=ChartProject.Demo();source.Chart["title"]="Untitled";source.Chart["custom"]="preserved";source.Chart["quantize"]=12;
        var auto=MidiImport.Read(bytes,source,"Tuplets.mid",MidiImport.ZeroMode.Auto);var p=new ChartProject{Chart=auto.Chart,Manifest=source.Manifest};p.Rebuild();
        Check(auto.Hits==6&&auto.ZeroKept==1,"MIDI AUTO keeps velocity zero with explicit note-off");
        Check(p.Title=="Tuplets"&&p.BPM==120&&(string)p.Chart["custom"]=="preserved","MIDI title, tempo and unrelated settings");
        Check(p.Notes.Where(n=>!n.Pedal).Select(n=>n.Velocity).SequenceEqual(new[]{1,2,3,0,5,4}),"MIDI six-level velocity mapping");
        Check(Math.Abs(p.Notes.First(n=>n.Velocity==2&&!n.Pedal).Beat-1.0/3)<1e-8,"MIDI running status keeps exact triplet time");
        Check(p.Measures[0]==3.5&&p.Meter(0).Item2==8&&p.Notes.Last().Local==0,"MIDI time signature change places next bar correctly");
        Check(p.Pedals.Count==1&&Math.Abs(p.Pedals[0].End-2.0/3)<1e-8&&!p.ClosedAt(2.0/3),"MIDI closed hats merge and open hat releases");
        var off=MidiImport.Read(bytes,source,"Tuplets.mid",MidiImport.ZeroMode.NoteOff);Check(off.Hits==5&&off.ZeroDropped==1,"MIDI note-off policy excludes zero");
        bool malformed=false;try{MidiImport.Read(bytes.Take(bytes.Length-3).ToArray(),source,"broken.mid",MidiImport.ZeroMode.Auto);}catch(InvalidDataException){malformed=true;}Check(malformed,"truncated MIDI fails without partial import");
        Check(JToken.DeepEquals(source.Chart["events"],ChartProject.Demo().Chart["events"]),"MIDI parser does not mutate the open project");
        if(Arg("-testReport")!="")File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Arg("-testReport")),"midi-import-fixture.mid"),bytes);
        Debug.Log("CROSS_RHYTHM_MIDI_TESTS_PASS");
    }
}
}
