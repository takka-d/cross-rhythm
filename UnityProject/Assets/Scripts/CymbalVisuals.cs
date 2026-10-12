using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
// Render a flat, tilted ride disc. Crash keeps the inherited v175 raised-bell silhouette.
// Shape and angle remain identifiable for outlines and grey MISS notes, without relying on colour.
public static class CymbalVisuals {
    static readonly Dictionary<int,Texture2D> rides=new Dictionary<int,Texture2D>();
    static float Ellipse(float x,float y,float rx,float ry)=> (Mathf.Sqrt(x*x/(rx*rx)+y*y/(ry*ry))-1)*Mathf.Min(rx,ry);
    public static Texture2D Ride(int strength,bool outline=false,bool missed=false){
        strength=Mathf.Clamp(strength,0,5);int key=strength+(outline?6:missed?12:0);
        if(rides.TryGetValue(key,out var texture))return texture;
        const int size=240;var pixels=new Color32[size*size];float sy=ChartProject.Heights[strength]/.5f;
        float angle=14*Mathf.Deg2Rad,ca=Mathf.Cos(angle),sa=Mathf.Sin(angle);
        Color baseColor=ChartVisuals.Hex(missed?"#91a3af":"#bf9b48"),edge=Color.Lerp(baseColor,Color.white,.16f);
        for(int py=0;py<size;py++)for(int px=0;px<size;px++){
            float u=(px+.5f-size/2f)/3,v=(py+.5f-size/2f)/(3*sy);
            float x=ca*u+sa*v,y=-sa*u+ca*v;
            float disc=Ellipse(x,y,21,5),bell=Ellipse(x,y-3,4.7f,4.2f),distance=Mathf.Min(disc,bell);
            bool hollow=outline||strength==0;float d=hollow?Mathf.Abs(distance)-.75f:distance;
            float alpha=Mathf.SmoothStep(1,0,Mathf.InverseLerp(-.23f,.23f,d));if(alpha<=0)continue;
            Color color=Color.Lerp(baseColor*.52f,baseColor,Mathf.Clamp01((y+5)/11));
            if(hollow||distance>-.8f||Mathf.Abs(bell)<.4f||Mathf.Abs(Ellipse(x,y,18,3.25f))<.25f)color=edge;
            color.a=alpha;pixels[py*size+px]=color;
        }
        texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Ride-"+key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels32(pixels);texture.Apply(false,false);rides[key]=texture;return texture;
    }
}
}
