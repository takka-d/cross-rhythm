using System;
using System.Linq;
using System.Collections.Generic;
namespace CrossRhythm {
[Serializable] public sealed class HitRecord { public int Index; public double Beat, Ms; public string Judge, Instrument; }
[Serializable] public sealed class ScoreResult {
    public const string Version="v1-provisional";
    public double Score, Accuracy, Hits, Beats, Coverage;
    public double? Bias, Spread, Between, Within;
    public string Rank;
    public int Total, Just, Fast, Late, Miss;
}
public static class RhythmScore {
    public static double? SD(IEnumerable<double> values){var a=values.ToArray();if(a.Length<2)return null;double avg=a.Average();return Math.Sqrt(a.Select(x=>(x-avg)*(x-avg)).Average());}
    static double Stable(double? spread,double coverage)=>spread.HasValue?Math.Max(0,1-spread.Value/60)*100*coverage:0;
    public static ScoreResult Calculate(IEnumerable<ChartNote> expected,IEnumerable<HitRecord> records){
        var ns=expected.Where(n=>!n.Pedal).ToArray();var ids=new HashSet<int>(ns.Select(n=>n.Index));var rs=records.Where(r=>ids.Contains(r.Index)).GroupBy(r=>r.Index).Select(g=>g.Last()).ToArray();var hs=rs.Where(r=>r.Judge!="MISS"&&!double.IsNaN(r.Ms)).ToArray();
        var s=new ScoreResult{Total=ns.Length,Coverage=ns.Length>0?(double)hs.Length/ns.Length:0};
        s.Bias=hs.Length>0?(double?)hs.Average(r=>r.Ms):null;s.Spread=SD(hs.Select(r=>r.Ms));
        s.Between=SD(hs.GroupBy(r=>Math.Floor(r.Beat+1e-7)).Select(g=>g.Average(r=>r.Ms)));
        s.Within=SD(hs.GroupBy(r=>Math.Round(r.Beat-Math.Floor(r.Beat+1e-7),6)).Where(g=>g.Count()>=2).Select(g=>g.Average(r=>r.Ms)));
        s.Accuracy=ns.Length>0?100.0/ns.Length*hs.Sum(r=>Math.Abs(r.Ms)<=45?1:Math.Max(0,(120-Math.Abs(r.Ms))/75)):0;
        s.Hits=Stable(s.Spread,s.Coverage);var valid=new[]{s.Between,s.Within}.Where(v=>v.HasValue).ToArray();s.Beats=valid.Length>0?valid.Average(v=>Stable(v,s.Coverage)):0;
        s.Score=Math.Round((s.Accuracy*.4+s.Hits*.3+s.Beats*.3)*10,MidpointRounding.AwayFromZero)/10;s.Rank=ns.Length==0?"—":s.Score>=90?"A":s.Score>=80?"B":s.Score>=65?"C":s.Score>=50?"D":"E";
        s.Just=rs.Count(r=>r.Judge=="JUST");s.Fast=rs.Count(r=>r.Judge=="FAST");s.Late=rs.Count(r=>r.Judge=="LATE");s.Miss=rs.Count(r=>r.Judge=="MISS");return s;
    }
}
}
