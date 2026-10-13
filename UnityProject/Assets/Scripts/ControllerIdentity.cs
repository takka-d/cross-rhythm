using System;
using UnityEngine.InputSystem.Layouts;
namespace CrossRhythm {
public static class ControllerIdentity {
    // A backend layout's displayName is not a hardware identity. In particular
    // Unity names every XInput layout "Xbox Controller", including adapters.
    public static string ReportedModel(InputDeviceDescription description){
        string product=(description.product??"").Trim(),interfaceName=(description.interfaceName??"").Trim();
        if(string.Equals(interfaceName,"XInput",StringComparison.OrdinalIgnoreCase)||product.IndexOf("XInput",StringComparison.OrdinalIgnoreCase)>=0||string.IsNullOrEmpty(product)||product.Equals("Gamepad",StringComparison.OrdinalIgnoreCase)||product.Equals("Xbox Controller",StringComparison.OrdinalIgnoreCase))return "";
        // WebGL standard mapping can expose an Xbox-shaped compatibility name
        // for Switch-style and third-party ABXY pads. It is not model evidence.
        if(string.Equals(interfaceName,"WebGL",StringComparison.OrdinalIgnoreCase)&&(product.IndexOf("Xbox",StringComparison.OrdinalIgnoreCase)>=0||product.IndexOf("Standard Gamepad",StringComparison.OrdinalIgnoreCase)>=0||product.IndexOf("Generic",StringComparison.OrdinalIgnoreCase)>=0))return "";
        return product.Replace('\n',' ').Replace('\r',' ');
    }
    public static string Label(InputDeviceDescription description,int number,bool english){string model=ReportedModel(description);return "Controller "+number+" · "+(model==""?(english?"Model unavailable":"機種情報なし"):model);}
}
}
