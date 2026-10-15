using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
// Geometry shared by rendering and picking. Matches v175 score geometry and the inherited HTML editor.
public static class ChartVisuals {
    public const float StageTripletGuideWidth=.5f;
    public const float StageTripletGuideOpacity=.018f;
    public static Color Hex(string hex){ColorUtility.TryParseHtmlString(hex,out var color);return color;}
    public static Color PlayColor(string inst)=>Hex(inst=="CR"?"#bf9b48":inst=="RD"?"#bf9b48":inst=="HH"?"#9ba9b8":inst=="SN"?"#5678c8":inst=="HT"?"#e9bb62":inst=="MT"?"#c8874a":inst=="FT"?"#be626b":inst=="BD"?"#2f8f9d":"#8162bd");
    public static Color EditColor(ChartProject p,ChartNote n){
        string hex=n.Articulation=="tambourine"?"#e3c18c":n.Instrument=="HH"?(p.ClosedAt(n.Beat)?"#f7d25f":"#ffad4f"):n.Instrument=="SN"?"#ff6f7d":n.Instrument=="CR"?"#7ee787":n.Instrument=="RD"?"#63d8c7":n.Instrument=="HT"?"#ff9f68":n.Instrument=="MT"?"#ff8b68":n.Instrument=="FT"?"#ff7468":n.Instrument=="BD"?"#59a9ff":"#c084fc";
        Color c=Hex(hex);c.a=.45f+.55f*Mathf.Clamp((float?)n.Source?["confidence"]??.8f,.15f,1);return c;
    }
    public static string TypeCode(ChartNote n){
        string a=n.Articulation;
        if(string.IsNullOrEmpty(a)||a=="normal")a=n.Instrument=="CR"?"crash":n.Instrument=="RD"?"ride":n.Instrument=="SN"||n.Instrument=="HT"||n.Instrument=="MT"||n.Instrument=="FT"?"center":"";
        if(n.Instrument=="RD"&&a=="crash")return "RC";
        switch(a){case "crash":return "CR";case "splash":return "SP";case "china":return "CH";case "ride":return "RD";case "cup":return "BL";case "center":return "N";case "rim_open":return "OR";case "rim_closed":return "CS";case "rimshot":return "RM";case "high":return "HI";case "buzz":return "BZ";case "tambourine":return "TB";default:return "";}
    }
    public static Rect EditRect(ChartProject p,ChartNote n,int row,float ppb,float rh){
        float span=(float)Math.Min(n.DisplaySpan,p.Measures[n.Measure]-n.Local)*ppb;
        float x=(float)n.Beat*ppb,w;
        if(n.Sustained){span=(float)Math.Min(n.Duration,p.Length-n.Beat)*ppb;float inset=Math.Min(2,span*.15f);x+=inset;w=Math.Max(2,span-2*inset);}
        else{x+=span*.15f;w=Math.Max(3,span*.7f);}
        float rawH=rh*ChartProject.Heights[n.Velocity],h=Math.Max(10,Mathf.Round(rawH)-1);
        return new Rect(x,Mathf.Round(row*rh+rh/2-rawH/2)+.5f,w,h);
    }
    public static double DisplayStep(ChartProject p,int bar)=>p.DisplayStep(bar);
    public static double BeatUnit(ChartProject p,int bar)=>bar<0?CountIn.Unit(p):4.0/p.Meter(bar).Item2;
    public static bool IsBeatLine(ChartProject p,int bar,double local){double beat=local/BeatUnit(p,bar);return Math.Abs(beat-Math.Round(beat))<1e-7;}
    public static void EditGridPoints(ChartProject p,int bar,SortedSet<double> points){
        points.Clear();
        double beat=BeatUnit(p,bar);for(int i=0;i*beat<p.Measures[bar]-1e-8;i++)points.Add(i*beat);
        double grid=p.Grid;for(int i=0;i*grid<p.Measures[bar]-1e-8;i++)points.Add(i*grid);
        // Imported and mixed-grid onsets remain visible even when the placement
        // grid differs. Guides use their actual timing, including MIDI tick rounding.
        foreach(var n in p.NotesInBar(bar))points.Add(n.Local);
    }
    public static bool TripletStep(double step){
        if(step<=0)return false;double power=Math.Log(step*3,2);
        return Math.Abs(power-Math.Round(power))<.003;
    }
    public static double[] GridPoints(ChartProject p,int bar){
        double length=bar<0?CountIn.Length(p):p.Measures[bar];var points=new SortedSet<double>{0,length};
        foreach(double step in new[]{.25,BeatUnit(p,bar)})for(int i=0;i*step<length-1e-8;i++)points.Add(Math.Round(i*step,9));
        // Add only the triplet group occupied by a note. Quintuplets and higher
        // keep the sixteenth-note guides, without altering their actual onsets.
        foreach(var n in p.NotesInBar(bar)){
            if(n.Pedal||!TripletStep(n.Step))continue;
            double step=Math.Pow(2,Math.Round(Math.Log(n.Step*3,2)))/3,span=step*3;
            double start=Math.Floor((n.Local+.00051)/span)*span;
            for(int i=0;i<=3;i++){double at=start+i*step;if(at>=0&&at<=length+1e-8)points.Add(Math.Round(Math.Min(length,at),9));}
        }
        return points.ToArray();
    }
    public static float CellX(ChartProject p,int bar,double local,float left,float width){double len=bar<0?CountIn.Length(p):p.Measures[bar];return left+(float)((local+Math.Min(DisplayStep(p,bar),len-local)/2)/len)*width;}
    static int Order(string inst){switch(inst){case "BD":return 0;case "HH":return 1;case "SN":return 2;case "HT":return 3;case "MT":return 4;case "FT":return 5;case "CR":return 6;case "RD":return 7;default:return 8;}}
    static readonly Comparison<ChartNote> OffsetComparison=CompareOffsetNotes;
    static int CompareOffsetNotes(ChartNote a,ChartNote b){
        int lane=StringComparer.Ordinal.Compare(a.Lane,b.Lane);if(lane!=0)return lane;
        int beat=Math.Round(a.Beat,3).CompareTo(Math.Round(b.Beat,3));if(beat!=0)return beat;
        int instrument=Order(a.Instrument).CompareTo(Order(b.Instrument));if(instrument!=0)return instrument;
        int id=StringComparer.Ordinal.Compare(a.Id,b.Id);return id!=0?id:a.Index.CompareTo(b.Index);
    }
    public static void FillSimultaneousOffsets(IList<ChartNote> notes,Dictionary<int,float> result,List<ChartNote> order){
        result.Clear();order.Clear();for(int i=0;i<notes.Count;i++)order.Add(notes[i]);order.Sort(OffsetComparison);
        for(int first=0;first<order.Count;){int end=first+1;double beat=Math.Round(order[first].Beat,3);string lane=order[first].Lane;
            while(end<order.Count&&StringComparer.Ordinal.Equals(order[end].Lane,lane)&&Math.Round(order[end].Beat,3)==beat)end++;
            for(int i=first;i<end;i++)result[order[i].Index]=(i-first-(end-first-1)*.5f)*16;first=end;
        }
    }
    public static Dictionary<int,float> SimultaneousOffsets(IEnumerable<ChartNote> notes){
        var source=notes as IList<ChartNote>??notes.ToList();var result=new Dictionary<int,float>();FillSimultaneousOffsets(source,result,new List<ChartNote>(source.Count));return result;
    }
}
}
