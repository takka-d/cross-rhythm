using System;

namespace CrossRhythm {
// Walks the time-sorted note index once during a performance. Keeping the next
// due note avoids rescanning an entire chart on every 120 Hz Update.
public sealed class PlaybackJudgementCursor {
    ChartProject project;
    int revision=-1,index;
    double anchor=double.NaN;

    public int Index=>index;
    public void Reset(){project=null;revision=-1;index=0;anchor=double.NaN;}

    void Sync(ChartProject next,double nextAnchor){
        if(ReferenceEquals(project,next)&&revision==next.Revision&&Math.Abs(anchor-nextAnchor)<1e-8)return;
        project=next;revision=next.Revision;anchor=nextAnchor;
        index=next.NoteLowerBound(nextAnchor-1e-8);
    }

    public ChartNote TakeNextDue(ChartProject next,double nextAnchor,double beat,bool automatic,double rate){
        if(next==null)return null;
        Sync(next,nextAnchor);rate=Math.Max(.25,Math.Min(2,rate));
        while(index<next.Notes.Count){
            var note=next.Notes[index];
            bool due=automatic?note.Beat<=beat:(next.SecondsAtBeat(beat)-next.SecondsAtBeat(note.Beat))/rate>.12;
            if(!due)return null;
            index++;
            if(note.Beat<nextAnchor-1e-8||note.Pedal)continue;
            return note;
        }
        return null;
    }
}
}
