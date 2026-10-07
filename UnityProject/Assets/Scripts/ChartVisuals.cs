using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
// Geometry shared by rendering and picking. Matches v175 score geometry and the inherited HTML editor.
public static class ChartVisuals {
    public static Color Hex(string hex){ColorUtility.TryParseHtmlString(hex,out var color);return color;}
    public static Color PlayColor(string inst)=>Hex(inst=="CR"||inst=="RD"?"#bf9b48":inst=="HH"?"#9ba9b8":inst=="SN"?"#5678c8":inst=="HT"||inst=="MT"||inst=="FT"?"#c8874a":inst=="BD"?"#2f8f9d":"#8162bd");
    public static Color EditColor(ChartProject p,ChartNote n){
        string hex=n.Instrument=="HH"?(p.ClosedAt(n.Beat)?"#f7d25f":"#ffad4f"):n.Instrument=="SN"?"#ff6f7d":n.Instrument=="CR"?"#7ee787":n.Instrument=="RD"?"#63d8c7":n.Instrument=="HT"?"#ff9f68":n.Instrument=="MT"?"#ff8b68":n.Instrument=="FT"?"#ff7468":n.Instrument=="BD"?"#59a9ff":"#c084fc";
        Color c=Hex(hex);c.a=.45f+.55f*Mathf.Clamp((float?)n.Source?["confidence"]??.8f,.15f,1);return c;
    }
    public static string TypeCode(ChartNote n){
        string a=n.Articulation;
        if(string.IsNullOrEmpty(a)||a=="normal")a=n.Instrument=="CR"?"crash":n.Instrument=="RD"?"ride":n.Instrument=="SN"||n.Instrument=="HT"||n.Instrument=="MT"||n.Instrument=="FT"?"center":"";
        if(n.Instrument=="RD"&&a=="crash")return "RC";
        switch(a){case "crash":return "CR";case "splash":return "SP";case "china":return "CH";case "ride":return "RD";case "cup":return "BL";case "center":return "N";case "rim_open":return "OR";case "rim_closed":return "CS";case "rimshot":return "RM";case "high":return "HI";case "buzz":return "BZ";default:return "";}
    }
    public static Rect EditRect(ChartProject p,ChartNote n,int row,float ppb,float rh){
        float span=(float)Math.Min(Math.Min(.25,n.Step),p.Measures[n.Measure]-n.Local)*ppb;
        bool duration=n.Pedal||(n.Instrument=="SN"&&(n.Articulation=="buzz"||n.Duration>Math.Max(.25,n.Step)+1e-8));
        float x=(float)n.Beat*ppb,w;
        if(duration){span=(float)Math.Min(n.Duration,p.Length-n.Beat)*ppb;float inset=Math.Min(2,span*.15f);x+=inset;w=Math.Max(2,span-2*inset);}
        else{x+=span*.15f;w=Math.Max(3,span*.7f);}
        float rawH=rh*ChartProject.Heights[n.Velocity],h=Math.Max(10,Mathf.Round(rawH)-1);
        return new Rect(x,Mathf.Round(row*rh+rh/2-rawH/2)+.5f,w,h);
    }
    public static double DisplayStep(ChartProject p,int bar)=>bar<0?.25:p.Notes.Where(n=>n.Measure==bar).Select(n=>n.Step).Append(.25).Min();
    public static double[] GridPoints(ChartProject p,int bar){
        double length=bar<0?CountIn.Length(p):p.Measures[bar];var notes=p.Notes.Where(n=>n.Measure==bar).ToArray();var points=new SortedSet<double>{0,length};
        foreach(double step in notes.Select(n=>n.Step).Concat(new[]{.25,1}).Distinct())for(int i=0;i*step<length-1e-8;i++)points.Add(Math.Round(i*step,9));
        foreach(var n in notes)points.Add(Math.Round(n.Local,9));return points.ToArray();
    }
    public static float CellX(ChartProject p,int bar,double local,float left,float width){double len=bar<0?CountIn.Length(p):p.Measures[bar];return left+(float)((local+Math.Min(DisplayStep(p,bar),len-local)/2)/len)*width;}
    static int Order(string inst){switch(inst){case "BD":return 0;case "HH":return 1;case "SN":return 2;case "HT":return 3;case "MT":return 4;case "FT":return 5;case "CR":return 6;case "RD":return 7;default:return 8;}}
    public static Dictionary<int,float> SimultaneousOffsets(IEnumerable<ChartNote> notes){
        var result=new Dictionary<int,float>();foreach(var group in notes.GroupBy(n=>n.Lane+"|"+Math.Round(n.Beat,3).ToString(System.Globalization.CultureInfo.InvariantCulture))){var same=group.OrderBy(n=>Order(n.Instrument)).ThenBy(n=>n.Id,StringComparer.Ordinal).ToArray();for(int i=0;i<same.Length;i++)result[same[i].Index]=(i-(same.Length-1)*.5f)*16;}return result;
    }
}
}
