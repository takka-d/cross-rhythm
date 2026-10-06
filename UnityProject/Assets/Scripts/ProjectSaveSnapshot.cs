using System.Collections.Generic;
using Newtonsoft.Json;
namespace CrossRhythm {
// The in-flight save belongs to this exact content, even if editing continues.
public sealed class ProjectSaveSnapshot {
    readonly string chart, manifest;
    readonly Dictionary<string,byte[]> files;
    public ProjectSaveSnapshot(ChartProject p){chart=p.Chart.ToString(Formatting.None);manifest=p.Manifest.ToString(Formatting.None);files=new Dictionary<string,byte[]>(p.Files);}
    public bool Matches(ChartProject p){
        if(chart!=p.Chart.ToString(Formatting.None)||manifest!=p.Manifest.ToString(Formatting.None)||files.Count!=p.Files.Count)return false;
        foreach(var pair in files)if(!p.Files.TryGetValue(pair.Key,out var bytes)||!ReferenceEquals(pair.Value,bytes))return false;
        return true;
    }
}
}
