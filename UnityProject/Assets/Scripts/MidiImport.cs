using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public static class MidiImport {
    public enum ZeroMode { Auto, NoteOff, Mute }
    sealed class Reader {
        public byte[] Bytes;public int At,End;
        public Reader(byte[] bytes){Bytes=bytes;End=bytes.Length;}
        public int Byte(){if(At>=End)throw new InvalidDataException("Truncated MIDI");return Bytes[At++];}
        public int Number(int length){int n=0;for(int i=0;i<length;i++)n=checked((n<<8)|Byte());if(n<0)throw new InvalidDataException("Invalid MIDI size");return n;}
        public int Variable(){int n=0;for(int i=0;i<4;i++){int b=Byte();n=(n<<7)|(b&127);if(b<128)return n;}throw new InvalidDataException("Invalid MIDI delta");}
        public void Skip(int n){if(n<0||n>End-At)throw new InvalidDataException("Truncated MIDI chunk");At+=n;}
        public void Chunk(string name){foreach(char c in name)if(Byte()!=c)throw new InvalidDataException("Missing "+name);}
    }
    sealed class Note {public int Track,Channel,Pitch,Velocity;public long Tick;public string Instrument,Type,Hat;public double Beat;}
    sealed class Meter {public long Tick;public int N,D;}
    public sealed class Result {public JObject Chart;public int Hits,ZeroKept,ZeroDropped,TempoChanges;}
    public static int Strength(int v)=>v<=12?0:v<=37?1:v<=62?2:v<=87?3:v<=113?4:5;
    static void Map(Note n){switch(n.Pitch){
        case 35:case 36:n.Instrument="BD";break;
        case 38:case 40:n.Instrument="SN";n.Type="center";break;
        case 37:n.Instrument="SN";n.Type="rim_closed";break;
        case 39:n.Instrument="SN";n.Type="rim_open";break;
        case 42:n.Instrument="HH";n.Hat="closed";break;
        case 44:n.Instrument="HHSTATE";n.Hat="pedal";break;
        case 46:n.Instrument="HH";n.Hat="open";break;
        case 49:case 57:n.Instrument="CR";n.Type="crash";break;
        case 55:n.Instrument="CR";n.Type="splash";break;
        case 52:n.Instrument="CR";n.Type="china";break;
        case 51:case 59:n.Instrument="RD";n.Type="ride";break;
        case 53:n.Instrument="RD";n.Type="cup";break;
        case 50:case 48:n.Instrument="HT";n.Type=n.Pitch==50?"high":"normal";break;
        case 47:case 45:n.Instrument="MT";n.Type="normal";break;
        case 43:case 41:n.Instrument="FT";n.Type="normal";break;
    }}
    public static Result Read(byte[] bytes,ChartProject source,string name,ZeroMode zeroMode){
        if(bytes.Length>32*1024*1024)throw new InvalidDataException("MIDI exceeds 32 MB");
        var r=new Reader(bytes);r.Chunk("MThd");int header=r.Number(4);if(header<6)throw new InvalidDataException("Invalid MIDI header");
        int format=r.Number(2),tracks=r.Number(2),ppq=r.Number(2);r.Skip(header-6);
        if(format>1||tracks<1||ppq==0||(ppq&0x8000)!=0)throw new InvalidDataException("Use MIDI format 0/1 with PPQ timing");
        var notes=new List<Note>();var off80=new HashSet<int>();var meters=new List<Meter>();var tempos=new List<Tuple<long,double>>();
        for(int track=0;track<tracks;track++){
            r.Chunk("MTrk");int length=r.Number(4);if(length>r.End-r.At)throw new InvalidDataException("Truncated MIDI track");int end=r.At+length,fileEnd=r.End;r.End=end;
            long tick=0;int running=0;
            while(r.At<end){
                tick+=r.Variable();int status=r.Byte();if(status<128){r.At--;status=running;if(status==0)throw new InvalidDataException("Missing MIDI running status");}else if(status<0xF0)running=status;
                if(status==0xFF){int type=r.Byte(),size=r.Variable(),at=r.At;r.Skip(size);
                    if(type==0x51&&size==3){int us=(bytes[at]<<16)|(bytes[at+1]<<8)|bytes[at+2];if(us==0)throw new InvalidDataException("Invalid MIDI tempo");tempos.Add(Tuple.Create(tick,60000000.0/us));}
                    else if(type==0x58&&size>=2){int n=bytes[at],pow=bytes[at+1];if(n<1||n>64||pow>6)throw new InvalidDataException("Unsupported MIDI meter");meters.Add(new Meter{Tick=tick,N=n,D=1<<pow});}
                    if(type==0x2F){r.At=end;break;}continue;
                }
                if(status==0xF0||status==0xF7){int size=r.Variable();r.Skip(size);running=0;continue;}
                if(status>=0xF0)throw new InvalidDataException("Unsupported MIDI status");
                int kind=status&0xF0,d1=r.Byte(),d2=kind==0xC0||kind==0xD0?0:r.Byte();if(d1>=128||d2>=128)throw new InvalidDataException("Invalid MIDI data");
                if(kind==0x90){var n=new Note{Track=track,Channel=status&15,Pitch=d1,Velocity=d2,Tick=tick,Beat=(double)tick/ppq};Map(n);notes.Add(n);}
                if(kind==0x80)off80.Add(track);
            }
            r.End=fileEnd;
        }
        var result=new Result();var resolved=new List<Note>();
        foreach(var n in notes){bool keep=n.Velocity>0||zeroMode==ZeroMode.Mute||(zeroMode==ZeroMode.Auto&&off80.Contains(n.Track));if(n.Velocity==0){if(keep)result.ZeroKept++;else result.ZeroDropped++;}if(keep&&n.Instrument!=null)resolved.Add(n);}
        if(resolved.Count==0)throw new InvalidDataException("No drum notes found in MIDI");
        int channel=resolved.Any(n=>n.Channel==9)?9:resolved.GroupBy(n=>n.Channel).OrderByDescending(g=>g.Count()).First().Key;
        var hits=resolved.Where(n=>n.Channel==channel).OrderBy(n=>n.Beat).ThenBy(n=>n.Pitch).ToArray();
        var orderedTempos=tempos.OrderBy(t=>t.Item1).ToArray();double bpm=orderedTempos.Length>0?orderedTempos[0].Item2:120;result.TempoChanges=orderedTempos.Count(t=>Math.Abs(t.Item2-bpm)>.001);
        if(bpm<20||bpm>600)throw new InvalidDataException("BPM must be 20–600");
        var time=meters.GroupBy(m=>m.Tick).Select(g=>g.Last()).OrderBy(m=>m.Tick).ToList();if(time.Count==0||time[0].Tick>0)time.Insert(0,new Meter{Tick=0,N=4,D=4});
        double total=Math.Max(4,Math.Ceiling((hits.Last().Beat+1e-8)/source.Grid)*source.Grid+source.Grid*2),cursor=0;int ti=0;
        var bars=new JArray();var signatures=new JArray();
        while(cursor<total-1e-8){
            while(ti+1<time.Count&&time[ti+1].Tick/(double)ppq<=cursor+1e-8)ti++;
            var m=time[ti];double len=m.N*4.0/m.D,next=ti+1<time.Count?time[ti+1].Tick/(double)ppq:double.PositiveInfinity;
            // A meter change inside a bar makes a short bar, preserving absolute MIDI times.
            len=Math.Min(len,next-cursor);if(len<=0||len>64||bars.Count>=100000)throw new InvalidDataException("Invalid MIDI measure map");
            bars.Add(len);signatures.Add(new JObject{{"numerator",m.N},{"denominator",m.D}});cursor+=len;
        }
        var chart=(JObject)source.Chart.DeepClone();chart["title"]=source.Title=="Untitled"?Path.GetFileNameWithoutExtension(name):source.Title;chart["bpm"]=bpm;chart["measures"]=bars;chart["timeSignatures"]=signatures;chart["events"]=new JArray();chart["velocityScaleVersion"]=4;
        var p=new ChartProject{Chart=chart,Manifest=source.Manifest};p.Rebuild();int id=0;
        Action<Note,double> add=(n,duration)=>{int bar=p.BarAt(n.Beat);var e=new JObject{{"id","midi-"+id++},{"measure",bar},{"beat",n.Beat-p.Starts[bar]},{"instrument",n.Instrument},{"velocity",Strength(n.Velocity)},{"source","midi"}};if(n.Type!=null)e["articulation"]=n.Type;if(duration>0)e["durationBeats"]=duration;e["gridStepBeats"]=ChartProject.EventStep(e);p.Events.Add(e);};
        foreach(var n in hits.Where(n=>n.Instrument!="HHSTATE"))add(n,0);
        var hats=hits.Where(n=>n.Hat!=null).ToArray();var ranges=new List<Tuple<Note,double>>();
        for(int i=0;i<hats.Length;i++){var n=hats[i];if(n.Hat=="open")continue;double end=i+1<hats.Length&&hats[i+1].Beat>n.Beat+1e-8?hats[i+1].Beat:n.Beat+source.Grid;
            var state=new Note{Beat=n.Beat,Instrument="HHSTATE",Velocity=n.Velocity};var last=ranges.LastOrDefault();
            if(last!=null&&n.Beat<=last.Item2+1e-8){last.Item1.Velocity=Math.Max(last.Item1.Velocity,n.Velocity);ranges[ranges.Count-1]=Tuple.Create(last.Item1,Math.Max(last.Item2,end));}else ranges.Add(Tuple.Create(state,end));
        }
        foreach(var range in ranges)add(range.Item1,range.Item2-range.Item1.Beat);
        p.Rebuild();result.Chart=chart;result.Hits=p.Notes.Count(n=>!n.Pedal);return result;
    }
}
}
