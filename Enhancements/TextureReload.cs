using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;

public static partial class MaterializeEnhancements {
    static readonly ImageState[] reloadImages=new ImageState[8];
    static readonly bool[] reloadRoughness=new bool[8];
    static readonly string[] reloadPaths=new string[8];
    static readonly Dictionary<string,string> reloadCache=new Dictionary<string,string>();
    static bool reloadCacheRead;
    static string ReloadKey(ImageState image){using(SHA256 hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(image.png));}
    static string ReloadCacheFile {get{return Path.Combine(Application.dataPath,"texture_sources.txt");}}
    static void ReadReloadCache(){
        if(reloadCacheRead)return;reloadCacheRead=true;
        try{if(File.Exists(ReloadCacheFile))foreach(string line in File.ReadAllLines(ReloadCacheFile)){string[] p=line.Split('\t');if(p.Length==2)reloadCache[p[0]]=Encoding.UTF8.GetString(Convert.FromBase64String(p[1]));}}catch(Exception e){Error("Texture source cache",e);}
    }
    static void RememberReload(int index,Texture2D texture,string path){
        ImageState image=SnapshotImage(texture);reloadImages[index]=image;reloadRoughness[index]=index==5&&UseRoughness;reloadPaths[index]=Path.GetFullPath(path);
        ReadReloadCache();reloadCache[ReloadKey(image)]=reloadPaths[index];
        try{List<string> lines=new List<string>();foreach(KeyValuePair<string,string> p in reloadCache)lines.Add(p.Key+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(p.Value)));File.WriteAllLines(ReloadCacheFile,lines.ToArray(),new UTF8Encoding(false));}catch(Exception e){Error("Texture source cache",e);}
    }
    static void ClearReload(int index){reloadImages[index]=null;reloadRoughness[index]=false;reloadPaths[index]=null;}
    static void ClearReloadSources(){for(int i=0;i<8;i++)ClearReload(i);}
    static void PackReloadSources(object po){string[] packed=new string[8];for(int i=0;i<8;i++)packed[i]=reloadImages[i]==null?"null":Convert.ToBase64String(reloadImages[i].png);Set(po,"zhReloadSources",packed);Set(po,"zhReloadRough",(bool[])reloadRoughness.Clone());}
    static void RestoreReloadSources(object po){
        ClearReloadSources();ReadReloadCache();string[] packed=Get(po,"zhReloadSources") as string[];bool[] rough=Get(po,"zhReloadRough") as bool[];
        for(int i=0;i<8;i++){
            if(packed!=null&&packed.Length==8&&!string.IsNullOrEmpty(packed[i])&&packed[i]!="null")reloadImages[i]=new ImageState{png=Convert.FromBase64String(packed[i]),filter=FilterMode.Bilinear,wrap=TextureWrapMode.Repeat,aniso=9,name=SourceFileNames[i]};
            else if(!string.IsNullOrEmpty(SourceFileNames[i]))reloadImages[i]=sourceImages[i];
            reloadRoughness[i]=rough!=null&&rough.Length==8?rough[i]:i==5&&smoothnessSourceRoughness;
            string path;if(reloadImages[i]!=null&&reloadCache.TryGetValue(ReloadKey(reloadImages[i]),out path))reloadPaths[i]=path;
        }
    }
    public static bool ReloadTexture(int index){
        if(index<0||index>=8||reloadImages[index]==null)return false;
        Texture2D raw=null,result=null;
        try{
            if(importing||Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy")))return false;
            Commit();CloseMapEditor();Call(Main,"CloseWindows");bool disk=!string.IsNullOrEmpty(reloadPaths[index])&&File.Exists(reloadPaths[index]);
            raw=disk?ReadTextureFile(reloadPaths[index]):RestoreImage(reloadImages[index]);ImageState image=SnapshotImage(raw);
            bool oldRough=smoothnessSourceRoughness;ImageState oldSource=sourceImages[index];
            sourceImages[index]=image;if(index==5)smoothnessSourceRoughness=reloadRoughness[index];
            try{result=BuildMapInput(index);}catch{sourceImages[index]=oldSource;smoothnessSourceRoughness=oldRough;throw;}
            Texture2D old=Get(Main,MapFields[index]) as Texture2D;Set(Main,MapFields[index],result);sourceTextures[index]=result;
            if(disk){bool meaning=reloadRoughness[index];RememberReload(index,raw,reloadPaths[index]);reloadRoughness[index]=meaning;}else reloadImages[index]=image;RefreshInputTexture(index);Commit();DestroyUnusedTexture(old);result=null;
            Status=disk?T("已从原文件重新加载（保留来源通道，可撤销）","Reloaded from source file; channels preserved; undo available"):T("原文件不可用，已恢复工程内的原始贴图（可撤销）","Source unavailable; restored the project's original texture; undo available");return true;
        }catch(Exception e){Error("Reload texture",e);return false;}finally{DestroyUnusedTexture(raw);DestroyUnusedTexture(result);}
    }
}
