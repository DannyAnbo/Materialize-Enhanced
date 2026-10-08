using System;
using System.IO;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    public static int TextureWidth,TextureHeight; // zero = native source dimensions
    static bool openResolution,resizing;
    static string resolutionWidth="2048",resolutionHeight="2048";
    static int MaximumTextureSize {get{return Math.Min(8192,SystemInfo.maxTextureSize);}}
    static Texture2D ResizeTexture(Texture2D texture,int width,int height) {
        if(texture==null||width==0||height==0||(texture.width==width&&texture.height==height))return texture;
        // Interpolate raw channel bytes, including alpha. Packed data is not gamma-corrected.
        Color32[] input=texture.GetPixels32(),output=new Color32[width*height];int sw=texture.width,sh=texture.height;
        int[] x0=new int[width],x1=new int[width];float[] fx=new float[width];
        for(int x=0;x<width;x++){float p=Mathf.Clamp((x+0.5f)*sw/width-0.5f,0,sw-1);x0[x]=(int)p;x1[x]=Math.Min(x0[x]+1,sw-1);fx[x]=p-x0[x];}
        for(int y=0;y<height;y++) {
            float p=Mathf.Clamp((y+0.5f)*sh/height-0.5f,0,sh-1);int y0=(int)p,y1=Math.Min(y0+1,sh-1);float fy=p-y0;
            for(int x=0;x<width;x++) {
                Color32 a=input[y0*sw+x0[x]],b=input[y0*sw+x1[x]],c=input[y1*sw+x0[x]],d=input[y1*sw+x1[x]];float f=fx[x];
                float ar=a.r+(b.r-a.r)*f,ag=a.g+(b.g-a.g)*f,ab=a.b+(b.b-a.b)*f,aa=a.a+(b.a-a.a)*f;
                output[y*width+x]=new Color32((byte)Mathf.RoundToInt(ar+(c.r+(d.r-c.r)*f-ar)*fy),(byte)Mathf.RoundToInt(ag+(c.g+(d.g-c.g)*f-ag)*fy),(byte)Mathf.RoundToInt(ab+(c.b+(d.b-c.b)*f-ab)*fy),(byte)Mathf.RoundToInt(aa+(c.a+(d.a-c.a)*f-aa)*fy));
            }
        }
        Texture2D result=new Texture2D(width,height,TextureFormat.RGBA32,false);result.SetPixels32(output);result.Apply();result.name=texture.name;result.filterMode=texture.filterMode;result.wrapMode=texture.wrapMode;result.anisoLevel=texture.anisoLevel;return result;
    }
    static Texture2D AtWorkingSize(Texture2D texture) {
        Texture2D result=ResizeTexture(texture,TextureWidth,TextureHeight);
        if(!object.ReferenceEquals(texture,result))UnityEngine.Object.Destroy(texture);return result;
    }
    public static bool SetTextureResolution(int width,int height) {
        if(width<0||height<0||(width==0)!=(height==0)||width>MaximumTextureSize||height>MaximumTextureSize){Status=T("尺寸必须在 1 到 ","Size must be between 1 and ")+MaximumTextureSize;return false;}
        if(resizing||importing||Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy"))){Status=T("请等待当前操作完成","Wait for the current operation");return false;}
        Texture2D[] replacements=new Texture2D[8];
        try {
            if(!Bind())return false;Commit();if(TextureWidth==width&&TextureHeight==height)return true;
            resizing=true;Call(Main,"CloseWindows");RememberPreviewIndex();
            int oldWidth=TextureWidth,oldHeight=TextureHeight;TextureWidth=width;TextureHeight=height;
            try {for(int i=0;i<8;i++)if(Get(Main,MapFields[i])!=null)replacements[i]=BuildMapInput(i);}
            catch {TextureWidth=oldWidth;TextureHeight=oldHeight;throw;}
            for(int i=0;i<8;i++)if(replacements[i]!=null) {
                Texture2D old=Get(Main,MapFields[i]) as Texture2D;Set(Main,MapFields[i],replacements[i]);sourceTextures[i]=replacements[i];DestroyUnusedTexture(old);replacements[i]=null;
            }
            RenderTexture hd=Get(Main,"_HDHeightMap") as RenderTexture;if(hd!=null){hd.Release();UnityEngine.Object.Destroy(hd);Set(Main,"_HDHeightMap",null);}
            Refresh(true);Call(Main,"ProcessPropertyMap");RefreshResolutionPreview();Commit();
            Status=T("贴图、视口与导出尺寸已同步：","Maps, viewport and exports now use: ")+(width==0?T("原始尺寸","Source size"):width+" × "+height);return true;
        }catch(Exception e){foreach(Texture2D texture in replacements)if(texture!=null)UnityEngine.Object.Destroy(texture);Error("Texture size",e);return false;}finally{resizing=false;}
    }
    static void RefreshResolutionPreview() {
        Material sample=Get(Main,"SampleMaterial") as Material;
        if(sample!=null&&previewIndex>=0&&previewIndex<8)sample.SetTexture("_MainTex",WorkflowTexture(Get(Main,MapFields[previewIndex]) as Texture2D));
    }
    static void RememberPreviewIndex() {
        Material sample=Get(Main,"SampleMaterial") as Material;if(sample==null)return;
        Texture selected=sample.GetTexture("_MainTex");for(int i=0;i<8;i++)if(Get(Main,MapFields[i])!=null&&object.ReferenceEquals(selected,Get(Main,MapFields[i]))){previewIndex=i;break;}
    }
    static void NormalizeGeneratedMaps() {
        if(resizing||TextureWidth==0||importing||Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy")))return;
        bool changed=false;RememberPreviewIndex();
        for(int i=0;i<8;i++) {
            Texture2D texture=Get(Main,MapFields[i]) as Texture2D;
            if(texture==null||(texture.width==TextureWidth&&texture.height==TextureHeight))continue;
            sourceImages[i]=SnapshotImage(texture);InputModes[i]=0;InputInvert[i]=false;
            SourceFileNames[i]="";
            if(i==5)smoothnessSourceRoughness=false;
            Texture2D result=ResizeTexture(texture,TextureWidth,TextureHeight);Set(Main,MapFields[i],result);sourceTextures[i]=result;DestroyUnusedTexture(texture);changed=true;
        }
        if(changed){Refresh(true);Call(Main,"ProcessPropertyMap");RefreshResolutionPreview();}
    }
    static void RestoreResolution(object po) {
        int[] size=Get(po,"zhTextureSize") as int[];TextureWidth=TextureHeight=0;
        if(size!=null&&size.Length==2&&size[0]>0&&size[1]>0&&size[0]<=MaximumTextureSize&&size[1]<=MaximumTextureSize){TextureWidth=size[0];TextureHeight=size[1];}
    }
    static void DrawResolution(float width,float height) {
        GUI.Window(8185,new Rect((width-500)/2,Mathf.Max(60,(height-300)/2),500,300),DrawResolutionWindow,T("统一纹理尺寸","Texture Resolution"),solidWindow);GUI.BringWindowToFront(8185);
    }
    static void DrawResolutionWindow(int id) {
        GUI.Label(new Rect(18,33,464,24),T("同时作用于工作贴图、视口和导出","Applies to working maps, viewport and exports"));
        int[] presets={256,512,1024,2048,4096,8192};
        for(int i=0;i<presets.Length;i++) {int n=presets[i];GUI.enabled=n<=MaximumTextureSize;
            if(GUI.Button(new Rect(18+(i%3)*156,70+(i/3)*36,148,30),n+" × "+n)){resolutionWidth=resolutionHeight=n.ToString();}
        }
        GUI.enabled=true;GUI.Label(new Rect(18,153,45,25),T("宽","Width"));resolutionWidth=GUI.TextField(new Rect(65,153,112,26),resolutionWidth);
        GUI.Label(new Rect(190,153,48,25),T("高","Height"));resolutionHeight=GUI.TextField(new Rect(240,153,112,26),resolutionHeight);
        if(GUI.Button(new Rect(365,153,117,26),T("应用","Apply"))) {int w,h;if(int.TryParse(resolutionWidth,out w)&&int.TryParse(resolutionHeight,out h)&&w>0&&h>0){if(SetTextureResolution(w,h))openResolution=false;}else Status=T("请输入有效宽高","Enter a valid width and height");}
        GUI.Label(new Rect(18,197,464,42),T("保留原始来源，可改回原尺寸。放大不会新增细节。\n更改尺寸可撤销；所选尺寸会保存在工程中。","Original sources are kept. Upscaling adds no detail.\nResolution changes can be undone and are saved in the project."));
        if(GUI.Button(new Rect(18,250,225,30),T("恢复各贴图原始尺寸","Restore Native Sizes"))){if(SetTextureResolution(0,0))openResolution=false;}
        if(GUI.Button(new Rect(257,250,225,30),T("关闭","Close")))openResolution=false;
    }
}
