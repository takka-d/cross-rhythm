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
        Check(HiHatClosure.Level(0)==1&&Math.Abs(HiHatClosure.Level(.010)-.82)<1e-10&&Math.Abs(HiHatClosure.Level(.052)-.16)<1e-10&&Math.Abs(HiHatClosure.Level(.112)-.006)<1e-10&&HiHatClosure.Level(.132)==0,"v175 exact closure envelope anchors");
        double previous=1;for(int i=0;i<=1420;i++){double v=HiHatClosure.Level(i*.0001);CheckQuiet(v<=previous+1e-12&&v>=0);previous=v;}Check(Math.Abs(HiHatClosure.Cutoff(.052,48000)-5200)<.001,"v175 lowpass reaches 5200Hz");
        Debug.Log("CROSS_RHYTHM_BINDINGS_HIHAT_TESTS_PASS");
    }
    static void CheckQuiet(bool ok){if(!ok)throw new Exception("Closure is not monotonic");}
}
}
