using System;
using System.Collections;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator WaitForPitchDSP(double target,Action<bool,string> Check){
        double deadline=Time.realtimeSinceStartupAsDouble+10;
        while(AudioSettings.dspTime<target&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
        Check(AudioSettings.dspTime>=target,"audio clock reaches scheduled playback time");
    }
    IEnumerator PitchRuntimeCheck(Action<bool,string> Check){
        OpenEditorProject(ChartProject.Demo());while(busy||!loaded)yield return null;
        const int sr=22050;var samples=new float[sr*14];for(int i=0;i<samples.Length;i++)samples[i]=(float)(.2*Math.Sin(2*Math.PI*440*i/sr));
        if(Audio.Song!=null)Destroy(Audio.Song);Audio.Song=AudioClip.Create("Pitch QA 440 Hz",samples.Length,1,sr,false);Audio.Song.SetData(samples,0);
        Begin(true);Audio.Pause();
        foreach(double rate in new[]{.25,.55,1,2}){
            ChangePractice(rate,false);Seek(1);Audio.Play(1);double frozen=Audio.Beat;
            if(rate!=1)Check(Audio.Preparing&&Audio.Beat==frozen,"pitch preparation holds chart at speed "+rate);
            double deadline=Time.realtimeSinceStartupAsDouble+8;while(Audio.Preparing&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Check(!Audio.Preparing&&Audio.Running&&string.IsNullOrEmpty(Audio.PlaybackError),"pitch playback prepares at speed "+rate);
            yield return WaitForPitchDSP(Audio.AnchorDSP+.3,Check);
            double actual=Audio.BackingTimelineSeconds,expected=Project.Offset+Project.SecondsAtBeat(Audio.Beat);
            AudioSettings.GetDSPBufferSize(out int bufferLength,out int bufferCount);
            Debug.Log($"PITCH_SYNC rate={rate} sample={actual:F6} expected={expected:F6} errorMs={(actual-expected)*1000:F3} playing={Audio.BackingIsPlaying} dsp={AudioSettings.dspTime:F6} anchor={Audio.AnchorDSP:F6} buffers={bufferLength}x{bufferCount} outputRate={AudioSettings.outputSampleRate}");
            Check(Audio.BackingIsPlaying&&Math.Abs(actual-expected)<.04,"pitch audio position matches chart at speed "+rate);
            if(rate!=1){var clip=Audio.PreparedPracticeClip;Check(clip!=null&&Audio.StretchSegmentCount<=3,"bounded pitch segments at speed "+rate);var pcm=new float[clip.samples*clip.channels];clip.GetData(pcm,0);Check(Math.Abs(PitchCheckSpectrum.Peak(pcm,1,0,sr/2,sr,sr,440)-440)<2,"prepared playback retains 440 Hz at speed "+rate);}
            double anchor=Audio.AnchorDSP;ChangePractice(rate,true);Check(Audio.Running&&Audio.AnchorDSP==anchor,"Normal Pro switch keeps audio continuous at speed "+rate);
            Audio.Pause();double beat=Audio.Beat;yield return new WaitForSeconds(.1f);Check(!Audio.BackingIsPlaying&&Audio.StretchSegmentCount==0&&Audio.Beat==beat,"pause clears prepared audio at speed "+rate);
        }
        ChangePractice(.55,false);Seek(0);Audio.Play(0);while(Audio.Preparing)yield return null;
        yield return WaitForPitchDSP(Audio.AnchorDSP+6.5,Check);Check(Audio.Running&&Audio.BackingIsPlaying&&Audio.StretchSegmentCount<=3&&Math.Abs(Audio.BackingTimelineSeconds-Project.SecondsAtBeat(Audio.Beat)-Project.Offset)<.04,"continuous playback crosses three chunk boundaries without drift");
        Seek(10);while(Audio.Preparing)yield return null;yield return WaitForPitchDSP(Audio.AnchorDSP+.3,Check);Check(Math.Abs(Audio.BackingTimelineSeconds-Project.SecondsAtBeat(Audio.Beat)-Project.Offset)<.04,"seek rebuilds pitch audio at chart position");
        ChangePractice(.25,false);Audio.Pause();yield return new WaitForSeconds(.2f);Check(!Audio.Running&&!Audio.Preparing&&!Audio.BackingIsPlaying&&Audio.StretchSegmentCount==0,"pause during rate preparation prevents stale audio restart");
        Audio.Play(-.2);while(Audio.Preparing)yield return null;Check(!Audio.BackingIsPlaying,"negative chart time keeps backing silent");yield return WaitForPitchDSP(Audio.DSPAt(0)+.3,Check);Check(Audio.BackingIsPlaying&&Math.Abs(Audio.BackingTimelineSeconds-Project.SecondsAtBeat(Audio.Beat)-Project.Offset)<.04,"count-in reaches source audio with preserved pitch");
        NavigateNow(Page.Edit);Check(Audio.Rate==1&&!Audio.Running&&!Audio.Preparing&&Audio.StretchSegmentCount==0,"leaving Practice cancels pitch processing");
    }
}
}
