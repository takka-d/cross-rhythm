using System;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public static class CountInTests {
    public static void Run(){
        void Check(bool ok,string name){if(!ok)throw new Exception("Count-in test failed: "+name);Debug.Log("PASS count-in: "+name);}
        foreach(var meter in new[]{Tuple.Create(4,4),Tuple.Create(3,4),Tuple.Create(5,4),Tuple.Create(6,8),Tuple.Create(7,8),Tuple.Create(2,4),Tuple.Create(8,8),Tuple.Create(12,8),Tuple.Create(9,8)}){
            var p=ChartProject.Demo();p.Chart["events"]=new JArray();p.Rebuild();p.SetMeter(0,meter.Item1,meter.Item2);
            double length=meter.Item1*4.0/meter.Item2,unit=4.0/meter.Item2;
            var cues=CountIn.Between(p,CountIn.Start(p),0).ToArray();
            Check(CountIn.Start(p)==-2*length&&p.BarAt(-2*length)==-2&&p.BarAt(-length)==-1&&p.BarAt(0)==0,"bar boundaries "+meter);
            Check(cues.Length==(meter.Item1%2==0?meter.Item1+2:meter.Item1*2)&&cues.Last()==-unit,"cue count and final cue "+meter);
            if(meter.Item1%2==0)Check(cues.Take(2).SequenceEqual(new[]{-2*length,-1.5*length}),"even meter first bar has two half-bar cues "+meter);
            Check(cues.All(c=>c<0)&&cues.Distinct().Count()==cues.Length,"no extra cue at chart start "+meter);
            Check(CountIn.Accent(p,-length)&&CountIn.Accent(p,-2*length)&&!CountIn.Accent(p,-unit),"bar accents "+meter);
            Check(CountIn.Unit(p)==unit&&ChartVisuals.GridPoints(p,-2).Last()==length,"visual beat size and grid "+meter);
            Check(CountIn.Between(p,-length+.01,-unit).SequenceEqual(cues.Where(c=>c>=-length+.01&&c<=-unit)),"resume skips earlier cues "+meter);
        }
    }
}
}
