using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CrossRhythm {
public static class ControlBindingsTests {
    static void Check(bool value,string name){if(!value)throw new Exception(name);Debug.Log("PASS "+name);}
    public static void Run(){
        var normal=ControlBindings.Defaults(false);var pro=ControlBindings.Defaults(true);
        Check(normal.PadRow("dpad/up")==1-1&&normal.PadRow("buttonEast")==1&&normal.PadRow("leftTrigger")==2,"Notion Normal directions, face buttons, feet");
        Check(pro.PadRow("buttonEast")==1&&pro.PadRow("buttonSouth")==3&&pro.PadRow("buttonNorth")==5&&pro.PadRow("buttonWest")==7&&pro.PadRow("leftTrigger")==10,"Notion Nintendo Pro mappings");
        normal.BindKey(0,Key.Digit9);Check(normal.Keys[1]==Key.Digit4,"key conflicts swap without losing other action");normal.BindKey(0,Key.Z);var loaded=ControlBindings.Load(normal.Save(),false,true);Check(loaded.Keys[0]==Key.Z&&loaded.Keys[1]==Key.Digit4,"key remap persisted separately from defaults");
        pro.BindPad(1,"rightShoulder");Check(pro.PadRow("buttonEast")==9&&pro.PadRow("rightShoulder")==1,"controller conflicts swap");Check(ControlBindings.Load(pro.Save(),true,true).PadRow("rightShoulder")==1,"controller remap roundtrip");
        var legacy=ControlBindings.Defaults(true);legacy.Pads[0]="start";legacy.Pads[3]="select";Check(legacy.MigrateReservedPads(true,true)&&legacy.PadRow("start")==-1&&legacy.PadRow("select")==-1&&legacy.Pads.All(p=>!string.IsNullOrEmpty(p))&&legacy.Pads.SelectMany(p=>p.Split('|')).Distinct().Count()==legacy.Pads.Sum(p=>p.Split('|').Length),"reserved legacy controller bindings migrate without empty or duplicate actions");
        Check(ControllerNavigationMath.Deadzone(new Vector2(.12f,-.18f),ControllerNavigationMath.PointerDeadzone)==Vector2.zero,"controller pointer deadzone ignores stick drift");
        Check(ControllerNavigationMath.Deadzone(new Vector2(.8f,0),ControllerNavigationMath.PointerDeadzone).x>.7f,"controller pointer retains deliberate stick movement");
        Check(ControllerNavigationMath.Direction(new Vector2(.8f,.2f))==KeyCode.RightArrow&&ControllerNavigationMath.Direction(new Vector2(.1f,-.9f))==KeyCode.DownArrow,"controller focus direction chooses dominant axis");
        Check(ControllerNavigationMath.Direction("dpad/up")==KeyCode.UpArrow&&ControllerNavigationMath.Direction("dpad/right")==KeyCode.RightArrow&&ControllerNavigationMath.Direction("buttonEast")==KeyCode.None,"short D-pad events retain their menu direction");
        Check(ControllerNavigationMath.ActivatesPointer(Vector2.zero,new Vector2(0,.8f),false)&&ControllerNavigationMath.ActivatesPointer(Vector2.zero,Vector2.zero,true)&&!ControllerNavigationMath.ActivatesPointer(Vector2.zero,Vector2.zero,false),"controller wheel and right-click activate the shared pointer without prior pointer movement");
        Check(ControllerNavigationMath.ControllerHover(true,true,false)==false&&ControllerNavigationMath.ControllerHover(true,false,true)&&ControllerNavigationMath.ControllerHover(false,true,false),"only the active pointer can hover a UI control");
        Check(ControllerNavigationMath.Scroll(new Vector2(10,20),new Vector2(.5f,-.5f),20,100,100,true,true)==new Vector2(20,10)&&ControllerNavigationMath.Scroll(new Vector2(2,3),new Vector2(-1,-1),20,100,100,true,true)==Vector2.zero,"controller wheel changes scroll position and clamps both axes");
        Check(ControllerNavigationMath.HomePath("home")&&ControllerNavigationMath.HomePath("homeButton")&&ControllerNavigationMath.HomePath("systemButton")&&!ControllerNavigationMath.HomePath("start"),"Nintendo, platform and PlayStation home controls share the Title action");
        var songRect=new Rect(200,100,400,90);Check(UiFeedback.NewHoverTarget(false,new Rect(),songRect.center)&&!UiFeedback.NewHoverTarget(true,songRect,new Vector2(598,188))&&UiFeedback.NewHoverTarget(true,songRect,new Vector2(601,188)),"hover target is based on remaining inside the visible button instead of element ids or cursor jitter");
        var feedbackObject=new GameObject("Hover feedback test");var feedback=feedbackObject.AddComponent<UiFeedback>();feedback.Gain=0;feedback.BeginHoverFrame();bool first=feedback.Hover("song:1",songRect,songRect.center);feedback.EndHoverFrame();feedback.BeginHoverFrame();bool regenerated=feedback.Hover("song:regenerated",songRect,songRect.center);feedback.EndHoverFrame();feedback.BeginHoverFrame();bool movedInside=feedback.Hover("song:regenerated",songRect,new Vector2(590,180));feedback.EndHoverFrame();feedback.BeginHoverFrame();feedback.EndHoverFrame();feedback.BeginHoverFrame();bool reentered=feedback.Hover("song:again",songRect,songRect.center);feedback.EndHoverFrame();UnityEngine.Object.DestroyImmediate(feedbackObject);Check(first&&!regenerated&&!movedInside&&reentered,"Songs hover sound latches across redraw and id regeneration until the pointer leaves every button");
        Check(!ControllerNavigationMath.SystemPointerVisible(true)&&ControllerNavigationMath.SystemPointerVisible(false)&&ControllerNavigationMath.MouseReclaimsPointer(true,new Vector2(.3f,0))&&!ControllerNavigationMath.MouseReclaimsPointer(true,new Vector2(.1f,0))&&!ControllerNavigationMath.MouseReclaimsPointer(false,new Vector2(1,0)),"controller pointer hides the system cursor and mouse movement reclaims it");
        Check(HiHatClosure.Level(0)==1&&Math.Abs(HiHatClosure.Level(.010)-.82)<1e-10&&Math.Abs(HiHatClosure.Level(.052)-.16)<1e-10&&Math.Abs(HiHatClosure.Level(.112)-.006)<1e-10&&HiHatClosure.Level(.132)==0,"v175 exact closure envelope anchors");
        double previous=1;for(int i=0;i<=1420;i++){double v=HiHatClosure.Level(i*.0001);CheckQuiet(v<=previous+1e-12&&v>=0);previous=v;}Check(Math.Abs(HiHatClosure.Cutoff(.052,48000)-5200)<.001,"v175 lowpass reaches 5200Hz");
        Debug.Log("CROSS_RHYTHM_BINDINGS_HIHAT_TESTS_PASS");
    }
    static void CheckQuiet(bool ok){if(!ok)throw new Exception("Closure is not monotonic");}
}
}
