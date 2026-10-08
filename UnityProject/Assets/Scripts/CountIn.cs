using System;
using System.Collections.Generic;
namespace CrossRhythm {
// Keep the chart meter and beat grid. Even numerators cue two equal halves
// in the first bar, then every denominator beat. Odd meters count all beats;
// a time signature alone cannot specify an asymmetric grouping such as 2+3.
public static class CountIn {
    public static double Length(ChartProject p)=>p.Measures[0];
    public static double Start(ChartProject p)=>-2*Length(p);
    public static double Unit(ChartProject p)=>4.0/p.Meter(0).Item2;
    public static int BarAt(ChartProject p,double beat)=>(int)Math.Floor(beat/Length(p));
    public static IEnumerable<double> Between(ChartProject p,double from,double to){
        double length=Length(p),unit=Unit(p);var meter=p.Meter(0);
        for(int bar=-2;bar<0;bar++){
            double step=bar==-2&&meter.Item1%2==0?length/2:unit;
            for(int i=0;i*step<length-1e-8;i++){
                double b=bar*length+i*step;if(b>=from-1e-8&&b<=to+1e-8)yield return b;
            }
        }
    }
    public static bool Accent(ChartProject p,double beat)=>Math.Abs(beat+2*Length(p))<1e-7||Math.Abs(beat+Length(p))<1e-7;
}
}
