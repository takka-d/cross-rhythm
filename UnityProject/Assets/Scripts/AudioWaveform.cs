using System;
namespace CrossRhythm {
// Cache channel extrema once. Rendering never reads or copies the full song.
public sealed class AudioWaveform {
    const int Block = 256;
    readonly float[] low, high;
    readonly int rate, frames;
    public double Seconds => (double)frames / rate;
    public AudioWaveform(int frameCount,int sampleRate){if(frameCount<0||sampleRate<1)throw new ArgumentException("Invalid PCM");rate=sampleRate;frames=frameCount;low=new float[(frames+Block-1)/Block];high=new float[low.Length];}
    public AudioWaveform(float[] pcm, int channels, int sampleRate) : this(channels>0?pcm.Length/channels:0,sampleRate) {
        if(channels<1 || sampleRate<1 || pcm.Length%channels!=0)throw new ArgumentException("Invalid PCM");
        Append(pcm,channels,0);
    }
    public void Append(float[] pcm,int channels,int frameOffset){
        if(channels<1||pcm.Length%channels!=0||frameOffset<0||frameOffset+pcm.Length/channels>frames)throw new ArgumentException("Invalid PCM range");
        for(int i=0;i<pcm.Length;i++){int b=(frameOffset+i/channels)/Block;float v=pcm[i];if(float.IsNaN(v)||float.IsInfinity(v))continue;low[b]=Math.Min(low[b],v);high[b]=Math.Max(high[b],v);}
    }
    public void Range(double from,double to,out float min,out float max){
        min=max=0;if(to<=from||to<=0||from>=Seconds||low.Length==0)return;
        int first=Math.Max(0,(int)Math.Floor(Math.Max(0,from)*rate/Block));
        int last=Math.Min(low.Length-1,(int)Math.Ceiling(Math.Min(Seconds,to)*rate/Block)-1);
        for(int b=first;b<=last;b++){min=Math.Min(min,low[b]);max=Math.Max(max,high[b]);}
    }
}
}
