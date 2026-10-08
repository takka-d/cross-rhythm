using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator MixerRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();p.Chart["mix"]=new JObject{{"backing",.3},{"drums",.7}};OpenEditorProject(p);
        double deadline=Time.realtimeSinceStartupAsDouble+30;while((busy||!loaded)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        check(loaded&&!busy,"mixer test audio ready");
        int before=undo.Count,prepared=Audio.PreparedSampleCount;Audio.Play(0);
        for(int i=99;i>=37;i--)SetEditorInstrumentGain("SN",i/100f);
        check(undo.Count==before+1&&p.InstrumentGain("SN")==.37f,"continuous mixer drag creates one undo group");
        check(Audio.Running&&Audio.PreparedSampleCount==prepared,"mixing does not stop playback or rebuild sample clips");
        Audio.Pause();Restore(false);check(p.InstrumentGain("SN")==1,"Undo restores mixer value");Restore(true);check(p.InstrumentGain("SN")==.37f,"Redo restores mixer value");
        mixerEditing="";SetEditorInstrumentGain("SN",1);check(undo.Count==before+2,"new mixer gesture starts a fresh undo group");
        float PlayVolume(string instrument,string articulation,float mix,bool pedal=false){
            p.SetInstrumentGain(instrument,mix);
            var f=BindingFlags.Instance|BindingFlags.NonPublic;int next=(int)typeof(RhythmAudio).GetField("voice",f).GetValue(Audio);
            var voices=(List<AudioSource>)typeof(RhythmAudio).GetField("voices",f).GetValue(Audio);
            if(pedal)Audio.Pedal();else Audio.Drum(new ChartNote{Instrument=instrument,Articulation=articulation,Velocity=5},true);
            return voices[next%voices.Count].volume;
        }
        foreach(var voice in new[]{("SN","rim_closed",false),("RD","crash",false),("HH","auto",false),("HH","",true)}){
            float normal=PlayVolume(voice.Item1,voice.Item2,1,voice.Item3),quiet=PlayVolume(voice.Item1,voice.Item2,.25f,voice.Item3),loud=PlayVolume(voice.Item1,voice.Item2,1.5f,voice.Item3),silent=PlayVolume(voice.Item1,voice.Item2,0,voice.Item3);
            check(normal>0&&Math.Abs(quiet/normal-.25)<.0001&&Math.Abs(loud/normal-1.5)<.0001&&silent==0,"actual audio voice follows mixer 0/25/100/150 percent: "+voice);
        }
        Audio.Stop();p.SetInstrumentGain("HH",.42f);p.SetInstrumentGain("RD",1.22f);p.SaveNative(Path.Combine(workspaceCheckRoot,"mixer-saved.crproj"),false);
        var reread=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName);
        check(reread.InstrumentGain("HH")==.42f&&reread.InstrumentGain("RD")==1.22f,"Windows mixer values persist in the actual saved file");
        NavigateNow(Page.Songs);yield return null;
    }
}
}
