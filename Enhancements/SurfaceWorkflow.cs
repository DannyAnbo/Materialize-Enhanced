using System;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    // The original shaders/generators continue to consume canonical smoothness.
    // The workflow controls import interpretation, visible maps and exported bytes.
    public static bool DefaultRoughness;
    public static bool UseRoughness {get{return DefaultRoughness;}}
    static bool smoothnessSourceRoughness;
    static Texture2D roughnessDisplay;
    static ImageState roughnessDisplaySource;
    static string SurfaceLabel {get{return UseRoughness?T("粗糙度","Roughness"):T("平滑度","Smoothness");}}
    static string SurfaceName(int index){return index==5?SurfaceLabel:(English?LabelsEn:LabelsZh)[index];}
    static string ChannelLabel(int selection){return selection==3?SurfaceLabel:(English?AlphaEn:AlphaZh)[selection];}
    static string OutputName(int index){return index==5&&(Names[index]=="_smoothness"||Names[index]=="_roughness")?(UseRoughness?"_roughness":"_smoothness"):Names[index];}
    static void InvertSurface(Texture2D texture) {
        Color32[] pixels=texture.GetPixels32();
        for(int i=0;i<pixels.Length;i++){Color32 p=pixels[i];pixels[i]=new Color32((byte)(255-p.r),(byte)(255-p.g),(byte)(255-p.b),p.a);}
        texture.SetPixels32(pixels);texture.Apply();
    }
    static Texture2D BuildMapInput(int index) {
        Texture2D result=BuildInputTexture(sourceImages[index],InputModes[index],InputInvert[index]);
        if(index==5&&smoothnessSourceRoughness&&result!=null){InvertSurface(result);imageCache.Remove(result.GetInstanceID());}
        return result;
    }
    public static Texture2D WorkflowTexture(Texture2D texture) {
        if(!UseRoughness||texture==null||!object.ReferenceEquals(texture,Get(Main,"_SmoothnessMap")))return texture;
        ImageState source=SnapshotImage(texture);
        if(roughnessDisplay==null||!object.ReferenceEquals(source,roughnessDisplaySource)) {
            ClearSurfaceDisplay();roughnessDisplay=RestoreImage(source);InvertSurface(roughnessDisplay);roughnessDisplaySource=source;
        }
        return roughnessDisplay;
    }
    static void ClearSurfaceDisplay(){if(roughnessDisplay!=null)UnityEngine.Object.Destroy(roughnessDisplay);roughnessDisplay=null;roughnessDisplaySource=null;}
    static void RefreshWorkflowPreview() {
        if(previewIndex!=5||editingIndex>=0||((GameObject)Get(Main,"TilingTextureMakerGuiObject")).activeSelf)return;
        Material sample=Get(Main,"SampleMaterial") as Material;
        if(sample!=null)sample.SetTexture("_MainTex",WorkflowTexture(Get(Main,"_SmoothnessMap") as Texture2D)??(Get(Main,"_TextureGrey") as Texture));
    }
    public static void SetLoadedTexture(object main,int mapType) {
        Main=main;string name=Enum.GetName(main.GetType().Assembly.GetType("MapType"),mapType);int index=Array.IndexOf(MapTypes,name);
        if(index<0||index>=MapFields.Length)return;previewIndex=index;
        Call(main,"SetPreviewMaterial",WorkflowTexture(Get(main,MapFields[index]) as Texture2D));Call(main,"FixSize");
    }
    public static bool SetSurfaceWorkflow(bool roughness) {
        if(!Bind()||importing||Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy")))return false;
        Commit();
        DefaultRoughness=roughness;editNames=null;ClearSurfaceDisplay();Call(Main,"ProcessPropertyMap");RefreshWorkflowPreview();
        SaveConfig();Commit();Status=T("软件默认表面贴图：","Application surface map: ")+SurfaceLabel;return true;
    }
    static void RestoreSurfaceWorkflow(object project) {
        // Only source interpretation belongs to the project. Display/export mode is global.
        smoothnessSourceRoughness=Convert.ToBoolean(Get(project,"zhSourceRoughness"));ClearSurfaceDisplay();
    }
    static void ApplySurfaceProperty(Color32[] pixels) {
        if(!UseRoughness||Get(Main,"_SmoothnessMap")==null)return;
        bool r=Convert.ToInt32(Get(Main,"propRed"))==3,g=Convert.ToInt32(Get(Main,"propGreen"))==3,b=Convert.ToInt32(Get(Main,"propBlue"))==3,a=Alpha==3;
        for(int i=0;i<pixels.Length;i++){Color32 p=pixels[i];if(r)p.r=(byte)(255-p.r);if(g)p.g=(byte)(255-p.g);if(b)p.b=(byte)(255-p.b);if(a)p.a=(byte)(255-p.a);pixels[i]=p;}
    }
}
