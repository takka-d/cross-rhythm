using System;
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
        Check(HiHatClosure.Level(0)==1&&Math.Abs(HiHatClosure.Level(.010)-.82)<1e-10&&Math.Abs(HiHatClosure.Level(.052)-.16)<1e-10&&Math.Abs(HiHatClosure.Level(.112)-.006)<1e-10&&HiHatClosure.Level(.132)==0,"v175 exact closure envelope anchors");
        double previous=1;for(int i=0;i<=1420;i++){double v=HiHatClosure.Level(i*.0001);CheckQuiet(v<=previous+1e-12&&v>=0);previous=v;}Check(Math.Abs(HiHatClosure.Cutoff(.052,48000)-5200)<.001,"v175 lowpass reaches 5200Hz");
        Debug.Log("CROSS_RHYTHM_BINDINGS_HIHAT_TESTS_PASS");
    }
    static void CheckQuiet(bool ok){if(!ok)throw new Exception("Closure is not monotonic");}
}
}
