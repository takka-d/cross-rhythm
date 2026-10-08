using System;
namespace CrossRhythm {
public static class PitchCheckSpectrum {
    // Frequency-domain check avoids counting extra zero crossings from spectral sidelobes.
    public static double Peak(float[] data,int channels,int channel,int start,int count,int sampleRate,double expected){
        double best=0,result=0;var windowed=new double[count];
        for(int i=0;i<count;i++)windowed[i]=data[(start+i)*channels+channel]*(.5-.5*Math.Cos(2*Math.PI*i/(count-1)));
        for(double hz=expected-5;hz<=expected+5;hz++){
            double coefficient=2*Math.Cos(2*Math.PI*hz/sampleRate),one=0,two=0;
            for(int i=0;i<count;i++){double next=windowed[i]+coefficient*one-two;two=one;one=next;}
            double power=(one*one+two*two-coefficient*one*two)/(count*(double)count);
            if(power>best){best=power;result=hz;}
        }
        return best>.0001?result:0;
    }
}
}
