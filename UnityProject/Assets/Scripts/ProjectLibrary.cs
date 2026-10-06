using System;
using System.Collections.Generic;

namespace CrossRhythm {
public static class ProjectLibrary {
    // A picker token is not a stable identity in browsers without file handles.
    public static int Upsert(List<ChartProject> library,ChartProject incoming){
        int index=library.FindIndex(p=>!string.IsNullOrEmpty(incoming.FilePath)&&string.Equals(p.FilePath,incoming.FilePath,StringComparison.OrdinalIgnoreCase));
        if(index<0&&!string.IsNullOrEmpty(incoming.Baseline))index=library.FindIndex(p=>p.Baseline==incoming.Baseline&&string.Equals(p.FileName,incoming.FileName,StringComparison.OrdinalIgnoreCase));
        if(index<0){library.Add(incoming);return library.Count-1;}
        var current=library[index];
        if(current.Dirty)return index;
        if(current.Baseline==incoming.Baseline){if(!string.IsNullOrEmpty(incoming.FilePath))current.FilePath=incoming.FilePath;return index;}
        library[index]=incoming;return index;
    }
}
}
