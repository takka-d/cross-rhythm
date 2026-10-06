using System;
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator EditorAudioCheck(){
        var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--smoke-output");string folder=args[i+1];Directory.CreateDirectory(folder);
        AudioListener.volume=0;yield return new WaitForSeconds(2);
        if(!loaded||Audio.Song==null){File.WriteAllText(Path.Combine(folder,"audio-check.txt"),"FAIL load: "+status);Application.Quit(2);yield break;}
        Current=Page.Edit;Audio.AnchorBeat=0;editorPanel=1;EditorPlayback();
        yield return new WaitForSeconds(1);
        int prepared=Audio.PreparedSampleCount,stopped=0,frames=0,over50=0;double maxGap=0,elapsed=0,previous=Time.realtimeSinceStartupAsDouble;
        while(elapsed<60){yield return null;double now=Time.realtimeSinceStartupAsDouble,gap=now-previous;previous=now;elapsed+=gap;maxGap=Math.Max(maxGap,gap);frames++;if(gap>.05)over50++;if(!Audio.Running||!Audio.Backing.isPlaying)stopped++;}
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"Edit-audio.png"));yield return new WaitForSeconds(.2f);
        bool noNewClips=prepared==Audio.PreparedSampleCount;
        File.WriteAllText(Path.Combine(folder,"audio-check.txt"),$"project={Project.Title}\nseconds={elapsed:0.000}\nframes={frames}\nbacking_stopped_frames={stopped}\nmax_frame_ms={maxGap*1000:0.00}\nframes_over_50ms={over50}\nprepared_samples={prepared}\nno_runtime_sample_creation={noNewClips}\nbeat={Audio.Beat:0.00}\n");
        Application.Quit(stopped==0&&noNewClips?0:2);
    }
    IEnumerator SmokeTest(){
        string folder=Path.Combine(Application.persistentDataPath,"smoke");var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--smoke-output");if(i>=0&&i+1<args.Length)folder=args[i+1];Directory.CreateDirectory(folder);
        yield return new WaitForSeconds(2);
        if(!loaded){File.WriteAllText(Path.Combine(folder,"FAILED.txt"),"Audio/project not loaded: "+status);Application.Quit(2);yield break;}
        bool editorActions=EditorSmokeChecks(folder);
        foreach(var page in new[]{Page.Title,Page.Songs,Page.Config,Page.Edit}){Current=page;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,page+".png"));yield return new WaitForSeconds(.2f);}
        Begin(true);auto=true;Seek(208);yield return new WaitForSeconds(2);bool playingAudio=Audio.Backing.isPlaying;int practiceRecords=Records.Count;Audio.Pause();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"Practice.png"));
        double before=Audio.Beat;Seek(before-4);bool seek=Math.Abs(Audio.Beat-(before-4))<.0001;bool strengths=Project.Notes.Where(n=>n.Instrument=="HH").Select(n=>n.Velocity).Distinct().Count()>1;
        Begin(false);Audio.Pause();Seek(208);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"Play.png"));
        var ns=Project.Notes.Where(n=>!n.Pedal).ToArray();Records=ns.Select(n=>new HitRecord{Index=n.Index,Beat=n.Beat,Instrument=n.Instrument,Ms=0,Judge="JUST"}).ToList();Result=RhythmScore.Calculate(ns,Records);Current=Page.Result;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"Result.png"));yield return new WaitForSeconds(.3f);
        bool midi=true;int mi=Array.IndexOf(args,"--midi-fixture");if(mi>=0&&mi+1<args.Length){
            var original=Project;Project=ChartProject.Read(original.Write(),"midi-smoke.crproj");Audio.Project=Project;Current=Page.Edit;Project.Chart["quantize"]=12;Project.Rebuild();undo.Clear();redo.Clear();midiZeroMode=MidiImport.ZeroMode.Auto;ImportMidi(File.ReadAllBytes(args[mi+1]),"Tuplets.mid");midi=Project.Notes.Count(n=>!n.Pedal)==6&&Project.Meter(0).Item2==8;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"Edit-MIDI.png"));yield return new WaitForSeconds(.2f);
            zoom=2;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"Edit-MIDI-Zoom.png"));yield return new WaitForSeconds(.2f);Project=original;Audio.Project=original;Audio.BPM=original.BPM;Audio.Offset=original.Offset;zoom=1;
        }
        bool audio=Audio.Song!=null&&Audio.Song.length>10;File.WriteAllText(Path.Combine(folder,"smoke.txt"),$"Title/Select/Config/Edit/Practice/Play/Result rendered\nproject={Project.Title}\nnotes={ns.Length}\nstrengths={strengths}\naudio={audio}\naudio_playing={playingAudio}\naudio_seconds={Audio.Song?.length}\nseek={seek}\nperfect_score={Result.Score}\npractice_records={practiceRecords}\nwaveform={Audio.Waveform!=null}\neditor_actions={editorActions}\nmidi_import={midi}\n");Application.Quit(audio&&playingAudio&&seek&&strengths&&practiceRecords==0&&editorActions&&midi?0:2);
    }
    bool EditorSmokeChecks(string folder){
        var original=Project;double b=Audio.Beat;bool result=false;
        try{
            Project=ChartProject.Demo();Project.Chart["events"]=new Newtonsoft.Json.Linq.JArray();Project.Chart["measures"]=new Newtonsoft.Json.Linq.JArray(4,4,4);Project.Rebuild();Audio.Project=Project;ResetEditorFields();
            meterNumerator="7";meterDenominator="8";ApplyMeter();if(Project.Measures[0]!=3.5)throw new Exception("Meter action failed");
            InsertEditorMeasure();if(Project.Measures.Length!=4||editMeasure!=1)throw new Exception("Insert action failed");Restore(false);if(Project.Measures.Length!=3||Audio.BPM!=Project.BPM)throw new Exception("Undo failed");Restore(true);if(Project.Measures.Length!=4)throw new Exception("Redo failed");
            SelectMeasure(1);RemoveEditorMeasure();if(Project.Measures.Length!=3)throw new Exception("Remove action failed");
            string path=Path.Combine(folder,"editor-roundtrip.crproj");Project.SaveNative(path,false);Project.Chart["title"]="Editor roundtrip";Save(false);var reloaded=ChartProject.Read(File.ReadAllBytes(path),"test");if(reloaded.Title!="Editor roundtrip"||reloaded.Meter(0).Item2!=8||Project.Dirty)throw new Exception("Save action failed");result=true;
        }catch(Exception e){File.WriteAllText(Path.Combine(folder,"editor-failure.txt"),e.ToString());}
        finally{Project=original;Audio.Project=original;Audio.BPM=original.BPM;Audio.Offset=original.Offset;Audio.AnchorBeat=b;editMeasure=0;ResetEditorFields();undo.Clear();redo.Clear();editScroll=Vector2.zero;}
        return result;
    }
}
}
