using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
// Cached variants retain the stage's footprint, velocity heights and MISS treatment.
public static class DrumNoteVisuals {
    static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
    public static bool IsTom(string instrument)=>instrument=="HT"||instrument=="MT"||instrument=="FT";
    static float Ellipse(float x,float y,float rx,float ry)=>(Mathf.Sqrt(x*x/(rx*rx)+y*y/(ry*ry))-1)*Mathf.Min(rx,ry);
    public static Texture2D HiHat(int strength,bool closed,bool outline=false,bool missed=false){
        strength=Mathf.Clamp(strength,0,5);string key="HH-"+strength+"-"+closed+"-"+outline+"-"+missed;
        if(textures.TryGetValue(key,out var cached))return cached;
        const int size=240;var pixels=new Color32[size*size];float sy=ChartProject.Heights[strength]/.5f,separation=closed?1.45f:6.5f;
        Color baseColor=ChartVisuals.Hex(missed?"#91a3af":"#9ba9b8"),edge=Color.Lerp(baseColor,Color.white,.2f);
        for(int py=0;py<size;py++)for(int px=0;px<size;px++){
            float x=(px+.5f-size/2f)/3,y=(py+.5f-size/2f)/(3*sy);
            float upper=Ellipse(x,y-separation,21,2.2f),lower=Ellipse(x,y+separation,21,2.2f),distance=Mathf.Min(upper,lower);
            bool hollow=outline||strength==0;float d=hollow?Mathf.Abs(distance)-.65f:distance;
            float alpha=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.22f,.22f,d));if(alpha<=0)continue;
            float surface=y>=0?y-separation:y+separation;
            Color c=Color.Lerp(baseColor*.48f,baseColor,Mathf.Clamp01((surface+2.2f)/4.4f));
            if(hollow||distance>-.7f)c=edge;c.a=alpha;pixels[py*size+px]=c;
        }
        var result=new Texture2D(size,size,TextureFormat.RGBA32,false){name=key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        result.SetPixels32(pixels);result.Apply(false,false);textures[key]=result;return result;
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
