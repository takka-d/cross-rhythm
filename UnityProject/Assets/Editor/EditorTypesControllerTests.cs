using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
namespace CrossRhythm {
public static class EditorTypesControllerTests {
    static void Check(bool ok,string message){if(!ok)throw new Exception("INPUT TYPES / CONTROLLER: "+message);}
    public static void Run(){
        var p=ChartProject.Demo();p.Chart["events"]=new JArray();p.Rebuild();var e=new EditorInteraction(new HashSet<int>()){Project=p,PPB=100,RowHeight=46};
        foreach(int row in new[]{0,1,2,3,4,5,6})foreach(string type in EditorNoteTypes.Types[row]){
            e.Clear();p.Chart["events"]=new JArray();p.Rebuild();EditorNoteTypes.Set(p,row,type);var at=new Vector2(112,row*46+23);e.Down(at,0,1,false,false);e.Up(at);
            Check(p.Notes.Single().Articulation==type,"placement uses lane input type "+row+"/"+type);
        }
        EditorNoteTypes.Set(p,3,"rim_closed");var reread=ChartProject.Read(p.Write(),"types.crproj");Check(EditorNoteTypes.Get(reread,3)=="rim_closed","per-project input type saves and reloads");
        Check(!EditorNoteTypes.Set(p,3,"china")&&EditorNoteTypes.Get(p,3)=="rim_closed","cross-instrument type rejected");
        p.Chart["editorInputTypes"]["RD"]="future-unknown";Check(EditorNoteTypes.Get(p,1)=="ride","unknown type falls back to its lane default");
        Check(ControllerIdentity.ReportedModel(new InputDeviceDescription{interfaceName="XInput",product="Xbox Controller"})=="","generic XInput name is not presented as physical model");
        Check(ControllerIdentity.ReportedModel(new InputDeviceDescription{interfaceName="WebGL",product="Xbox Controller"})=="","browser generic controller model is also unknown");
        Check(ControllerIdentity.ReportedModel(new InputDeviceDescription{interfaceName="HID",product="Pro Controller"})=="Pro Controller","device-reported product is retained when available");
        Check(ControllerIdentity.Label(new InputDeviceDescription(),2,true)=="Controller 2 · Model unavailable","English model unavailable label contains no Japanese");
        Debug.Log("CROSS_RHYTHM_EDITOR_TYPES_CONTROLLER_TESTS_PASS");
    }
}
}
