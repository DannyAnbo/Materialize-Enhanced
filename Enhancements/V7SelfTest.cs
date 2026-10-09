using System;
using System.IO;
using System.Collections;
using System.Reflection;
using UnityEngine;
public static class V7SelfTest {
    static object Get(object o,string n){return MaterializeEnhancements.Get(o,n);} static void Set(object o,string n,object v){MaterializeEnhancements.Set(o,n,v);} static object Call(object o,string n,params object[] a){return MaterializeEnhancements.Call(o,n,a);}
    static object Private(string name,params object[] a){return typeof(MaterializeEnhancements).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,a);}
    static Texture2D Map(object main,int index){return Get(main,MaterializeEnhancements.MapFields[index]) as Texture2D;}
    static int Type(object main,int i){return Convert.ToInt32(Enum.Parse(main.GetType().Assembly.GetType("MapType"),new[]{"height","diffuse","diffuseOriginal","normal","metallic","smoothness","edge","ao"}[i]));}
    static Texture2D Pattern(int seed){Texture2D t=new Texture2D(32,32,TextureFormat.RGBA32,false,true);Color32[] p=new Color32[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++)p[y*32+x]=new Color32((byte)(30+(x*13+y*7+seed)%180),(byte)(20+(x*5+y*11+seed)%210),(byte)(40+(x*3+y*9+seed)%170),(byte)(70+(x*7+y*5)%180));t.SetPixels32(p);t.Apply();t.wrapMode=TextureWrapMode.Repeat;return t;}
    static bool Same(Color32[] a,Color32[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))return false;return true;}
    static Color32[] Rt(RenderTexture r){Texture2D t=(Texture2D)Private("ReadRenderTexture",r);Color32[] pixels=t.GetPixels32();UnityEngine.Object.Destroy(t);return pixels;}
    public static void StartOnly(object main,string output){Directory.CreateDirectory(output);((MonoBehaviour)main).StartCoroutine(RunOnly(main,output));}
    static IEnumerator RunOnly(object main,string output){int failures=0;string report="";Action<bool,string> check=delegate(bool okay,string label){report+=(okay?"PASS ":"FAIL ")+label+"\n";if(!okay)failures++;File.WriteAllText(Path.Combine(output,"runtime-tests.txt"),report);};yield return ((MonoBehaviour)main).StartCoroutine(Run(main,output,check));report+="COMPLETE failures="+failures;File.WriteAllText(Path.Combine(output,"runtime-tests.txt"),report);}
    public static IEnumerator Run(object main,string output,Action<bool,string> check){
        string folder=Path.Combine(output,"v7");Directory.CreateDirectory(folder);object sl=Get(main,"SaveLoadProjectScript");
        MaterializeEnhancements.SetSurfaceWorkflow(false);Private("NewProjectNow");
        string input=Path.Combine(folder,"原始 贴图.png");Texture2D pattern=Pattern(0);Color32[] first=pattern.GetPixels32();File.WriteAllBytes(input,pattern.EncodeToPNG());UnityEngine.Object.Destroy(pattern);
        for(int i=0;i<8;i++){
            yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Type(main,i),input));
            check(MaterializeEnhancements.OpenMapEditor(i),"Existing map category "+i+" opens current-map editor");
            check(MaterializeEnhancements.ApplyMapEdit(0,1,1,1,1,false),"Apply editor to category "+i);
            Color32[] same=Map(main,i).GetPixels32();bool exact=Same(first,same);check(exact,"Neutral editing preserves imported map "+i);
            MaterializeEnhancements.OpenMapEditor(i);MaterializeEnhancements.ApplyMapEdit(.5f,1,1,1,1,false);Color32[] edited=Map(main,i).GetPixels32();bool alpha=true;for(int p=0;p<first.Length;p++)alpha&=first[p].a==edited[p].a;
            check(!Same(edited,same)&&alpha&&MaterializeEnhancements.SourceFileNames[i]==Path.GetFileName(input),"Edit changes original content, preserves alpha and filename "+i);
            MaterializeEnhancements.Undo();check(Same(Map(main,i).GetPixels32(),same),"Undo current-map edit "+i);MaterializeEnhancements.Redo();check(Same(Map(main,i).GetPixels32(),edited),"Redo current-map edit "+i);
        }
        MaterializeEnhancements.SetInputChannel(4,2,true);pattern=Pattern(41);Color32[] modified=pattern.GetPixels32();File.WriteAllBytes(input,pattern.EncodeToPNG());UnityEngine.Object.Destroy(pattern);
        check(MaterializeEnhancements.ReloadTexture(4),"Reload reads changed disk file");Color32[] reloaded=Map(main,4).GetPixels32();bool channel=true;for(int i=0;i<modified.Length;i++)channel&=reloaded[i].r==255-modified[i].g&&reloaded[i].g==reloaded[i].r;
        check(channel&&MaterializeEnhancements.InputModes[4]==2&&MaterializeEnhancements.InputInvert[4],"Reload preserves G channel and inversion");
        MaterializeEnhancements.Undo();MaterializeEnhancements.Redo();check(Same(reloaded,Map(main,4).GetPixels32()),"Reload supports undo and redo");
        string project=Path.Combine(folder,"reload-project.mtz");MaterializeEnhancements.SaveProject(sl,project,2);check(!File.ReadAllText(project).Contains(input),"Project embeds reload originals without private absolute source paths");
        Call(sl,"LoadProject",project);yield return new WaitForSeconds(.3f);
        string[] cached=(string[])typeof(MaterializeEnhancements).GetField("reloadPaths",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);check(cached[4]==input,"Reopened project reconnects disk source after its content changed");
        File.Move(input,input+".hidden");Call(sl,"LoadProject",project);yield return new WaitForSeconds(.4f);
        check(MaterializeEnhancements.ReloadTexture(4)&&Same(reloaded,Map(main,4).GetPixels32()),"Missing disk source restores embedded original after project reopen");File.Move(input+".hidden",input);
        string selection=Path.Combine(folder,"selection.mtz");bool[] selected=new bool[9];selected[4]=selected[8]=true;MaterializeEnhancements.ExportSelected(sl,selection,2,selected);yield return new WaitForSeconds(.4f);
        check(Directory.GetFiles(folder,"selection*.png").Length==2&&File.Exists(Path.Combine(folder,"selection_msao.png")),"Export selection writes only metallic and property RGBA maps");
        bool[] none=new bool[9];MaterializeEnhancements.ExportSelected(sl,Path.Combine(folder,"none.mtz"),2,none);yield return null;check(Directory.GetFiles(folder,"none*.png").Length==0,"Empty export selection writes no texture files");
        MaterializeEnhancements.SetTextureResolution(32,32);Private("OpenTiling");yield return null;object gui=Get(main,"TilingTextureMakerGuiScript");yield return null;
        foreach(float value in new[]{.5f,1f,2f,4f}){Set(gui,"Falloff",value);Set(gui,"doStuff",true);MaterializeEnhancements.UpdateTiling(gui);Texture2D t=(Texture2D)Private("ReadRenderTexture",Get(gui,"_DiffuseMapTemp"));File.WriteAllBytes(Path.Combine(folder,"falloff-"+value+".png"),t.EncodeToPNG());UnityEngine.Object.Destroy(t);}
        Set(gui,"Falloff",1f);Set(gui,"doStuff",true);MaterializeEnhancements.UpdateTiling(gui);Color32[] f1=Rt(Get(gui,"_DiffuseMapTemp") as RenderTexture);Set(gui,"Falloff",2f);Set(gui,"doStuff",true);MaterializeEnhancements.UpdateTiling(gui);Color32[] f2=Rt(Get(gui,"_DiffuseMapTemp") as RenderTexture);check(!Same(f1,f2),"GPU edge falloff continues changing pixels above one");
        Set(gui,"OverlapX",1f);Set(gui,"doStuff",true);MaterializeEnhancements.UpdateTiling(gui);Color32[] o1=Rt(Get(gui,"_DiffuseMapTemp") as RenderTexture);Set(gui,"OverlapX",3f);Set(gui,"doStuff",true);MaterializeEnhancements.UpdateTiling(gui);check(!Same(o1,Rt(Get(gui,"_DiffuseMapTemp") as RenderTexture)),"Overlap strength above one changes output without collapsed UVs");
        Set(gui,"techniqueSplat",true);Set(gui,"Falloff",1f);Set(gui,"doStuff",true);MaterializeEnhancements.UpdateTiling(gui);f1=Rt(Get(gui,"_DiffuseMapTemp") as RenderTexture);Set(gui,"Falloff",2f);Set(gui,"doStuff",true);MaterializeEnhancements.UpdateTiling(gui);f2=Rt(Get(gui,"_DiffuseMapTemp") as RenderTexture);check(!Same(f1,f2),"Splat edge falloff changes pixels above one");
        MaterializeEnhancements.SetSurfaceWorkflow(true);check(Get(gui,"_SmoothnessMapTemp")!=null,"Surface map has independent generated tiling preview");
        check(MaterializeEnhancements.ApplyTiling(gui),"Tiling results apply through undoable operation");bool sizes=true;for(int i=0;i<8;i++)sizes&=Map(main,i).width==32&&Map(main,i).height==32;check(sizes,"Applied tiled maps match viewport and export resolution");MaterializeEnhancements.Undo();MaterializeEnhancements.Redo();
        Private("NewProjectNow");yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Type(main,5),input));Private("OpenTiling");yield return null;yield return null;
        check(Map(main,0)==null&&Get(gui,"_SmoothnessMapTemp")!=null,"Roughness-only tiling works without adding a height map");Call(gui,"Close");
        ShaderMaskTest(main,folder,check);
        MaterializeEnhancements.SaveProject(sl,Path.Combine(folder,"startup-project.mtz"),2);
        check(File.Exists(Path.Combine(folder,"startup-project.mtz")),"Startup regression fixture saved");
    }
    static void ShaderMaskTest(object main,string folder,Action<bool,string> check){
        Material mat=new Material(Shader.Find("Hidden/Blit_Shader"));Texture2D source=Pattern(3),blur=Pattern(65),overlay=Pattern(90);RenderTexture rt=RenderTexture.GetTemporary(32,32,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        mat.SetTexture("_MainTex",source);mat.SetTexture("_BlurTex",blur);mat.SetTexture("_AvgTex",overlay);mat.SetFloat("_HotSpot",0);mat.SetFloat("_DarkSpot",0);mat.SetFloat("_LightPow",1);mat.SetFloat("_DarkPow",1);mat.SetFloat("_Saturation",1);mat.SetFloat("_FinalContrast",1);mat.SetFloat("_GamaCorrection",1);
        mat.SetFloat("_LightMaskPow",1);mat.SetFloat("_DarkMaskPow",1);Graphics.Blit(source,rt,mat,11);Color32[] a=Rt(rt);mat.SetFloat("_LightMaskPow",3);mat.SetFloat("_DarkMaskPow",3);Graphics.Blit(source,rt,mat,11);Color32[] b=Rt(rt);check(!Same(a,b),"Diffuse mask power above one changes actual exported shader output");RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(blur);UnityEngine.Object.Destroy(overlay);UnityEngine.Object.Destroy(mat);
    }
    public static IEnumerator CheckStartup(object main,string output){
        yield return new WaitForSeconds(1);bool loaded=MaterializeEnhancements.CurrentProjectPath.Length>0&&Map(main,5)!=null;MaterializeEnhancements.RequestNewProject();yield return null;bool blank=MaterializeEnhancements.CurrentProjectPath=="";for(int i=0;i<9;i++)blank&=Map(main,i)==null;
        File.WriteAllText(output,(loaded&&blank?"PASS":"FAIL")+" command-line project startup then new project; loaded="+loaded+" blank="+blank);Application.Quit();
    }
}
