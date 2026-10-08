using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
public sealed partial class RhythmAudio {
    const int StretchBlock=512;
    readonly List<AudioSource> stretchSources=new List<AudioSource>();
    readonly List<StretchSegment> stretchSegments=new List<StretchSegment>();
    sealed class StretchSegment {public AudioSource Source;public AudioClip Clip;public double Start,End,SongStart,Rate;}
    Coroutine stretchWork;PitchStretch stretchProcessor;int playGeneration;
    public bool Preparing {get;private set;}
    public string PlaybackError {get;private set;}="";
    public int StretchSegmentCount=>stretchSegments.Count;
    public int SegmentsPrepared {get;private set;}
    public double MaxSegmentPreparationSeconds {get;private set;}
    internal AudioClip PreparedPracticeClip=>stretchSegments.Count==0?null:stretchSegments[0].Clip;
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]static extern void CRStretchAudioStop();
    [System.Runtime.InteropServices.DllImport("__Internal")]static extern int CRStretchAudioSchedule(float[] pcm,int frames,int channels,int sampleRate,double outputOffset,double delay,double sourceSecond,double rate,float gain);
    [System.Runtime.InteropServices.DllImport("__Internal")]static extern double CRStretchAudioPosition();
    [System.Runtime.InteropServices.DllImport("__Internal")]static extern int CRStretchAudioPlaying();
    [System.Runtime.InteropServices.DllImport("__Internal")]static extern void CRStretchAudioGain(float gain);
    public bool BackingIsPlaying=>Backing.isPlaying||CRStretchAudioPlaying()!=0;
#else
    // Unity marks a scheduled source as playing even before its audible start.
    public bool BackingIsPlaying=>Backing.isPlaying||stretchSegments.Exists(s=>AudioSettings.dspTime>=s.Start&&AudioSettings.dspTime<s.End&&s.Source.isPlaying);
#endif
    public double BackingTimelineSeconds {
        get {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(stretchSegments.Count>0)return CRStretchAudioPosition();
#endif
            foreach(var segment in stretchSegments)if(AudioSettings.dspTime>=segment.Start&&AudioSettings.dspTime<segment.End)
            return segment.SongStart+segment.Source.timeSamples/(double)segment.Clip.frequency*segment.Rate;
            return Song==null?0:Backing.timeSamples/(double)Song.frequency;}
    }
    void StopStretch(){
        playGeneration++;Preparing=false;SegmentsPrepared=0;MaxSegmentPreparationSeconds=0;
#if UNITY_WEBGL && !UNITY_EDITOR
        CRStretchAudioStop();
#endif
        if(stretchWork!=null){StopCoroutine(stretchWork);stretchWork=null;}
        stretchProcessor?.Dispose();stretchProcessor=null;
        foreach(var s in stretchSources){s.Stop();s.clip=null;}
        foreach(var segment in stretchSegments)if(segment.Clip!=null)Destroy(segment.Clip);
        stretchSegments.Clear();
    }
    void StretchFailure(string error){double beat=Beat;Stop();AnchorBeat=beat;PlaybackError=error;Debug.LogError("Practice audio: "+error);}
    void UpdateStretch(){
#if UNITY_WEBGL && !UNITY_EDITOR
        CRStretchAudioGain(BackingGain);
#else
        foreach(var source in stretchSources)source.volume=BackingGain;
#endif
    }
    float[] ReadSongFrames(AudioClip song,long start,int count,float[] reuse=null){
        int channels=song.channels;var data=reuse!=null&&reuse.Length==count*channels?reuse:new float[count*channels];
#if UNITY_WEBGL && !UNITY_EDITOR
        if(song==Song&&webSongPCM!=0){
            Array.Clear(data,0,data.Length);long webFirst=Math.Max(0,start),webEnd=Math.Min(song.samples,start+count);
            if(webEnd>webFirst)System.Runtime.InteropServices.Marshal.Copy(new IntPtr(webSongPCM+webFirst*channels*4),data,(int)(webFirst-start)*channels,(int)(webEnd-webFirst)*channels);
            return data;
        }
#endif
        if(start>=0&&start+count<=song.samples){if(!song.GetData(data,(int)start))throw new Exception("Cannot read source audio");return data;}
        Array.Clear(data,0,data.Length);long first=Math.Max(0,start),end=Math.Min(song.samples,start+count);
        if(end>first){var valid=new float[(int)(end-first)*channels];if(!song.GetData(valid,(int)first))throw new Exception("Cannot read source audio");Array.Copy(valid,0,data,(int)(first-start)*channels,valid.Length);}
        return data;
    }
    IEnumerator StreamPitch(double beat,double rate,int generation){
        var song=Song;int sr=song.frequency,ch=song.channels;double sourceSecond=Offset+ChartSeconds(beat);
        long start=(long)Math.Round(Math.Max(0,sourceSecond)*sr);
        long length=(long)Math.Ceiling((song.samples-start)/rate);
        string error=null;int inputLatency=0,skip=0;
        try{
            stretchProcessor=new PitchStretch(ch,sr);inputLatency=stretchProcessor.InputLatency;skip=stretchProcessor.OutputLatency;
            stretchProcessor.Seek(ReadSongFrames(song,start,inputLatency),rate);
            while(stretchSources.Count<3){var source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.priority=0;stretchSources.Add(source);}
        }catch(Exception e){error=e.Message;}
        if(error!=null){StretchFailure(error);yield break;}
        long processed=0,emitted=0;double firstDSP=0;int index=0;
        var output=new float[StretchBlock*ch];var inputs=new Dictionary<int,float[]>();
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(emitted<length&&generation==playGeneration){
            int slot=index%3;
            var previous=stretchSegments.Find(s=>s.Source==stretchSources[slot]);
            while(previous!=null&&AudioSettings.dspTime<previous.End+.02){if(generation!=playGeneration)yield break;yield return null;}
            if(previous!=null){previous.Source.clip=null;if(previous.Clip!=null)Destroy(previous.Clip);stretchSegments.Remove(previous);}
            double prepareStart=Time.realtimeSinceStartupAsDouble;
            int frames=(int)Math.Min(sr*2L,length-emitted),filled=0;var pcm=new float[frames*ch];
            while(filled<frames){
                if(generation!=playGeneration)yield break;
                // Absolute sample totals prevent rounding errors from accumulating at rates such as 0.55.
                int n=Math.Min(StretchBlock,frames-filled+skip);
                long inputAt=(long)Math.Round(processed*rate),nextInput=(long)Math.Round((processed+n)*rate);
                int inFrames=(int)(nextInput-inputAt);inputs.TryGetValue(inFrames,out var input);
                try{
                    input=ReadSongFrames(song,start+inputLatency+inputAt,inFrames,input);inputs[inFrames]=input;
                    stretchProcessor.Process(input,inFrames,output,n);
                }catch(Exception e){error=e.Message;}
                if(error!=null){StretchFailure(error);yield break;}
                processed+=n;int discard=Math.Min(skip,n);skip-=discard;
                int copy=n-discard;if(copy>0){Array.Copy(output,discard*ch,pcm,filled*ch,copy*ch);filled+=copy;}
                if(watch.Elapsed.TotalMilliseconds>=3){yield return null;watch.Restart();}
            }
            if(generation!=playGeneration)yield break;
            AudioClip clip=null;
#if !UNITY_WEBGL || UNITY_EDITOR
            try{clip=AudioClip.Create("Practice pitch-preserved",frames,ch,sr,false);if(!clip.SetData(pcm,0))throw new Exception("Cannot prepare practice audio");}
            catch(Exception e){if(clip!=null)Destroy(clip);error=e.Message;}
            if(error!=null){StretchFailure(error);yield break;}
#endif
            SegmentsPrepared++;MaxSegmentPreparationSeconds=Math.Max(MaxSegmentPreparationSeconds,Time.realtimeSinceStartupAsDouble-prepareStart);
            if(index==0){AnchorDSP=AudioSettings.dspTime+.15;firstDSP=AnchorDSP+Math.Max(0,-sourceSecond)/rate;Preparing=false;}
            double when=firstDSP+emitted/(double)sr;
            if(when<AudioSettings.dspTime+.015){if(clip!=null)Destroy(clip);StretchFailure("Audio preparation fell behind playback");yield break;}
            var sourcePlayer=stretchSources[slot];
#if UNITY_WEBGL && !UNITY_EDITOR
            if(CRStretchAudioSchedule(pcm,frames,ch,sr,emitted/(double)sr,when-AudioSettings.dspTime,start/(double)sr,rate,BackingGain)==0){StretchFailure("Audio preparation fell behind playback");yield break;}
#else
            // Stop plus a new clip resets the position. Avoid an extra Web audio seek before scheduling.
            sourcePlayer.Stop();sourcePlayer.clip=clip;sourcePlayer.pitch=1;sourcePlayer.volume=BackingGain;sourcePlayer.PlayScheduled(when);
#endif
            stretchSegments.Add(new StretchSegment{Source=sourcePlayer,Clip=clip,Start=when,End=when+frames/(double)sr,SongStart=(start+emitted*rate)/sr,Rate=rate});
            emitted+=frames;index++;yield return null;watch.Restart();
        }
        stretchProcessor?.Dispose();stretchProcessor=null;stretchWork=null;
    }
}
}
