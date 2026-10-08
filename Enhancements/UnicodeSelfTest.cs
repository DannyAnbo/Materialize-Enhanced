using System;
using System.IO;
using System.Collections;
using System.Xml;
using UnityEngine;

public static class UnicodeSelfTest
{
    static object Get(object o,string n){return MaterializeEnhancements.Get(o,n);}
    static object Call(object o,string n,params object[] a){return MaterializeEnhancements.Call(o,n,a);}
    static Texture2D Map(object main,int i){return Get(main,MaterializeEnhancements.MapFields[i]) as Texture2D;}
    static bool Same(Texture2D a,Texture2D b){if(a==null||b==null||a.width!=b.width||a.height!=b.height)return false;Color32[] p=a.GetPixels32(),q=b.GetPixels32();for(int i=0;i<p.Length;i++)if(!p[i].Equals(q[i]))return false;return true;}
    public static IEnumerator Run(object main,string output,Action<bool,string> check) {
        object sl=Get(main,"SaveLoadProjectScript");string folder=Path.Combine(Path.Combine(output,"unicode"),"中文 文件夹");Directory.CreateDirectory(folder);
        Texture2D original=new Texture2D(12,8,TextureFormat.RGBA32,false);Color32[] p=new Color32[96];for(int i=0;i<p.Length;i++)p[i]=new Color32((byte)(i*7),(byte)(i*13),(byte)(i*19),(byte)(i*23));original.SetPixels32(p);original.Apply();
        foreach(string ext in new string[]{"png","jpg","tga","bmp","tiff"}) {
            string file=Path.Combine(folder,"木板 合并通道."+ext);yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.SaveTexture(sl,ext,original,file.Substring(0,file.Length-ext.Length-1)));
            check(File.Exists(file),"Export supports Chinese directory and file name: "+ext);
            yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Convert.ToInt32(Enum.Parse(main.GetType().Assembly.GetType("MapType"),"metallic")),file));
            Texture2D expected=original;
            if(ext=="jpg"){expected=new Texture2D(2,2,TextureFormat.RGBA32,false);expected.LoadImage(File.ReadAllBytes(file));}
            check(Same(Map(main,4),expected),"Chinese path imports actual pixels: "+ext);if(!object.ReferenceEquals(expected,original))UnityEngine.Object.Destroy(expected);
        }
        string tiff=Path.Combine(folder,"木板 合并通道.tiff"),tif=Path.Combine(folder,"混合 通道.tif"),jpg=Path.Combine(folder,"木板 合并通道.jpg"),jpeg=Path.Combine(folder,"混合 颜色.jpeg");File.Copy(tiff,tif,true);File.Copy(jpg,jpeg,true);
        foreach(string file in new string[]{tif,jpeg}) {
            MaterializeEnhancements.Set(main,"mapTypeToLoad",Enum.Parse(main.GetType().Assembly.GetType("MapType"),"normal"));Call(main,"OpenFile",file);yield return null;
            check(Map(main,3)!=null&&Map(main,3).width==12,"Open File callback supports Chinese path and extension alias: "+Path.GetExtension(file));
        }
        MaterializeEnhancements.HandleDrop(new string[]{Path.Combine(folder,"木板 合并通道.tga")},new Vector2(55,95));yield return null;
        check(Same(Map(main,0),original),"Drag import route accepts Chinese TGA path for height category");
        check(MaterializeEnhancements.SourceFileNames[0]=="木板 合并通道.tga"&&MaterializeEnhancements.SourceFileNames[4]=="木板 合并通道.tiff","Imported file names are recorded independently for each texture category");
        MaterializeEnhancements.Names[4]="{project}_金属度";string project=Path.Combine(folder,"中文 工程.mtz");MaterializeEnhancements.SaveProject(sl,project,3);MaterializeEnhancements.ExportAll(sl,project,3);yield return new WaitForSeconds(0.3f);
        check(File.Exists(Path.Combine(folder,"中文 工程_金属度.tga")),"Custom Chinese texture names export inside a Chinese project directory");
        XmlDocument doc=new XmlDocument();doc.Load(project);foreach(string field in new string[]{"zhEmbedded","zhInputModes","zhInputInvert","zhInputSources","zhSourceRoughness"}){XmlNode n=doc.DocumentElement.SelectSingleNode(field);if(n!=null)doc.DocumentElement.RemoveChild(n);}string legacy=Path.Combine(folder,"旧版 外置工程.mtz");doc.Save(legacy);
        MaterializeEnhancements.SetSurfaceWorkflow(true);Call(sl,"LoadProject",legacy);yield return new WaitForSeconds(0.4f);
        check(Same(Map(main,4),original)&&MaterializeEnhancements.UseRoughness,"Legacy project loads external Chinese TGA textures while retaining application workflow");
        check(Directory.GetFiles(folder,"*.writing").Length==0&&File.Exists(tiff)&&File.Exists(tif),"Unicode conversion preserves original source files and leaves no staged files");
        MaterializeEnhancements.SetSurfaceWorkflow(false);Call(sl,"LoadProject",project);yield return new WaitForSeconds(0.3f);
        check(Same(Map(main,4),original)&&!MaterializeEnhancements.ProjectDirty,"Embedded Chinese project reload restores texture pixels and clean state");
        check(MaterializeEnhancements.SourceFileNames[4]=="木板 合并通道.tiff"&&MaterializeEnhancements.SourceFileNames[0]=="木板 合并通道.tga","Project round-trip retains Chinese source names without storing full paths");
        string replacement=Path.Combine(folder,"另一张_金属度.png");File.Copy(Path.Combine(folder,"木板 合并通道.png"),replacement,true);
        yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Convert.ToInt32(Enum.Parse(main.GetType().Assembly.GetType("MapType"),"metallic")),replacement));
        check(MaterializeEnhancements.SourceFileNames[4]=="另一张_金属度.png","Replacing a texture updates its displayed file name");
        MaterializeEnhancements.Undo();check(MaterializeEnhancements.SourceFileNames[4]=="木板 合并通道.tiff","Undo import restores the previous file name");
        MaterializeEnhancements.Redo();check(MaterializeEnhancements.SourceFileNames[4]=="另一张_金属度.png","Redo import restores the new file name");
        MaterializeEnhancements.SetInputChannel(4,2,false);MaterializeEnhancements.SetTextureResolution(24,16);
        check(MaterializeEnhancements.SourceFileNames[4]=="另一张_金属度.png","Channel extraction and resize retain the original source file name");
        MaterializeEnhancements.Commit();Call(main,"ClearTexture",Enum.Parse(main.GetType().Assembly.GetType("MapType"),"metallic"));MaterializeEnhancements.Commit();
        check(string.IsNullOrEmpty(MaterializeEnhancements.SourceFileNames[4]),"Clearing a texture removes its stale file name");
        MaterializeEnhancements.Undo();check(MaterializeEnhancements.SourceFileNames[4]=="另一张_金属度.png","Undo clear restores texture and file name together");
        MaterializeEnhancements.RequestNewProject();MaterializeEnhancements.DiscardAndClose();bool blank=true;foreach(string name in MaterializeEnhancements.SourceFileNames)blank&=string.IsNullOrEmpty(name);
        check(blank&&!MaterializeEnhancements.ProjectDirty,"New project removes all previous source file names");
        Call(sl,"LoadProject",project);yield return new WaitForSeconds(0.3f);
        UnityEngine.Object.Destroy(original);
    }
}
