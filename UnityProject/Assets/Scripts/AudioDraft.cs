using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
// Local frequency-band draft, ported from v175 browserDraft. This is a heuristic,
// not source separation or an ADTOF model. It never modifies the input project.
public sealed class AudioDraft {
    public ChartProject Layout;public JArray Events=new JArray();
    struct Point {public int Bar;public double Local,Beat;}
    public IEnumerable<float> Run(float[] mono,int sampleRate,ChartProject source,bool extend){
        Layout=new ChartProject{Chart=(JObject)source.Chart.DeepClone(),Manifest=(JObject)source.Manifest.DeepClone()};Layout.Rebuild();
        double end=Math.Max(0,(mono.Length/(double)sampleRate-Layout.Offset)*Layout.BPM/60);
        if(extend&&end>Layout.Length){
            var bars=(JArray)Layout.Chart["measures"];double length=Layout.Measures.Last(),total=Layout.Length;
            var meters=new JArray(Enumerable.Range(0,bars.Count).Select(i=>{var t=Layout.Meter(i);return new JObject{{"numerator",t.Item1},{"denominator",t.Item2}};}));
            while(total<end){bars.Add(length);meters.Add(meters.Last.DeepClone());total+=length;}
            Layout.Chart["timeSignatures"]=meters;Layout.Rebuild();
        }
        var points=new List<Point>();double q=Layout.Grid;
        for(int bar=0;bar<Layout.Measures.Length;bar++)for(int j=0;j*q<Layout.Measures[bar]-1e-9;j++){
            if(points.Count>=250000)throw new InvalidOperationException("Too many grid points. Choose a coarser Grid.");
            points.Add(new Point{Bar=bar,Local=j*q,Beat=Layout.Starts[bar]+j*q});
        }
        var levels=new float[3][];var early=new float[points.Count];var late=new float[points.Count];var filtered=new float[mono.Length];
        for(int band=0;band<3;band++){
            var filter=new Biquad(band,sampleRate);levels[band]=new float[points.Count];
            for(int i=0;i<mono.Length;i++){filtered[i]=filter.Next(mono[i]);if((i&32767)==32767)yield return (band+i/(float)mono.Length)/3;}
            for(int i=0;i<points.Count;i++){
                double t=Layout.Offset+points[i].Beat*60/Layout.BPM;
                levels[band][i]=Rms(filtered,sampleRate,t,.025);
                if(band==2){early[i]=Rms(filtered,sampleRate,t+.015,.018);late[i]=Rms(filtered,sampleRate,t+.16,.035);}
                if((i&2047)==2047)yield return (band+.98f)/3;
            }
        }
        double[] thresholds={Percentile(levels[0],.82),Percentile(levels[1],.83),Percentile(levels[2],.70)};
        for(int i=0;i<points.Count;i++){
            var p=points[i];double t=Layout.Offset+p.Beat*60/Layout.BPM;if(t<0||t>=mono.Length/(double)sampleRate)continue;
            for(int band=0;band<3;band++){
                var values=levels[band];double value=values[i],threshold=thresholds[band];
                if(value<=Math.Max(1e-8,threshold))continue;
                if(band<2&&(value<(i>0?values[i-1]:0)||value<(i+1<values.Length?values[i+1]:0)))continue;
                string inst=band==0?"BD":band==1?"SN":"HH";
                Add(p,inst,q,Math.Min(1,(band==2?.50:.55)+(band==2?.35:.45)*value/(2*threshold+.00001)));
                if(band==2&&late[i]<=early[i]*.42)Add(p,"HHSTATE",q,.6);
            }
            if((i&2047)==2047)yield return .99f;
        }
        yield return 1;
    }
    void Add(Point p,string inst,double q,double confidence){var e=new JObject{{"id",Guid.NewGuid().ToString("N")},{"measure",p.Bar},{"beat",p.Local},{"instrument",inst},{"velocity",4},{"gridStepBeats",q},{"confidence",confidence},{"source","browser-draft"}};if(inst=="SN"||inst=="HHSTATE")e["durationBeats"]=q;if(inst=="SN")e["articulation"]="center";Events.Add(e);}
    public JObject Apply(ChartProject source,bool replace){
        var chart=(JObject)source.Chart.DeepClone();chart["measures"]=Layout.Chart["measures"].DeepClone();if(Layout.Chart["timeSignatures"]!=null)chart["timeSignatures"]=Layout.Chart["timeSignatures"].DeepClone();
        var events=replace?new JArray():(JArray)source.Events.DeepClone();
        var existing=new HashSet<string>(events.OfType<JObject>().Select(Key));
        foreach(JObject note in Events)if(existing.Add(Key(note)))events.Add(note.DeepClone());chart["events"]=events;return chart;
    }
    static string Key(JObject n)=>((int?)n["measure"]??0)+":"+Math.Round((double?)n["beat"]??0,8).ToString(System.Globalization.CultureInfo.InvariantCulture)+":"+(string)n["instrument"];
    static double Percentile(float[] values,double p){var sorted=(float[])values.Clone();Array.Sort(sorted);return sorted.Length==0?0:sorted[(int)Math.Floor((sorted.Length-1)*p)];}
    static float Rms(float[] x,int sr,double t,double window){int c=(int)Math.Floor(t*sr),r=Math.Max(4,(int)Math.Floor(window*sr)),n=0;double sum=0;for(int i=Math.Max(0,c-r),end=Math.Min(x.Length,c+r);i<end;i+=2){sum+=x[i]*x[i];n++;}return (float)Math.Sqrt(sum/Math.Max(1,n));}
    struct Biquad {
        double b0,b1,b2,a1,a2,z1,z2;
        public Biquad(int band,int sr){double f=Math.Min(sr*.45,band==0?180:band==1?1200:3800),w=2*Math.PI*f/sr,c=Math.Cos(w),s=Math.Sin(w),alpha=s/(2*(band==1?.8:.7)),a0=1+alpha;
            b0=(band==0?(1-c)/2:band==1?alpha:(1+c)/2)/a0;b1=(band==0?1-c:band==1?0:-(1+c))/a0;b2=b0*(band==1?-1:1);a1=-2*c/a0;a2=(1-alpha)/a0;z1=z2=0;
        }
        public float Next(float x){double y=b0*x+z1;z1=b1*x-a1*y+z2;z2=b2*x-a2*y;return (float)y;}
    }
}
}
