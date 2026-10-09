using System;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public static class EditorNoteTypes {
    public static readonly string[][] Types={new[]{"crash","splash","china"},new[]{"ride","cup","crash"},new[]{"auto","tambourine"},new[]{"center","rim_closed","rim_open","buzz"},new[]{"center","high","rimshot"},new[]{"center","high","rimshot"},new[]{"center","high","rimshot"},new[]{"normal"},new[]{"closed"}};
    public static string Get(ChartProject project,int row){string value=(string)(project.Chart["editorInputTypes"] as JObject)?[EditorInteraction.Lanes[row]];return Array.IndexOf(Types[row],value)>=0?value:Types[row][0];}
    public static bool Set(ChartProject project,int row,string type){
        if(row<0||row>=Types.Length||Array.IndexOf(Types[row],type)<0||Get(project,row)==type)return false;
        if(project.Chart["editorInputTypes"] is not JObject settings)project.Chart["editorInputTypes"]=settings=new JObject();
        settings[EditorInteraction.Lanes[row]]=type;return true;
    }
}
}
