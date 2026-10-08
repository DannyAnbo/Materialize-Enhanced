using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

// Opt-in checks inside the actual Unity player, using its texture and coroutine APIs.
public static class EnhanceSelfTest
{
    static StringBuilder report=new StringBuilder();
    static string output;
    static int failures;
    static object Get(object o,string n){return MaterializeEnhancements.Get(o,n);}
    static void Set(object o,string n,object v){MaterializeEnhancements.Set(o,n,v);}
    static object Call(object o,string n,params object[] args){return MaterializeEnhancements.Call(o,n,args);}
    static void Check(bool condition,string label){report.AppendLine((condition?"PASS ":"FAIL ")+label);if(!condition)failures++;File.WriteAllText(Path.Combine(output,"runtime-tests.txt"),report.ToString());}
    public static void Start(object main,string path) {
        output=Path.GetFullPath(path);Directory.CreateDirectory(output);
        MaterializeEnhancements.RecentStorageOverride=Path.Combine(output,"selftest-recent-projects.txt");
        MaterializeEnhancements.ReadRecentProjects();
        ((MonoBehaviour)main).StartCoroutine(Run(main));
    }
    static Texture2D Make(int width,int height,int seed) {
        Texture2D t=new Texture2D(width,height,TextureFormat.RGBA32,false);Color32[] p=new Color32[width*height];
        for(int i=0;i<p.Length;i++)p[i]=new Color32((byte)((i*17+seed)%256),(byte)((i*11+seed)%256),(byte)((i*7+seed)%256),255);
        t.SetPixels32(p);t.Apply();return t;
    }
    static bool Same(Texture2D a,Texture2D b){if(a==null||b==null||a.width!=b.width||a.height!=b.height)return false;Color32[] ap=a.GetPixels32(),bp=b.GetPixels32();for(int i=0;i<ap.Length;i++)if(!ap[i].Equals(bp[i]))return false;return true;}
    static IEnumerator Run(object main) {
        yield return new WaitForSeconds(0.5f);
        object sl=Get(main,"SaveLoadProjectScript");object matGui=Get(sl,"materailGui"),mat=Get(matGui,"MatS");
        try {
            MaterializeEnhancements.Commit();Set(mat,"Metallic",2f);Set(mat,"MetallicText","2");MaterializeEnhancements.Commit();
            MaterializeEnhancements.Undo();Check((float)Get(mat,"Metallic")==1f,"Undo restores previous parameter");
        }catch(Exception e){Check(false,"Undo exception: "+e);}
        yield return new WaitForSeconds(0.4f);
        try {
            MaterializeEnhancements.Redo();Check((float)Get(mat,"Metallic")==2f,"Redo survives subsequent update frames");
            Set(mat,"Smoothness",1.7f);MaterializeEnhancements.Commit();
            MaterializeEnhancements.ResetParameter("MaterialSettings","Metallic");Check((float)Get(mat,"Metallic")==1f&&(float)Get(mat,"Smoothness")==1.7f,"One parameter resets without changing others");
            MaterializeEnhancements.Undo();Check((float)Get(mat,"Metallic")==2f,"Default reset itself is undoable");
            MaterializeEnhancements.Redo();Check((float)Get(mat,"Metallic")==1f,"Redo default reset");
            MaterializeEnhancements.FreeRanges=true;Check(MaterializeEnhancements.ClampInput(2000f,0f,1f)==2000f,"Numeric input accepts values outside range");
            MaterializeEnhancements.FreeRanges=false;Check(MaterializeEnhancements.ClampInput(2f,0f,1f)==1f,"Original range switch");MaterializeEnhancements.FreeRanges=true;
            bool rejects=false;try{MaterializeEnhancements.ValidateNames(new string[]{"same","same","a","b","c","d","e","f"});}catch{rejects=true;}Check(rejects,"Duplicate names rejected");
            Check(MaterializeEnhancements.ProjectBase(Path.Combine(Path.Combine(output,"folder.with.dots"),"asset.v2.mtz")).EndsWith("asset.v2"),"Dots in folder and project name preserved");
        }catch(Exception e){Check(false,"Parameters exception: "+e);}
        Texture2D[] originals=new Texture2D[8];Color32[][] expectedPixels=new Color32[8][];
        for(int i=0;i<8;i++){originals[i]=Make(16,16,20+i*19);expectedPixels[i]=originals[i].GetPixels32();Set(main,MaterializeEnhancements.MapFields[i],originals[i]);}
        Call(main,"SetMaterialValues");
        yield return ((MonoBehaviour)main).StartCoroutine(EditingTests(main,sl,mat));
        for(int i=0;i<8;i++){originals[i]=Make(16,16,20+i*19);Set(main,MaterializeEnhancements.MapFields[i],originals[i]);}
        try {
            MaterializeEnhancements.Alpha=1;Call(main,"ProcessPropertyMap");Texture2D prop=Get(main,"_PropertyMap") as Texture2D;
            Check(prop.format==TextureFormat.RGBA32,"Property map uses RGBA32");Color32[] p=prop.GetPixels32(),h=originals[0].GetPixels32();bool exact=true;
            for(int i=0;i<p.Length;i++)if(p[i].a!=h[i].r)exact=false;Check(exact,"Height copied to alpha for every pixel");
            MaterializeEnhancements.Alpha=6;Call(main,"ProcessPropertyMap");p=((Texture2D)Get(main,"_PropertyMap")).GetPixels32();Color32[] ao=originals[7].GetPixels32(),ed=originals[6].GetPixels32();exact=true;
            for(int i=0;i<p.Length;i++){byte expected=(byte)Mathf.RoundToInt(Mathf.Clamp01(ao[i].r/255f*(ed[i].r/255f+0.5f))*255);if(Math.Abs(p[i].a-expected)>1)exact=false;}Check(exact,"AO plus edge matches shader formula within 8-bit rounding");
            MaterializeEnhancements.Alpha=0;Call(main,"ProcessPropertyMap");p=((Texture2D)Get(main,"_PropertyMap")).GetPixels32();exact=true;for(int i=0;i<p.Length;i++)if(p[i].a!=255)exact=false;Check(exact,"None produces opaque alpha");
            Texture2D small=Make(4,8,99);Set(main,"_HeightMap",small);MaterializeEnhancements.Alpha=1;MaterializeEnhancements.ApplyAlpha(main);Check(((Texture2D)Get(main,"_PropertyMap")).GetPixels32()[255].a<255,"Different texture sizes resample safely");Set(main,"_HeightMap",originals[0]);UnityEngine.Object.Destroy(small);
            MaterializeEnhancements.Alpha=1;Call(main,"ProcessPropertyMap");File.WriteAllBytes(Path.Combine(output,"property_rgba.png"),((Texture2D)Get(main,"_PropertyMap")).EncodeToPNG());
            string[] names={"{project}_高度","{project}_颜色","{project}_原图","{project}_法线","{project}_金属","{project}_平滑","{project}_边缘","{project}_遮蔽"};MaterializeEnhancements.Names=names;
            string folder=Path.Combine(output,"folder.with.dots");Directory.CreateDirectory(folder);MaterializeEnhancements.SaveProject(sl,Path.Combine(folder,"test.v2.mtz"),2);
            Check(File.Exists(Path.Combine(folder,"test.v2.mtz")),"Self-contained project saved");
            Check(Directory.GetFiles(folder,"*.png").Length==0,"Saving embedded project creates no external images");
            MaterializeEnhancements.ExportAll(sl,Path.Combine(folder,"test.v2.mtz"),2);
        }catch(Exception e){Check(false,"Texture exception: "+e);}
        yield return new WaitForSeconds(0.7f);
        string projectFile=Path.Combine(Path.Combine(output,"folder.with.dots"),"test.v2.mtz");
        try {
            string xml=File.ReadAllText(projectFile);Check(xml.Contains("<zhEmbedded>")&&xml.Contains("<zhMapNames>")&&xml.Contains("<zhChannels>"),"Embedded images, names and RGBA selections serialized");
            string[] files=Directory.GetFiles(Path.GetDirectoryName(projectFile),"*.png");Check(files.Length==8,"Exactly eight textures exported");bool named=true;for(int i=0;i<8;i++)if(!File.Exists(Path.Combine(Path.GetDirectoryName(projectFile),MaterializeEnhancements.ExportName("test.v2",MaterializeEnhancements.Names[i])+".png")))named=false;Check(named,"Custom Unicode names used by actual exported files");
            // Move only the mtz into a new directory, excluding all external textures.
            string isolated=Path.Combine(output,"isolated");Directory.CreateDirectory(isolated);File.Copy(projectFile,Path.Combine(isolated,"portable.mtz"),true);
            MaterializeEnhancements.Alpha=0;Call(sl,"LoadProject",Path.Combine(isolated,"portable.mtz"));
        }catch(Exception e){Check(false,"Save/load exception: "+e);}
        yield return new WaitForSeconds(0.8f);
        try {
            bool exact=true;for(int i=0;i<8;i++){Texture2D restored=Get(main,MaterializeEnhancements.MapFields[i]) as Texture2D;if(restored==null || restored.width!=16 || restored.height!=16){exact=false;continue;}Color32[] p=restored.GetPixels32();for(int j=0;j<p.Length;j++)if(!p[j].Equals(expectedPixels[i][j]))exact=false;}
            Check(exact,"All eight textures restored pixel-exactly without external files");Check(MaterializeEnhancements.Alpha==1,"Alpha selection restored from project");
            Texture2D prop=Get(main,"_PropertyMap") as Texture2D;Check(prop!=null&&prop.GetPixels32()[20].a==expectedPixels[0][20].r,"Property map also restored with alpha");
            // Verify replacing an existing project and both native RGBA export formats.
            MaterializeEnhancements.SaveProject(sl,projectFile,2);Check(File.Exists(projectFile)&&!File.Exists(projectFile+".writing"),"Atomic overwrite of an existing project");
            foreach(string extension in new string[]{"png","tga","tiff"}) {
                IEnumerator save=Call(sl,"SaveTexture",extension,prop,Path.Combine(output,"property_rgba_"+extension)) as IEnumerator;
                ((MonoBehaviour)sl).StartCoroutine(save);
            }
        }catch(Exception e){Check(false,"Restore exception: "+e);}
        yield return new WaitForSeconds(0.7f);
        try {
            foreach(string extension in new string[]{"png","tga","tiff"})Check(File.Exists(Path.Combine(output,"property_rgba_"+extension+"."+extension)),"Property file exported: "+extension);
            XmlDocument doc=new XmlDocument();doc.Load(projectFile);
            foreach(string name in new string[]{"zhEmbedded","zhMapNames","zhChannels","zhInputModes","zhInputInvert","zhInputSources"}){XmlNode node=doc.DocumentElement.SelectSingleNode(name);if(node!=null)doc.DocumentElement.RemoveChild(node);}
            string legacy=Path.Combine(Path.GetDirectoryName(projectFile),"legacy.mtz");doc.Save(legacy);Call(sl,"LoadProject",legacy);
        }catch(Exception e){Check(false,"Legacy setup exception: "+e);}
        yield return new WaitForSeconds(1f);
        try {
            bool good=true;for(int i=0;i<8;i++){Texture2D t=Get(main,MaterializeEnhancements.MapFields[i]) as Texture2D;if(t==null || t.width!=16 || t.GetPixels32()[10].r!=expectedPixels[i][10].r)good=false;}Check(good,"Old projects still load external texture files");
            Texture2D big=Make(4096,4096,77),pm=new Texture2D(4096,4096,TextureFormat.RGBA32,false);
            Set(main,"_HeightMap",big);Set(main,"_PropertyMap",pm);MaterializeEnhancements.Alpha=1;
            var watch=System.Diagnostics.Stopwatch.StartNew();MaterializeEnhancements.ApplyAlpha(main);watch.Stop();
            Color32[] p=pm.GetPixels32();Check(p[0].a==77 && p[p.Length-1].a==(byte)(((p.Length-1)*17+77)%256),"4K alpha is correct at first and last pixels");
            report.AppendLine("4K alpha duration milliseconds="+watch.ElapsedMilliseconds);
            UnityEngine.Object.Destroy(big);UnityEngine.Object.Destroy(pm);
            Call(sl,"LoadProject",Path.Combine(Path.Combine(output,"isolated"),"portable.mtz"));
        }catch(Exception e){Check(false,"Legacy/4K exception: "+e);}
        yield return new WaitForSeconds(0.3f);
        yield return ((MonoBehaviour)main).StartCoroutine(ChannelSelfTest.Run(main,output,Check));
        yield return ((MonoBehaviour)main).StartCoroutine(RecentSelfTest.Run(main,output,Check));
        yield return ((MonoBehaviour)main).StartCoroutine(SessionSelfTest.Run(main,output,Check));
        yield return ((MonoBehaviour)main).StartCoroutine(ResolutionSelfTest.Run(main,output,Check));
        report.AppendLine("COMPLETE failures="+failures);File.WriteAllText(Path.Combine(output,"runtime-tests.txt"),report.ToString());
        MaterializeEnhancements.Status="Runtime checks complete: "+failures+" failures";
    }
    static IEnumerator EditingTests(object main,object sl,object mat) {
        object pp=Get(main,"PostProcessGuiScript");
        try {
            MaterializeEnhancements.Commit();Set(mat,"Metallic",2.5f);MaterializeEnhancements.Commit();
            float before=(float)Get(pp,"BloomAmount");Set(pp,"BloomAmount",3.5f);MaterializeEnhancements.Commit();
            MaterializeEnhancements.Undo();Check((float)Get(pp,"BloomAmount")==before&&(float)Get(mat,"Metallic")==2.5f,"Undo spans material and post-process modules");
            MaterializeEnhancements.Redo();Check((float)Get(pp,"BloomAmount")==3.5f,"Post-process redo");
            MaterializeEnhancements.ResetParameter("PostProcessGui","BloomAmount");Check((float)Get(pp,"BloomAmount")==1f&&(float)Get(mat,"Metallic")==2.5f,"Post-process single default preserves material values");
            bool wasEnglish=MaterializeEnhancements.English;Texture2D prior=Get(main,"_DiffuseMap") as Texture2D;
            MaterializeEnhancements.SwitchLanguage(true);Check(MaterializeEnhancements.Translate("保存项目")=="Save Project","Live switch translates cached Chinese labels to English");
            MaterializeEnhancements.SwitchLanguage(false);Check(MaterializeEnhancements.Translate("Save Project")=="保存项目"&&object.ReferenceEquals(prior,Get(main,"_DiffuseMap"))&&(float)Get(mat,"Metallic")==2.5f,"Live language switch preserves textures and edits");MaterializeEnhancements.SwitchLanguage(wasEnglish);
            MaterializeEnhancements.ChooseChannel(0,0);MaterializeEnhancements.ChooseChannel(0,2);MaterializeEnhancements.Undo();Check(Convert.ToInt32(Get(main,"propRed"))==0,"Property R channel selection undo");MaterializeEnhancements.Redo();Check(Convert.ToInt32(Get(main,"propRed"))==2,"Property R channel selection redo");
            MaterializeEnhancements.ChooseChannel(3,0);MaterializeEnhancements.ChooseChannel(3,4);MaterializeEnhancements.Undo();Check(MaterializeEnhancements.Alpha==0,"Property alpha selection undo");
            MaterializeEnhancements.Commit();int format=Convert.ToInt32(Get(main,"selectedFormat"));Call(main,"SetFormat",Enum.ToObject(Get(main,"selectedFormat").GetType(),format==2?3:2));MaterializeEnhancements.Commit();MaterializeEnhancements.Undo();Check(Convert.ToInt32(Get(main,"selectedFormat"))==format,"Export format menu undo");
            bool active=(bool)Get(pp,"EnablePostProcess");Set(pp,"EnablePostProcess",!active);MaterializeEnhancements.Commit();MaterializeEnhancements.Undo();Check((bool)Get(pp,"EnablePostProcess")==active,"Post-process checkbox undo");
            object gui=Get(sl,"metallicGui"),oldSettings=Get(gui,"MS");object replacement=Activator.CreateInstance(oldSettings.GetType());Set(gui,"MS",replacement);Set(replacement,"FinalBias",0.37f);MaterializeEnhancements.Commit();MaterializeEnhancements.Undo();Check((float)Get(Get(gui,"MS"),"FinalBias")!=0.37f,"Replacing module settings does not erase history");
            Check(MaterializeEnhancements.DropReady,"Native Windows file drop receiver installed");
            int[] columns={0,1,1,2,3,4,5,6};bool targets=true;
            for(int i=0;i<8;i++){float x=12+columns[i]*118+(i==1?78:i==2?28:55);if(MaterializeEnhancements.DropTarget(new Vector2(x,95))!=i)targets=false;}
            Check(targets,"Eight separate drop targets including source and edited diffuse");
        }catch(Exception e){Check(false,"Editing feature exception: "+e);}
        string import=Path.Combine(output,"拖入测试.png");Texture2D incoming=Make(12,20,177);File.WriteAllBytes(import,incoming.EncodeToPNG());
        byte oldRed=((Texture2D)Get(main,"_DiffuseMap")).GetPixels32()[10].r;
        yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,1,import));
        try {
            Texture2D imported=(Texture2D)Get(main,"_DiffuseMap");Check(imported.width==12&&imported.height==20,"Unicode image import reaches the requested map slot");
            MaterializeEnhancements.Undo();Check(((Texture2D)Get(main,"_DiffuseMap")).GetPixels32()[10].r==oldRed,"Undo import restores previous texture pixels");
        }catch(Exception e){Check(false,"Import undo exception: "+e);}
        yield return new WaitForSeconds(0.4f);
        try {
            MaterializeEnhancements.Redo();Texture2D imported=(Texture2D)Get(main,"_DiffuseMap");Check(imported.width==12&&Same(imported,incoming),"Import redo survives subsequent frames");
            MaterializeEnhancements.Commit();Call(main,"ClearTexture",Enum.Parse(main.GetType().Assembly.GetType("MapType"),"diffuse"));MaterializeEnhancements.Commit();MaterializeEnhancements.Undo();Check(Same((Texture2D)Get(main,"_DiffuseMap"),incoming)&&Get(main,"_DiffuseMapOriginal")!=null,"Undo clear restores both diffuse maps");
            Texture2D normal=(Texture2D)Get(main,"_NormalMap");byte green=normal.GetPixels32()[10].g;MaterializeEnhancements.Commit();Call(main,"FlipNormalMapY");MaterializeEnhancements.Commit();MaterializeEnhancements.Undo();Check(((Texture2D)Get(main,"_NormalMap")).GetPixels32()[10].g==green,"Undo captures in-place normal-map pixel edits");
            MaterializeEnhancements.Commit();Call(main,"ShowFullMaterial");Set(mat,"LightIntensity",2.2f);MaterializeEnhancements.Commit();
            Call(main,"CloseWindows");((GameObject)Get(main,"MetallicGuiObject")).SetActive(true);Call(Get(sl,"metallicGui"),"NewTexture");
        }catch(Exception e){Check(false,"Texture history exception: "+e);}
        yield return new WaitForSeconds(0.5f);
        try{MaterializeEnhancements.Undo();Check((float)Get(mat,"LightIntensity")!=2.2f,"Opening another module keeps prior parameter history");Call(main,"CloseWindows");}catch(Exception e){Check(false,"Module history exception: "+e);}
        foreach(string ext in new string[]{"tga","tiff","bmp"}) {
            string name=Path.Combine(output,"导入_"+ext);IEnumerator save=Call(sl,"SaveTexture",ext,incoming,name) as IEnumerator;if(save!=null)yield return ((MonoBehaviour)sl).StartCoroutine(save);
            yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,3,name+"."+ext));
            Check(Same((Texture2D)Get(main,"_MetallicMap"),incoming),"Unicode "+ext+" import reaches metallic slot pixel-exactly");
        }
        int beforeSteps=Convert.ToInt32(Get(main,"selectedCubemap"));MaterializeEnhancements.Commit();Array cubes=(Array)Get(main,"CubeMaps");Set(main,"selectedCubemap",(beforeSteps+1)%cubes.Length);MaterializeEnhancements.Commit();MaterializeEnhancements.Undo();Check(Convert.ToInt32(Get(main,"selectedCubemap"))==beforeSteps,"Cubemap menu undo");
        MaterializeEnhancements.Commit();Call(main,"ClearAllTextures");MaterializeEnhancements.Commit();MaterializeEnhancements.Undo();bool all=true;for(int i=0;i<8;i++)all&=Get(main,MaterializeEnhancements.MapFields[i])!=null;Check(all,"Undo clear all restores eight texture slots");
        object fb=Get(main,"fileBrowser");System.Reflection.MethodInfo browse=typeof(MaterializeEnhancements).GetMethod("Browse",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        browse.Invoke(null,new object[]{"Save Project","SaveProject",true});Check(!(bool)Get(fb,"inputMustExist")&&(bool)Get(fb,"showTextInput")&&(bool)Get(fb,"overwriteWarn"),"Save dialog accepts new filenames and warns before overwriting");Call(fb,"Close");
        browse.Invoke(null,new object[]{"Load Project","LoadProject",true});Check((bool)Get(fb,"inputMustExist")&&!(bool)Get(fb,"overwriteWarn"),"Load dialog requires an existing project");Call(fb,"Close");
        string cancelStatus=MaterializeEnhancements.Status;int cancelFiles=Directory.GetFiles(output).Length;
        MaterializeEnhancements.SaveProject(sl,null,2);MaterializeEnhancements.ExportAll(sl,null,2);
        Check(MaterializeEnhancements.Status==cancelStatus&&Directory.GetFiles(output).Length==cancelFiles,"Cancel save or export does not write files or report an error");
        string dotted=Path.Combine(output,"single.v2.png");MaterializeEnhancements.SaveFile(sl,dotted,2,incoming,"");Check(File.Exists(dotted)&&!File.Exists(Path.Combine(output,"single.png")),"Single-map save preserves dots in asset names");
        MaterializeEnhancements.Commit();bool plane=(bool)Get(Get(sl,"materailGui"),"planeShown");Set(Get(sl,"materailGui"),"planeShown",!plane);Set(Get(sl,"materailGui"),"cubeShown",plane);Shader.EnableKeyword("TOP_PROJECTION");MaterializeEnhancements.Commit();MaterializeEnhancements.Undo();Check((bool)Get(Get(sl,"materailGui"),"planeShown")==plane&&Shader.IsKeywordEnabled("TOP_PROJECTION")==!plane,"Preview shape undo restores its shader projection mode");
    }
}
