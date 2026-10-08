using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Xml.Serialization;
using UnityEngine;

// Compiled against the installed Unity 2017 assemblies. No assembly cloning.
public static partial class MaterializeEnhancements
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    public static object Main;
    public static int Alpha;
    public static bool FreeRanges=true;
    public static bool English;
    public static string Status="";
    public static string[] Names={"_height","_diffuse","_diffuseOriginal","_normal","_metallic","_smoothness","_edge","_ao"};
    public static readonly string[] MapFields={"_HeightMap","_DiffuseMap","_DiffuseMapOriginal","_NormalMap","_MetallicMap","_SmoothnessMap","_EdgeMap","_AOMap","_PropertyMap"};
    static readonly string[] GuiFields={"heightFromDiffuseGui","editDiffuseGui","normalFromHeightGui","metallicGui","SmoothnessGui","edgeFromNormalGui","aoFromNormalGui","materailGui"};
    static readonly string[] SettingFields={"HFDS","EDS","NFHS","MS","SS","ES","AOS","MatS"};
    static readonly string[] LabelsZh={"高度","漫反射","原始漫反射","法线","金属度","平滑度","边缘","AO"};
    static readonly string[] LabelsEn={"Height","Diffuse","Original diffuse","Normal","Metallic","Smoothness","Edge","AO"};
    static readonly string[] AlphaZh={"无（不透明）","高度","金属度","平滑度","边缘","AO","AO + 边缘"};
    static readonly string[] AlphaEn={"None (opaque)","Height","Metallic","Smoothness","Edge","AO","AO + Edge"};
    static readonly Dictionary<string,FieldInfo> Fields=new Dictionary<string,FieldInfo>();
    static readonly Dictionary<int,Vector2> Ranges=new Dictionary<int,Vector2>();
    static object[] guis,settings;
    static bool started,cliDone,windowOpen;
    static float changedAt;
    static Rect window=new Rect(1180,65,520,600);
    static string[] editNames;
    static string configPath { get {return Path.Combine(Application.dataPath,"zh_config.txt");} }
    static string T(string zh,string en){return English?en:zh;}
    public static object Get(object obj,string name) {
        if(obj==null)return null;
        string key=obj.GetType().FullName+"|"+name;
        FieldInfo f;
        if(!Fields.TryGetValue(key,out f)){f=obj.GetType().GetField(name,Flags);Fields[key]=f;}
        return f==null?null:f.GetValue(obj);
    }
    public static void Set(object obj,string name,object value) {
        if(obj==null)return;
        Get(obj,name);FieldInfo f=Fields[obj.GetType().FullName+"|"+name];if(f!=null)f.SetValue(obj,value);
    }
    public static object Call(object obj,string name,params object[] args) {
        if(obj==null)return null;
        foreach(MethodInfo m in obj.GetType().GetMethods(Flags))if(m.Name==name && m.GetParameters().Length==args.Length) {
            ParameterInfo[] ps=m.GetParameters();bool matches=true;
            for(int i=0;i<ps.Length;i++)if(args[i]==null?ps[i].ParameterType.IsValueType:!ps[i].ParameterType.IsInstanceOfType(args[i])){matches=false;break;}
            if(matches)return m.Invoke(obj,args);
        }
        return null;
    }
    static void Error(string context,Exception ex) {
        while(ex.InnerException!=null)ex=ex.InnerException;
        Status=context+": "+ex.Message;Debug.LogError("MaterializeEnhancements "+Status+"\n"+ex);
        if(errorReports.Add(Status))try{File.AppendAllText(Path.Combine(Application.dataPath,"enhance-errors.log"),DateTime.Now+" "+Status+"\n"+ex+"\n");}catch{}
    }
    static readonly HashSet<string> errorReports=new HashSet<string>();
    public static void ReadConfig() {
        try {
            if(!File.Exists(configPath))return;
            foreach(string line in File.ReadAllLines(configPath)) {
                int eq=line.IndexOf('=');if(eq<1)continue;
                string k=line.Substring(0,eq).Trim(),v=line.Substring(eq+1).Trim();int n;
                if(k=="alpha" && int.TryParse(v,out n))Alpha=Mathf.Clamp(n,0,6);
                else if(k=="freeRanges")FreeRanges=v!="0";
                else if(k=="language")English=v=="en";
                else if(k=="defaultSurface")DefaultRoughness=v=="roughness";
                else if(k.StartsWith("name") && int.TryParse(k.Substring(4),out n) && n>=0 && n<8 && v.Length>0)Names[n]=v;
            }
        }catch(Exception e){Error("Config",e);}
    }
    public static void SaveConfig() {
        try {
            ValidateNames(Names);
            StringBuilder b=new StringBuilder();b.AppendLine("alpha="+Alpha);b.AppendLine("freeRanges="+(FreeRanges?"1":"0"));b.AppendLine("language="+(English?"en":"zh"));
            b.AppendLine("defaultSurface="+(DefaultRoughness?"roughness":"smoothness"));
            for(int i=0;i<8;i++)b.AppendLine("name"+i+"="+Names[i]);
            File.WriteAllText(configPath,b.ToString(),new UTF8Encoding(false));Status=T("配置已保存，立即生效","Settings saved and applied");
        }catch(Exception e){Error("Config",e);}
    }
    public static string ExportName(string project,string name) {
        string result=name.StartsWith("_")?project+name:name.Replace("{project}",project);
        if(result.Length==0 || result.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || result.EndsWith(".") || result.EndsWith(" "))throw new ArgumentException("Invalid filename: "+result);
        string stem=result.Split('.')[0].ToUpperInvariant();
        if(stem=="CON" || stem=="PRN" || stem=="AUX" || stem=="NUL" || (stem.Length==4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3]>='1' && stem[3]<='9'))throw new ArgumentException("Reserved filename: "+result);
        return result;
    }
    public static void ValidateNames(string[] names) {
        if(names==null || names.Length!=8)throw new ArgumentException("Eight texture names required");
        HashSet<string> seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int i=0;i<8;i++)if(!seen.Add(ExportName("project",names[i].Trim())))throw new ArgumentException(T("贴图名称不能重复","Texture names must be unique"));
    }
    static bool Bind() {
        object sl=Get(Main,"SaveLoadProjectScript");if(sl==null)return false;
        object[] newGuis=new object[8],newSettings=new object[8];
        for(int i=0;i<8;i++) {
            newGuis[i]=Get(sl,GuiFields[i]);if(newGuis[i]==null)return false;
            Call(newGuis[i],"InitializeSettings");newSettings[i]=Get(newGuis[i],SettingFields[i]);if(newSettings[i]==null)return false;
        }
        guis=newGuis;settings=newSettings;
        if(History.Count==0)ResetHistory();return true;
    }
    public static void Tick(object main) {
        try {
            if(quitting)return;
            Main=main;
            if(!started){started=true;ReadConfig();ReadRecentProjects();}
            if(Time.time<0.5f || !Bind())return;
            if(!cliDone && Time.time>1f) {
                cliDone=true;
                string[] args=Environment.GetCommandLineArgs();
                for(int i=1;i<args.Length;i++) {
                    if(args[i]=="--enhance-selftest" && i+1<args.Length){EnhanceSelfTest.Start(main,args[i+1]);break;}
                    if(args[i].EndsWith(".mtz",StringComparison.OrdinalIgnoreCase) && File.Exists(args[i])) {Call(Get(main,"SaveLoadProjectScript"),"LoadProject",Path.GetFullPath(args[i]));break;}
                }
            }
            PollDrops();
            NormalizeGeneratedMaps();
            TrackHistory();
            RefreshWorkflowPreview();
            object browser=Get(main,"fileBrowser");
            bool browsing=browser!=null&&Convert.ToBoolean(Get(browser,"isActive"));
            SessionTick(browsing);
        }catch(Exception e){Error("Update",e);}
    }
    public static float ClampInput(float value,float left,float right) {
        if(float.IsNaN(value)||float.IsInfinity(value))return left;
        return FreeRanges?value:Mathf.Clamp(value,left,right);
    }
    public static float FreeSlider(Rect rect,float value,float left,float right) {
        return FreeSliderAxis(rect,value,left,right,false);
    }
    public static float FreeVerticalSlider(Rect rect,float value,float top,float bottom) {
        return FreeSliderAxis(rect,value,top,bottom,true);
    }
    static float FreeSliderAxis(Rect rect,float value,float left,float right,bool vertical) {
        if(!FreeRanges)return vertical?GUI.VerticalSlider(rect,value,left,right):GUI.HorizontalSlider(rect,value,left,right);
        int id=GUIUtility.GetControlID(FocusType.Passive);Vector2 range;
        if(!Ranges.TryGetValue(id,out range)) {float r=Mathf.Max(Mathf.Abs(right-left),0.01f);range=new Vector2(Mathf.Min(left,right)-r*3,Mathf.Max(left,right)+r*3);}
        range.x=Mathf.Min(range.x,value);range.y=Mathf.Max(range.y,value);
        float from=left<=right?range.x:range.y,to=left<=right?range.y:range.x;
        float result=vertical?GUI.VerticalSlider(rect,value,from,to):GUI.HorizontalSlider(rect,value,from,to);
        if(Event.current.type==EventType.MouseDrag) {float d=Mathf.Max((range.y-range.x)*0.15f,0.01f);if(result>=range.y)range.y+=d;else if(result<=range.x)range.x-=d;}
        Ranges[id]=range;return result;
    }
    public static float FreeSliderL(float value,float left,float right,params GUILayoutOption[] options) {
        Rect rect=GUILayoutUtility.GetRect(100,16,options);return FreeSlider(rect,value,left,right);
    }
    public static float FreeVerticalSliderL(float value,float top,float bottom,params GUILayoutOption[] options) {
        Rect rect=GUILayoutUtility.GetRect(16,100,options);return FreeVerticalSlider(rect,value,top,bottom);
    }
    public static void ApplyAlpha(object main) {
        try {
            Texture2D pm=Get(main,"_PropertyMap") as Texture2D;if(pm==null)return;
            int width=pm.width,height=pm.height,selection=Alpha;
            Color32[] pixels=pm.GetPixels32();Texture2D src=null,edge=null;
            int index=Alpha==1?0:Alpha==2?4:Alpha==3?5:Alpha==4?6:7;
            if(Alpha>0)src=Get(main,MapFields[index]) as Texture2D;
            if(Alpha==6)edge=Get(main,"_EdgeMap") as Texture2D;
            bool hasSource=src!=null,hasEdge=edge!=null;
            Color32[] sp=hasSource && src.width==width && src.height==height?src.GetPixels32():null;
            Color32[] ep=hasEdge && edge.width==width && edge.height==height?edge.GetPixels32():null;
            for(int i=0;i<pixels.Length;i++) {
                float a=1f;
                if(selection>0) {
                    if(sp!=null && selection!=6){pixels[i].a=sp[i].r;continue;}
                    float u=((i%width)+0.5f)/width,v=((i/width)+0.5f)/height;
                    a=!hasSource?0f:sp!=null?sp[i].r/255f:src.GetPixelBilinear(u,v).r;
                    if(selection==6)a*=0.5f+(!hasEdge?0.5f:ep!=null?ep[i].r/255f:edge.GetPixelBilinear(u,v).r);
                }
                pixels[i].a=(byte)Mathf.RoundToInt(Mathf.Clamp01(a)*255f);
            }
            ApplySurfaceProperty(pixels);pm.SetPixels32(pixels);pm.Apply();
        }catch(Exception e){Error("Alpha",e);}
    }
    static string[] Pack(object main) {
        string[] embedded=new string[9];
        for(int i=0;i<9;i++){Texture2D t=Get(main,MapFields[i]) as Texture2D;embedded[i]=t==null?"null":Convert.ToBase64String(t.EncodeToPNG());}
        return embedded;
    }
    public static void SaveProject(object sl,string path,int format) {
        if(string.IsNullOrEmpty(path))return;
        try {
            Main=Get(sl,"mainGui");if(!Bind())throw new InvalidOperationException("Settings not ready");
            ValidateNames(Names);Commit();path=ProjectBase(path);string project=Path.GetFileName(path),extension=Extension(format);
            object po=Activator.CreateInstance(Main.GetType().Assembly.GetType("ProjectObject"));
            string[] pathFields={"heightMapPath","diffuseMapPath","diffuseMapOriginalPath","normalMapPath","metallicMapPath","smoothnessMapPath","edgeMapPath","aoMapPath"};
            HashSet<string> paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for(int i=0;i<8;i++) {
                Call(guis[i],"GetValues",po);string name=ExportName(project,OutputName(i))+"."+extension;
                if(!paths.Add(name))throw new ArgumentException("Duplicate export filename: "+name);
                if(string.Equals(name,Path.GetFileName(path)+".mtz",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Texture name conflicts with project");
                Set(po,pathFields[i],Get(Main,MapFields[i])==null?"null":name);
            }
            Set(po,"zhEmbedded",Pack(Main));Set(po,"zhMapNames",(string[])Names.Clone());
            Set(po,"zhChannels",new int[]{Convert.ToInt32(Get(Main,"propRed")),Convert.ToInt32(Get(Main,"propGreen")),Convert.ToInt32(Get(Main,"propBlue")),Alpha});
            Set(po,"zhInputModes",(int[])InputModes.Clone());Set(po,"zhInputInvert",(bool[])InputInvert.Clone());Set(po,"zhInputSources",PackInputSources());
            Set(po,"zhSessionValues",PackSessionValues());
            Set(po,"zhTextureSize",new int[]{TextureWidth,TextureHeight});
            Set(po,"zhSourceRoughness",smoothnessSourceRoughness);
            Set(po,"zhSourceNames",(string[])SourceFileNames.Clone());
            // Stage the complete project before touching an existing project file.
            string file=path+".mtz",temp=file+".writing";
            using(FileStream stream=new FileStream(temp,FileMode.Create,FileAccess.Write))new XmlSerializer(po.GetType()).Serialize(stream,po);
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
            Set(sl,"thisProject",po);ProjectSaved(file);Status=T("工程已保存（包含纹理）","Project saved with embedded textures");
        }catch(Exception e){Error("Save project",e);}
    }
    public static string ProjectBase(string path) {
        path=Path.GetFullPath(path);
        return path.EndsWith(".mtz",StringComparison.OrdinalIgnoreCase)?path.Substring(0,path.Length-4):path;
    }
    static string Extension(int format){return format==0?"bmp":format==1?"jpg":format==2?"png":format==3?"tga":"tiff";}
    public static void ExportAll(object sl,string path,int format) {
        if(string.IsNullOrEmpty(path))return;
        try {ValidateNames(Names);((MonoBehaviour)sl).StartCoroutine(ExportRoutine(sl,ProjectBase(path),format));}
        catch(Exception e){Error("Export",e);}
    }
    static IEnumerator ExportRoutine(object sl,string path,int format) {
        object main=Get(sl,"mainGui");string project=Path.GetFileName(path),dir=Path.GetDirectoryName(path);string[] names=(string[])Names.Clone();
        names[5]=OutputName(5);
        for(int i=0;i<8;i++) {
            Texture2D t=Get(main,MapFields[i]) as Texture2D;if(t==null)continue;
            string name=Path.Combine(dir,ExportName(project,names[i]));
            IEnumerator save=Call(sl,"SaveTexture",Extension(format),t,name) as IEnumerator;
            if(save!=null)yield return ((MonoBehaviour)sl).StartCoroutine(save);
        }
        Status=T("全部贴图已导出","All texture maps exported");
    }
    public static IEnumerator LoadTextures(object sl,string path) {
        object po=Get(sl,"thisProject");string[] embedded=Get(po,"zhEmbedded") as string[];
        if(embedded==null || embedded.Length<8)return LoadExternalTextures(sl,po,path);
        return RestoreTextures(sl,po,embedded,path);
    }
    static IEnumerator LoadExternalTextures(object sl,object po,string path) {
        object main=Get(sl,"mainGui");Call(main,"CloseWindows");ClearInputSources();
        string[] pathFields={"heightMapPath","diffuseMapPath","diffuseMapOriginalPath","normalMapPath","metallicMapPath","smoothnessMapPath","edgeMapPath","aoMapPath"};
        string[] mapNames={"height","diffuse","diffuseOriginal","normal","metallic","smoothness","edge","ao"};
        string directory=Path.GetDirectoryName(Path.GetFullPath(path));
        Set(sl,"busy",true);
        for(int i=0;i<8;i++) {
            try {
                string relative=Get(po,pathFields[i]) as string;if(string.IsNullOrEmpty(relative) || relative=="null")continue;
                string file=Path.IsPathRooted(relative)?relative:Path.Combine(directory,relative);
                // Legacy project files always describe canonical smoothness, regardless of the global mode.
                Texture2D t=ReadTextureFile(file);Texture2D oldTexture=Get(main,MapFields[i]) as Texture2D;
                Set(main,MapFields[i],t);DestroyUnusedTexture(oldTexture);
            }catch(Exception e){Error("External texture",e);}
            yield return null;
        }
        Texture2D previous=Get(main,"_PropertyMap") as Texture2D;if(previous!=null)UnityEngine.Object.Destroy(previous);Set(main,"_PropertyMap",null);
        Call(main,"SetMaterialValues");Call(main,"FixSize");Call(main,"ProcessPropertyMap");
        Main=main;Bind();RestoreSourceNames(po,false);RestoreResolution(po);RestoreSurfaceWorkflow(po);Call(main,"ProcessPropertyMap");RestoreSessionValues(po);ResetHistory();ProjectLoaded(path);Set(sl,"busy",false);RememberProject(path);
    }
    static IEnumerator RestoreTextures(object sl,object po,string[] embedded,string path) {
        object main=Get(sl,"mainGui");Call(main,"CloseWindows");Texture2D[] decoded=new Texture2D[Math.Min(embedded.Length,9)];bool valid=true;
        ImageState[] inputs=null;int[] inputModes=null;bool[] inputInvert=null;
        try {
            for(int i=0;i<decoded.Length;i++) {
                if(string.IsNullOrEmpty(embedded[i]) || embedded[i]=="null")continue;
                decoded[i]=new Texture2D(2,2,TextureFormat.RGBA32,false);
                if(!decoded[i].LoadImage(Convert.FromBase64String(embedded[i])))throw new InvalidDataException("Invalid embedded texture "+i);
            }
            ReadProjectInputs(po,decoded,out inputs,out inputModes,out inputInvert);
        }catch(Exception e){valid=false;Error("Load textures",e);}
        if(!valid){foreach(Texture2D t in decoded)if(t!=null)UnityEngine.Object.Destroy(t);yield break;}
        Texture2D previousProperty=Get(main,"_PropertyMap") as Texture2D;if(previousProperty!=null)UnityEngine.Object.Destroy(previousProperty);
        for(int i=0;i<decoded.Length;i++)Set(main,MapFields[i],decoded[i]);
        Main=main;RestoreInputHistory(inputs,inputModes,inputInvert);
        RestoreSurfaceWorkflow(po);
        RestoreSourceNames(po,true);
        string[] names=Get(po,"zhMapNames") as string[];if(names!=null && names.Length==8)Names=(string[])names.Clone();
        int[] channels=Get(po,"zhChannels") as int[];
        if(channels!=null && channels.Length==4) {
            string[] fields={"propRed","propGreen","propBlue"};
            for(int i=0;i<3;i++)Set(main,fields[i],Enum.ToObject(Get(main,fields[i]).GetType(),channels[i]));Alpha=Mathf.Clamp(channels[3],0,6);
        }
        // Use the application's own initialization when a generator is opened.
        Call(main,"SetMaterialValues");Call(main,"FixSize");
        Call(main,"ProcessPropertyMap");
        Main=main;Bind();RestoreResolution(po);RestoreSessionValues(po);ResetHistory();ProjectLoaded(path);Set(sl,"busy",false);RememberProject(path);Status=T("已从工程恢复纹理","Textures restored from project");
        yield return null;
    }
}
