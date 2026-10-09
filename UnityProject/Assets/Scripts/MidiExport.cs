using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
namespace CrossRhythm {
// SMF type 1: conductor plus GM percussion on channel 10. No source audio.
public static class MidiExport {
    public const int PPQ=32760;
    public sealed class Result {public byte[] Bytes;public int Hits,Muted,Approximate;}
    sealed class Event {public long Tick;public int Priority;public byte[] Data;}
    public static long Tick(double beat){if(!double.IsFinite(beat)||beat<0||beat>10000000)throw new InvalidDataException("Invalid MIDI position");return checked((long)Math.Round(beat*PPQ,MidpointRounding.AwayFromZero));}
    public static int Velocity(int strength)=>new[]{0,25,50,75,100,127}[Math.Max(0,Math.Min(5,strength))];
    public static int Pitch(ChartProject p,ChartNote n){switch(n.Instrument){
        case "BD":return 36;
        case "HHSTATE":return 44;
        case "HH":return n.Articulation=="tambourine"?54:p.ClosedAt(n.Beat)?42:46;
        case "CR":return n.Articulation=="splash"?55:n.Articulation=="china"?52:49;
        case "RD":return n.Articulation=="cup"?53:n.Articulation=="crash"?49:51;
        case "SN":return n.Articulation=="rim_closed"?37:38;
        case "HT":return n.Articulation=="high"?50:48;
        case "MT":return n.Articulation=="high"?47:45;
        case "FT":return n.Articulation=="high"?43:41;
        default:throw new InvalidDataException("No GM mapping for "+n.Instrument);
    }}
    static void Number(Stream s,long n,int size){for(int i=size-1;i>=0;i--)s.WriteByte((byte)(n>>(i*8)));}
    static void Variable(Stream s,long n){if(n<0||n>0x0fffffff)throw new InvalidDataException("MIDI event gap is too large");int shift=21;while(shift>0&&(n>>shift)==0)shift-=7;for(;shift>=0;shift-=7)s.WriteByte((byte)(((n>>shift)&127)|(shift>0?128:0)));}
    static byte[] Meta(int kind,byte[] body){using(var s=new MemoryStream()){s.WriteByte(255);s.WriteByte((byte)kind);Variable(s,body.Length);s.Write(body,0,body.Length);return s.ToArray();}}
    static Event E(double beat,int priority,params byte[] data)=>new Event{Tick=Tick(beat),Priority=priority,Data=data};
    static void Track(Stream target,List<Event> events,long end){
        using(var s=new MemoryStream()){
            long previous=0;
            foreach(var e in events.OrderBy(e=>e.Tick).ThenBy(e=>e.Priority)){
                // Long empty regions are legal: split oversized deltas with empty text events.
                while(e.Tick-previous>0x0fffffff){Variable(s,0x0fffffff);s.Write(new byte[]{255,1,0},0,3);previous+=0x0fffffff;}
                Variable(s,e.Tick-previous);s.Write(e.Data,0,e.Data.Length);previous=e.Tick;
            }
            long tail=Math.Max(end,previous)-previous;
            while(tail>0x0fffffff){Variable(s,0x0fffffff);s.Write(new byte[]{255,1,0},0,3);tail-=0x0fffffff;}
            Variable(s,tail);s.Write(new byte[]{255,47,0},0,3);
            target.Write(Encoding.ASCII.GetBytes("MTrk"),0,4);Number(target,s.Length,4);s.Position=0;s.CopyTo(target);
        }
    }
    public static Result Write(ChartProject p){
        var result=new Result();var conductor=new List<Event>{E(0,0,Meta(3,Encoding.UTF8.GetBytes(p.Title))),E(0,0,Meta(1,Encoding.UTF8.GetBytes("Cross Rhythm / "+p.Artist)))};
        foreach(var t in p.Tempo.Points){int us=(int)Math.Round(60000000/t.BPM);conductor.Add(E(t.Beat,1,Meta(81,new[]{(byte)(us>>16),(byte)(us>>8),(byte)us})));}
        Tuple<int,int> prior=null;
        for(int bar=0;bar<p.Measures.Length;bar++){
            var m=p.Meter(bar);if(m.Equals(prior))continue;prior=m;
            int power=0;while((1<<power)<m.Item2)power++;
            if(m.Item1<1||m.Item1>255||power>6||(1<<power)!=m.Item2)throw new InvalidDataException("Unsupported MIDI time signature");
            conductor.Add(E(p.Starts[bar],2,Meta(88,new[]{(byte)m.Item1,(byte)power,(byte)24,(byte)8})));
        }
        var drums=new List<Event>{E(0,0,Meta(3,Encoding.ASCII.GetBytes("GM Drums")))};
        var sounding=p.Notes.Where(n=>n.Velocity>0).ToArray();result.Muted=p.Notes.Count-sounding.Length;
        foreach(var group in sounding.GroupBy(n=>Pitch(p,n))){
            var notes=group.OrderBy(n=>n.Beat).ToArray();
            var nextTicks=new long[notes.Length];long following=long.MaxValue;
            for(int j=notes.Length-1;j>=0;j--){if(j+1<notes.Length&&Tick(notes[j+1].Beat)>Tick(notes[j].Beat))following=Tick(notes[j+1].Beat);nextTicks[j]=following;}
            for(int i=0;i<notes.Length;i++){
                var n=notes[i];long start=Tick(n.Beat),stop=Tick(Math.Min(p.Length,n.Beat+(n.Pedal||n.Articulation=="buzz"?n.Duration:.125)));
                long next=nextTicks[i];
                stop=Math.Max(start+1,Math.Min(stop,next));
                drums.Add(new Event{Tick=start,Priority=2,Data=new[]{(byte)0x99,(byte)group.Key,(byte)Velocity(n.Velocity)}});
                drums.Add(new Event{Tick=stop,Priority=1,Data=new[]{(byte)0x89,(byte)group.Key,(byte)0}});
                result.Hits++;
                if(n.Articulation=="buzz"||n.Articulation=="rim_open"||n.Articulation=="rimshot"||(n.Instrument=="RD"&&n.Articulation=="crash"))result.Approximate++;
            }
        }
        using(var s=new MemoryStream()){
            s.Write(Encoding.ASCII.GetBytes("MThd"),0,4);Number(s,6,4);Number(s,1,2);Number(s,2,2);Number(s,PPQ,2);
            Track(s,conductor,Tick(p.Length));Track(s,drums,Tick(p.Length));result.Bytes=s.ToArray();return result;
        }
    }
    public static string FileName(string title){var invalid=Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();var clean=new string((title??"").Where(c=>c>=32&&!invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');if(clean.Length>100)clean=clean.Substring(0,100);return (string.IsNullOrEmpty(clean)?"Cross Rhythm":clean)+".mid";}
    public static void SaveNative(string path,byte[] bytes){
        if(!string.Equals(Path.GetExtension(path),".mid",StringComparison.OrdinalIgnoreCase)&&!string.Equals(Path.GetExtension(path),".midi",StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a .mid file");
        string temp=path+".crtmp-"+Guid.NewGuid().ToString("N");
        try{File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
        if(!File.ReadAllBytes(path).SequenceEqual(bytes))throw new IOException("MIDI write verification failed");
    }
}
}
