using System;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public static class EditorNoteTypes {
    public static readonly string[][] Types={new[]{"crash","splash","china"},new[]{"ride","cup","crash"},new[]{"auto","tambourine"},new[]{"center","rim_closed","rim_open","buzz"},new[]{"center","high","rimshot"},new[]{"center","high","rimshot"},new[]{"center","high","rimshot"},new[]{"normal"},new[]{"closed"}};
    public static string Label(string instrument,string type){
        if(string.IsNullOrEmpty(type)||type=="center"||type=="normal"||type=="auto"){
            switch(instrument){case "HH":return "Hi-hat";case "SN":return "Snare";case "HT":return "High tom";case "MT":return "Mid tom";case "FT":return "Floor tom";case "BD":return "Kick";case "CR":return "Crash";case "RD":return "Ride";default:return "Pedal";}
        }
        switch(type){case "rim_closed":return "Closed rimshot";case "rim_open":return "Open rimshot";case "buzz":return "Buzz roll";case "crash":return instrument=="RD"?"Ride crash":"Crash";case "closed":return instrument=="HHSTATE"?"Pedal":"Closed hi-hat";default:return char.ToUpperInvariant(type[0])+type.Substring(1);}
    }
    public static string Get(ChartProject project,int row){string value=(string)(project.Chart["editorInputTypes"] as JObject)?[EditorInteraction.Lanes[row]];return Array.IndexOf(Types[row],value)>=0?value:Types[row][0];}
    public static bool Set(ChartProject project,int row,string type){
        if(row<0||row>=Types.Length||Array.IndexOf(Types[row],type)<0||Get(project,row)==type)return false;
        if(project.Chart["editorInputTypes"] is not JObject settings)project.Chart["editorInputTypes"]=settings=new JObject();
        settings[EditorInteraction.Lanes[row]]=type;return true;
    }
}
}
