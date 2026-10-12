using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
// Cached variants retain the stage's footprint, velocity heights and MISS treatment.
public static class DrumNoteVisuals {
    static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
    public static bool IsTom(string instrument)=>instrument=="HT"||instrument=="MT"||instrument=="FT";
    public static Texture2D HiHat(int strength,bool closed,bool outline=false,bool missed=false){
        strength=Mathf.Clamp(strength,0,5);string key="HH-"+strength+"-"+closed+"-"+outline+"-"+missed;
        if(textures.TryGetValue(key,out var cached))return cached;
        var data=Resources.Load<TextAsset>("Stage175/HH-"+(outline?"outline":missed?"miss":"normal")+"-"+strength);
        if(data==null)return null;
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name=key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.LoadImage(data.bytes,false);
        if(!closed){
            // Retain the original v175 silhouette and strength. Only separate its two halves.
            var source=texture.GetPixels32();var pixels=new Color32[source.Length];int w=texture.width,h=texture.height,half=h/2,gap=Mathf.RoundToInt(h/80f*3);
            for(int y=0;y<h;y++){int from=y<half?y+gap:y-gap;if(from<0||from>=h||(y<half?from>=half:from<half))continue;
                for(int x=0;x<w;x++)pixels[y*w+x]=source[from*w+x];
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
        }
        textures[key]=texture;return texture;
    }
    public static Texture2D Tom(string instrument,int strength,bool outline=false,bool missed=false){
        strength=Mathf.Clamp(strength,0,5);string key=instrument+"-"+(outline?"outline":missed?"miss":"normal")+"-"+strength;
        if(textures.TryGetValue(key,out var cached))return cached;
        var bytes=Resources.Load<TextAsset>("Stage175/"+key);if(bytes==null)return null;
        var result=new Texture2D(2,2,TextureFormat.RGBA32,false){name=key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};result.LoadImage(bytes.bytes,false);
        if(!missed){
            var pixels=result.GetPixels32();Color.RGBToHSV(ChartVisuals.PlayColor(instrument),out float hue,out float sat,out float value);
            Color.RGBToHSV(ChartVisuals.Hex("#c8874a"),out _,out float sourceSat,out float sourceValue);
            for(int i=0;i<pixels.Length;i++){
                Color original=pixels[i];if(original.a==0)continue;Color.RGBToHSV(original,out _,out float s,out float v);
                Color c=Color.HSVToRGB(hue,Mathf.Clamp01(s*sat/sourceSat),Mathf.Clamp01(v*value/sourceValue));c.a=original.a;pixels[i]=c;
            }
            result.SetPixels32(pixels);result.Apply(false,false);
        }
        textures[key]=result;return result;
    }
}
}
