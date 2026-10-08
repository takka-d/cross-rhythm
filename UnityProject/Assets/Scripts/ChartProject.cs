using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrossRhythm {
public sealed class ChartNote {
    public JObject Source;
    public int Index, Measure, Velocity;
    public double Beat, Local, Duration, Step;
    public string Instrument, Articulation, Lane, Id;
    public bool Pedal => Instrument == "HHSTATE";
}
public sealed class PedalRange { public double Start, End; }
public sealed class ChartProject {
    public JObject Manifest, Chart;
    public Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
    public string FilePath = "", FileName = "Untitled.crproj", Baseline = "";
    public bool Dirty;
    public List<ChartNote> Notes = new List<ChartNote>();
    public List<PedalRange> Pedals = new List<PedalRange>();
    public double[] Starts, Measures;
    public double BPM => (double?)Chart["bpm"] ?? 120;
    public double Offset => (double?)Chart["audioOffsetSec"] ?? 0;
    public double Length => Starts.Last() + Measures.Last();
    public string SongTitle => (string)Chart["title"] ?? (string)Manifest["title"] ?? "";
    public string Title => string.IsNullOrWhiteSpace(SongTitle) ? Path.GetFileNameWithoutExtension(FileName) : SongTitle;
    public string Artist => (string)Chart["artist"] ?? (string)Manifest["artist"] ?? "";
    public int PlayableNoteCount {get;private set;}
    public double NotesPerSecond => Length>0 ? PlayableNoteCount*BPM/(60*Length) : 0;
    // Automatic chart difficulty, separate from the A-E performance result.
    public string Difficulty => NotesPerSecond>=11 ? "S" : NotesPerSecond>=8 ? "A" : NotesPerSecond>=3 ? "B" : "C";
    public void SetSongInfo(string title,string artist){Chart["title"]=Manifest["title"]=title??"";Chart["artist"]=Manifest["artist"]=artist??"";Dirty=true;}
    public string AudioPath => (string)Manifest["audio"]?["path"] ?? "";
    public JArray Events => (JArray)Chart["events"];
    public double Grid => 4.0 / Math.Max(1, Math.Min(1024, (double?)Chart["quantize"] ?? 16));
    public static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)); }
    public static int Strength(JToken v) { double n; return v == null || v.Type == JTokenType.Null || !double.TryParse(v.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out n) ? 4 : (int)Math.Max(0, Math.Min(5, Math.Floor(n + .5))); }
    public static readonly float[] Gains = { 0, .25f, .5f, .75f, 1, 1.27f };
    public static readonly float[] Heights = { .24f, .30f, .36f, .43f, .5f, .62f };
    public static ChartProject Read(byte[] bytes, string name, string filePath = "") {
        var p = new ChartProject {FileName=name,FilePath=filePath,Baseline=Hash(bytes)};
        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read)) {
            long sum = 0;
            foreach (var e in zip.Entries) {
                sum += e.Length; if (sum > 256 * 1024 * 1024) throw new Exception("Project exceeds 256 MB.");
                using (var s=e.Open()) using(var m=new MemoryStream()){s.CopyTo(m);p.Files.Add(e.FullName,m.ToArray());}
            }
        }
        if(!p.Files.ContainsKey("chart.json") || !p.Files.ContainsKey("manifest.json")) throw new Exception("manifest.json / chart.json missing");
        p.Manifest=JObject.Parse(System.Text.Encoding.UTF8.GetString(p.Files["manifest.json"]));
        p.Chart=JObject.Parse(System.Text.Encoding.UTF8.GetString(p.Files["chart.json"]));
        if((string)p.Manifest["format"]!="cross-rhythm-project" || (string)p.Chart["schema"]!="cross-rhythm-chart/v1") throw new Exception("Unsupported project format");
        p.Rebuild(); return p;
    }
    public void Rebuild() {
        if (!(Chart["measures"] is JArray bars) || bars.Count==0 || !(Chart["events"] is JArray)) throw new Exception("Invalid chart");
        if(BPM<20 || BPM>600 || double.IsNaN(BPM)) throw new Exception("BPM must be 20–600");
        Measures=bars.Select(x=>(double)x).ToArray(); Starts=new double[Measures.Length];
        for(int i=0;i<Measures.Length;i++){if(Measures[i]<=0 || Measures[i]>64)throw new Exception("Invalid measure length");if(i>0)Starts[i]=Starts[i-1]+Measures[i-1];}
        Notes.Clear(); Pedals.Clear();
        int ix=0;
        foreach(JObject e in Events){
            int m=(int?)e["measure"]??0;double b=(double?)e["beat"]??0;
            if(m<0||m>=Measures.Length||b<0||b>=Measures[m]){ix++;continue;}
            string inst=(string)e["instrument"]??"SN";
            if(inst=="PEDAL"){ix++;continue;} if(inst=="LBD")inst="BD"; if(inst=="OHH")inst="HH";
            var n=new ChartNote{Source=e,Index=ix++,Measure=m,Local=b,Beat=Starts[m]+b,Instrument=inst,Articulation=(string)e["articulation"]??"",Velocity=Strength(e["velocity"]),Id=(string)e["id"]??("n"+ix),Duration=(double?)e["durationBeats"]??Grid,Step=EventStep(e)};
            if(n.Duration<=0)n.Duration=Grid;
            Notes.Add(n);
            if(n.Pedal)Pedals.Add(new PedalRange{Start=n.Beat,End=Math.Min(Length,n.Beat+n.Duration)});
        }
        Pedals=Pedals.OrderBy(x=>x.Start).ToList();
        for(int i=1;i<Pedals.Count;){if(Pedals[i].Start<=Pedals[i-1].End+1e-7){Pedals[i-1].End=Math.Max(Pedals[i-1].End,Pedals[i].End);Pedals.RemoveAt(i);}else i++;}
        foreach(var group in Notes.Where(n=>!n.Pedal && n.Instrument!="BD").GroupBy(n=>Math.Round(n.Beat,6))){
            var ns=group.ToList(); var used=new HashSet<string>();
            foreach(var n in ns){string h=((string)n.Source["hand"]??(string)n.Source["sticking"]??"").ToUpperInvariant();n.Lane=(h=="R"||h=="RIGHT"||h=="H1")?"H1":(h=="L"||h=="LEFT"||h=="H2")?"H2":null;if(n.Lane!=null&&!used.Add(n.Lane))n.Lane=null;}
            if(ns.Count>1){var cym=ns.FirstOrDefault(n=>n.Lane==null&&(n.Instrument=="CR"||n.Instrument=="RD"||n.Instrument=="HH"));if(cym!=null&&used.Add("H1"))cym.Lane="H1";var sn=ns.FirstOrDefault(n=>n.Lane==null&&n.Instrument=="SN");if(sn!=null&&used.Add("H2"))sn.Lane="H2";}
            foreach(var n in ns.Where(n=>n.Lane==null))n.Lane=ns.Count==1?"HANY":used.Add("H1")?"H1":used.Add("H2")?"H2":"HANY";
        }
        foreach(var n in Notes.Where(n=>n.Instrument=="BD"))n.Lane=ClosedAt(n.Beat)?"F1":"FANY";
        foreach(var n in Notes.Where(n=>n.Pedal))n.Lane="F2";
        Notes=Notes.OrderBy(n=>n.Beat).ThenBy(n=>n.Index).ToList();
        PlayableNoteCount=Notes.Count(n=>!n.Pedal);
    }
    public static double EventStep(JObject e,double tolerance=.00051){double explicitStep=(double?)e["gridStepBeats"]??0;if(explicitStep>=1.0/256&&explicitStep<=4)return explicitStep;double b=(double?)e["beat"]??0;if(Math.Abs(b-Math.Round(b*4)/4)<=tolerance)return .25;foreach(int n in new[]{8,3,6,12,5,10,7,14,9,11,13,15,16,18,20,21,22,24,26,28,30,32,64})if(Math.Abs(b-Math.Round(b*n)/n)<=tolerance)return 1.0/n;return .25;}
    public bool ClosedAt(double beat)=>Pedals.Any(p=>beat>=p.Start-1e-8&&beat<p.End-1e-8);
    public int BarAt(double beat){if(beat<0)return CountIn.BarAt(this,beat);int i=Array.BinarySearch(Starts,beat);return i>=0?i:Math.Max(0,Math.Min(Starts.Length-1,~i-1));}
    public double SnapBeat(double beat){
        int m=Math.Max(0,BarAt(Math.Max(0,Math.Min(Length-1e-9,beat))));
        double local=Math.Round((beat-Starts[m])/Grid,MidpointRounding.AwayFromZero)*Grid;
        return Starts[m]+Math.Max(0,Math.Min((Math.Ceiling(Measures[m]/Grid-1e-9)-1)*Grid,local));
    }
    public Tuple<int,int> Meter(int m){
        var a=Chart["timeSignatures"] as JArray;var s=a!=null&&m<a.Count?a[m] as JObject:null;
        int n=(int?)s?["numerator"]??0,d=(int?)s?["denominator"]??0;
        if(n>0&&d>0&&Math.Abs(n*4.0/d-Measures[m])<1e-8)return Tuple.Create(n,d);
        foreach(int den in new[]{4,8,16,32,64}){double num=Measures[m]*den/4;if(Math.Abs(num-Math.Round(num))<1e-8)return Tuple.Create((int)Math.Round(num),den);}
        return Tuple.Create((int)Math.Round(Measures[m]*64/4),64);
    }
    public IEnumerable<double> Pulses(double from,double to){
        for(int bar=Math.Max(-2,BarAt(from));bar<=Math.Min(Measures.Length-1,BarAt(to));bar++){
            double start=bar<0?bar*CountIn.Length(this):Starts[bar],length=bar<0?CountIn.Length(this):Measures[bar],unit=bar<0?CountIn.Unit(this):4.0/Meter(bar).Item2;
            int first=Math.Max(0,(int)Math.Ceiling((from-start-1e-8)/unit));
            for(int i=first;i*unit<length-1e-8;i++){double b=start+i*unit;if(b>to+1e-8)break;yield return b;}
        }
    }
    public bool IsBarStart(double beat){int m=BarAt(beat);return Math.Abs(beat-(m<0?m*CountIn.Length(this):Starts[m]))<1e-7;}
    JArray MeterArray(){return new JArray(Enumerable.Range(0,Measures.Length).Select(m=>{var s=Meter(m);return new JObject{{"numerator",s.Item1},{"denominator",s.Item2}};}));}
    public bool CanSetMeter(int m,int numerator,int denominator){
        if(m<0||m>=Measures.Length||numerator<1||numerator>64||!new[]{1,2,4,8,16,32,64}.Contains(denominator))return false;
        double length=numerator*4.0/denominator;
        return length<=64&&!Events.OfType<JObject>().Any(e=>(int?)e["measure"]==m&&((double?)e["beat"]??0)>=length);
    }
    public bool SetMeter(int m,int numerator,int denominator){
        if(!CanSetMeter(m,numerator,denominator))return false;
        var meters=MeterArray();meters[m]=new JObject{{"numerator",numerator},{"denominator",denominator}};
        Chart["timeSignatures"]=meters;((JArray)Chart["measures"])[m]=numerator*4.0/denominator;Dirty=true;Rebuild();return true;
    }
    public void InsertMeasure(int after){
        int at=Math.Max(0,Math.Min(Measures.Length,after+1));var meters=MeterArray();
        meters.Insert(at,new JObject{{"numerator",4},{"denominator",4}});Chart["timeSignatures"]=meters;
        ((JArray)Chart["measures"]).Insert(at,4);foreach(JObject e in Events)if(((int?)e["measure"]??0)>=at)e["measure"]=(int)e["measure"]+1;
        Dirty=true;Rebuild();
    }
    public bool CanRemoveMeasure(int m)=>Measures.Length>1&&m>=0&&m<Measures.Length&&!Events.OfType<JObject>().Any(e=>(int?)e["measure"]==m);
    public bool RemoveMeasure(int m){
        if(!CanRemoveMeasure(m))return false;var meters=MeterArray();meters.RemoveAt(m);Chart["timeSignatures"]=meters;
        ((JArray)Chart["measures"]).RemoveAt(m);foreach(JObject e in Events)if(((int?)e["measure"]??0)>m)e["measure"]=(int)e["measure"]-1;
        Dirty=true;Rebuild();return true;
    }
    public byte[] Write() {
        using(var buffer=new MemoryStream()){
            using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true)){
                var values=new Dictionary<string,byte[]>(Files);
                values["chart.json"]=System.Text.Encoding.UTF8.GetBytes(Chart.ToString(Formatting.None));
                values["manifest.json"]=System.Text.Encoding.UTF8.GetBytes(Manifest.ToString(Formatting.None));
                foreach(var pair in values){var entry=zip.CreateEntry(pair.Key,CompressionLevel.NoCompression);entry.LastWriteTime=new DateTimeOffset(2000,1,1,0,0,0,TimeSpan.Zero);using(var s=entry.Open())s.Write(pair.Value,0,pair.Value.Length);}
            }return buffer.ToArray();
        }
    }
    public void SaveNative(string path,bool overwrite){
        if(File.Exists(path) && string.Equals(FilePath,path,StringComparison.OrdinalIgnoreCase) && Hash(File.ReadAllBytes(path))!=Baseline)throw new Exception("File changed outside Cross Rhythm. Use Save As with a different file.");
        byte[] bytes=Write();string tmp=path+".crtmp-"+Guid.NewGuid().ToString("N");
        try{File.WriteAllBytes(tmp,bytes);if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}finally{if(File.Exists(tmp))File.Delete(tmp);}
        var verified=Hash(bytes);if(Hash(File.ReadAllBytes(path))!=verified)throw new IOException("Saved file verification failed.");Baseline=verified;FilePath=path;FileName=Path.GetFileName(path);Dirty=false;
    }
    public static ChartProject Demo(){
        var p=new ChartProject();p.Manifest=JObject.Parse("{\"format\":\"cross-rhythm-project\",\"version\":1,\"title\":\"Rhythm Check\",\"chart\":\"chart.json\"}");
        p.Chart=new JObject{{"schema","cross-rhythm-chart/v1"},{"title","Rhythm Check"},{"bpm",120},{"audioOffsetSec",0},{"quantize",16},{"velocityScaleVersion",4},{"measures",new JArray(Enumerable.Repeat(4,16))},{"events",new JArray()}};
        int id=0;for(int m=0;m<16;m++){int div=m<4?2:m<8?3:m<12?5:7;for(int j=0;j<div*4;j++)p.Events.Add(new JObject{{"id","demo"+id++},{"measure",m},{"beat",(double)j/div},{"instrument","HH"},{"velocity",j%div==0?4:2},{"gridStepBeats",1.0/div}});foreach(int b in new[]{0,2})p.Events.Add(new JObject{{"id","demo"+id++},{"measure",m},{"beat",b},{"instrument","BD"},{"velocity",4}});foreach(int b in new[]{1,3})p.Events.Add(new JObject{{"id","demo"+id++},{"measure",m},{"beat",b},{"instrument","SN"},{"velocity",4}});}
        p.FileName="Rhythm Check.crproj";p.Rebuild();return p;
    }
}
}
