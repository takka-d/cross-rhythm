#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator PracticeDraftCheck(){
        yield return new WaitForSeconds(2);var lines=new List<string>();
        void Check(bool ok,string name){lines.Add((ok?"PASS ":"FAIL ")+name);Debug.Log(lines.Last());}
        var p=EmptyEditorProject();p.Chart["measures"]=new JArray(4);p.Chart["quantize"]=20;p.Rebuild();OpenEditorProject(p);
        yield return new WaitForSeconds(1);
        const int sr=22050;var samples=new float[sr*8];for(int h=1;h<15;h++)for(int i=0;i<sr/8;i++){int at=(int)(h*.5*sr)+i;samples[at]=(float)(Math.Sin(2*Math.PI*(h%3==0?80:h%3==1?1200:6500)*i/sr)*Math.Exp(-i/(.018*sr)));}
        Audio.Song=AudioClip.Create("Synthetic QA audio",samples.Length,1,sr,false);Audio.Song.SetData(samples,0);Audio.Waveform=new AudioWaveform(samples,1,sr);loaded=true;busy=false;
        string original=Project.Chart.ToString();yield return GenerateDraft();Check(draftResult!=null&&Project.Chart.ToString()==original,"draft generation preserves original");
        ApplyDraft();Check(Project.PlayableNoteCount>0&&Project.Length==16,"apply creates notes and extends bars");string generated=Project.Chart.ToString();Restore(false);Check(Project.Chart.ToString()==original,"Undo restores exact chart");Restore(true);Check(Project.Chart.ToString()==generated,"Redo restores generated chart");
        var restored=ChartProject.Read(Project.Write(),"qa.crproj");Check(JToken.DeepEquals(restored.Chart,Project.Chart),"draft saves and reloads losslessly");
        foreach(double rate in new[]{.25,2.0}){
            Begin(true);Seek(1);ChangePractice(rate,rate==2);auto=true;
            while(Audio.Preparing)yield return null;yield return new WaitForSeconds(.3f);double startBeat=Audio.Beat,startDSP=AudioSettings.dspTime;
            yield return new WaitForSeconds(.5f);double dt=AudioSettings.dspTime-startDSP;
            Check(Math.Abs((Audio.Beat-startBeat)-dt*Project.BPM/60*rate)<.003,"live DSP clock ×"+rate);
            Check(Audio.BackingIsPlaying&&Math.Abs(Audio.Backing.pitch-1)<.001,"backing audio playing ×"+rate);
            Check(pro==(rate==2)&&Records.Count==0,"practice mode switched without score record");
            Audio.Pause();double stopped=Audio.Beat;ChangePractice(1,!pro);Check(!Audio.Running&&Math.Abs(stopped-Audio.Beat)<.00001,"paused mode/rate switch keeps position");
        }
        NavigateNow(Page.Edit);Check(Audio.Rate==1,"Edit returns to normal speed");
        yield return GenerateDraft();draftCancel=false;var cancelled=GenerateDraft();cancelled.MoveNext();draftCancel=true;while(cancelled.MoveNext())yield return null;Check(!draftRunning&&draftResult==null,"cancel discards generated draft without applying");
        string folder=workspaceCheckRoot;File.WriteAllLines(Path.Combine(folder,"practice-draft-check.txt"),lines);Application.Quit(lines.Any(x=>x.StartsWith("FAIL"))?2:0);
    }
}
}
#endif
