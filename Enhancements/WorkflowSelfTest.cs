using System;
using System.IO;
using System.Collections;
using System.Reflection;
using System.Xml;
using UnityEngine;

public static class WorkflowSelfTest
{
    static object Get(object o,string n){return MaterializeEnhancements.Get(o,n);}
    static void Set(object o,string n,object v){MaterializeEnhancements.Set(o,n,v);}
    static object Call(object o,string n,params object[] a){return MaterializeEnhancements.Call(o,n,a);}
    static Texture2D Map(object main,int i){return Get(main,MaterializeEnhancements.MapFields[i]) as Texture2D;}
    static bool Pixels(Texture2D map,Color32[] raw,int channel,bool invert) {
        if(map==null)return false;Color32[] p=map.GetPixels32();if(p.Length!=raw.Length)return false;
        for(int i=0;i<p.Length;i++){Color32 e=raw[i];if(channel>0){byte v=channel==1?e.r:channel==2?e.g:channel==3?e.b:e.a;e=new Color32(v,v,v,255);}if(invert)e=new Color32((byte)(255-e.r),(byte)(255-e.g),(byte)(255-e.b),e.a);if(!p[i].Equals(e))return false;}return true;
    }
    static bool Empty(object main){for(int i=0;i<9;i++)if(Map(main,i)!=null)return false;return true;}
    static int Steps(){return ((IList)typeof(MaterializeEnhancements).GetField("History",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)).Count;}
    public static IEnumerator Run(object main,string output,Action<bool,string> check) {
        string folder=Path.Combine(output,"workflow");Directory.CreateDirectory(folder);
        object sl=Get(main,"SaveLoadProjectScript"),fb=Get(main,"fileBrowser");
        string old=Path.Combine(Path.Combine(Path.Combine(output,"channels"),"portable"),"channels.mtz");
        MaterializeEnhancements.SetSurfaceWorkflow(true);Call(sl,"LoadProject",old);yield return new WaitForSeconds(0.3f);
        check(MaterializeEnhancements.UseRoughness&&!MaterializeEnhancements.ProjectDirty,"Opening an earlier project preserves global roughness preference and clean state");
        Color32[] prior=Map(main,5).GetPixels32();int steps=Steps();
        MaterializeEnhancements.SetSurfaceWorkflow(false);MaterializeEnhancements.SetSurfaceWorkflow(true);
        check(Pixels(Map(main,5),prior,0,false)&&!MaterializeEnhancements.ProjectDirty&&Steps()==steps,"Global workflow switch preserves canonical data without adding project history or unsaved changes");
        MaterializeEnhancements.DefaultRoughness=false;MaterializeEnhancements.ReadConfig();
        check(MaterializeEnhancements.UseRoughness,"Roughness application preference round-trips through local config");
        MaterializeEnhancements.RequestNewProject();yield return null;
        check(Empty(main)&&MaterializeEnhancements.CurrentProjectPath==""&&!MaterializeEnhancements.ProjectDirty&&MaterializeEnhancements.UseRoughness,"Clean new project clears maps and path while preserving global roughness preference");
        check(MaterializeEnhancements.TextureWidth==0&&Steps()==1&&MaterializeEnhancements.InputModes[5]==0&&!MaterializeEnhancements.InputInvert[5],"New project resets working size, source channel selections and undo history");
        Texture2D packed=new Texture2D(4,2,TextureFormat.RGBA32,false);
        Color32[] raw={new Color32(0,32,240,17),new Color32(64,96,192,63),new Color32(128,160,128,128),new Color32(255,224,0,255),new Color32(20,50,80,110),new Color32(33,77,111,222),new Color32(90,45,210,5),new Color32(199,100,2,254)};
        packed.SetPixels32(raw);packed.Apply();string input=Path.Combine(folder,"packed-input.png");File.WriteAllBytes(input,packed.EncodeToPNG());UnityEngine.Object.Destroy(packed);
        int type=Convert.ToInt32(Enum.Parse(main.GetType().Assembly.GetType("MapType"),"smoothness"));
        yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,type,input));
        check(Pixels(Map(main,5),raw,0,true),"Roughness import converts RGB to canonical smoothness and preserves alpha");
        check(Pixels(MaterializeEnhancements.WorkflowTexture(Map(main,5)),raw,0,false),"Roughness preview reproduces original input bytes");
        Material full=Get(main,"FullMaterial") as Material,sample=Get(main,"SampleMaterial") as Material;
        check(object.ReferenceEquals(full.GetTexture("_SmoothnessMap"),Map(main,5))&&object.ReferenceEquals(sample.GetTexture("_MainTex"),MaterializeEnhancements.WorkflowTexture(Map(main,5))),"Full material uses smoothness while texture viewport displays roughness");
        for(int c=1;c<=4;c++){MaterializeEnhancements.SetInputChannel(5,c,false);check(Pixels(Map(main,5),raw,c,true),"Packed roughness channel "+c+" uses original source with automatic conversion");}
        MaterializeEnhancements.SetInputChannel(5,2,true);check(Pixels(Map(main,5),raw,2,false),"User inversion composes correctly with roughness conversion");
        MaterializeEnhancements.SetInputChannel(5,0,false);
        foreach(string ext in new string[]{"png","tga","tiff","jpg","bmp"})yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.SaveTexture(sl,ext,Map(main,5),Path.Combine(folder,"roughness-"+ext)));
        check(File.Exists(Path.Combine(folder,"roughness-png.png"))&&File.Exists(Path.Combine(folder,"roughness-tiff.tiff")),"Roughness exports are written through the actual export pipeline");
        for(int c=0;c<4;c++)MaterializeEnhancements.ChooseChannel(c,3);
        Color32[] prop=Map(main,8).GetPixels32();bool exact=true;for(int i=0;i<prop.Length;i++)if(prop[i].r!=raw[i].r||prop[i].g!=raw[i].r||prop[i].b!=raw[i].r||prop[i].a!=raw[i].r)exact=false;
        check(exact,"Property map RGBA channels pack roughness values in roughness mode");
        File.WriteAllBytes(Path.Combine(folder,"property-roughness.png"),Map(main,8).EncodeToPNG());
        string saved=Path.Combine(folder,"roughness-project.mtz");MaterializeEnhancements.SaveProject(sl,saved,2);
        check(File.ReadAllText(saved).Contains("<zhSourceRoughness>true</zhSourceRoughness>")&&!File.ReadAllText(saved).Contains("<zhRoughness>"),"Project stores source interpretation without storing the application workflow setting");
        MaterializeEnhancements.SetSurfaceWorkflow(false);Call(sl,"LoadProject",saved);yield return new WaitForSeconds(0.3f);
        check(!MaterializeEnhancements.UseRoughness&&Pixels(Map(main,5),raw,0,true)&&!MaterializeEnhancements.ProjectDirty,"Loading a roughness-source project keeps the software's smoothness setting");
        prop=Map(main,8).GetPixels32();check(prop[0].r==255-raw[0].r&&prop[0].a==255-raw[0].r,"Loading regenerates property channels for the current global workflow");
        MaterializeEnhancements.SetInputChannel(5,4,false);MaterializeEnhancements.SetSurfaceWorkflow(true);MaterializeEnhancements.Undo();
        check(MaterializeEnhancements.UseRoughness&&MaterializeEnhancements.InputModes[5]==0&&Pixels(Map(main,5),raw,0,true),"Undo source selection keeps global roughness preference");
        MaterializeEnhancements.Redo();check(MaterializeEnhancements.UseRoughness&&Pixels(Map(main,5),raw,4,true),"Redo source selection keeps global roughness preference and source metadata");
        MaterializeEnhancements.SetTextureResolution(8,4);MaterializeEnhancements.SetTextureResolution(0,0);MaterializeEnhancements.SetInputChannel(5,0,false);
        check(Pixels(Map(main,5),raw,0,true),"Resize and return to source size do not double-invert roughness inputs");
        MaterializeEnhancements.Names[5]="_smoothness";MaterializeEnhancements.ExportAll(sl,Path.Combine(folder,"named.mtz"),2);yield return new WaitForSeconds(0.3f);
        check(File.Exists(Path.Combine(folder,"named_roughness.png")),"Default export suffix follows application roughness setting");
        MaterializeEnhancements.Names[5]="custom-surface";MaterializeEnhancements.SetSurfaceWorkflow(false);MaterializeEnhancements.SetSurfaceWorkflow(true);
        check(MaterializeEnhancements.Names[5]=="custom-surface","Workflow changes preserve custom texture names");
        MaterializeEnhancements.Names[5]="_smoothness";
        yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Convert.ToInt32(Enum.Parse(main.GetType().Assembly.GetType("MapType"),"diffuseOriginal")),input));
        object generator=Get(sl,"SmoothnessGui");if(Get(generator,"blitSmoothnessMaterial")==null)Call(generator,"Start");Call(generator,"InitializeTextures");
        yield return ((MonoBehaviour)main).StartCoroutine((IEnumerator)Call(generator,"ProcessBlur"));
        yield return ((MonoBehaviour)main).StartCoroutine((IEnumerator)Call(generator,"ProcessSmoothness"));
        MaterializeEnhancements.Commit();Color32[] actualGenerated=Map(main,5).GetPixels32();
        check(Pixels(MaterializeEnhancements.WorkflowTexture(Map(main,5)),actualGenerated,0,true),"Actual smoothness generator displays inverted roughness without changing shader data");
        File.WriteAllBytes(Path.Combine(folder,"generated-canonical.png"),Map(main,5).EncodeToPNG());
        yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.SaveTexture(sl,"png",Map(main,5),Path.Combine(folder,"generated-roughness")));
        MaterializeEnhancements.SetInputChannel(5,1,false);check(Pixels(Map(main,5),actualGenerated,1,false),"Actual generator output keeps canonical source interpretation after channel extraction");
        // A generator replaces the canonical map. Its output is always smoothness.
        Texture2D generated=new Texture2D(4,2,TextureFormat.RGBA32,false);generated.SetPixels32(raw);generated.Apply();Set(main,"_SmoothnessMap",generated);MaterializeEnhancements.Commit();
        MaterializeEnhancements.SetInputChannel(5,1,false);
        check(Pixels(Map(main,5),raw,1,false)&&Pixels(MaterializeEnhancements.WorkflowTexture(Map(main,5)),raw,1,true),"Generated smoothness is adopted without roughness-source conversion");
        MaterializeEnhancements.RequestNewProject();check(MaterializeEnhancements.ClosePromptVisible&&!Empty(main),"New on modified project shows save-discard-cancel warning");
        MaterializeEnhancements.CancelClose();check(!MaterializeEnhancements.ClosePromptVisible&&!Empty(main),"Cancel new preserves current textures and unsaved modifications");
        MaterializeEnhancements.RequestNewProject();MaterializeEnhancements.DiscardAndClose();yield return new WaitForSeconds(0.3f);
        check(Empty(main)&&!MaterializeEnhancements.ProjectDirty&&MaterializeEnhancements.CurrentProjectPath==""&&MaterializeEnhancements.UseRoughness,"Discard and New resets project without quitting or changing global setting");
        bool paths=true;foreach(string f in new string[]{"QuicksavePathHeight","QuicksavePathDiffuse","QuicksavePathNormal","QuicksavePathMetallic","QuicksavePathSmoothness","QuicksavePathEdge","QuicksavePathAO","QuicksavePathProperty","QuicksavePath"})paths&=string.IsNullOrEmpty(Get(main,f) as string);
        check(paths&&Get(sl,"thisProject")==null,"New project clears quick-save paths and previous project object");
        object mat=Get(Get(sl,"materailGui"),"MatS");float defaultMetal=(float)Get(mat,"Metallic");Set(mat,"Metallic",defaultMetal+0.3f);MaterializeEnhancements.Commit();
        MaterializeEnhancements.RequestNewProject();MaterializeEnhancements.SaveAndClose();check((bool)Get(fb,"isActive"),"Save and New on untitled project opens save dialog before clearing anything");
        Call(main,"SaveProject",(object)null);Call(fb,"Close");yield return null;
        check(MaterializeEnhancements.ProjectDirty&&Math.Abs((float)Get(mat,"Metallic")-(defaultMetal+0.3f))<0.00001f&&MaterializeEnhancements.ClosePromptVisible,"Cancelled save keeps modified project and new-project warning");MaterializeEnhancements.CancelClose();
        string beforeNew=Path.Combine(folder,"save-before-new.mtz");MaterializeEnhancements.SaveProject(sl,beforeNew,2);Set(mat,"Metallic",defaultMetal+0.5f);MaterializeEnhancements.Commit();
        MaterializeEnhancements.RequestNewProject();MaterializeEnhancements.SaveAndClose();yield return new WaitForSeconds(0.3f);
        check(File.Exists(beforeNew)&&!MaterializeEnhancements.ProjectDirty&&MaterializeEnhancements.CurrentProjectPath==""&&(float)Get(mat,"Metallic")==defaultMetal,"Save and New writes previous changes then restores empty project defaults");
        Set(mat,"Metallic",defaultMetal+0.7f);MaterializeEnhancements.Commit();MaterializeEnhancements.CurrentProjectPath=Path.Combine(folder,"missing-directory/fail.mtz");MaterializeEnhancements.RequestNewProject();MaterializeEnhancements.SaveAndClose();yield return null;
        check(MaterializeEnhancements.ProjectDirty&&MaterializeEnhancements.ClosePromptVisible&&Math.Abs((float)Get(mat,"Metallic")-(defaultMetal+0.7f))<0.00001f,"Failed save before New preserves project and does not queue reset");MaterializeEnhancements.CancelClose();MaterializeEnhancements.CurrentProjectPath="";
        MaterializeEnhancements.RequestNewProject();MaterializeEnhancements.DiscardAndClose();
        MaterializeEnhancements.SetSurfaceWorkflow(false);MaterializeEnhancements.SaveConfig();
        check(!MaterializeEnhancements.UseRoughness&&!MaterializeEnhancements.ProjectDirty,"Global smoothness preference also leaves empty project clean");
    }
}
