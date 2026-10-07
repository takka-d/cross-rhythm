using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public static class PracticeDraftTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception("TEST FAILED: "+message);Debug.Log("PASS "+message);}
    public static float[] Signal(int sr=22050){var pcm=new float[sr*8];for(int hit=1;hit<15;hit++){double frequency=hit%3==0?80:hit%3==1?1200:6500;for(int i=0;i<sr/8;i++){int at=(int)(hit*.5*sr)+i;if(at<pcm.Length)pcm[at]+=(float)(Math.Sin(2*Math.PI*frequency*i/sr)*Math.Exp(-i/(.018*sr)));}}return pcm;}
    public static void Run(){
        var p=ChartProject.Demo();p.Chart["measures"]=new JArray(4);p.Chart["events"]=new JArray();p.Chart["quantize"]=12;p.Rebuild();string before=p.Chart.ToString();
        var job=new AudioDraft();foreach(var progress in job.Run(Signal(),22050,p,true)){}
        Check(p.Chart.ToString()==before,"analysis never mutates source before Apply");
        Check(job.Layout.Length==16,"draft extends short chart to entire audio");
        foreach(string inst in new[]{"BD","SN","HH"})Check(job.Events.Any(n=>(string)n["instrument"]==inst),"synthetic draft detects "+inst);
        Check(job.Events.All(n=>Math.Abs((double)n["beat"]*3-Math.Round((double)n["beat"]*3))<1e-7),"draft respects triplet quantization");
        var applied=job.Apply(p,true);var outProject=new ChartProject{Chart=applied,Manifest=p.Manifest};outProject.Rebuild();var copy=ChartProject.Read(outProject.Write(),"draft.crproj");Check(JToken.DeepEquals(copy.Chart,applied),"generated notes and extended measures survive save and reload");
        var appended=job.Apply(outProject,false);Check(((JArray)appended["events"]).Count==job.Events.Count,"append draft does not duplicate existing hits");
        var silent=new AudioDraft();foreach(var x in silent.Run(new float[22050],22050,p,false)){}Check(silent.Events.Count==0,"silence creates no phantom notes");
        p.SetMeter(0,7,8);p.Chart["audioOffsetSec"]=-.5;p.Rebuild();job=new AudioDraft();foreach(var x in job.Run(Signal(),22050,p,true)){}Check(job.Layout.Meter(1).Item1==7&&job.Layout.Meter(1).Item2==8,"draft extension preserves irregular meter");
        Check(job.Events.All(n=>job.Layout.Starts[(int)n["measure"]]+(double)n["beat"]>=1),"draft respects negative audio offset");
        var go=new GameObject("Rate test");var audio=go.AddComponent<RhythmAudio>();audio.Running=true;audio.BPM=130;audio.AnchorBeat=8;audio.AnchorDSP=10;
        foreach(double speed in new[]{.25,.5,1,1.5,2}){audio.Rate=speed;double beat=audio.BeatAt(13.25);Check(Math.Abs(audio.DSPAt(beat)-13.25)<1e-10,"practice clock inverse ×"+speed);Check(Math.Abs((audio.BeatAt(10.12)-8)*60000/130/speed-120)<1e-8,"practice judgment uses elapsed milliseconds ×"+speed);}
        audio.Running=false;UnityEngine.Object.DestroyImmediate(go);Debug.Log("CROSS_RHYTHM_PRACTICE_DRAFT_TESTS_PASS");
    }
}
}
