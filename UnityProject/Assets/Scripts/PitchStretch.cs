using System;
using System.Runtime.InteropServices;
namespace CrossRhythm {
// The same processor is linked into WebAssembly and the Windows player.
public sealed class PitchStretch : IDisposable {
#if UNITY_WEBGL && !UNITY_EDITOR
    const string Library="__Internal";
#else
    const string Library="CrossRhythmStretch";
#endif
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern IntPtr CRStretchCreate(int channels,int rate);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern void CRStretchDestroy(IntPtr p);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int CRStretchInputLatency(IntPtr p);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int CRStretchOutputLatency(IntPtr p);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int CRStretchSeek(IntPtr p,float[] data,int frames,double rate);
    [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]static extern int CRStretchProcess(IntPtr p,float[] input,int inFrames,[Out]float[] output,int outFrames);
    IntPtr handle;readonly int channels;
    public int InputLatency {get;}
    public int OutputLatency {get;}
    public PitchStretch(int channels,int sampleRate){this.channels=channels;handle=CRStretchCreate(channels,sampleRate);if(handle==IntPtr.Zero)throw new Exception("Pitch processor could not start");InputLatency=CRStretchInputLatency(handle);OutputLatency=CRStretchOutputLatency(handle);}
    public void Seek(float[] samples,double rate){if(handle==IntPtr.Zero||CRStretchSeek(handle,samples,samples.Length/channels,rate)==0)throw new Exception("Pitch processor seek failed");}
    public void Process(float[] input,int inFrames,float[] output,int outFrames){if(handle==IntPtr.Zero||input.Length<inFrames*channels||output.Length<outFrames*channels||CRStretchProcess(handle,input,inFrames,output,outFrames)==0)throw new Exception("Pitch processor failed");}
    public void Dispose(){if(handle!=IntPtr.Zero){CRStretchDestroy(handle);handle=IntPtr.Zero;}}
}
}
