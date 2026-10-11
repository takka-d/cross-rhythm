using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public static class PedalImportChecks {
    public sealed class Message { public int Tick,Status,Pitch,Value; }
    public static Message M(int tick,int status,int pitch,int value=0)=>new Message{Tick=tick,Status=status,Pitch=pitch,Value=value};
    static void Number(Stream s,int value,int count){for(int i=count-1;i>=0;i--)s.WriteByte((byte)(value>>(i*8)));}
    static void Delta(Stream s,int value){var b=new List<byte>{(byte)(value&127)};while((value>>=7)>0)b.Insert(0,(byte)((value&127)|128));s.Write(b.ToArray(),0,b.Count);}
    public static byte[] File(params Message[][] tracks){using(var f=new MemoryStream()){
        f.Write(new byte[]{77,84,104,100,0,0,0,6,0,(byte)(tracks.Length==1?0:1)},0,10);Number(f,tracks.Length,2);Number(f,960,2);
        foreach(var events in tracks)using(var t=new MemoryStream()){
            int last=0;foreach(var e in events.OrderBy(e=>e.Tick)){Delta(t,e.Tick-last);last=e.Tick;t.WriteByte((byte)e.Status);t.WriteByte((byte)e.Pitch);t.WriteByte((byte)e.Value);}
            t.Write(new byte[]{0,255,47,0},0,4);f.Write(new byte[]{77,84,114,107},0,4);Number(f,(int)t.Length,4);t.Position=0;t.CopyTo(f);
        }return f.ToArray();
    }}
    public static ChartProject Read(byte[] bytes,MidiImport.ZeroMode mode=MidiImport.ZeroMode.Auto){var source=ChartProject.Demo();var r=MidiImport.Read(bytes,source,"pedal.mid",mode);var p=new ChartProject{Chart=r.Chart,Manifest=(JObject)source.Manifest.DeepClone()};p.Rebuild();return p;}
    public static byte[] Fixture()=>File(new[]{M(0,0x99,44,100),M(120,0x89,44),M(960,0x99,44,75),M(1080,0x99,44),M(1920,0x99,46,100),M(2040,0x89,46),M(2880,0x99,42,100),M(3000,0x89,42),M(3840,0x99,46,100),M(3960,0x89,46)});
    public static void Run(Action<bool,string> check){
        var p=Read(Fixture());var pedals=p.Notes.Where(n=>n.Pedal).ToArray();
        check(pedals.Count(n=>n.Beat<2)==2&&pedals.Take(2).All(n=>Math.Abs(n.Duration-.125)<1e-10),"MIDI 44 uses Note Off and zero-velocity release, without a ghost strike");
        check(p.ClosedAt(.05)&&!p.ClosedAt(.5)&&p.ClosedAt(1.05)&&!p.ClosedAt(1.5),"isolated pedal strikes release during the empty gaps");
        check(!p.ClosedAt(2)&&p.ClosedAt(3.9)&&!p.ClosedAt(4),"closed/open stick hats keep their inferred state boundaries");
        check(p.PlayableNoteCount==3&&p.Notes.Count(n=>!n.Pedal)==3,"pedal strikes remain excluded from scoring and difficulty");
        foreach(var mode in new[]{MidiImport.ZeroMode.Auto,MidiImport.ZeroMode.Mute,MidiImport.ZeroMode.NoteOff}){
            p=Read(File(new[]{M(0,0x99,44,100),M(120,0x99,44),M(200,0x89,38)}),mode);
            check(p.Notes.Count==1&&Math.Abs(p.Notes[0].Duration-.125)<1e-10,"pedal zero-velocity release is not a held mute note in "+mode);
        }
        p=Read(File(new[]{M(0,0x99,44,100),M(7680,0x99,46,100)}));
        check(p.Notes.First().Duration==.25&&!p.ClosedAt(1),"missing release is one short cell, not a hold until the next hat");
        p=Read(File(new[]{M(0,0x99,44,100),M(0,0x89,44)}));check(p.Notes.Single().Duration==.25,"zero-length release receives a visible short pulse");
        p=Read(File(new[]{M(2880,0x99,44,100),M(5280,0x89,44)}));
        check(p.Notes.Single().Duration==2.5&&p.Length>=5.5&&p.ClosedAt(5.49)&&!p.ClosedAt(5.5),"explicit long pedal duration crosses a bar and extends imported coverage");
        p=Read(File(new[]{M(0,0x99,44,100),M(1920,0x99,46,100),M(7680,0x89,44)}));
        check(p.Notes.Single(n=>n.Pedal).Duration==2&&!p.ClosedAt(2),"explicit open hi-hat ends an earlier long MIDI pedal");
        p=Read(File(new[]{M(0,0x99,44,100),M(120,0x89,44),M(120,0x99,44,50),M(240,0x89,44)}));
        check(p.Notes.Count==2&&p.Notes.All(n=>n.Duration==.125)&&p.Notes[1].Velocity==2,"touching pedal strikes keep their separate onset and strength");
        p=Read(File(new[]{M(333,0x99,44,100),M(373,0x89,44)}));
        check(Math.Abs(p.Notes[0].Beat-333.0/960)<1e-12&&Math.Abs(p.Notes[0].Duration-40.0/960)<1e-12,"pedal onset and release retain exact MIDI ticks without quantizing");
        p=Read(File(new[]{M(0,0x99,44,100),M(60,0x88,44),M(120,0x89,44)}));
        check(p.Notes.Single().Duration==.125,"release from another channel cannot end the pedal");
        p=Read(File(new[]{M(0,0x99,44,100),M(240,0x89,44)},new[]{M(0,0x99,38,100),M(60,0x89,44)}));
        check(p.Notes.Single(n=>n.Pedal).Duration==.25,"release from another track cannot end the pedal");
        p=Read(File(new[]{M(0,0x99,44,100),M(120,0x99,44,50),M(240,0x89,44),M(360,0x89,44)}));
        check(p.Notes[0].Duration==.125&&p.Notes[1].Duration==.25,"overlapping same-pitch releases pair in onset order and stop at retrigger");
        p=Read(File(new[]{M(0,0x99,38,0),M(960,0x99,38,100)}),MidiImport.ZeroMode.Mute);
        check(p.Notes.Count==2&&p.Notes[0].Velocity==0,"legacy explicit zero-strength option for other drums remains available");
        var original=ChartProject.Demo();original.Chart["events"]=new JArray(new JObject{{"instrument","HHSTATE"},{"measure",0},{"beat",1},{"durationBeats",3.5},{"velocity",4}});original.Rebuild();
        string before=original.Chart.ToString();p=ChartProject.Read(original.Write(),"manual.crproj");
        check(p.Notes.Single().Duration==3.5&&p.ClosedAt(4.49),"existing manually authored long pedal hold is unchanged on project load");
        p=Read(MidiExport.Write(original).Bytes,MidiImport.ZeroMode.NoteOff);
        check(p.Notes.Single().Beat==1&&p.Notes.Single().Duration==3.5,"exported explicit pedal duration survives MIDI reimport");
        MidiImport.Read(Fixture(),original,"pedal.mid",MidiImport.ZeroMode.Auto);
        check(original.Chart.ToString()==before,"MIDI preview never mutates the source chart");
        p=Read(Fixture());var saved=ChartProject.Read(p.Write(),"pedal.crproj");
        check(JToken.DeepEquals(p.Chart,saved.Chart)&&saved.Notes[0].Duration==.125,"imported pedal duration survives project save and load");
    }
}
}
