using System;
using System.IO;
using System.Collections;
using UnityEngine;

public static class ResolutionSelfTest
{
    static object Get(object o,string n){return MaterializeEnhancements.Get(o,n);}
    static object Call(object o,string n,params object[] args){return MaterializeEnhancements.Call(o,n,args);}
    static Texture2D Map(object main,int i){return Get(main,MaterializeEnhancements.MapFields[i]) as Texture2D;}
    static bool AllSize(object main,int width,int height){for(int i=0;i<9;i++)if(Map(main,i)!=null&&(Map(main,i).width!=width||Map(main,i).height!=height))return false;return true;}
    public static IEnumerator Run(object main,string output,Action<bool,string> check) {
        string folder=Path.Combine(output,"resolution");Directory.CreateDirectory(folder);object sl=Get(main,"SaveLoadProjectScript");
        string project=Path.Combine(Path.Combine(Path.Combine(output,"channels"),"portable"),"channels.mtz");Call(sl,"LoadProject",project);yield return new WaitForSeconds(0.2f);
        MaterializeEnhancements.SetInputChannel(4,0,false);Color32[] original=Map(main,4).GetPixels32();File.WriteAllBytes(Path.Combine(folder,"original.png"),Map(main,4).EncodeToPNG());
        Call(main,"SetLoadedTexture",Enum.Parse(main.GetType().Assembly.GetType("MapType"),"metallic"));
        check(MaterializeEnhancements.SetTextureResolution(32,24)&&AllSize(main,32,24),"Changing resolution resizes all eight maps and property map");
        Material sample=Get(main,"SampleMaterial") as Material,full=Get(main,"FullMaterial") as Material;
        check(object.ReferenceEquals(sample.GetTexture("_MainTex"),Map(main,4))&&object.ReferenceEquals(full.GetTexture("_MetallicMap"),Map(main,4)),"Viewport preview and full material use the resized working texture");
        File.WriteAllBytes(Path.Combine(folder,"resized.png"),Map(main,4).EncodeToPNG());
        MaterializeEnhancements.Undo();check(MaterializeEnhancements.TextureWidth==0&&Map(main,4).width==12&&Map(main,4).height==9,"Undo restores native resolution and working image");
        MaterializeEnhancements.Redo();check(MaterializeEnhancements.TextureWidth==32&&AllSize(main,32,24),"Redo restores requested resolution and every map");
        check(!MaterializeEnhancements.SetTextureResolution(-1,32)&&!MaterializeEnhancements.SetTextureResolution(90000,90000)&&AllSize(main,32,24),"Invalid resolutions leave all textures intact");
        MaterializeEnhancements.SetInputChannel(4,4,false);check(Map(main,4).width==32&&Map(main,4).height==24,"Source channel reselection keeps selected working resolution");
        MaterializeEnhancements.SetTextureResolution(0,0);MaterializeEnhancements.SetInputChannel(4,0,false);Color32[] restored=Map(main,4).GetPixels32();bool exact=restored.Length==original.Length;for(int i=0;i<restored.Length&&exact;i++)exact=restored[i].Equals(original[i]);
        check(exact,"Returning to native size preserves original RGBA bytes after resizing and channel changes");
        MaterializeEnhancements.SetTextureResolution(64,40);
        string imported=Path.Combine(Path.Combine(output,"channels"),"合并_RGBA.png");
        yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Convert.ToInt32(Enum.Parse(main.GetType().Assembly.GetType("MapType"),"metallic")),imported));
        check(AllSize(main,64,40),"Import under a selected resolution uses that size in working textures");
        object normal=Get(sl,"normalFromHeightGui");
        // In this opt-in test the generator object has never been opened; run its
        // normal Start initialization before invoking its real rendering pipeline.
        if(Get(normal,"MGS")==null)Call(normal,"Start");
        Call(normal,"InitializeTextures");
        yield return ((MonoBehaviour)main).StartCoroutine((IEnumerator)Call(normal,"ProcessHeight"));
        yield return ((MonoBehaviour)main).StartCoroutine((IEnumerator)Call(normal,"ProcessNormal"));
        check(Map(main,3).width==64&&Map(main,3).height==40,"Actual normal-map generator renders at the selected working resolution");
        foreach(string ext in new string[]{"png","tga","tiff","jpg","bmp"})yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.SaveTexture(sl,ext,Map(main,4),Path.Combine(folder,"export-"+ext)));
        check(File.Exists(Path.Combine(folder,"export-png.png"))&&File.Exists(Path.Combine(folder,"export-tga.tga"))&&File.Exists(Path.Combine(folder,"export-tiff.tiff"))&&File.Exists(Path.Combine(folder,"export-jpg.jpg"))&&File.Exists(Path.Combine(folder,"export-bmp.bmp")),"All five supported export formats are written at selected resolution");
        string saved=Path.Combine(folder,"resolution-project.mtz");MaterializeEnhancements.SaveProject(sl,saved,2);MaterializeEnhancements.SetTextureResolution(16,16);Call(sl,"LoadProject",saved);yield return new WaitForSeconds(0.2f);
        check(MaterializeEnhancements.TextureWidth==64&&MaterializeEnhancements.TextureHeight==40&&AllSize(main,64,40)&&!MaterializeEnhancements.ProjectDirty,"Embedded project restores requested resolution, textures and clean state");
        MaterializeEnhancements.SetTextureResolution(0,0);MaterializeEnhancements.SetInputChannel(4,0,false);restored=Map(main,4).GetPixels32();exact=restored.Length==original.Length;for(int i=0;i<restored.Length&&exact;i++)exact=restored[i].Equals(original[i]);
        check(exact,"Project retains native source bytes independently of resized working maps");
        MaterializeEnhancements.SetTextureResolution(1024,1024);check(AllSize(main,1024,1024),"1K preset uses real 1024 by 1024 working maps");
        MaterializeEnhancements.SetTextureResolution(4096,4096);check(AllSize(main,4096,4096),"4K preset uses real 4096 by 4096 working maps");
        yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.SaveTexture(sl,"png",Map(main,4),Path.Combine(folder,"export-4k")));
        MaterializeEnhancements.SetTextureResolution(0,0);Call(sl,"LoadProject",project);yield return new WaitForSeconds(0.2f);
        check(MaterializeEnhancements.TextureWidth==0&&Map(main,4).width==12,"Earlier projects without size setting retain native dimensions");
    }
}
