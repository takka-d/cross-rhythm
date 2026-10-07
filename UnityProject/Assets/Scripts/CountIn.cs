using System;
using System.Collections.Generic;
namespace CrossRhythm {
// Two half-note cues, then four quarter-note cues. Beat zero starts the chart.
public static class CountIn {
    public static readonly double[] Beats={-8,-6,-4,-3,-2,-1};
    public static readonly int[] Numbers={1,2,1,2,3,4};
    public static bool IsCue(double beat)=>Array.Exists(Beats,b=>Math.Abs(b-beat)<1e-7);
    public static bool Accent(double beat)=>Math.Abs(beat+8)<1e-7||Math.Abs(beat+4)<1e-7;
    public static IEnumerable<double> Between(double from,double to){foreach(var b in Beats)if(b>=from-1e-8&&b<=to+1e-8)yield return b;}
}
}
