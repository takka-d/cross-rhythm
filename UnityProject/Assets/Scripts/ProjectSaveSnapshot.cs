using System.Collections.Generic;
using Newtonsoft.Json;
namespace CrossRhythm {
// Captures managed data only; the native writer never touches live editor data.
public sealed class ProjectSaveSnapshot {
    readonly string chart, manifest;
    readonly Dictionary<string,byte[]> files;
    readonly string path,name,baseline;
    public ProjectSaveSnapshot(ChartProject p){chart=p.Chart.ToString(Formatting.None);manifest=p.Manifest.ToString(Formatting.None);files=new Dictionary<string,byte[]>(p.Files);path=p.FilePath;name=p.FileName;baseline=p.Baseline;}
    public ChartProject CopyForSave()=>new ChartProject{Chart=Newtonsoft.Json.Linq.JObject.Parse(chart),Manifest=Newtonsoft.Json.Linq.JObject.Parse(manifest),Files=new Dictionary<string,byte[]>(files),FilePath=path,FileName=name,Baseline=baseline};
    public bool Matches(ChartProject p){
        if(chart!=p.Chart.ToString(Formatting.None)||manifest!=p.Manifest.ToString(Formatting.None)||files.Count!=p.Files.Count)return false;
        foreach(var pair in files)if(!p.Files.TryGetValue(pair.Key,out var bytes)||!ReferenceEquals(pair.Value,bytes))return false;
        return true;
    }
}
}
