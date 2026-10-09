using System;
using UnityEngine.InputSystem.Layouts;
namespace CrossRhythm {
public static class ControllerIdentity {
    // A backend layout's displayName is not a hardware identity. In particular
    // Unity names every XInput layout "Xbox Controller", including adapters.
    public static string ReportedModel(InputDeviceDescription description){
        string product=(description.product??"").Trim();
        if(string.Equals(description.interfaceName,"XInput",StringComparison.OrdinalIgnoreCase)||product.IndexOf("XInput",StringComparison.OrdinalIgnoreCase)>=0||string.IsNullOrEmpty(product)||product.Equals("Gamepad",StringComparison.OrdinalIgnoreCase)||product.Equals("Xbox Controller",StringComparison.OrdinalIgnoreCase))return "";
        return product.Replace('\n',' ').Replace('\r',' ');
    }
    public static string Label(InputDeviceDescription description,int number,bool english){string model=ReportedModel(description);return "Controller "+number+" · "+(model==""?(english?"Model unavailable":"機種情報なし"):model);}
}
}
