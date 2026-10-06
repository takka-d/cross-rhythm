using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using Newtonsoft.Json.Linq;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using NAudio.Wave;
#endif
namespace CrossRhythm {
public sealed class RhythmAudio : MonoBehaviour {
    public AudioSource Backing;
    public AudioClip Song;
    public AudioWaveform Waveform;
    public bool Running;
    public double Rate=1;
    public double AnchorBeat, AnchorDSP, BPM=120, Offset;
    public float BackingGain=.7f, DrumGain=.8f;
    public ChartProject Project;
    public Action<string> OnReady;
    Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    Dictionary<string,AudioClip> gainClips=new Dictionary<string,AudioClip>();
    readonly Dictionary<string,float> gainScales=new Dictionary<string,float>();
    public int PreparedSampleCount=>gainClips.Count;
    List<AudioSource> voices=new List<AudioSource>();
    List<AudioSource> openHats=new List<AudioSource>();
    readonly Dictionary<AudioSource,double> voiceStarts=new Dictionary<AudioSource,double>();
    int voice,hatVoice;
    readonly List<HiHatEnvelope> hatVoices=new List<HiHatEnvelope>();
    int loadGeneration;
    public double Beat => Running?AnchorBeat+(AudioSettings.dspTime-AnchorDSP)*BPM/60*Rate:AnchorBeat;
    public double BeatAt(double dsp)=>Running?AnchorBeat+(dsp-AnchorDSP)*BPM/60*Rate:AnchorBeat;
    public double DSPAt(double beat)=>AnchorDSP+(beat-AnchorBeat)*60/BPM/Rate;
    public static readonly string[] SampleKeys={"BD","SN","SN_RIM","SIDE","SN_BUZZ","HH","OHH","HH_PEDAL","HT","FT","TOM_RIM","RD","CUP","RIDE_CRASH","CR","SPLASH","CHINA"};
    static readonly float[] SampleGains={.78f,1.141123f,1.172821f,1.109425f,1.077727f,.54f,.57f,.58f,.70f,.72f,.74f,.50f,.54f,.168936f,1.505649f,.56f,.56f};
    void Awake(){Backing=gameObject.AddComponent<AudioSource>();Backing.playOnAwake=false;Backing.priority=0;for(int i=0;i<128;i++){var s=gameObject.AddComponent<AudioSource>();s.playOnAwake=false;voices.Add(s);}foreach(string k in SampleKeys)clips[k]=Resources.Load<AudioClip>("Drums/"+k);
#if !UNITY_WEBGL || UNITY_EDITOR
        for(int i=0;i<32;i++){var go=new GameObject("OpenHat"+i);go.transform.SetParent(transform);var envelope=go.AddComponent<HiHatEnvelope>();envelope.Source=go.AddComponent<AudioSource>();envelope.Source.playOnAwake=false;envelope.Clear();hatVoices.Add(envelope);}
#endif
    }
    public void Stop(){Running=false;Backing.Stop();foreach(var s in voices)s.Stop();openHats.Clear();voiceStarts.Clear();StopHats();}
    public void Pause(){double b=Beat;Stop();AnchorBeat=b;}
    public void Seek(double beat){bool play=Running;Stop();AnchorBeat=beat;if(play)Play(beat);}
    public void Play(double beat){Stop();AnchorBeat=beat;AnchorDSP=AudioSettings.dspTime+.08;Running=true;if(Song!=null){Backing.clip=Song;Backing.pitch=(float)Rate;Backing.volume=BackingGain;double sec=Offset+beat*60/BPM;double wait=Math.Max(0,-sec)/Rate;if(sec<Song.length){Backing.timeSamples=(int)Math.Max(0,Math.Min(Song.samples-1,Math.Round(Math.Max(0,sec)*Song.frequency)));Backing.PlayScheduled(AnchorDSP+wait);}}}
    public void Preview(){Stop();Rate=1;Backing.pitch=1;if(Song==null)return;Backing.clip=Song;Backing.volume=BackingGain;Backing.timeSamples=(int)Math.Min(Song.samples-1,Math.Max(0,Offset)*Song.frequency);Backing.Play();}
    void Update(){if(Backing!=null)Backing.volume=BackingGain;}
    public void Load(ChartProject p){
        Stop();Rate=1;Backing.pitch=1;foreach(var c in gainClips.Values)Destroy(c);gainClips.Clear();gainScales.Clear();loadGeneration++;Project=p;BPM=p.BPM;Offset=p.Offset;AnchorBeat=-8;
        if(Song!=null)Destroy(Song);Song=null;Waveform=null;
        BackingGain=PlayerPrefs.GetFloat("musicVolume",(float?)p.Chart["mix"]?["backing"]??.7f);DrumGain=PlayerPrefs.GetFloat("drumsVolume",(float?)p.Chart["mix"]?["drums"]??.8f);
        StartCoroutine(FinishLoad(p,loadGeneration));
    }
    IEnumerator FinishLoad(ChartProject p,int generation){
        // Web clips decode asynchronously. Never read an unready clip as silence.
        double deadline=Time.realtimeSinceStartupAsDouble+30;
        foreach(var clip in clips.Values){
            if(clip==null)continue;
            if(clip.loadState==AudioDataLoadState.Unloaded)clip.LoadAudioData();
            while(clip.loadState==AudioDataLoadState.Loading&&Time.realtimeSinceStartupAsDouble<deadline){
                if(generation!=loadGeneration)yield break;
                yield return null;
            }
            if(generation!=loadGeneration)yield break;
            if(clip.loadState!=AudioDataLoadState.Loaded){OnReady?.Invoke("Drum sample could not load: "+clip.name);yield break;}
        }
        try {
        PrepareDrums();
#if UNITY_WEBGL && !UNITY_EDITOR
        if(clips.TryGetValue("OHH",out var hat)&&hat!=null){var pcm=new float[hat.samples*hat.channels];if(!hat.GetData(pcm,0))throw new Exception("Cannot prepare open hi-hat");CRHatPrepare(pcm,hat.samples,hat.channels,hat.frequency);}
#endif
        if(!p.Files.ContainsKey(p.AudioPath)){OnReady?.Invoke("");yield break;}
#if UNITY_WEBGL && !UNITY_EDITOR
            byte[] data=p.Files[p.AudioPath];CRDecodeAudio(data,data.Length,gameObject.name,loadGeneration);
#else
            string ext=Path.GetExtension(p.AudioPath);string temp=Path.Combine(Application.temporaryCachePath,"cross-rhythm-audio"+ext);File.WriteAllBytes(temp,p.Files[p.AudioPath]);
            float[] pcm;int channels,rate;DecodeNative(temp,out pcm,out channels,out rate);
            Song=AudioClip.Create("Backing",pcm.Length/channels,channels,rate,false);Song.SetData(pcm,0);Waveform=new AudioWaveform(pcm,channels,rate);OnReady?.Invoke("");
#endif
        }catch(Exception e){OnReady?.Invoke(e.Message);}
    }
    public static void DecodeNative(string path,out float[] pcm,out int channels,out int rate){
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        using(var reader=new MediaFoundationReader(path)){var provider=reader.ToSampleProvider();channels=provider.WaveFormat.Channels;rate=provider.WaveFormat.SampleRate;var samples=new List<float>();var block=new float[65536];int count;while((count=provider.Read(block,0,block.Length))>0){for(int i=0;i<count;i++)samples.Add(block[i]);if(samples.Count>48000*2*60*30)throw new Exception("Audio too long");}pcm=samples.ToArray();}
#else
        pcm=new float[0];channels=2;rate=48000;throw new Exception("Audio decoder unavailable on this platform");
#endif
    }
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]static extern void CRDecodeAudio(byte[] bytes,int length,string target,int generation);
    [DllImport("__Internal")]static extern void CRFree(int pointer);
#endif
    public void OnAudioDecoded(string json){
#if UNITY_WEBGL && !UNITY_EDITOR
        var o=JObject.Parse(json);int ptr=(int)o["pointer"];
        try{if((int)o["generation"]!=loadGeneration)return;int count=(int)o["count"],ch=(int)o["channels"],rate=(int)o["rate"];var samples=new float[count];Marshal.Copy(new IntPtr(ptr),samples,0,count);Song=AudioClip.Create("Backing",count/ch,ch,rate,false);Song.SetData(samples,0);Waveform=new AudioWaveform(samples,ch,rate);OnReady?.Invoke("");}finally{CRFree(ptr);}
#endif
    }
    public void OnAudioError(string msg){try{var o=JObject.Parse(msg);if((int)o["generation"]==loadGeneration)OnReady?.Invoke((string)o["error"]);}catch{OnReady?.Invoke(msg);}}
    public static string KeyFor(ChartNote n,bool closed){switch(n.Instrument){case "BD":return "BD";case "HH":return closed?"HH":"OHH";case "CR":return n.Articulation=="splash"?"SPLASH":n.Articulation=="china"?"CHINA":"CR";case "RD":return n.Articulation=="cup"?"CUP":n.Articulation=="crash"?"RIDE_CRASH":"RD";case "SN":return n.Articulation=="rim_closed"?"SIDE":n.Articulation=="rim_open"?"SN_RIM":n.Articulation=="buzz"?"SN_BUZZ":"SN";case "HT":case "MT":return n.Articulation=="rimshot"?"TOM_RIM":"HT";case "FT":return n.Articulation=="rimshot"?"TOM_RIM":"FT";default:return "HH_PEDAL";}}
    public void Choke(double when=-1){double close=when<0?AudioSettings.dspTime:when;
#if UNITY_WEBGL && !UNITY_EDITOR
        CRHatClose(Math.Max(0,close-AudioSettings.dspTime));
#else
        foreach(var h in hatVoices){if(h.Close(close))h.Source.SetScheduledEndTime(close+HiHatClosure.Stop);}
#endif
    }
    void StopHats(){
#if UNITY_WEBGL && !UNITY_EDITOR
        CRHatStop();
#else
        foreach(var h in hatVoices){h.Source.Stop();h.Clear();}
#endif
    }
    void PlayHat(AudioClip clip,float level,float pitch,double when){double start=when<0?AudioSettings.dspTime:Math.Max(AudioSettings.dspTime,when);
#if UNITY_WEBGL && !UNITY_EDITOR
        CRHatPlay(level*(gainScales.TryGetValue("OHH",out var g)?g:1),pitch,Math.Max(0,start-AudioSettings.dspTime));
#else
        var h=hatVoices[hatVoice++%hatVoices.Count];h.Source.Stop();h.Prepare(start);h.Source.clip=clip;h.Source.pitch=pitch;h.Source.volume=level;h.Source.PlayScheduled(start);
#endif
    }
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]static extern void CRHatPrepare(float[] pcm,int frames,int channels,int rate);
    [DllImport("__Internal")]static extern void CRHatPlay(float gain,float pitch,double delay);
    [DllImport("__Internal")]static extern void CRHatClose(double delay);
    [DllImport("__Internal")]static extern void CRHatStop();
    [DllImport("__Internal")]static extern void CRHatCancelFuture();
#endif
    public void CancelFutureDrums(){foreach(var pair in voiceStarts)if(pair.Value>AudioSettings.dspTime)pair.Key.Stop();
#if UNITY_WEBGL && !UNITY_EDITOR
        CRHatCancelFuture();
#else
        foreach(var h in hatVoices)if(h.Start>AudioSettings.dspTime){h.Source.Stop();h.Clear();}
#endif
    }
    public void Drum(ChartNote n,bool closed,double when=-1){
        string key=KeyFor(n,closed);float pitch=1;
        if(n.Instrument=="HT")pitch=n.Articulation=="rimshot"?1.34f:n.Articulation=="high"?1.18f:1;
        if(n.Instrument=="MT")pitch=n.Articulation=="rimshot"?1.16f:n.Articulation=="high"?1:.84f;
        if(n.Instrument=="FT"&&n.Articulation=="high")pitch=1.18f;
        float mix=(float?)Project?.Chart["mix"]?["instruments"]?[n.Instrument]??1;
        float kitGain=1,kitRate=1;string kit=(string)Project?.Chart["drumKit"]??"studio";
        // Preset maps are copied from the HTML reference without altering project values.
        var preset=KitPreset.Get(kit,key);kitGain=preset.x;kitRate=preset.y;
        PlayKey(key,ChartProject.Gains[n.Velocity]*mix*kitGain,pitch*kitRate,when,n.Articulation=="buzz"?n.Duration*60/BPM/Rate:0);
    }
    public void Pedal(double when=-1){PlayKey("HH_PEDAL",1,1,when,0);}
    public void Click(bool accent,double when){PlayKey("SIDE",accent?.5f:.25f,accent?1.7f:1.4f,when,0);}
    void PrepareDrums(){
        float maxMix=1;var mix=Project.Chart["mix"]?["instruments"] as JObject;
        if(mix!=null)foreach(var item in mix.Properties())maxMix=Math.Max(maxMix,(float?)item.Value??1);
        string kit=(string)Project.Chart["drumKit"]??"studio";
        for(int k=0;k<SampleKeys.Length;k++){
            string key=SampleKeys[k];var clip=clips[key];if(clip==null)continue;
            float level=Math.Max(1,1.27f*maxMix*KitPreset.Get(kit,key).x*SampleGains[k]);
            if(level<=1)continue;
            var samples=new float[clip.samples*clip.channels];
            if(!clip.GetData(samples,0))throw new Exception("Cannot prepare drum sample: "+key);
            for(int i=0;i<samples.Length;i++)samples[i]*=level;
            var prepared=AudioClip.Create(key+"-gain",clip.samples,clip.channels,clip.frequency,false);prepared.SetData(samples,0);
            gainScales[key]=level;gainClips[key]=prepared;
        }
    }
    void PlayKey(string key,float gain,float pitch,double when,double duration){
        if(!clips.TryGetValue(key,out var clip)||clip==null)return;
        float level=DrumGain*gain*SampleGains[Array.IndexOf(SampleKeys,key)];
        if(gainClips.TryGetValue(key,out var prepared)){clip=prepared;level/=gainScales[key];}
        if(key=="HH"||key=="HH_PEDAL")Choke(when);
        if(key=="OHH"){PlayHat(clip,level,pitch,when);return;}
        var s=voices[voice++%voices.Count];openHats.Remove(s);s.Stop();s.clip=clip;s.pitch=pitch;s.volume=level;s.loop=duration>0;
        double start=when<0?AudioSettings.dspTime:Math.Max(AudioSettings.dspTime,when);
        voiceStarts[s]=start;
        if(when<0)s.Play();else s.PlayScheduled(start);
        if(duration>0)s.SetScheduledEndTime(start+duration);
        if(key=="OHH")openHats.Add(s);
    }
}
public static class KitPreset {
    static JObject data;
    public static Vector2 Get(string kit,string key){if(data==null){var text=Resources.Load<TextAsset>("kits");data=text==null?new JObject():JObject.Parse(text.text);}var k=data[kit]??data["studio"];float g=(float?)k?["gainAll"]??1,r=(float?)k?["rateAll"]??1;return new Vector2(g*((float?)k?["sampleGain"]?[key]??1),r*((float?)k?["sampleRate"]?[key]??1));}
}
}
