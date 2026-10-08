using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CrossRhythm {
// Editor input in chart coordinates. Windows and Web use the same v175 gestures.
public sealed class EditorInteraction {
    public static readonly string[] Lanes={"CR","RD","HH","SN","HT","MT","FT","BD","HHSTATE"};
    public ChartProject Project;
    public readonly HashSet<int> Selection;
    public Action BeforeEdit, Changed;
    public Action<double> Seek;
    public Action<ChartNote> Preview;
    public float PPB=56, RowHeight=46;
    public int DefaultVelocity=4, Anchor=-1, Active=-1, ContextTarget=-1;
    public bool ContextOpen, Batch;
    public double Cursor, ContextBeat,DefaultDuration=.25;
    public Vector2 ContextPoint;
    public string Message="";
    enum Gesture {None,Right,Range,Move,ResizeLeft,ResizeRight,Pedal,Seek}
    Gesture gesture;
    Vector2 origin,current;
    bool moved,pushed,groupGrab;
    int target;
    double originalStart,originalEnd;
    sealed class Original {public int Index,Lane; public double Beat,Length;}
    List<Original> originals;
    JArray clipboard;
    double[] copiedBeats;
    public bool Capturing=>gesture!=Gesture.None;
    public bool AutoScrolling=>moved||gesture==Gesture.Seek||gesture==Gesture.Pedal;
    public bool Selecting=>(gesture==Gesture.Right||gesture==Gesture.Range)&&moved;
    public bool HasClipboard=>clipboard!=null&&clipboard.Count>0;
    public Rect SelectionBox=>Rect.MinMaxRect(Math.Min(origin.x,current.x),Math.Min(origin.y,current.y),Math.Max(origin.x,current.x),Math.Max(origin.y,current.y));
    public EditorInteraction(HashSet<int> selection){Selection=selection;}
    public void Reset(){Cancel();Clear();ContextOpen=false;Cursor=0;}
    public void Cancel(){gesture=Gesture.None;originals=null;}
    public void Clear(){Selection.Clear();Anchor=Active=-1;Batch=false;}
    ChartNote Note(int index)=>Project.Notes.FirstOrDefault(n=>n.Index==index);
    public Rect NoteRect(ChartNote n)=>ChartVisuals.EditRect(Project,n,Array.IndexOf(Lanes,n.Instrument),PPB,RowHeight);
    ChartNote Hit(Vector2 point)=>Project.Notes.OrderByDescending(n=>n.Index).FirstOrDefault(n=>NoteRect(n).Contains(point));
    ChartNote HitResizeEdge(Vector2 point){return Project.Notes.Where(n=>Selection.Contains(n.Index)&&(n.Pedal||n.Instrument=="SN")).FirstOrDefault(n=>{var r=NoteRect(n);return point.y>=r.yMin-4&&point.y<=r.yMax+4&&(Math.Abs(point.x-r.xMin)<=7||Math.Abs(point.x-r.xMax)<=7);});}
    public void SetDuration(double beats){if(!(!double.IsNaN(beats)&&!double.IsInfinity(beats))||beats<=0)return;DefaultDuration=beats;var notes=Project.Notes.Where(n=>Selection.Contains(n.Index)&&(n.Pedal||n.Instrument=="SN")).ToArray();if(notes.Length==0)return;Snapshot();foreach(var n in notes)n.Source["durationBeats"]=Math.Min(beats,Project.Length-n.Beat);Commit();}
    public static double WheelBeat(double current,float delta,double grid,double length)=>Math.Max(0,Math.Min(length,current+delta*grid));
    public Rect Bounds(){var rects=Project.Notes.Where(n=>Selection.Contains(n.Index)).Select(NoteRect).ToArray();return rects.Length==0?default:Rect.MinMaxRect(rects.Min(r=>r.xMin),rects.Min(r=>r.yMin),rects.Max(r=>r.xMax),rects.Max(r=>r.yMax));}
    public static double CellStart(ChartProject p,double beat){
        if(beat>=p.Length-1e-9)return p.Length;
        int m=Math.Max(0,p.BarAt(Math.Max(0,beat)));
        // Pointer positions arrive as floats. A drawn tuplet line can round just
        // below its double-precision beat; keep it in the intended cell.
        double tolerance=Math.Min(p.Grid*.001,Math.Max(1e-9,Math.Abs(beat)*1.2e-7));
        double cell=Math.Floor((beat-p.Starts[m]+tolerance)/p.Grid);
        cell=Math.Min(cell,Math.Ceiling(p.Measures[m]/p.Grid-1e-9)-1);
        return p.Starts[m]+Math.Max(0,cell)*p.Grid;
    }
    public static double PlacementBeat(ChartProject p,double beat)=>p.SnapToGrid?CellStart(p,beat):Math.Max(0,Math.Min(p.Length,beat));
    public static double AudioOffsetForDrag(ChartProject p,double offset,double fromBeat,double toBeat){
        double delta=toBeat-fromBeat;
        if(p.SnapToGrid)delta=Math.Round(delta/p.Grid,MidpointRounding.AwayFromZero)*p.Grid;
        double target=Math.Max(0,Math.Min(p.Length,fromBeat+delta));
        return offset+p.SecondsAtBeat(fromBeat)-p.SecondsAtBeat(target);
    }
    public static double CellEnd(ChartProject p,double beat){double start=CellStart(p,beat);if(start>=p.Length)return p.Length;int m=p.BarAt(start);return Math.Min(p.Starts[m]+p.Measures[m],start+p.Grid);}
    public static double Follow(double view,double span,double beat,double length){if(beat>view+span*.82||beat<view)view=beat-span*.25;return Math.Max(0,Math.Min(Math.Max(0,length-span),view));}
    public static double EdgeSpeed(float x,float width){const float edge=44;return x<edge?-12*(.18+.82*Math.Min(1,(edge-x)/edge)):x>width-edge?12*(.18+.82*Math.Min(1,(x-width+edge)/edge)):0;}
    double Length(ChartNote n)=>n.Pedal||n.Instrument=="SN"?n.Duration:n.Step;
    void Commit(){Project.Dirty=true;Project.Rebuild();Changed?.Invoke();}
    void Snapshot(){BeforeEdit?.Invoke();}
    void Only(ChartNote n){Clear();if(n!=null){Selection.Add(n.Index);Anchor=Active=n.Index;}}
    void RefreshAnchor(){Anchor=Project.Notes.Where(n=>Selection.Contains(n.Index)).OrderBy(n=>n.Beat).ThenBy(n=>Array.IndexOf(Lanes,n.Instrument)).Select(n=>n.Index).DefaultIfEmpty(-1).First();Active=Anchor;}
    public void SelectLane(int row,bool additive){ContextOpen=false;if(!additive)Clear();foreach(var n in Project.Notes.Where(n=>n.Instrument==Lanes[row]))Selection.Add(n.Index);Batch=true;RefreshAnchor();}
    public void SelectAll(){Clear();foreach(var n in Project.Notes)Selection.Add(n.Index);Batch=true;RefreshAnchor();}
    void Range(ChartNote n){var anchor=Note(Anchor);if(anchor==null){Only(n);return;}double lo=Math.Min(anchor.Beat,n.Beat)-1e-9,hi=Math.Max(anchor.Beat,n.Beat)+1e-9;Selection.Clear();foreach(var item in Project.Notes.Where(v=>v.Beat>=lo&&v.Beat<=hi))Selection.Add(item.Index);Batch=true;Active=n.Index;}
    void SetCursor(float x){Cursor=PlacementBeat(Project,Math.Max(0,Math.Min(Project.Length,x/PPB)));Seek?.Invoke(Cursor);}
    public void Down(Vector2 point,int button,int clicks,bool shift,bool control){
        ContextOpen=false;origin=current=point;moved=pushed=false;target=-1;
        if(button==1){gesture=Gesture.Right;return;}
        if(button!=0)return;
        if(point.y<0){if(Batch||Selection.Count>1)Clear();gesture=Gesture.Seek;SetCursor(point.x);return;}
        var hit=HitResizeEdge(point)??Hit(point);
        if(shift){
            if(hit!=null)Range(hit);
            else {var anchor=Note(Anchor);if(anchor!=null){double end=point.x/PPB,lo=Math.Min(anchor.Beat,end)-1e-9,hi=Math.Max(anchor.Beat,end)+1e-9;Selection.Clear();foreach(var n in Project.Notes.Where(n=>n.Beat>=lo&&n.Beat<=hi))Selection.Add(n.Index);Batch=true;}}
            gesture=Gesture.Range;return;
        }
        if(hit!=null&&control){if(!Selection.Add(hit.Index))Selection.Remove(hit.Index);if(Anchor<0||!Selection.Contains(Anchor))RefreshAnchor();Active=hit.Index;Batch=Selection.Count>1;return;}
        if(shift||control)return;
        if(clicks>=2&&hit!=null){Cancel();Delete(hit.Index);return;}
        if(hit!=null&&(hit.Pedal||hit.Instrument=="SN")&&(hit.Pedal||Selection.Count<=1||!Selection.Contains(hit.Index))){
            Rect r=NoteRect(hit);float edge=hit.Pedal?Math.Min(10,Math.Max(5,r.width*.24f)):Math.Min(9,Math.Max(2,r.width*.22f));
            if(point.x<r.xMin+edge||point.x>r.xMax-edge){Only(hit);target=hit.Index;originalStart=hit.Beat;originalEnd=Math.Min(Project.Length,hit.Beat+hit.Duration);gesture=point.x<r.center.x?Gesture.ResizeLeft:Gesture.ResizeRight;return;}
        }
        if(hit!=null||(Selection.Count>1&&Bounds().Contains(point))){
            if(hit!=null&&!Selection.Contains(hit.Index))Only(hit);
            groupGrab=hit==null;if(hit==null)hit=Project.Notes.Where(n=>Selection.Contains(n.Index)).OrderBy(n=>NoteRect(n).center.x+NoteRect(n).center.y).First();
            target=hit.Index;Active=target;originals=Project.Notes.Where(n=>Selection.Contains(n.Index)).Select(n=>new Original{Index=n.Index,Lane=Array.IndexOf(Lanes,n.Instrument),Beat=n.Beat,Length=Length(n)}).ToList();gesture=Gesture.Move;Preview?.Invoke(hit);return;
        }
        if(Batch||Selection.Count>1){Clear();return;}
        Clear();int row=(int)(point.y/RowHeight);double beat=PlacementBeat(Project,point.x/PPB);if(row<0||row>=Lanes.Length||beat<0||beat>=Project.Length)return;
        var duplicate=Project.Notes.FirstOrDefault(n=>n.Instrument==Lanes[row]&&Math.Abs(n.Beat-beat)<1e-6);if(duplicate!=null){Only(duplicate);return;}
        Snapshot();int bar=Project.BarAt(beat);string inst=Lanes[row];var e=new JObject{{"id","u-"+Guid.NewGuid().ToString("N")},{"measure",bar},{"beat",beat-Project.Starts[bar]},{"instrument",inst},{"velocity",DefaultVelocity},{"gridStepBeats",Project.Grid},{"confidence",1},{"source","manual"}};
        if(inst=="SN"||inst=="HHSTATE")e["durationBeats"]=Math.Min(inst=="HHSTATE"?Math.Max(Project.Grid,DefaultDuration):Project.Grid,Project.Length-beat);
        e["articulation"]=inst=="CR"?"crash":inst=="RD"?"ride":inst=="SN"||inst=="HT"||inst=="MT"||inst=="FT"?"center":inst=="HHSTATE"?"closed":"auto";
        target=Project.Events.Count;Project.Events.Add(e);Commit();Only(Note(target));Preview?.Invoke(Note(target));
        if(inst=="HHSTATE"){gesture=Gesture.Pedal;originalStart=beat;pushed=true;}
    }
    public void Move(Vector2 point){
        current=point;if(!Capturing)return;
        if(gesture==Gesture.Seek){SetCursor(point.x);return;}
        if(gesture==Gesture.Right||gesture==Gesture.Range){if(Vector2.Distance(origin,point)>=5)moved=true;if(moved){Selection.Clear();foreach(var n in Project.Notes)if(NoteRect(n).Overlaps(SelectionBox,true))Selection.Add(n.Index);Batch=true;RefreshAnchor();}return;}
        if(!moved&&Vector2.Distance(origin,point)<5)return;moved=true;
        if(gesture==Gesture.Move){
            // Move by the gesture delta, not by the absolute pointer cell. This
            // preserves imported timing and the grab offset on mixed grids.
            var anchor=originals.First(n=>n.Index==target);double db=Project.SnapToGrid?Math.Floor((point.x-origin.x)/PPB/Project.Grid+.5)*Project.Grid:(point.x-origin.x)/PPB;
            int lane=(int)Math.Floor(point.y/RowHeight);int dl=lane>=0&&lane<Lanes.Length?lane-anchor.Lane:(int)Math.Floor((point.y-origin.y)/RowHeight+.5);
            MoveOriginals(originals,db,dl,true);return;
        }
        double start=originalStart,end=originalEnd,minLength=Project.SnapToGrid?Project.Grid:1e-6;
        double pointedStart=PlacementBeat(Project,point.x/PPB),pointedEnd=Project.SnapToGrid?CellEnd(Project,point.x/PPB):pointedStart;
        if(gesture==Gesture.ResizeLeft)start=Math.Min(pointedStart,end-minLength);
        if(gesture==Gesture.ResizeRight)end=Math.Max(start+minLength,pointedEnd);
        if(gesture==Gesture.Pedal){double pointed=pointedStart;start=Math.Min(originalStart,pointed);end=pointed<originalStart?(Project.SnapToGrid?CellEnd(Project,originalStart):originalStart):Math.Max(start+minLength,pointedEnd);}
        start=Math.Max(0,Math.Min(Project.Length-1e-9,start));end=Math.Max(start+1e-9,Math.Min(Project.Length,end));
        var note=Note(target);if(note==null||Math.Abs(note.Beat-start)<1e-9&&Math.Abs(note.Duration-(end-start))<1e-9)return;
        if(!pushed){Snapshot();pushed=true;}int m=Project.BarAt(start);note.Source["measure"]=m;note.Source["beat"]=start-Project.Starts[m];note.Source["durationBeats"]=end-start;Commit();
    }
    public void Up(Vector2 point){
        if(!Capturing)return;Move(point);
        if(gesture==Gesture.Right&&!moved){var hit=Hit(point);if(hit==null)Clear();else if(!Selection.Contains(hit.Index))Only(hit);ContextTarget=hit?.Index??-1;ContextBeat=PlacementBeat(Project,point.x/PPB);ContextPoint=point;ContextOpen=true;}
        if(gesture==Gesture.Move&&groupGrab&&!moved)Clear();
        if(gesture==Gesture.Move&&moved)Dedupe();
        if(Selecting)Message=Selection.Count+" notes selected";
        Cancel();
    }
    void MoveOriginals(List<Original> items,double db,int dl,bool dragging){
        db=Math.Max(-items.Min(n=>n.Beat),Math.Min(Project.Length-items.Max(n=>n.Beat+n.Length),db));dl=Math.Max(-items.Min(n=>n.Lane),Math.Min(Lanes.Length-1-items.Max(n=>n.Lane),dl));
        if(!items.Any(o=>{var n=Note(o.Index);return n!=null&&(Math.Abs(n.Beat-o.Beat-db)>1e-9||n.Instrument!=Lanes[o.Lane+dl]);}))return;
        if(!dragging||!pushed){Snapshot();pushed=true;}
        foreach(var o in items){var n=Note(o.Index);if(n==null)continue;double beat=o.Beat+db;int bar=Project.BarAt(beat);n.Source["measure"]=bar;n.Source["beat"]=beat-Project.Starts[bar];n.Source["instrument"]=Lanes[o.Lane+dl];}Commit();
    }
    public void Nudge(double beats,int lanes){var items=Project.Notes.Where(n=>Selection.Contains(n.Index)).Select(n=>new Original{Index=n.Index,Lane=Array.IndexOf(Lanes,n.Instrument),Beat=n.Beat,Length=Length(n)}).ToList();if(items.Count==0)return;MoveOriginals(items,beats,lanes,false);Dedupe();RefreshAnchor();}
    public void Delete(int index=-1){var ids=index<0?Selection.ToArray():new[]{index};if(ids.Length==0)return;Snapshot();foreach(int i in ids.OrderByDescending(i=>i))if(i>=0&&i<Project.Events.Count)Project.Events.RemoveAt(i);Clear();Commit();}
    public void Copy(){if(Selection.Count==0)return;var notes=Project.Notes.Where(n=>Selection.Contains(n.Index)).ToArray();clipboard=new JArray(notes.Select(n=>n.Source.DeepClone()));copiedBeats=notes.Select(n=>n.Beat).ToArray();Clear();Message="Copied";}
    public void Paste(double requested){
        if(!HasClipboard)return;double first=copiedBeats.Min(),span=0;
        for(int i=0;i<clipboard.Count;i++){var e=(JObject)clipboard[i];double length=(string)e["instrument"]=="SN"||(string)e["instrument"]=="HHSTATE"?(double?)e["durationBeats"]??ChartProject.EventStep(e):ChartProject.EventStep(e);span=Math.Max(span,copiedBeats[i]-first+length);}
        if(span>Project.Length+1e-9){Message="The copied range is longer than this chart";return;}
        double anchor=PlacementBeat(Project,Math.Max(0,Math.Min(Project.Length-span,requested)));Snapshot();Clear();
        for(int i=0;i<clipboard.Count;i++){var e=(JObject)clipboard[i].DeepClone();double b=anchor+copiedBeats[i]-first;int m=Project.BarAt(b);e["measure"]=m;e["beat"]=b-Project.Starts[m];e["id"]="u-"+Guid.NewGuid().ToString("N");Selection.Add(Project.Events.Count);Project.Events.Add(e);}Commit();Dedupe();RefreshAnchor();Batch=Selection.Count>1;Cursor=anchor;Seek?.Invoke(Cursor);
    }
    void Dedupe(){
        var chosen=new Dictionary<string,JObject>();var preferred=new HashSet<JObject>(Selection.Where(i=>i>=0&&i<Project.Events.Count).Select(i=>(JObject)Project.Events[i]));
        foreach(JObject e in Project.Events){string key=e["measure"]+"|"+((double?)e["beat"]??0).ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+"|"+e["instrument"];if(!chosen.TryGetValue(key,out var prev)||preferred.Contains(e)||!preferred.Contains(prev))chosen[key]=e;}
        if(chosen.Count==Project.Events.Count)return;var keep=new HashSet<JObject>(chosen.Values);var remaining=Project.Events.OfType<JObject>().Where(keep.Contains).ToArray();Project.Chart["events"]=new JArray(remaining);Selection.Clear();for(int i=0;i<remaining.Length;i++)if(preferred.Contains(remaining[i]))Selection.Add(i);Commit();RefreshAnchor();
    }
}
}
