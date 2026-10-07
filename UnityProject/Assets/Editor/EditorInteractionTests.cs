using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CrossRhythm {
public static class EditorInteractionTests {
    static void Check(bool ok,string label){if(!ok)throw new Exception("EDITOR INPUT TEST FAILED: "+label);Debug.Log("PASS editor input: "+label);}
    sealed class Harness {
        public ChartProject P=ChartProject.Demo();
        public EditorInteraction E;
        public Stack<JObject> Undo=new Stack<JObject>();
        public int Seeks;
        public Harness(){
            P.Chart["measures"]=new JArray(4,3);P.Chart["events"]=new JArray();
            Add(0,"CR");Add(1.0/3,"HH");Add(1,"SN");Add(1,"BD");Add(3,"SN");Add(4.25,"HH");Add(5,"HHSTATE",1);
            P.Rebuild();E=new EditorInteraction(new HashSet<int>()){Project=P,PPB=100,RowHeight=46,BeforeEdit=()=>Undo.Push((JObject)P.Chart.DeepClone()),Seek=b=>Seeks++};
        }
        void Add(double b,string inst,double length=.25){int m=b>=4?1:0;P.Events.Add(new JObject{{"id","test"+P.Events.Count},{"measure",m},{"beat",b-(m==1?4:0)},{"instrument",inst},{"velocity",4},{"gridStepBeats",inst=="HH"&&b<1?1.0/3:.25},{"durationBeats",length}});}
        public Vector2 At(int i)=>E.NoteRect(P.Notes.First(n=>n.Index==i)).center;
        public void Click(int i,int button=0,int count=1,bool shift=false,bool ctrl=false){var v=At(i);E.Down(v,button,count,shift,ctrl);E.Up(v);}
        public void Restore(){P.Chart=Undo.Pop();P.Rebuild();E.Reset();}
    }
    public static void Run(){
        var h=new Harness();string raw=h.P.Chart.ToString();h.Click(2,1);
        Check(h.E.ContextOpen&&h.E.Selection.SetEquals(new[]{2})&&h.P.Chart.ToString()==raw&&h.Seeks==0,"right click opens context without deletion, seek or modification");
        h.E.Down(h.At(2),1,1,false,false);Check(!h.E.AutoScrolling,"stationary right click never auto-scrolls into a range drag");h.E.Up(h.At(2));
        h.Click(2,0,2);Check(h.P.Notes.Count==6&&!h.P.Notes.Any(n=>n.Instrument=="SN"&&n.Beat==1),"left double click deletes only the pointed note");h.Restore();Check(h.P.Chart.ToString()==raw,"double click is one undoable edit");
        h.E.Down(new Vector2(0,0),1,1,false,false);h.E.Move(new Vector2(155,370));h.E.Up(new Vector2(155,370));
        Check(h.E.Selection.SetEquals(new[]{0,1,2,3})&&!h.E.ContextOpen&&h.P.Chart.ToString()==raw,"right drag selects intersecting notes across lanes without changes");
        int count=h.P.Events.Count;h.E.Down(new Vector2(600,100),0,1,false,false);h.E.Up(new Vector2(600,100));Check(h.E.Selection.Count==0&&h.P.Events.Count==count,"blank click dismisses batch selection without adding");
        h.Click(1);h.Click(4,shift:true);Check(h.E.Selection.SetEquals(new[]{1,2,3,4}),"Shift selects time range across lanes");h.Click(2,shift:true);Check(h.E.Selection.SetEquals(new[]{1,2,3}),"subsequent Shift click keeps initial anchor");
        h.Click(4,ctrl:true);h.Click(2,ctrl:true);Check(h.E.Selection.SetEquals(new[]{1,3,4}),"Ctrl independently toggles notes");
        h.E.SelectLane(3,false);Check(h.E.Selection.SetEquals(new[]{2,4}),"lane label selects whole lane");h.E.SelectLane(7,true);Check(h.E.Selection.SetEquals(new[]{2,3,4}),"Ctrl lane label adds lane");h.E.SelectAll();Check(h.E.Selection.Count==7,"Ctrl+A includes pedal state");
        h.E.Copy();Check(h.E.HasClipboard&&h.E.Selection.Count==0,"copy retains clipboard and clears selection");
        h.E.Paste(h.P.Length);Check(h.E.Selection.Count==7&&h.P.Notes.All(n=>n.Beat+(n.Pedal||n.Instrument=="SN"?n.Duration:n.Step)<=h.P.Length+1e-8),"paste near end clamps complete range without dropping notes");
        h=new Harness();h.Click(2);h.E.Copy();h.E.Paste(1);Check(h.P.Events.Count==7&&h.E.Selection.Count==1,"paste replaces same lane/time instead of duplicating");h.Restore();Check(h.P.Events.Count==7,"paste and overwrite restore together");
        h=new Harness();h.Click(1);h.Click(2,ctrl:true);var before=h.P.Notes.ToDictionary(n=>n.Index,n=>n.Beat);h.E.Nudge(.25,0);Check(Math.Abs(h.P.Notes.First(n=>n.Index==1).Beat-7.0/12)<1e-8&&Math.Abs(h.P.Notes.First(n=>n.Index==2).Beat-1.25)<1e-8,"arrow move preserves relative mixed-tuplet timing");
        h.E.Nudge(-20,-20);var hh=h.P.Notes.First(n=>n.Id=="test1");var sn=h.P.Notes.First(n=>n.Id=="test2");Check(Math.Abs(hh.Beat)<1e-8&&Math.Abs(sn.Beat-2.0/3)<1e-8&&hh.Instrument=="CR"&&sn.Instrument=="RD","group clamps whole offset at time and lane boundaries");
        h=new Harness();h.E.SelectLane(3,false);var center=h.At(2);int undos=h.Undo.Count;h.E.Down(center,0,1,false,false);h.E.Move(center+new Vector2(100,0));h.E.Move(center+new Vector2(200,0));h.E.Up(center+new Vector2(200,0));Check(h.Undo.Count==undos+1&&h.P.Notes.Where(n=>n.Instrument=="SN").Select(n=>n.Beat).SequenceEqual(new[]{3.0,5.0}),"left drag moves selection and creates one undo snapshot");h.Restore();Check(h.P.Notes.First(n=>n.Index==2).Beat==1,"drag undo restores original");
        h=new Harness();h.Click(1);h.Click(3,ctrl:true);Rect bounds=h.E.Bounds();var blank=new Vector2(bounds.center.x,250);h.E.Down(blank,0,1,false,false);h.E.Move(blank+new Vector2(50,0));h.E.Up(blank+new Vector2(50,0));Check(Math.Abs(h.P.Notes.First(n=>n.Index==1).Beat-5.0/6)<1e-8&&h.P.Notes.First(n=>n.Index==3).Beat==1.5,"selected group can be dragged from blank space inside bounds");
        h=new Harness();var r=h.E.NoteRect(h.P.Notes.First(n=>n.Index==2));h.E.Down(new Vector2(r.xMax-1,r.center.y),0,1,false,false);h.E.Move(new Vector2(190,r.center.y));h.E.Up(new Vector2(190,r.center.y));var resized=h.P.Notes.First(n=>n.Index==2);Check(resized.Beat==1&&resized.Duration==1,"snare right edge extends to pointed cell end without changing start");
        h=new Harness();var start=new Vector2(220,8.5f*46);h.E.Down(start,0,1,false,false);h.E.Move(new Vector2(90,start.y));h.E.Up(new Vector2(90,start.y));var pedal=h.P.Notes.First(n=>n.Pedal&&n.Beat<2);Check(pedal.Beat==.75&&pedal.Duration==1.5,"pedal creation drags backwards across cells");
        h=new Harness();h.E.Down(new Vector2(133,-20),0,1,false,false);h.E.Move(new Vector2(289,-20));h.E.Up(new Vector2(289,-20));Check(h.E.Cursor==2.75&&h.P.Events.Count==7&&h.Seeks>=2,"wave/ruler drag seeks without adding notes");
        h=new Harness();h.E.Down(new Vector2(173,4.5f*46),0,1,false,false);h.E.Up(new Vector2(173,4.5f*46));Check(h.P.Notes.Any(n=>n.Instrument=="HT"&&n.Beat==1.5),"placement uses cell floor, not nearest grid point");
        h.P.Chart["measures"]=new JArray(3,4);h.P.Chart["quantize"]=20;h.P.Rebuild();Check(Math.Abs(EditorInteraction.CellStart(h.P,3.31)-3.2)<1e-8&&Math.Abs(EditorInteraction.CellEnd(h.P,2.99)-3)<1e-8,"odd tuplets snap per bar and stop at boundary");
        Check(Math.Abs(EditorInteraction.Follow(0,10,8.3,64)-5.8)<1e-8&&EditorInteraction.Follow(5.8,10,8.4,64)==5.8&&EditorInteraction.Follow(50,10,63,64)==54,"playback follows at 82 percent and returns playhead to 25 percent with end clamp");
        Check(EditorInteraction.EdgeSpeed(0,800)<0&&EditorInteraction.EdgeSpeed(800,800)>0&&EditorInteraction.EdgeSpeed(400,800)==0,"edge drag scroll works both directions only near edges");
        h=new Harness();var pedalRect=h.E.NoteRect(h.P.Notes.First(n=>n.Pedal));h.Click(6);h.E.Down(new Vector2(pedalRect.xMax+3,pedalRect.center.y),0,1,false,false);h.E.Move(new Vector2(670,pedalRect.center.y));h.E.Up(new Vector2(670,pedalRect.center.y));Check(h.P.Notes.First(n=>n.Pedal).Duration==1.75,"selected pedal edge can be grabbed outside its border and extended");h.Restore();
        h.Click(6);h.E.SetDuration(.5);Check(h.P.Notes.First(n=>n.Pedal).Duration==.5&&h.P.Pedals.Single().End==5.5,"numeric pedal length updates the playable state");
        var roundtrip=ChartProject.Read(h.P.Write(),"pedal.crproj");Check(roundtrip.Notes.First(n=>n.Pedal).Duration==.5,"pedal length survives save and reload");
        h.E.Clear();h.E.SetDuration(1.5);var pt=new Vector2(200,8.5f*46);h.E.Down(pt,0,1,false,false);h.E.Up(pt);Check(h.P.Notes.Any(n=>n.Pedal&&n.Beat==2&&n.Duration==1.5),"new pedal uses the chosen numeric length");
        Check(EditorInteraction.WheelBeat(2,3,.25,7)==2.75&&EditorInteraction.WheelBeat(0,-3,.25,7)==0&&EditorInteraction.WheelBeat(6.75,3,.25,7)==7,"wheel seeks by grid and clamps both ends");
        h=new Harness();raw=h.P.Chart.ToString();
        h.E.Down(new Vector2(0,0),0,1,true,false);h.E.Move(new Vector2(155,370));h.E.Up(new Vector2(155,370));
        Check(h.E.Selection.SetEquals(new[]{0,1,2,3})&&!h.E.ContextOpen&&h.P.Chart.ToString()==raw&&h.Undo.Count==0,"Shift left drag selects a rectangle without adding or moving notes");
        h.E.Clear();h.Click(1);h.E.Down(new Vector2(250,100),0,1,true,false);h.E.Up(new Vector2(250,100));
        Check(h.E.Selection.SetEquals(new[]{1,2,3})&&h.P.Chart.ToString()==raw,"Shift blank click extends the anchored time range");
        h.Click(4,ctrl:true,count:2);Check(h.E.Selection.Contains(4)&&h.P.Chart.ToString()==raw,"Ctrl double click never deletes a note");
        h.Click(2,shift:true,count:2);Check(h.P.Chart.ToString()==raw,"Shift double click never deletes a note");
        Check(CountIn.Between(-8,0).SequenceEqual(new double[]{-8,-6,-4,-3,-2,-1})&&CountIn.Numbers.SequenceEqual(new[]{1,2,1,2,3,4}),"count-in has exactly six cues and ends before the chart");
        Check(CountIn.Between(-5.5,-2.5).SequenceEqual(new double[]{-4,-3}),"resuming part-way through count-in never repeats earlier cues");
        Debug.Log("CROSS_RHYTHM_EDITOR_INPUT_TESTS_PASS");
    }
}
}
