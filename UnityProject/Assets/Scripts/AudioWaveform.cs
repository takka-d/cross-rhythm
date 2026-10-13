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

// Reuses the expensive audio min/max queries while the editor viewport is unchanged.
// The arrays grow only when a wider viewport is requested, so steady repaint calls
// neither rescan the waveform nor create managed garbage.
public sealed class WaveformViewportCache {
    const float ColumnPixels=2;
    AudioWaveform source;
    ChartProject project;
    int revision=-1,count;
    double offset;
    float pixelsPerBeat,left,right;
    float[] low=Array.Empty<float>(),high=Array.Empty<float>();
    public int Count=>count;
    public int RebuildCount {get;private set;}
    public float XAt(int index)=>left+index*ColumnPixels;
    public float LowAt(int index)=>low[index];
    public float HighAt(int index)=>high[index];
    void Capacity(int needed){
        if(low.Length>=needed)return;
        int size=Math.Max(16,low.Length);while(size<needed)size*=2;
        Array.Resize(ref low,size);Array.Resize(ref high,size);
    }
    public bool Ensure(AudioWaveform waveform,ChartProject chart,double audioOffset,float ppb,float fromX,float toX){
        if(waveform==null||chart==null||ppb<=0||toX<=fromX){count=0;source=waveform;project=chart;return false;}
        int columns=(int)Math.Ceiling((toX-fromX)/ColumnPixels);
        if(ReferenceEquals(source,waveform)&&ReferenceEquals(project,chart)&&revision==chart.Revision&&offset.Equals(audioOffset)&&pixelsPerBeat.Equals(ppb)&&left.Equals(fromX)&&right.Equals(toX)&&count==columns)return false;
        Capacity(columns);source=waveform;project=chart;revision=chart.Revision;offset=audioOffset;pixelsPerBeat=ppb;left=fromX;right=toX;count=columns;
        for(int i=0;i<count;i++){float x=left+i*ColumnPixels;double from=offset+project.SecondsAtBeat(x/pixelsPerBeat),to=offset+project.SecondsAtBeat((x+ColumnPixels)/pixelsPerBeat);source.Range(from,to,out low[i],out high[i]);}
        RebuildCount++;return true;
    }
}
}
