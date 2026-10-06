using System;
using UnityEngine;
namespace CrossRhythm {
public static class HiHatClosure {
    public const double Stop=.142;
    public static double Level(double t){if(t<=0)return 1;if(t<.010)return 1-.18*t/.010;if(t<.052)return .82*Math.Pow(.16/.82,(t-.010)/.042);if(t<.112)return .16*Math.Pow(.006/.16,(t-.052)/.060);if(t<.132)return .006*(1-(t-.112)/.020);return 0;}
    public static double Cutoff(double t,double rate){double initial=rate*.5,target=Math.Min(5200,rate*.45);return initial*Math.Pow(target/initial,Math.Max(0,Math.Min(1,t/.052)));}
}
// Each open-hat voice has its own gain/filter state. The audio thread applies the
// v175 envelope independently from velocity and mix gain, even during slow frames.
public sealed class HiHatEnvelope:MonoBehaviour {
    readonly object gate=new object();
    public AudioSource Source;
    public double Start {get;private set;}
    double close=double.PositiveInfinity;
    double[] z1=new double[8],z2=new double[8];
    int rate;
    public void Prepare(double start){lock(gate){Start=start;close=double.PositiveInfinity;Array.Clear(z1,0,z1.Length);Array.Clear(z2,0,z2.Length);rate=AudioSettings.outputSampleRate;}}
    public bool Close(double when){lock(gate){if(Start>when+1e-6||close<=when+1e-6)return false;close=when;return true;}}
    public void Clear(){lock(gate){close=double.PositiveInfinity;Start=double.PositiveInfinity;}}
    void OnAudioFilterRead(float[] data,int channels){
        lock(gate){if(double.IsPositiveInfinity(close)||rate<=0)return;double t=AudioSettings.dspTime-close,step=1.0/rate;
            double b0=1,b1=0,b2=0,a1=0,a2=0;
            for(int i=0;i<data.Length;i+=channels,t+=step){if(t<0)continue;
                if((i/channels)%16==0||b1==0){double hz=Math.Min(rate*.499,HiHatClosure.Cutoff(t,rate)),w=2*Math.PI*hz/rate,c=Math.Cos(w),alpha=Math.Sin(w)/Math.Sqrt(2),a0=1+alpha;b0=(1-c)/2/a0;b1=(1-c)/a0;b2=b0;a1=-2*c/a0;a2=(1-alpha)/a0;}
                double level=HiHatClosure.Level(t);
                for(int ch=0;ch<channels;ch++){double x=data[i+ch],y=b0*x+z1[ch];z1[ch]=b1*x-a1*y+z2[ch];z2[ch]=b2*x-a2*y;data[i+ch]=(float)(y*level);}
            }
        }
    }
}
}
