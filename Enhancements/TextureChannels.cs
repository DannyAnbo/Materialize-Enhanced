using System;
using System.IO;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    // Keep immutable originals so changing R to G never samples the previously extracted R image.
    static readonly ImageState[] sourceImages=new ImageState[8];
    static readonly Texture2D[] sourceTextures=new Texture2D[8];
    public static readonly int[] InputModes=new int[8]; // 0 full image, 1 R, 2 G, 3 B, 4 A
    public static readonly bool[] InputInvert=new bool[8];
    static readonly string[] InputChannelNames={"RGBA","R","G","B","A"};

    static Texture2D BuildInputTexture(ImageState source,int mode,bool inverse) {
        Texture2D result=RestoreImage(source);if(result==null)return null;
        if(mode!=0||inverse) {
            Color32[] pixels=result.GetPixels32();
            for(int i=0;i<pixels.Length;i++) {
                Color32 pixel=pixels[i];
                if(mode!=0) {
                    byte value=mode==1?pixel.r:mode==2?pixel.g:mode==3?pixel.b:pixel.a;
                    if(inverse)value=(byte)(255-value);pixels[i]=new Color32(value,value,value,255);
                }else pixels[i]=new Color32((byte)(255-pixel.r),(byte)(255-pixel.g),(byte)(255-pixel.b),pixel.a);
            }
            result.SetPixels32(pixels);result.Apply();
        }else imageCache[result.GetInstanceID()]=source;
        return AtWorkingSize(result);
    }
    static void DestroyUnusedTexture(Texture2D texture) {
        if(texture==null)return;
        foreach(string field in MapFields)if(object.ReferenceEquals(Get(Main,field),texture))return;
        UnityEngine.Object.Destroy(texture);
    }
    static void RefreshInputTexture(int index) {
        RenderTexture hd=Get(Main,"_HDHeightMap") as RenderTexture;
        if(hd!=null){hd.Release();UnityEngine.Object.Destroy(hd);Set(Main,"_HDHeightMap",null);}
        Refresh(true);Call(Main,"SetLoadedTexture",MapEnum(index));
        if(Get(Main,"_PropertyMap")!=null)Call(Main,"ProcessPropertyMap");
    }
    public static bool SetInputChannel(int index,int mode,bool inverse) {
        if(index<0||index>=8||mode<0||mode>4)return false;
        Texture2D result=null;
        try {
            if(!Bind())return false;Commit();
            if(InputModes[index]==mode&&InputInvert[index]==inverse)return true;
            ImageState source=sourceImages[index];Texture2D old=Get(Main,MapFields[index]) as Texture2D;
            InputModes[index]=mode;InputInvert[index]=inverse;
            if(source!=null)result=BuildMapInput(index);
            if(old!=null){Set(Main,MapFields[index],result);sourceTextures[index]=result;RefreshInputTexture(index);}
            Commit();DestroyUnusedTexture(old);
            Status=T("已选择来源通道：","Source channel selected: ")+(mode==0?T("整图","Full image"):InputChannelNames[mode])+(inverse?T("（反相）"," (inverted)"):"");return true;
        }catch(Exception e){DestroyUnusedTexture(result);Error("Source channel",e);return false;}
    }
    static void AdoptImportedTexture(int index,Texture2D original) {
        ImageState source=SnapshotImage(original);Texture2D old=Get(Main,MapFields[index]) as Texture2D;
        if(index==5)smoothnessSourceRoughness=UseRoughness;
        sourceImages[index]=source;
        Texture2D result=InputModes[index]==0&&!InputInvert[index]&&!(index==5&&smoothnessSourceRoughness)&&(TextureWidth==0||(original.width==TextureWidth&&original.height==TextureHeight))?original:BuildMapInput(index);
        sourceImages[index]=source;sourceTextures[index]=result;Set(Main,MapFields[index],result);
        RefreshInputTexture(index);DestroyUnusedTexture(old);if(!object.ReferenceEquals(result,original))DestroyUnusedTexture(original);
    }
    static void RestoreInputHistory(ImageState[] sources,int[] modes,bool[] inverses) {
        for(int i=0;i<8;i++){sourceImages[i]=sources[i];InputModes[i]=modes[i];InputInvert[i]=inverses[i];sourceTextures[i]=Get(Main,MapFields[i]) as Texture2D;}
    }
    static void ClearInputSources() {
        smoothnessSourceRoughness=false;
        for(int i=0;i<8;i++){sourceImages[i]=null;sourceTextures[i]=null;InputModes[i]=0;InputInvert[i]=false;SourceFileNames[i]="";}
    }
    static string[] PackInputSources() {
        string[] packed=new string[8];
        for(int i=0;i<8;i++){ImageState working=null;Texture2D t=Get(Main,MapFields[i]) as Texture2D;if(t!=null)imageCache.TryGetValue(t.GetInstanceID(),out working);packed[i]=sourceImages[i]==null?"null":object.ReferenceEquals(sourceImages[i],working)?"same":Convert.ToBase64String(sourceImages[i].png);}
        return packed;
    }
    static void ReadProjectInputs(object project,Texture2D[] maps,out ImageState[] sources,out int[] modes,out bool[] inverses) {
        sources=new ImageState[8];modes=new int[8];inverses=new bool[8];
        int[] savedModes=Get(project,"zhInputModes") as int[];bool[] savedInvert=Get(project,"zhInputInvert") as bool[];string[] savedSources=Get(project,"zhInputSources") as string[];
        bool enhanced=savedModes!=null&&savedModes.Length==8&&savedInvert!=null&&savedInvert.Length==8&&savedSources!=null&&savedSources.Length==8;
        for(int i=0;i<8;i++) {
            sources[i]=SnapshotImage(maps[i]);if(!enhanced)continue;
            modes[i]=savedModes[i];inverses[i]=savedInvert[i];if(modes[i]<0||modes[i]>4)throw new InvalidDataException("Invalid source channel");
            string encoded=savedSources[i];
            if(string.IsNullOrEmpty(encoded)||encoded=="null")sources[i]=null;
            else if(encoded!="same") {
                ImageState source=new ImageState{png=Convert.FromBase64String(encoded),aniso=9,filter=FilterMode.Bilinear,wrap=TextureWrapMode.Repeat,name=""};
                Texture2D check=RestoreImage(source);
                UnityEngine.Object.Destroy(check);
                sources[i]=source;
            }
            if(maps[i]!=null&&sources[i]==null)throw new InvalidDataException("Missing original texture for selected channel");
        }
    }
}
