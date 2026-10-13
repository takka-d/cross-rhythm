using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;
namespace CrossRhythm {
// Logical keys retain the established v175 lane/foot arbitration. Physical inputs are independent.
public sealed class ControlBindings {
    static readonly string[] ReservedPads={"start","select","leftStickPress","rightStickPress"};
    static readonly string[] AssignablePads={"buttonSouth","buttonEast","buttonWest","buttonNorth","leftShoulder","rightShoulder","leftTrigger","rightTrigger","dpad/up","dpad/down","dpad/left","dpad/right"};
    public static readonly Key[] Normal={Key.Digit4,Key.Digit9,Key.V,Key.M};
    public static readonly Key[] Pro={Key.R,Key.O,Key.E,Key.P,Key.Digit4,Key.Digit9,Key.Digit3,Key.Digit0,Key.V,Key.M,Key.C,Key.Comma};
    public Key[] Keys;
    public string[] Pads;
    public static ControlBindings Defaults(bool pro,bool nintendo=true){
        return new ControlBindings{Keys=(pro?Pro:Normal).ToArray(),Pads=pro?new[]{
            "dpad/left",nintendo?"buttonEast":"buttonSouth","dpad/down",nintendo?"buttonSouth":"buttonEast",
            "dpad/up",nintendo?"buttonNorth":"buttonWest","dpad/right",nintendo?"buttonWest":"buttonNorth",
            "leftShoulder","rightShoulder","leftTrigger","rightTrigger"
        }:new[]{"dpad/left|dpad/down|dpad/up|dpad/right","buttonSouth|buttonEast|buttonWest|buttonNorth","leftShoulder|leftTrigger","rightShoulder|rightTrigger"}};
    }
    public int KeyRow(Key key)=>Array.IndexOf(Keys,key);
    public int PadRow(string path)=>Array.FindIndex(Pads,p=>p.Split('|').Contains(path));
    public void BindKey(int row,Key key){int conflict=KeyRow(key);if(conflict>=0&&conflict!=row)Keys[conflict]=Keys[row];Keys[row]=key;}
    public void BindPad(int row,string path){int conflict=PadRow(path);if(conflict>=0&&conflict!=row)Pads[conflict]=Pads[row];Pads[row]=path;}
    public bool MigrateReservedPads(bool pro,bool nintendo){
        bool changed=false;var cleaned=new string[Pads.Length];var used=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<Pads.Length;i++){
            var keep=Pads[i].Split('|').Where(p=>!ReservedPads.Contains(p)).ToArray();
            if(keep.Length!=Pads[i].Split('|').Length)changed=true;
            cleaned[i]=string.Join("|",keep);foreach(var path in keep)used.Add(path);
        }
        if(!changed)return false;
        var defaults=Defaults(pro,nintendo).Pads;
        for(int i=0;i<cleaned.Length;i++)if(string.IsNullOrEmpty(cleaned[i])){
            var replacement=defaults[i].Split('|').Where(p=>!used.Contains(p)).ToArray();
            if(replacement.Length==0){var path=AssignablePads.FirstOrDefault(p=>!used.Contains(p));replacement=string.IsNullOrEmpty(path)?Array.Empty<string>():new[]{path};}
            if(replacement.Length==0){Pads=Defaults(pro,nintendo).Pads;return true;}
            cleaned[i]=string.Join("|",replacement);foreach(var path in replacement)used.Add(path);
        }
        Pads=cleaned;return true;
    }
    public string Save()=>new JObject{{"keys",new JArray(Keys.Select(k=>k.ToString()))},{"pads",new JArray(Pads)}}.ToString(Newtonsoft.Json.Formatting.None);
    public static ControlBindings Load(string json,bool pro,bool nintendo){var result=Defaults(pro,nintendo);try{var o=JObject.Parse(json);var k=(JArray)o["keys"];var p=(JArray)o["pads"];if(k.Count!=result.Keys.Length||p.Count!=k.Count)return result;var keys=k.Select(t=>Enum.Parse<Key>((string)t)).ToArray();if(keys.Any(x=>x==Key.None||x==Key.Escape)||keys.Distinct().Count()!=keys.Length)return result;var pads=p.Select(t=>(string)t).ToArray();if(pads.Any(string.IsNullOrEmpty)||pads.SelectMany(s=>s.Split('|')).Distinct().Count()!=pads.Sum(s=>s.Split('|').Length))return result;result.Keys=keys;result.Pads=pads;}catch{}return result;}
    public static string KeyName(Key key)=>key.ToString().Replace("Digit","").Replace("Comma",",").Replace("Semicolon",";").Replace("Left","L ").Replace("Right","R ");
}
}
