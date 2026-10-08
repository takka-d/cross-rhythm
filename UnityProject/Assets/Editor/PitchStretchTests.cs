using System;
using UnityEngine;
namespace CrossRhythm {
public static class PitchStretchTests {
    public static void Run(){int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception("Pitch stretch: "+message);checks++;}
        const int sr=22050,ch=2,frames=sr*4;var input=new float[frames*ch];for(int i=0;i<frames;i++){input[i*2]=(float)(.2*Math.Sin(2*Math.PI*440*i/sr));input[i*2+1]=(float)(.2*Math.Sin(2*Math.PI*660*i/sr));}
        foreach(double speed in new[]{.25,.55,.95,1,1.25,1.7,2})using(var p=new PitchStretch(ch,sr)){
            var initial=new float[p.InputLatency*ch];Array.Copy(input,initial,initial.Length);p.Seek(initial,speed);
            int length=(int)Math.Ceiling(frames/speed),done=0,copied=0,skip=p.OutputLatency;var result=new float[length*ch];var block=new float[512*ch];
            while(copied<length){int n=Math.Min(512,length-copied+skip),at=(int)Math.Round(done*speed),count=(int)Math.Round((done+n)*speed)-at;var source=new float[count*ch];int valid=Math.Max(0,Math.Min(count,frames-p.InputLatency-at));if(valid>0)Array.Copy(input,(p.InputLatency+at)*ch,source,0,valid*ch);p.Process(source,count,block,n);done+=n;int discard=Math.Min(skip,n);skip-=discard;Array.Copy(block,discard*ch,result,copied*ch,(n-discard)*ch);copied+=n-discard;}
            Check(Math.Abs(PitchCheckSpectrum.Peak(result,ch,0,sr/2,sr,sr,440)-440)<2,"left pitch at "+speed);
            Check(Math.Abs(PitchCheckSpectrum.Peak(result,ch,1,sr/2,sr,sr,660)-660)<2,"right pitch at "+speed);
            double power=0;foreach(float v in result){if(float.IsNaN(v)||float.IsInfinity(v))throw new Exception("Nonfinite stretched audio");power+=v*v;}
            Check(power/result.Length>.005&&power/result.Length<.08,"finite audible stereo at "+speed);
            Check(copied==length,"exact duration at "+speed);
        }
        Debug.Log("CROSS_RHYTHM_PITCH_TESTS_PASS "+checks);
    }
}
}
