using System;
using UnityEngine;
public static partial class MaterializeEnhancements {
    static int editingIndex=-1;
    static Texture2D editOriginal,editSmall,editPreview;
    static float editExposure,editContrast=1,editGamma=1,editSaturation=1,editNormal=1;
    static bool editInvert;
    static string editExposureText="0",editContrastText="1",editGammaText="1",editSaturationText="1",editNormalText="1";
    static void CloseMapEditor(){editingIndex=-1;foreach(Texture2D t in new[]{editOriginal,editSmall,editPreview})if(t!=null)UnityEngine.Object.Destroy(t);editOriginal=editSmall=editPreview=null;}
    public static bool OpenMapEditor(int index){
        if(index<0||index>=8||Get(Main,MapFields[index])==null)return false;
        CloseMapEditor();Commit();Call(Main,"CloseWindows");editingIndex=index;
        editOriginal=RestoreImage(SnapshotImage(Get(Main,MapFields[index]) as Texture2D));
        if(index==5&&UseRoughness)InvertSurface(editOriginal);
        float factor=Mathf.Min(1,512f/Mathf.Max(editOriginal.width,editOriginal.height));
        editSmall=ResizeTexture(editOriginal,Mathf.Max(1,Mathf.RoundToInt(editOriginal.width*factor)),Mathf.Max(1,Mathf.RoundToInt(editOriginal.height*factor)));
        if(object.ReferenceEquals(editSmall,editOriginal))editSmall=RestoreImage(SnapshotImage(editOriginal));
        editExposure=0;editContrast=editGamma=editSaturation=editNormal=1;editInvert=false;
        editExposureText="0";editContrastText=editGammaText=editSaturationText=editNormalText="1";UpdateEditPreview();return true;
    }
    static Texture2D EditedTexture(Texture2D source,int index,float exposure,float contrast,float gamma,float saturation,float normal,bool inverse){
        Color32[] pixels=source.GetPixels32();float gain=Mathf.Pow(2,Mathf.Clamp(exposure,-30,30));gamma=Mathf.Max(0.0001f,gamma);
        for(int i=0;i<pixels.Length;i++){
            Color c=pixels[i];
            if(index==3&&normal!=1){Vector3 n=new Vector3((c.r*2-1)*normal,(c.g*2-1)*normal,c.b*2-1).normalized;c.r=n.x*.5f+.5f;c.g=n.y*.5f+.5f;c.b=n.z*.5f+.5f;}
            float gray=c.r*.2126f+c.g*.7152f+c.b*.0722f;
            c.r=gray+(c.r-gray)*saturation;c.g=gray+(c.g-gray)*saturation;c.b=gray+(c.b-gray)*saturation;
            c.r=Mathf.Pow(Mathf.Max(0,(c.r-.5f)*contrast+.5f)*gain,1/gamma);c.g=Mathf.Pow(Mathf.Max(0,(c.g-.5f)*contrast+.5f)*gain,1/gamma);c.b=Mathf.Pow(Mathf.Max(0,(c.b-.5f)*contrast+.5f)*gain,1/gamma);
            if(inverse){c.r=1-c.r;c.g=1-c.g;c.b=1-c.b;}pixels[i]=c;
        }
        Texture2D result=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);result.SetPixels32(pixels);result.Apply();result.name=source.name;result.wrapMode=TextureWrapMode.Repeat;return result;
    }
    static void UpdateEditPreview(){if(editPreview!=null)UnityEngine.Object.Destroy(editPreview);editPreview=EditedTexture(editSmall,editingIndex,editExposure,editContrast,editGamma,editSaturation,editNormal,editInvert);}
    public static bool ApplyMapEdit(float exposure,float contrast,float gamma,float saturation,float normal,bool inverse){
        if(editingIndex<0)return false;Texture2D result=null;
        try{
            int index=editingIndex;Commit();result=EditedTexture(editOriginal,index,exposure,contrast,gamma,saturation,normal,inverse);
            if(index==5&&UseRoughness)InvertSurface(result);
            Texture2D old=Get(Main,MapFields[index]) as Texture2D;
            sourceImages[index]=SnapshotImage(result);sourceTextures[index]=result;InputModes[index]=0;InputInvert[index]=false;if(index==5)smoothnessSourceRoughness=false;
            Set(Main,MapFields[index],result);RefreshInputTexture(index);Commit();DestroyUnusedTexture(old);result=null;CloseMapEditor();
            Status=T("当前贴图已编辑（可撤销；重新加载可恢复原图）","Current texture edited; undo or reload to restore the source");return true;
        }catch(Exception e){DestroyUnusedTexture(result);Error("Edit texture",e);return false;}
    }
    static bool EditorParameter(float y,string title,ref float value,ref string text,float min,float max){float next;string nt;bool changed=ParameterFloat(new Rect(18,y,280,40),title,value,text,out next,out nt,min,max,"","");value=next;text=nt;if(GUI.Button(new Rect(276,y-2,22,20),new GUIContent("↶",T("恢复此参数默认值","Reset this control")))){value=y==50?0:1;text=value.ToString(Invariant);changed=true;}return changed;}
    static void DrawMapEditor(float width,float height){GUI.Window(8186,new Rect((width-650)/2,Mathf.Max(60,(height-540)/2),650,540),DrawMapEditWindow,T("编辑当前贴图：","Edit Current Texture: ")+SurfaceName(editingIndex),solidWindow);GUI.BringWindowToFront(8186);}
    static void DrawMapEditWindow(int id){
        bool changed=EditorParameter(50,T("曝光","Exposure"),ref editExposure,ref editExposureText,-4,4);
        changed|=EditorParameter(98,T("对比度","Contrast"),ref editContrast,ref editContrastText,0,2);
        changed|=EditorParameter(146,T("伽马","Gamma"),ref editGamma,ref editGammaText,.1f,3);
        if(editingIndex==1||editingIndex==2)changed|=EditorParameter(194,T("饱和度","Saturation"),ref editSaturation,ref editSaturationText,0,2);
        if(editingIndex==3)changed|=EditorParameter(194,T("法线强度","Normal Strength"),ref editNormal,ref editNormalText,0,3);
        bool inverse=GUI.Toggle(new Rect(18,246,280,25),editInvert,T("反相 RGB（保留 Alpha）","Invert RGB (preserve alpha)"));changed|=inverse!=editInvert;editInvert=inverse;
        if(GUI.Button(new Rect(18,282,280,28),T("恢复编辑默认值","Reset Edit Controls"))){editExposure=0;editContrast=editGamma=editSaturation=editNormal=1;editInvert=false;editExposureText="0";editContrastText=editGammaText=editSaturationText=editNormalText="1";changed=true;}
        if(changed)UpdateEditPreview();
        GUI.Label(new Rect(322,30,308,25),T("编辑后预览","Edited Preview"));if(editPreview!=null)GUI.DrawTexture(new Rect(322,60,308,308),editPreview,ScaleMode.ScaleToFit);
        GUI.Label(new Rect(18,334,280,91),T("调整的是本类别当前贴图。\n应用后保留名称和重新加载来源。\n关闭或取消不会修改贴图。","Adjusts this category's current map.\nKeeps its name and reload source.\nCancel leaves the map unchanged."));
        GUI.Label(new Rect(322,384,308,38),editOriginal.width+" × "+editOriginal.height+T("（应用与导出使用完整尺寸）"," (full size on Apply/export)"));
        if(GUI.Button(new Rect(18,460,194,36),T("应用编辑","Apply Edit")))ApplyMapEdit(editExposure,editContrast,editGamma,editSaturation,editNormal,editInvert);
        if(GUI.Button(new Rect(228,460,194,36),T("取消","Cancel")))CloseMapEditor();
        if(GUI.Button(new Rect(438,460,194,36),T("转到生成器","Open Generator"))){int i=editingIndex,c=i==0?0:i==1||i==2?1:i-1;CloseMapEditor();OpenGenerator(c);}
    }
}
