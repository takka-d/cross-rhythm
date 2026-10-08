using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public static class TupletTests {
    static void Check(bool ok,string label){if(!ok)throw new Exception("TUPLET TEST FAILED: "+label);Debug.Log("PASS tuplet: "+label);}
    static ChartProject Empty(){var p=ChartProject.Demo();p.Chart["measures"]=new JArray(3.5,4,4,4,4,4,4,4);p.Chart["events"]=new JArray();p.Rebuild();return p;}
    static byte[] Midi(int ppq,int division,bool loose=false){
        var track=new List<byte>();int last=0;
        void V(int value){var bytes=new List<byte>{(byte)(value&127)};while((value>>=7)>0)bytes.Insert(0,(byte)(128|(value&127)));track.AddRange(bytes);}
        void E(int tick,params byte[] data){V(tick-last);track.AddRange(data);last=tick;}
        E(0,255,88,4,7,3,24,8);E(0,255,81,3,7,161,32);
        for(int i=0;i<division;i++)E((int)Math.Round(i*(double)ppq/division),153,51,(byte)(40+i%6*15));
        E((int)(3.5*ppq),255,88,4,4,2,24,8);
        for(int i=0;i<division;i++)E((int)Math.Round((3.5+i/(double)division)*ppq)+(loose&&i==1?3:0),153,38,100);
        E(last+ppq,255,47,0);
        var b=new List<byte>();void N(int n,int count){for(int i=count-1;i>=0;i--)b.Add((byte)(n>>(8*i)));}
        b.AddRange(System.Text.Encoding.ASCII.GetBytes("MThd"));N(6,4);N(0,2);N(1,2);N(ppq,2);
        b.AddRange(System.Text.Encoding.ASCII.GetBytes("MTrk"));N(track.Count,4);b.AddRange(track);return b.ToArray();
    }
    public static void Run(){
        foreach(int d in new[]{3,5,7,9,11,13}){
            var p=Empty();p.Chart["quantize"]=4*d;p.Rebuild();
            foreach(float ppb in new[]{30f,56f,112f,1120f}){
                var e=new EditorInteraction(new HashSet<int>()){Project=p,PPB=ppb,RowHeight=46};
                for(int bar=0;bar<2;bar++)for(int i=1;i<d;i++){
                    double b=p.Starts[bar]+i/(double)d;var point=new Vector2((float)(b*ppb),4.5f*46);
                    e.Down(point,0,1,false,false);e.Up(point);
                    Check(p.Notes.Any(n=>n.Instrument=="HT"&&Math.Abs(n.Beat-b)<1e-8),$"{d}-tuplet line click at bar {bar}, step {i}, zoom {ppb}");
                    p.Chart["events"]=new JArray();p.Rebuild();e.Reset();
                }
            }
            p.Chart["quantize"]=16;p.Events.Add(new JObject{{"measure",1},{"beat",1.0/d},{"instrument","HH"},{"velocity",4},{"gridStepBeats",1.0/d}});p.Rebuild();
            var edit=new EditorInteraction(new HashSet<int>()){Project=p,PPB=200,RowHeight=46};double original=p.Notes.Single().Beat;
            var at=edit.NoteRect(p.Notes.Single()).center;edit.Down(at,0,1,false,false);edit.Move(at+new Vector2(0,92));edit.Up(at+new Vector2(0,92));
            Check(p.Notes.Single().Beat==original&&p.Notes.Single().Instrument=="HT",$"{d}-tuplet vertical drag preserves exact onset under straight grid");
            at=edit.NoteRect(p.Notes.Single()).center;edit.Down(at,0,1,false,false);edit.Up(at+new Vector2(50,0));
            Check(Math.Abs(p.Notes.Single().Beat-original-.25)<1e-8,$"{d}-tuplet drag uses grid delta without losing phase");
            var points=new SortedSet<double>();ChartVisuals.EditGridPoints(p,1,points);
            Check(points.Contains(p.Notes.Single().Local)&&points.Contains(.25),$"{d}-tuplet guide coexists with active straight grid");
            edit.Copy();edit.Paste(8);Check(p.Notes.Count==2&&p.Notes.All(n=>Math.Abs(n.Step-1.0/d)<1e-8),$"{d}-tuplet paste preserves step metadata");
            foreach(int ppq in new[]{480,960}){
                var imported=MidiImport.Read(Midi(ppq,d),Empty(),"tuplets.mid",MidiImport.ZeroMode.NoteOff);
                p=new ChartProject{Chart=imported.Chart,Manifest=Empty().Manifest};p.Rebuild();var notes=p.Notes.Where(n=>!n.Pedal).ToArray();
                Check(notes.Length==2*d&&p.Measures[0]==3.5,$"MIDI {ppq} / {d}-tuplet note count and 7/8 boundary");
                for(int i=0;i<notes.Length;i++){
                    double expected=Math.Round(((i>=d?3.5:0)+(i%d)/(double)d)*ppq)/ppq;
                    Check(notes[i].Beat==expected,$"MIDI {ppq} / {d}-tuplet tick {i} stays exact");
                    Check(Math.Abs(notes[i].Step-Math.Min(.25,1.0/d))<1e-8||d==3&&Math.Abs(notes[i].Step-1.0/d)<1e-8,$"MIDI {ppq} / {d}-tuplet display step {i}");
                }
                var before=p.Chart.ToString();var copy=ChartProject.Read(p.Write(),"tuplets.crproj");
                Check(JToken.DeepEquals(p.Chart,copy.Chart)&&copy.Notes.Select(n=>n.Beat).SequenceEqual(p.Notes.Select(n=>n.Beat)),$"MIDI {ppq} / {d}-tuplet exact save/reload");
                ChartVisuals.GridPoints(p,1);ChartVisuals.EditGridPoints(p,1,points);Check(before==p.Chart.ToString(),"drawing never changes MIDI timing");
            }
        }
        var loose=MidiImport.Read(Midi(480,7,true),Empty(),"loose.mid",MidiImport.ZeroMode.NoteOff);
        var free=new ChartProject{Chart=loose.Chart,Manifest=Empty().Manifest};free.Rebuild();
        Check(free.Notes.Any(n=>!n.Pedal&&n.Beat==(Math.Round((3.5+1.0/7)*480)+3)/480),"intentional off-grid timing is not quantized");
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-tupletFixture");if(index>=0)File.WriteAllBytes(args[index+1],Midi(480,7));
        Debug.Log("CROSS_RHYTHM_TUPLET_TESTS_PASS");
    }
}
}
