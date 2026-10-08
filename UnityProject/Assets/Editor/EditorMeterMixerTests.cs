using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public static class EditorMeterMixerTests {
    public static void Run(){
        void Check(bool ok,string name){if(!ok)throw new Exception("METER/MIXER: "+name);Debug.Log("PASS meter/mixer: "+name);}
        foreach(var meter in new[]{(4,4),(3,4),(6,8),(7,8),(9,8),(12,8),(2,2),(5,16),(7,32)}){
            var p=ChartProject.Demo();p.Events.Clear();p.Rebuild();p.SetMeter(0,meter.Item1,meter.Item2);p.Chart["quantize"]=12;
            var before=p.Chart.ToString();var grid=new SortedSet<double>();ChartVisuals.EditGridPoints(p,0,grid);
            var expected=Enumerable.Range(0,meter.Item1).Select(i=>i*4.0/meter.Item2).ToArray();
            Check(grid.Where(b=>ChartVisuals.IsBeatLine(p,0,b)).SequenceEqual(expected),"editor beat lines follow numerator and denominator "+meter);
            Check(ChartVisuals.GridPoints(p,0).Where(b=>b<p.Measures[0]-1e-8&&ChartVisuals.IsBeatLine(p,0,b)).SequenceEqual(expected),"stage beat lines match Edit "+meter);
            Check(Math.Abs(ChartVisuals.BeatUnit(p,-1)-4.0/meter.Item2)<1e-9,"count-in grid uses the same denominator "+meter);
            Check(p.Chart.ToString()==before,"rendering preserves imported timing "+meter);
            Check(ChartVisuals.BeatUnit(p,1)==1,"following 4/4 bar retains its own beat size "+meter);
        }
        var chart=ChartProject.Demo();chart.Chart["mix"]=new JObject{{"backing",.23},{"drums",.81},{"custom","preserve"}};
        var events=chart.Events.ToString();
        Check(chart.InstrumentGain("SN")==1,"missing instrument volume defaults to 100 percent");
        chart.SetInstrumentGain("SN",.37f);chart.SetInstrumentGain("HH",0);chart.SetInstrumentGain("RD",1.5f);
        Check(chart.InstrumentGain("HHSTATE")==0&&chart.InstrumentGain("HH_PEDAL")==0&&chart.InstrumentGain("OHH")==0,"HH family shares the same mixer gain");
        var restored=ChartProject.Read(chart.Write(),"mixer.crproj");
        Check(restored.InstrumentGain("SN")==.37f&&restored.InstrumentGain("RD")==1.5f,"per-instrument mix survives save reload");
        Check((double)restored.Chart["mix"]["backing"]==.23&&(double)restored.Chart["mix"]["drums"]==.81&&(string)restored.Chart["mix"]["custom"]=="preserve"&&restored.Events.ToString()==events,"mix edits preserve music/master volumes, extensions and notes");
        chart.SetInstrumentGain("CR",10);chart.SetInstrumentGain("FT",-1);Check(chart.InstrumentGain("CR")==1.5f&&chart.InstrumentGain("FT")==0,"editor mixer respects the v175 zero to 150 percent range");
    }
}
}
