using System;
using System.Collections.Generic;
namespace CrossRhythm {
public sealed partial class ChartProject {
    public int Revision {get;private set;}
    int[] barNoteStarts=Array.Empty<int>();
    double[] barDisplaySteps=Array.Empty<double>(),noteEndPrefix=Array.Empty<double>();
    readonly Dictionary<int,ChartNote> noteByIndex=new Dictionary<int,ChartNote>();
    readonly Dictionary<string,int> instrumentCounts=new Dictionary<string,int>();
    void RebuildNoteIndex(){
        barNoteStarts=new int[Measures.Length+1];barDisplaySteps=new double[Measures.Length];noteEndPrefix=new double[Notes.Count];
        noteByIndex.Clear();instrumentCounts.Clear();int cursor=0;double end=0;
        for(int bar=0;bar<Measures.Length;bar++){
            barNoteStarts[bar]=cursor;double step=.25;
            while(cursor<Notes.Count&&Notes[cursor].Measure==bar){
                var n=Notes[cursor];step=Math.Min(step,n.Step);end=Math.Max(end,n.Beat+Math.Max(n.Duration,.25));noteEndPrefix[cursor]=end;
                noteByIndex[n.Index]=n;instrumentCounts.TryGetValue(n.Instrument,out int count);instrumentCounts[n.Instrument]=count+1;cursor++;
            }
            barDisplaySteps[bar]=step;
        }
        barNoteStarts[Measures.Length]=cursor;
        foreach(var p in Pedals){p.HasKick=false;for(int i=NoteLowerBound(p.Start);i<Notes.Count&&Notes[i].Beat<p.End;i++)if(Notes[i].Instrument=="BD"){p.HasKick=true;break;}}
        Revision++;
    }
    public int NoteLowerBound(double beat){int lo=0,hi=Notes.Count;while(lo<hi){int mid=lo+(hi-lo)/2;if(Notes[mid].Beat<beat)lo=mid+1;else hi=mid;}return lo;}
    public IEnumerable<ChartNote> NotesBetween(double from,double to){for(int i=NoteLowerBound(from);i<Notes.Count&&Notes[i].Beat<=to;i++)yield return Notes[i];}
    public IEnumerable<ChartNote> NotesInBar(int bar){if(bar<0||bar>=Measures.Length)yield break;for(int i=barNoteStarts[bar];i<barNoteStarts[bar+1];i++)yield return Notes[i];}
    public IEnumerable<ChartNote> NotesOverlapping(double from,double to){
        int lo=0,hi=noteEndPrefix.Length;while(lo<hi){int mid=lo+(hi-lo)/2;if(noteEndPrefix[mid]<from)lo=mid+1;else hi=mid;}
        for(int i=lo;i<Notes.Count&&Notes[i].Beat<=to;i++)if(Notes[i].Beat+Math.Max(Notes[i].Duration,.25)>=from)yield return Notes[i];
    }
    public IEnumerable<PedalRange> PedalsOverlapping(double from,double to){
        int lo=0,hi=Pedals.Count;while(lo<hi){int mid=lo+(hi-lo)/2;if(Pedals[mid].End<=from)lo=mid+1;else hi=mid;}
        for(int i=lo;i<Pedals.Count&&Pedals[i].Start<to;i++)yield return Pedals[i];
    }
    public double DisplayStep(int bar)=>bar<0?.25:barDisplaySteps[bar];
    public int InstrumentCount(string instrument)=>instrumentCounts.TryGetValue(instrument,out int count)?count:0;
    public ChartNote NoteAtIndex(int index)=>noteByIndex.TryGetValue(index,out var n)?n:null;
}
}
