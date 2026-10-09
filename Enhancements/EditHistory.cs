using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    // Snapshots own immutable encoded images, never Unity objects that another operation can destroy.
    sealed class ImageState { public byte[] png; public string name; public FilterMode filter; public TextureWrapMode wrap; public int aniso; }
    sealed class EditState { public object[][] values; public object[][] extras; public ImageState[] maps,sources,reload; public int[] inputModes; public bool[] inputInvert,reloadRough; public int alpha,width,height; public bool ranges,sourceRoughness; public string[] names,sourceNames,reloadPaths; }
    static readonly List<EditState> History=new List<EditState>();
    static readonly Dictionary<int,ImageState> imageCache=new Dictionary<int,ImageState>();
    static readonly HashSet<int> dirtyImages=new HashSet<int>();
    static readonly Dictionary<string,object> factory=new Dictionary<string,object>();
    static int historyIndex;
    static EditState last;
    static bool pending,applying;
    static object[] extraObjects;
    static FieldInfo[][] extraFields;
    static readonly string[][] ExtraNames={
        new string[]{"propRed","propGreen","propBlue","selectedFormat","selectedCubemap"},
        new string[]{"EnablePostProcess","UseTAA","BloomThreshold","BloomThresholdText","BloomAmount","BloomAmountText","LensFlareAmount","LensFlareAmountText","LensDirtAmount","LensDirtAmountText","VignetteAmount","VignetteAmountText","DOFMaxBlur","DOFMaxBlurText","DOFFocalDepth","DOFFocalDepthText","DOFMaxDistance","DOFMaxDistanceText","AutoFocus"},
        new string[]{"planeShown","cubeShown","cylinderShown","sphereShown","dispOffset"},
        new string[]{"Falloff","OverlapX","OverlapY","TexTiling","TexOffsetX","TexOffsetY","FalloffText","OverlapXText","OverlapYText","TexTilingText","TexOffsetXText","TexOffsetYText","SplatRotation","SplatRotationText","SplatRotationRandom","SplatRotationRandomText","SplatScale","SplatScaleText","SplatWobble","SplatWobbleText","SplatWobbleRandom","SplatWobbleRandomText","SplatRandomize","SplatRandomizeText","NewTexSelectionX","NewTexSelectionY","techniqueOverlap","techniqueSplat"},
        new string[]{"pointTL","pointTR","pointBL","pointBR","Slider","LensDistort","LensDistortText","PerspectiveX","PerspectiveXText","PerspectiveY","PerspectiveYText"},
        new string[]{"normalMapMaxStyle","normalMapMayaStyle","postProcessEnabled","propRed","propGreen","propBlue","fileFormat"},
        new string[]{"Slider"},new string[]{"Slider"},new string[]{"Slider"},new string[]{"Slider"},new string[]{"Slider"},new string[]{"Slider"},new string[]{"Slider"}
    };
    static void BindExtras() {
        extraObjects=new object[]{Main,Get(Main,"PostProcessGuiScript"),guis[7],Get(Main,"TilingTextureMakerGuiScript"),Get(Main,"AlignmentGuiScript"),Get(Get(Main,"SettingsGuiScript"),"settings"),guis[0],guis[1],guis[2],guis[3],guis[4],guis[5],guis[6]};
        if(extraFields==null) {
            extraFields=new FieldInfo[extraObjects.Length][];
            for(int i=0;i<extraObjects.Length;i++) {
                List<FieldInfo> fs=new List<FieldInfo>();
                if(extraObjects[i]!=null)foreach(string n in ExtraNames[i]){FieldInfo f=extraObjects[i].GetType().GetField(n,Flags);if(f!=null)fs.Add(f);}
                extraFields[i]=fs.ToArray();
            }
        }
    }
    static object[][] Values(object[] objects,FieldInfo[][] fields) {
        object[][] result=new object[objects.Length][];
        for(int i=0;i<objects.Length;i++){result[i]=new object[fields[i].Length];for(int j=0;j<fields[i].Length;j++)result[i][j]=fields[i][j].GetValue(objects[i]);}
        return result;
    }
    static FieldInfo[][] SettingInfos() {
        FieldInfo[][] fs=new FieldInfo[8][];for(int i=0;i<8;i++)fs[i]=settings[i].GetType().GetFields(BindingFlags.Instance|BindingFlags.Public);return fs;
    }
    static ImageState SnapshotImage(Texture2D texture) {
        if(texture==null)return null;int id=texture.GetInstanceID();ImageState saved;
        if(!imageCache.TryGetValue(id,out saved)||dirtyImages.Contains(id)) {
            saved=new ImageState{png=texture.EncodeToPNG(),name=texture.name,filter=texture.filterMode,wrap=texture.wrapMode,aniso=texture.anisoLevel};
            imageCache[id]=saved;dirtyImages.Remove(id);
        }
        return saved;
    }
    static Texture2D RestoreImage(ImageState saved) {
        if(saved==null)return null;Texture2D texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!texture.LoadImage(saved.png)){UnityEngine.Object.Destroy(texture);throw new InvalidOperationException("Cannot restore image");}
        texture.name=saved.name;texture.filterMode=saved.filter;texture.wrapMode=saved.wrap;texture.anisoLevel=saved.aniso;return texture;
    }
    static EditState Capture() {
        BindExtras();EditState state=new EditState();state.values=Values(settings,SettingInfos());state.extras=Values(extraObjects,extraFields);
        state.maps=new ImageState[9];state.alpha=Alpha;state.ranges=FreeRanges;state.names=(string[])Names.Clone();state.width=TextureWidth;state.height=TextureHeight;
        for(int i=0;i<9;i++) {
            Texture2D texture=Get(Main,MapFields[i]) as Texture2D;
            bool replaced=i<8&&(!object.ReferenceEquals(texture,sourceTextures[i])||(texture!=null&&dirtyImages.Contains(texture.GetInstanceID())));
            state.maps[i]=SnapshotImage(texture);
            if(i<8) {
                if(replaced){if(texture==null)ClearReload(i);sourceImages[i]=state.maps[i];sourceTextures[i]=texture;InputModes[i]=0;InputInvert[i]=false;SourceFileNames[i]="";if(i==5)smoothnessSourceRoughness=false;}
                if(texture!=null&&InputModes[i]==0&&!InputInvert[i]&&sourceImages[i]==null)sourceImages[i]=state.maps[i];
            }
        }
        state.sources=(ImageState[])sourceImages.Clone();state.inputModes=(int[])InputModes.Clone();state.inputInvert=(bool[])InputInvert.Clone();
        state.sourceRoughness=smoothnessSourceRoughness;
        state.sourceNames=(string[])SourceFileNames.Clone();state.reload=(ImageState[])reloadImages.Clone();state.reloadRough=(bool[])reloadRoughness.Clone();state.reloadPaths=(string[])reloadPaths.Clone();
        return state;
    }
    static bool SameValues(object[][] a,object[][] b) {
        if(a.Length!=b.Length)return false;
        for(int i=0;i<a.Length;i++){if(a[i].Length!=b[i].Length)return false;for(int j=0;j<a[i].Length;j++)if(!object.Equals(a[i][j],b[i][j]))return false;}return true;
    }
    static bool Equal(EditState a,EditState b) {
        if(a==null||b==null||a.alpha!=b.alpha||a.ranges!=b.ranges||a.sourceRoughness!=b.sourceRoughness||a.width!=b.width||a.height!=b.height||!SameValues(a.values,b.values)||!SameValues(a.extras,b.extras))return false;
        // Property map is derived from channel selections, source maps and the global workflow.
        for(int i=0;i<8;i++)if(!object.ReferenceEquals(a.maps[i],b.maps[i]))return false;
        for(int i=0;i<8;i++)if(a.names[i]!=b.names[i]||a.sourceNames[i]!=b.sourceNames[i]||!object.ReferenceEquals(a.reload[i],b.reload[i])||a.reloadRough[i]!=b.reloadRough[i]||a.inputModes[i]!=b.inputModes[i]||a.inputInvert[i]!=b.inputInvert[i]||!object.ReferenceEquals(a.sources[i],b.sources[i]))return false;return true;
    }
    static void ResetHistory() {
        History.Clear();imageCache.Clear();dirtyImages.Clear();historyIndex=0;pending=false;last=Capture();History.Add(last);
    }
    static void Prune() {
        // Image data is shared across parameter-only steps; cap unique encoded history to 512 MiB.
        while(History.Count>2) {
            HashSet<ImageState> unique=new HashSet<ImageState>();long bytes=0;
            foreach(EditState s in History)foreach(ImageState m in s.maps)if(m!=null&&unique.Add(m))bytes+=m.png.Length;
            foreach(EditState s in History)foreach(ImageState m in s.reload)if(m!=null&&unique.Add(m))bytes+=m.png.Length;
            foreach(EditState s in History)foreach(ImageState m in s.sources)if(m!=null&&unique.Add(m))bytes+=m.png.Length;
            if(History.Count<=120&&bytes<=512L*1024*1024)break;History.RemoveAt(0);historyIndex--;
        }
        HashSet<int> live=new HashSet<int>();foreach(string f in MapFields){Texture2D t=Get(Main,f) as Texture2D;if(t!=null)live.Add(t.GetInstanceID());}
        foreach(int id in new List<int>(imageCache.Keys))if(!live.Contains(id)){imageCache.Remove(id);dirtyImages.Remove(id);}
    }
    public static void Commit() {
        if(settings==null||applying)return;
        EditState current=Capture();
        if(History.Count==0){History.Add(current);historyIndex=0;}
        else if(!Equal(current,History[historyIndex])) {
            if(historyIndex<History.Count-1)History.RemoveRange(historyIndex+1,History.Count-historyIndex-1);
            History.Add(current);historyIndex=History.Count-1;Prune();
        }
        last=current;pending=false;UpdateProjectDirty(current);
    }
    static void TrackHistory() {
        if(applying||importing||quitting||Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy")))return;EditState current=Capture();UpdateProjectDirty(current);
        if(!Equal(current,last)){pending=true;changedAt=Time.realtimeSinceStartup;last=current;}
        if(pending&&!Input.GetMouseButton(0)&&Time.realtimeSinceStartup-changedAt>0.25f)Commit();
    }
    public static void ImageApplied(Texture2D texture) {
        if(texture!=null&&!applying&&Main!=null)foreach(string f in MapFields)if(object.ReferenceEquals(Get(Main,f),texture)){dirtyImages.Add(texture.GetInstanceID());break;}
    }
    static void Apply(EditState state) {
        applying=true;
        try {
            GUIUtility.keyboardControl=0;BindExtras();FieldInfo[][] fs=SettingInfos();
            for(int i=0;i<8;i++)for(int j=0;j<fs[i].Length;j++)fs[i][j].SetValue(settings[i],state.values[i][j]);
            for(int i=0;i<extraObjects.Length;i++)for(int j=0;j<extraFields[i].Length;j++)extraFields[i][j].SetValue(extraObjects[i],state.extras[i][j]);
            bool mapsChanged=false;Material sample=Get(Main,"SampleMaterial") as Material;
            if(sample!=null)for(int i=0;i<8;i++)if(object.ReferenceEquals(sample.GetTexture("_MainTex"),Get(Main,MapFields[i]))&&Get(Main,MapFields[i])!=null)previewIndex=i;
            for(int i=0;i<9;i++) {
                Texture2D current=Get(Main,MapFields[i]) as Texture2D;ImageState cached=null;
                if(current!=null)imageCache.TryGetValue(current.GetInstanceID(),out cached);
                if(object.ReferenceEquals(cached,state.maps[i])&&(current==null)==(state.maps[i]==null))continue;
                ImageState m=state.maps[i];Texture2D restored=RestoreImage(m);
                if(restored!=null)imageCache[restored.GetInstanceID()]=m;
                Set(Main,MapFields[i],restored);if(current!=null)UnityEngine.Object.Destroy(current);mapsChanged=true;
            }
            if(mapsChanged){RenderTexture hd=Get(Main,"_HDHeightMap") as RenderTexture;if(hd!=null){hd.Release();UnityEngine.Object.Destroy(hd);}Set(Main,"_HDHeightMap",null);}
            Alpha=state.alpha;FreeRanges=state.ranges;Names=(string[])state.names.Clone();TextureWidth=state.width;TextureHeight=state.height;
            RestoreInputHistory(state.sources,state.inputModes,state.inputInvert);
            Array.Copy(state.sourceNames,SourceFileNames,8);Array.Copy(state.reload,reloadImages,8);Array.Copy(state.reloadRough,reloadRoughness,8);Array.Copy(state.reloadPaths,reloadPaths,8);
            smoothnessSourceRoughness=state.sourceRoughness;ClearSurfaceDisplay();
            if(sample!=null&&previewIndex>=0)sample.SetTexture("_MainTex",(Get(Main,MapFields[previewIndex]) as Texture)??(Get(Main,"_TextureGrey") as Texture));
            Call(Main,"SetFormat",Get(Main,"selectedFormat"));
            Refresh(mapsChanged);
            if(Get(Main,"_PropertyMap")!=null)Call(Main,"ProcessPropertyMap");
            RefreshWorkflowPreview();
            Shader.SetGlobalInt("_FlipNormalY",Convert.ToBoolean(Get(extraObjects[5],"normalMapMayaStyle"))?1:0);
            if(Convert.ToBoolean(Get(guis[7],"planeShown")))Shader.DisableKeyword("TOP_PROJECTION");else Shader.EnableKeyword("TOP_PROJECTION");
            object probe=Get(Main,"reflectionProbe");if(probe!=null)Call(probe,"RenderProbe");
            last=Capture();pending=false;UpdateProjectDirty(last);
        }finally{applying=false;}
    }
    static int previewIndex=-1;
    static void Refresh(bool mapsChanged) {
        object po=Activator.CreateInstance(Main.GetType().Assembly.GetType("ProjectObject"));
        for(int i=0;i<8;i++)Set(po,SettingFields[i],settings[i]);
        for(int i=0;i<8;i++){Call(guis[i],"SetValues",po);if(mapsChanged)Set(guis[i],"newTexture",true);Set(guis[i],"doStuff",true);}
        if(mapsChanged)Call(Main,"FixSize");Call(Main,"SetMaterialValues");
        object pp=Get(Main,"PostProcessGuiScript");Call(pp,Convert.ToBoolean(Get(pp,"EnablePostProcess"))?"PostProcessOn":"PostProcessOff");
        Set(Get(Main,"TilingTextureMakerGuiScript"),"doStuff",true);Set(Get(Main,"AlignmentGuiScript"),"doStuff",true);
    }
    public static void Undo() {try{if(!Bind())return;Commit();if(historyIndex>0){Apply(History[--historyIndex]);Status=T("已撤销","Undone");}}catch(Exception e){Error("Undo",e);}}
    public static void Redo() {try{if(!Bind())return;Commit();if(historyIndex<History.Count-1){Apply(History[++historyIndex]);Status=T("已重做","Redone");}}catch(Exception e){Error("Redo",e);}}
    public static object DefaultParameter(string type,string field) {
        string key=type+"."+field;object value;if(factory.TryGetValue(key,out value))return value;
        Type t=Main.GetType().Assembly.GetType(type);if(t==null)return 0f;
        object defaults=typeof(MonoBehaviour).IsAssignableFrom(t)?null:Activator.CreateInstance(t);
        if(defaults!=null)value=t.GetField(field,Flags).GetValue(defaults);
        else {value=FactoryDefaults.Get(type,field);}
        factory[key]=value;return value;
    }
    public static void ResetParameter(string type,string field) {
        if(!Bind())return;Commit();BindExtras();object target=null;
        foreach(object s in settings)if(s.GetType().Name==type)target=s;
        foreach(object o in extraObjects)if(o!=null&&o.GetType().Name==type)target=o;
        if(target==null)return;Set(target,field,DefaultParameter(type,field));
        if(Get(target,field+"Text")!=null)Set(target,field+"Text",Convert.ToString(Get(target,field),System.Globalization.CultureInfo.InvariantCulture));
        Refresh(false);Commit();Status=T("已恢复此参数的默认值","Parameter default restored");
    }
}
