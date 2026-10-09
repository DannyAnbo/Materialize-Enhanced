using System;
using System.Text;
using UnityEngine;
public static partial class MaterializeEnhancements {
    static readonly string[] TileTemps={"_HeightMapTemp","_DiffuseMapTemp","_DiffuseMapOriginalTemp","_NormalMapTemp","_MetallicMapTemp","_SmoothnessMapTemp","_EdgeMapTemp","_AOMapTemp"};
    static readonly string[] TileNames={"_DisplacementMap","_DiffuseMap","_DiffuseMapOriginal","_NormalMap","_MetallicMap","_SmoothnessMap","_EdgeMap","_AOMap"};
    static readonly string[] TileParameters={"Falloff","OverlapX","OverlapY","SplatRotation","SplatRotationRandom","SplatScale","SplatWobble","SplatWobbleRandom","SplatRandomize"};
    static int tilePreview=-1;
    static string tileSignature="";
    static Texture2D tileRoughness,tileGuide;
    static bool tileDirty=true;
    static Rect tileWindow=new Rect(20,365,330,590);
    static float EffectiveOverlap(float value){if(value<=.5f)return Mathf.Max(.0001f,value);return .5f+(value-.5f)/(1+2*(value-.5f));}
    static bool HasMaps(){for(int i=0;i<8;i++)if(Get(Main,MapFields[i])!=null)return true;return false;}
    static void OpenTiling(){CloseMapEditor();Call(Main,"CloseWindows");Call(Main,"FixSize");tileSignature="";tileDirty=true;tilePreview=-1;previewIndex=-1;((GameObject)Get(Main,"TilingTextureMakerGuiObject")).SetActive(true);Call(Get(Main,"TilingTextureMakerGuiScript"),"Initialize");}
    public static void UpdateTiling(object gui){
        try{
            Material blit=Get(gui,"blitMaterial") as Material;if(blit==null||Main==null)return;
            Material full=Get(gui,"thisMaterial") as Material;if(full==null)return;
            float repeat=Convert.ToSingle(Get(gui,"TexTiling"));full.SetVector("_Tiling",new Vector4(repeat,repeat,Convert.ToSingle(Get(gui,"TexOffsetX")),Convert.ToSingle(Get(gui,"TexOffsetY"))));
            StringBuilder key=new StringBuilder();foreach(string f in TileParameters)key.Append(Get(gui,f)).Append('|');key.Append(Get(gui,"techniqueSplat"));key.Append('|').Append(Get(gui,"NewTexSelectionX")).Append('|').Append(Get(gui,"NewTexSelectionY"));key.Append('|').Append(TextureWidth).Append('|').Append(TextureHeight);
            bool requested=Convert.ToBoolean(Get(gui,"doStuff"));if(tileSignature==key.ToString()&&!requested)return;
            Set(gui,"doStuff",false);tileSignature=key.ToString();RebuildTiling(gui);tileDirty=true;
        }catch(Exception e){Error("Tiling",e);}
    }
    static void RebuildTiling(object gui){
        int[] sizes={512,1024,2048,4096};int width=TextureWidth>0?TextureWidth:sizes[Mathf.Clamp(Convert.ToInt32(Get(gui,"NewTexSelectionX")),0,3)],height=TextureHeight>0?TextureHeight:sizes[Mathf.Clamp(Convert.ToInt32(Get(gui,"NewTexSelectionY")),0,3)];
        if(TextureWidth==0){Texture2D native=Get(Main,"_HeightMap") as Texture2D;for(int i=0;i<8&&native==null;i++)native=Get(Main,MapFields[i]) as Texture2D;if(native!=null){width=native.width;height=native.height;}}
        Set(gui,"NewTexSizeX",width);Set(gui,"NewTexSizeY",height);float aspect=width/(float)height;Set(gui,"targetAR",new Vector2(aspect,1/aspect));
        Vector3 scale=new Vector3(Mathf.Sqrt(aspect),1/Mathf.Sqrt(aspect),1);Set(gui,"objectScale",scale);GameObject obj=Get(gui,"testObject") as GameObject;if(obj!=null)obj.transform.localScale=scale;
        bool splat=Convert.ToBoolean(Get(gui,"techniqueSplat"));Set(gui,"tileTech",Enum.ToObject(Get(gui,"tileTech").GetType(),splat?1:0));
        UnityEngine.Random.State random=UnityEngine.Random.state;UnityEngine.Random.InitState(1778);
        Call(gui,aspect>=6?"SKRectWide3":aspect>=3?"SKRectWide2":aspect>=1.5f?"SKRectWide":aspect<=.17f?"SKRectTall3":aspect<=.34f?"SKRectTall2":aspect<=.67f?"SKRectTall":"SKSquare");UnityEngine.Random.state=random;
        Material blit=Get(gui,"blitMaterial") as Material,full=Get(gui,"thisMaterial") as Material;
        float ox=Convert.ToSingle(Get(gui,"OverlapX")),oy=Convert.ToSingle(Get(gui,"OverlapY"));
        blit.SetFloat("_Falloff",Mathf.Max(.0001f,Convert.ToSingle(Get(gui,"Falloff"))));blit.SetFloat("_OverlapX",EffectiveOverlap(ox));blit.SetFloat("_OverlapY",EffectiveOverlap(oy));
        Texture2D originalHeight=Get(Main,"_HeightMap") as Texture2D;
        try{
            if(originalHeight==null){if(tileGuide==null){tileGuide=new Texture2D(1,1,TextureFormat.RGBA32,false,true);tileGuide.SetPixel(0,0,new Color(.5f,.5f,.5f,1));tileGuide.Apply();}Set(Main,"_HeightMap",tileGuide);}blit.SetTexture("_HeightTex",Get(Main,"_HeightMap") as Texture);
            for(int i=0;i<8;i++){
                Texture2D texture=i==0?originalHeight:Get(Main,MapFields[i]) as Texture2D;RenderTexture previous=Get(gui,TileTemps[i]) as RenderTexture;
                if(texture==null){if(previous!=null){previous.Release();UnityEngine.Object.Destroy(previous);}Set(gui,TileTemps[i],null);continue;}
                blit.SetFloat("_IsHeight",i==0?1:0);blit.SetFloat("_IsNormal",i==3?1:0);
                RenderTexture result=Call(gui,"TileTexture",texture,previous,TileNames[i]) as RenderTexture;Set(gui,TileTemps[i],result);full.SetTexture(i==2?"_DiffuseMap":TileNames[i],result);
            }
            if(Get(Main,"_DiffuseMap")!=null)full.SetTexture("_DiffuseMap",Get(gui,TileTemps[1]) as Texture);
            full.SetTexture("_HDDisplacementMap",Get(gui,TileTemps[0]) as Texture);blit.SetFloat("_IsHeight",0);blit.SetFloat("_IsNormal",0);
        }finally{Set(Main,"_HeightMap",originalHeight);}
    }
    static Texture2D ReadRenderTexture(RenderTexture rt){RenderTexture old=RenderTexture.active;try{RenderTexture.active=rt;Texture2D texture=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false,true);texture.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);texture.Apply();texture.wrapMode=TextureWrapMode.Repeat;return texture;}finally{RenderTexture.active=old;}}
    public static bool ApplyTiling(object gui){
        Texture2D[] results=new Texture2D[8];
        try{
            Commit();for(int i=0;i<8;i++){RenderTexture rt=Get(gui,TileTemps[i]) as RenderTexture;if(Get(Main,MapFields[i])!=null&&rt!=null)results[i]=ReadRenderTexture(rt);}
            for(int i=0;i<8;i++)if(results[i]!=null){Texture2D old=Get(Main,MapFields[i]) as Texture2D;Set(Main,MapFields[i],results[i]);sourceTextures[i]=results[i];sourceImages[i]=SnapshotImage(results[i]);InputModes[i]=0;InputInvert[i]=false;if(i==5)smoothnessSourceRoughness=false;results[i]=null;DestroyUnusedTexture(old);}
            Call(gui,"Close");Refresh(true);Call(Main,"ProcessPropertyMap");Commit();Status=T("平铺结果已应用（可撤销）","Tiled maps applied; undo available");return true;
        }catch(Exception e){Error("Apply tiling",e);return false;}finally{foreach(Texture2D t in results)DestroyUnusedTexture(t);}
    }
    public static void DrawTiling(object gui){
        if(Main==null)return;EnsureStyles();Matrix4x4 old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(uiScale,uiScale,1));
        try{
            float width=Screen.width/uiScale,height=Screen.height/uiScale;tileWindow.y=Mathf.Max(350,Mathf.Min(tileWindow.y,height-525));tileWindow.height=Mathf.Min(590,height-tileWindow.y-10);tileWindow=GUI.Window(8188,tileWindow,delegate(int id){DrawTilingWindow(gui);},T("纹理平铺生成器","Texture Tiling"),solidWindow);
            if(tilePreview>=0){RenderTexture rt=Get(gui,TileTemps[tilePreview]) as RenderTexture;Texture image=rt;
                if(tilePreview==5&&UseRoughness&&rt!=null){if(tileDirty||tileRoughness==null){if(tileRoughness!=null)UnityEngine.Object.Destroy(tileRoughness);RenderTexture small=RenderTexture.GetTemporary(Mathf.Min(1024,rt.width),Mathf.Min(1024,rt.height),0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);Graphics.Blit(rt,small);tileRoughness=ReadRenderTexture(small);InvertSurface(tileRoughness);RenderTexture.ReleaseTemporary(small);}image=tileRoughness;}
                Rect area=new Rect(365,368,Mathf.Max(200,width-385),Mathf.Max(150,height-400));GUI.Box(area,SurfaceName(tilePreview)+T("：平铺预览"," / Tiled Preview"),solidPanel);
                if(image!=null){float repeats=Mathf.Max(.05f,Mathf.Abs(Convert.ToSingle(Get(gui,"TexTiling"))));GUI.DrawTextureWithTexCoords(new Rect(area.x+12,area.y+30,area.width-24,area.height-42),image,new Rect(Convert.ToSingle(Get(gui,"TexOffsetX")),Convert.ToSingle(Get(gui,"TexOffsetY")),repeats*(area.width-24)/(area.height-42)/(image.width/(float)image.height),repeats));}
            }tileDirty=false;
        }finally{GUI.matrix=old;}
    }
    static Vector2 tileScroll;
    static void TileParameter(object gui,float y,string field,string label,float min,float max){float v=Convert.ToSingle(Get(gui,field)),n;string text=Get(gui,field+"Text") as string,nt;if(ParameterFloat(new Rect(12,y,282,40),label,v,text,out n,out nt,min,max,"TilingTextureMakerGui",field)){Set(gui,field,n);Set(gui,field+"Text",nt);Set(gui,"doStuff",true);}}
    static void DrawTilingWindow(object gui){
        GUI.Label(new Rect(12,28,306,20),T("预览类别（重复次数由“平铺次数”控制）","Preview category (repeat count below)"));
        string[] labels=new string[9];labels[0]=T("完整材质","Full Material");for(int i=0;i<8;i++)labels[i+1]=SurfaceName(i);
        int choice=GUI.SelectionGrid(new Rect(12,52,306,90),tilePreview+1,labels,3);if(choice!=tilePreview+1){tilePreview=choice-1;tileDirty=true;previewIndex=-1;}
        bool splat=Convert.ToBoolean(Get(gui,"techniqueSplat"));bool ns=GUI.SelectionGrid(new Rect(12,151,306,27),splat?1:0,new[]{T("边缘重叠","Overlap"),T("随机拼接","Splat")},2)==1;
        if(ns!=splat){Set(gui,"techniqueSplat",ns);Set(gui,"techniqueOverlap",!ns);Set(gui,"doStuff",true);}
        float available=Mathf.Max(110,tileWindow.height-232);tileScroll=GUI.BeginScrollView(new Rect(8,186,314,available),tileScroll,new Rect(0,0,298,ns?440:285));
        TileParameter(gui,4,"Falloff",T("边缘衰减","Edge Falloff"),.01f,4);
        if(!ns){TileParameter(gui,48,"OverlapX",T("边缘重叠 X","Overlap X"),0,4);TileParameter(gui,92,"OverlapY",T("边缘重叠 Y","Overlap Y"),0,4);}
        else{TileParameter(gui,48,"SplatRotation",T("旋转","Rotation"),0,2);TileParameter(gui,92,"SplatRotationRandom",T("随机旋转","Random Rotation"),0,2);TileParameter(gui,136,"SplatScale",T("拼接缩放","Splat Scale"),.1f,4);TileParameter(gui,180,"SplatWobble",T("偏移量","Wobble"),0,4);TileParameter(gui,224,"SplatRandomize",T("随机种子","Random Seed"),0,10);}
        float y=ns?272:146;TileParameter(gui,y,"TexTiling",T("预览平铺次数","Preview Repeat"),.1f,8);TileParameter(gui,y+44,"TexOffsetX",T("预览偏移 X","Preview Offset X"),-2,2);TileParameter(gui,y+88,"TexOffsetY",T("预览偏移 Y","Preview Offset Y"),-2,2);GUI.EndScrollView();
        if(GUI.Button(new Rect(12,tileWindow.height-38,147,28),T("应用平铺结果","Apply Tiled Maps")))ApplyTiling(gui);
        if(GUI.Button(new Rect(171,tileWindow.height-38,147,28),T("关闭","Close"))){Call(gui,"Close");Call(Main,"SetMaterialValues");}
    }
}
