using System;
using System.IO;
using System.Linq;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    IEnumerator InputTypesRuntimeCheck(Action<bool,string> check){
        var p=EmptyEditorProject();OpenEditorProject(p);while(busy||!loaded)yield return null;
        int history=undo.Count;var before=(JArray)p.Events.DeepClone();SetLaneInputType(3,"rim_closed");SetLaneInputType(1,"cup");
        check(undo.Count==history+2&&JToken.DeepEquals(before,p.Events),"lane input types are undoable and do not change existing notes");
        var point=new Vector2(EditorPPB,EditorRowHeight*3.5f);Editor.Down(point,0,1,false,false);Editor.Up(point);
        check(p.Notes.Single(n=>n.Instrument=="SN").Articulation=="rim_closed","actual editor pointer route places selected closed rimshot");
        Restore(false);check(p.Events.Count==before.Count&&EditorNoteTypes.Get(p,3)=="rim_closed","Undo placement retains chosen input type");
        Restore(false);check(EditorNoteTypes.Get(p,1)=="ride"&&EditorNoteTypes.Get(p,3)=="rim_closed","Undo only changes the most recent lane type");Restore(true);
        p.SaveNative(Path.Combine(workspaceCheckRoot,"input-types-saved.crproj"),false);var saved=ChartProject.Read(File.ReadAllBytes(p.FilePath),p.FileName);
        check(EditorNoteTypes.Get(saved,1)=="cup"&&EditorNoteTypes.Get(saved,3)=="rim_closed","Windows save and reload retains separate lane input types");
        var normal=normalBindings.Save();var proMap=proBindings.Save();bool layout=nintendo;bool oldOpen=bindingsOpen;
        // This suite runs in a hidden window. Generic virtual pads do not
        // advertise background support, unlike many physical gamepads.
        var background=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var device=InputSystem.AddDevice<Gamepad>("QA Controller");InputSystem.EnableDevice(device);
        try{
            bindingsOpen=true;SetButtonLayout(!layout);
            check(normalBindings.Save()==normal&&proBindings.Save()==proMap,"button layout change preserves custom performance mappings");
            check(PlayerPrefs.GetInt("padNintendo",-1)==(nintendo?1:0),"button display selection persists");
            InputSystem.QueueStateEvent(device,new GamepadState().WithButton(GamepadButton.East));
            double deadline=Time.unscaledTimeAsDouble+2;
            do{yield return null;}while((lastPadId!=device.deviceId||lastPadPath!="buttonEast")&&Time.unscaledTimeAsDouble<deadline);
            File.WriteAllText(Path.Combine(workspaceCheckRoot,"controller-input.json"),new JObject{{"focused",Application.isFocused},{"enabled",device.enabled},{"device",device.deviceId},{"receivedDevice",lastPadId},{"button",lastPadPath},{"pressed",device.buttonEast.isPressed},{"status",ControllerInputStatus()}}.ToString());
            check(lastPadId==device.deviceId&&lastPadPath=="buttonEast"&&ControllerInputStatus().Contains(PadName("buttonEast")),"actual InputSystem event reaches controller input monitor");
            InputSystem.QueueStateEvent(device,new GamepadState());yield return null;
            check(!device.buttonEast.isPressed,"controller monitor does not hold buttons after release");
        }finally{SetButtonLayout(layout);bindingsOpen=oldOpen;InputSystem.RemoveDevice(device);InputSystem.settings.backgroundBehavior=background;lastPadPath="";lastPadId=0;bindingNotice="";}
        NavigateNow(Page.Songs);yield return null;
    }
}
}
