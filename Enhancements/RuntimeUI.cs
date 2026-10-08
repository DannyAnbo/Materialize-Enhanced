using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    static readonly Rect[] dropRects=new Rect[8];
    static readonly string[] MapTypes={"height","diffuse","diffuseOriginal","normal","metallic","smoothness","edge","ao","property"};
    static readonly string[] QuickFields={"QuicksavePathHeight","QuicksavePathDiffuse","QuicksavePathDiffuse","QuicksavePathNormal","QuicksavePathMetallic","QuicksavePathSmoothness","QuicksavePathEdge","QuicksavePathAO"};
    static readonly string[] ObjectNames={"HeightFromDiffuseGuiObject","EditDiffuseGuiObject","NormalFromHeightGuiObject","MetallicGuiObject","SmoothnessGuiObject","EdgeFromNormalGuiObject","AOFromNormalGuiObject"};
    static int openChannel=-1;
    static int openInput=-1;
    static Rect inputWindow;
    static Rect channelWindow;
    static float uiScale=1;
    static bool confirmClear;
    static Dictionary<string,string> translations;
    static GUIStyle solidWindow,solidPanel;
    static void EnsureStyles() {
        if(solidWindow!=null)return;
        Texture2D background=new Texture2D(1,1,TextureFormat.RGBA32,false);background.SetPixel(0,0,new Color(0.12f,0.13f,0.14f,1));background.Apply();
        solidWindow=new GUIStyle(GUI.skin.window);solidWindow.normal.background=background;solidWindow.onNormal.background=background;
        solidPanel=new GUIStyle(GUI.skin.box);solidPanel.normal.background=background;
    }
    public static string Localize(string english,string chinese){return English?english:chinese;}
    public static string Translate(string value) {
        if(translations==null) {
            translations=new Dictionary<string,string>();
            using(Stream stream=typeof(MaterializeEnhancements).Assembly.GetManifestResourceStream("translations"))
            using(StreamReader reader=new StreamReader(stream)) {
                string line;while((line=reader.ReadLine())!=null){string[] p=line.Split('\t');if(p.Length!=2)continue;string en=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[0])),zh=System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(p[1]));translations[en]=zh;if(!translations.ContainsKey("en|"+zh))translations["en|"+zh]=en;}
            }
        }
        string result;return translations.TryGetValue(English?"en|"+value:value,out result)?result:value;
    }
    public static void SwitchLanguage(bool english){English=english;SaveConfig();Status=T("已切换为中文","Switched to English");}
    public static void Label(Rect r,string text){GUI.Label(r,Translate(text));}
    public static void Box(Rect r,string text){GUI.Box(r,Translate(text));}
    public static bool Button(Rect r,string text){return GUI.Button(r,Translate(text));}
    public static bool Toggle(Rect r,bool value,string text){return GUI.Toggle(r,value,Translate(text));}
    public static Rect Window(int id,Rect r,GUI.WindowFunction fn,string title){EnsureStyles();return GUI.Window(id,r,fn,Translate(title),solidWindow);}
    public static string FilenameTextField(string value,params GUILayoutOption[] options) {
        object browser=Get(Main,"fileBrowser");object bounds=Get(browser,"screenRect");
        float width=bounds is Rect?((Rect)bounds).width:Screen.width;
        // Long paths scroll within the field instead of pushing Save outside the dialog.
        return GUILayout.TextField(value,GUILayout.MinWidth(40),GUILayout.MaxWidth(Mathf.Max(40,width-650)),GUILayout.ExpandWidth(true));
    }
    static object MapEnum(int index){return Enum.Parse(Main.GetType().Assembly.GetType("MapType"),MapTypes[index]);}
    static void Browse(string title,string callback,bool project) {
        object fb=Get(Main,"fileBrowser");Set(fb,"fileMasks",project?"*.mtz":"*.png;*.jpg;*.jpeg;*.tga;*.bmp;*.tif;*.tiff");
        bool opening=callback=="OpenFile"||callback=="LoadProject";
        Set(fb,"inputMustExist",opening);Set(fb,"overwriteWarn",!opening);Set(fb,"acceptsDirectories",false);Set(fb,"showTextInput",true);Set(fb,"hideExtensions",false);
        foreach(MethodInfo m in fb.GetType().GetMethods(Flags))if(m.Name=="ShowBrowser"&&m.GetParameters().Length==2) {
            MethodInfo target=callback=="ExportFiles"?typeof(MaterializeEnhancements).GetMethod("ExportFiles"):Main.GetType().GetMethod(callback,Flags,null,new Type[]{typeof(string)},null);
            Delegate fn=callback=="ExportFiles"?Delegate.CreateDelegate(m.GetParameters()[1].ParameterType,target):Delegate.CreateDelegate(m.GetParameters()[1].ParameterType,Main,target);
            m.Invoke(fb,new object[]{title,fn});Set(fb,"okButtonString",opening?T("打开","Open"):T("保存","Save"));break;
        }
        openChannel=-1;openInput=-1;openRecent=false;
    }
    public static void ExportFiles(string path){if(!string.IsNullOrEmpty(path))ExportAll(Get(Main,"SaveLoadProjectScript"),path,Convert.ToInt32(Get(Main,"selectedFormat")));}
    static void SaveMap(int index,bool quick) {
        if(index==8)Call(Main,"ProcessPropertyMap");
        Set(Main,"textureToSave",Get(Main,MapFields[index]));Set(Main,"mapType",index==8?"_msao":Names[index]);
        if(quick){Call(Main,"SaveFile",Get(Main,index==8?"QuicksavePathProperty":QuickFields[index]));}
        else Browse(index==8?T("保存属性贴图","Save Property Map"):T("保存贴图","Save Texture"),"SaveFile",false);
    }
    public static void ChooseChannel(int channel,int selection) {
        Commit();if(channel==3)Alpha=selection;else {string f=new string[]{"propRed","propGreen","propBlue"}[channel];Set(Main,f,Enum.ToObject(Get(Main,f).GetType(),selection));}
        Call(Main,"ProcessPropertyMap");Commit();SaveConfig();openChannel=-1;
    }
    static void OpenGenerator(int column) {
        Call(Main,"CloseWindows");Call(Main,"FixSize");((GameObject)Get(Main,ObjectNames[column])).SetActive(true);
        int group=column;Call(guis[group],"NewTexture");Call(guis[group],"DoStuff");
    }
    static void InputButton(Rect rect,int index,bool compact) {
        string channel=InputModes[index]==0?T("整图","Full"):InputChannelNames[InputModes[index]];
        string label=(compact?"":T("通道：","Use: "))+channel+(InputInvert[index]?T("反"," Inv"):"")+" ▾";
        if(InputModes[index]!=0||InputInvert[index])GUI.backgroundColor=new Color(0.4f,0.7f,0.9f);
        if(GUI.Button(rect,new GUIContent(label,T("从此类别的原始贴图选择 R/G/B/A；各类别独立","Choose R/G/B/A from this map's original texture; independent per category")))) {
            openInput=openInput==index?-1:index;openChannel=-1;openRecent=false;
            inputWindow=new Rect(Mathf.Min(rect.x,Screen.width/uiScale-240),rect.yMax+4,230,246);
        }
        GUI.backgroundColor=Color.white;
    }
    static void DrawCard(int column,int index,string title) {
        float x=12+column*118,y=54;GUI.Box(new Rect(x,y,110,236),title,solidPanel);
        Texture2D image=Get(Main,MapFields[index]) as Texture2D;
        if(column==1) {
            GUI.Label(new Rect(x+5,y+21,50,19),T("原图","Source"));GUI.Label(new Rect(x+57,y+21,50,19),T("结果","Result"));
            Rect original=new Rect(x+5,y+41,48,60),edited=new Rect(x+57,y+41,48,60);dropRects[2]=original;dropRects[1]=edited;
            Texture2D a=Get(Main,"_DiffuseMapOriginal") as Texture2D,b=Get(Main,"_DiffuseMap") as Texture2D;
            if(a!=null)GUI.DrawTexture(original,a,ScaleMode.ScaleToFit);else GUI.Box(original,"+");
            if(b!=null)GUI.DrawTexture(edited,b,ScaleMode.ScaleToFit);else GUI.Box(edited,"+");
            InputButton(new Rect(x+5,y+104,48,23),2,true);InputButton(new Rect(x+57,y+104,48,23),1,true);
            index=2;image=a!=null?a:b;
        }else {
            dropRects[index]=new Rect(x+5,y+24,100,78);
            if(image!=null)GUI.DrawTexture(dropRects[index],image,ScaleMode.ScaleToFit);else GUI.Box(dropRects[index],T("拖入贴图","Drop texture"));
            InputButton(new Rect(x+5,y+104,100,23),index,false);
        }
        if(GUI.Button(new Rect(x+5,y+130,22,22),new GUIContent("P",T("从剪贴板粘贴","Paste from clipboard")))){Set(Main,"mapTypeToLoad",MapEnum(index));Call(Main,"PasteFile");}
        GUI.enabled=image!=null;
        if(GUI.Button(new Rect(x+31,y+130,22,22),new GUIContent("C",T("复制贴图","Copy texture")))){Set(Main,"textureToSave",image);Call(Main,"CopyFile");}
        GUI.enabled=true;
        if(GUI.Button(new Rect(x+57,y+130,22,22),new GUIContent("O",T("打开贴图","Open texture")))){Set(Main,"mapTypeToLoad",MapEnum(index));Browse(T("打开贴图","Open Texture"),"OpenFile",false);}
        GUI.enabled=image!=null;
        if(GUI.Button(new Rect(x+83,y+130,22,22),new GUIContent("S",T("保存贴图","Save texture"))))SaveMap(column==1&&Get(Main,"_DiffuseMap")!=null?1:index,false);
        GUI.enabled=image!=null&&!string.IsNullOrEmpty(Get(Main,QuickFields[index]) as string);
        if(GUI.Button(new Rect(x+10,y+157,90,22),T("快速保存","Quick Save")))SaveMap(column==1&&Get(Main,"_DiffuseMap")!=null?1:index,true);
        GUI.enabled=image!=null;
        if(GUI.Button(new Rect(x+10,y+184,90,22),T("预览","Preview")))Call(Main,"SetLoadedTexture",MapEnum(column==1&&Get(Main,"_DiffuseMap")!=null?1:index));
        GUI.enabled=column==1?Get(Main,"_DiffuseMapOriginal")!=null:column==0?(Get(Main,"_DiffuseMapOriginal")!=null||Get(Main,"_DiffuseMap")!=null||Get(Main,"_NormalMap")!=null):column==2?Get(Main,"_HeightMap")!=null:column<=4?(Get(Main,"_DiffuseMapOriginal")!=null||Get(Main,"_DiffuseMap")!=null):(Get(Main,"_NormalMap")!=null||Get(Main,"_HeightMap")!=null);
        if(GUI.Button(new Rect(x+5,y+211,48,20),T("编辑","Create")))OpenGenerator(column);
        GUI.enabled=image!=null;
        if(GUI.Button(new Rect(x+57,y+211,48,20),T("清除","Clear"))){Commit();Call(Main,"ClearTexture",MapEnum(column==1?1:index));Call(Main,"CloseWindows");Call(Main,"SetMaterialValues");Call(Main,"FixSize");Commit();}
        GUI.enabled=true;
    }
    public static void Draw(object main) {
        Matrix4x4 matrix=GUI.matrix;int depth=GUI.depth;bool enabled=GUI.enabled;
        try {
            Main=main;GUI.enabled=true;GUI.depth=-50;EnsureStyles();
            uiScale=Mathf.Min(1,Screen.width/1200f);
            if(closePrompt&&!Convert.ToBoolean(Get(Get(main,"fileBrowser"),"isActive"))){GUI.matrix=Matrix4x4.Scale(new Vector3(uiScale,uiScale,1));DrawClosePrompt(Screen.width/uiScale,Screen.height/uiScale);return;}
            if(Convert.ToBoolean(Get(main,"hideGui"))) {
                if(GUI.Button(new Rect(Screen.width-110,10,100,30),T("显示界面","Show UI"))){Set(main,"hideGui",false);object list=Get(main,"objectsToUnhide");if(list is System.Collections.IEnumerable)foreach(GameObject o in (System.Collections.IEnumerable)list)o.SetActive(true);}return;
            }
            object fb=Get(main,"fileBrowser");if(fb!=null&&Convert.ToBoolean(Get(fb,"isActive")))return;
            uiScale=Mathf.Min(1,Screen.width/1200f);GUI.matrix=Matrix4x4.Scale(new Vector3(uiScale,uiScale,1));
            float w=Screen.width/uiScale;
            if(GUI.Button(new Rect(12,12,104,30),T("增强设置","Enhancements"))){windowOpen=!windowOpen;openRecent=false;editNames=(string[])Names.Clone();}
            GUI.enabled=History.Count>1;if(GUI.Button(new Rect(124,12,78,30),T("撤销","Undo")))Undo();
            GUI.enabled=historyIndex<History.Count-1;if(GUI.Button(new Rect(210,12,78,30),T("重做","Redo")))Redo();GUI.enabled=true;
            if(GUI.Button(new Rect(300,12,120,30),English?"中文 / Chinese":"English / 英文"))SwitchLanguage(!English);
            if(GUI.Button(new Rect(432,12,112,30),new GUIContent(T("保存项目","Save Project"),T("Ctrl+S 保存；Ctrl+Shift+S 另存为","Ctrl+S Save; Ctrl+Shift+S Save As"))))SaveCurrentProject(false);
            if(GUI.Button(new Rect(552,12,112,30),T("加载项目","Load Project")))Browse(T("加载项目","Load Project"),"LoadProject",true);
            if(GUI.Button(new Rect(672,12,104,30),T("最近项目 ▾","Recent ▾"))){openRecent=!openRecent;openChannel=-1;openInput=-1;windowOpen=false;recentScroll=Vector2.zero;}
            if(GUI.Button(new Rect(784,12,148,30),T("导出全部贴图","Export All Maps")))Browse(T("导出全部贴图","Export All Maps"),"ExportFiles",true);
            if(GUI.Button(new Rect(940,12,112,30),T("关于我","About"))){openAbout=!openAbout;openRecent=false;windowOpen=false;openInput=-1;openChannel=-1;}
            if(GUI.Button(new Rect(w-112,12,100,30),T("隐藏界面","Hide UI"))){Set(main,"hideGui",true);Call(main,"HideWindows");}
            int[] indices={0,1,3,4,5,6,7};string[] titles=English?new string[]{"Height","Diffuse","Normal","Metallic","Smoothness","Edge","AO"}:new string[]{"高度贴图","漫反射贴图","法线贴图","金属度贴图","平滑度贴图","边缘贴图","AO 贴图"};
            for(int i=0;i<7;i++)DrawCard(i,indices[i],titles[i]);
            float px=850;GUI.Box(new Rect(px,54,330,236),T("保存格式与属性贴图 RGBA","Export Format / Property Map RGBA"),solidPanel);
            string[] formats={"BMP","JPG","PNG","TGA","TIFF"};int current=Convert.ToInt32(Get(main,"selectedFormat"));
            for(int i=0;i<5;i++){GUI.backgroundColor=current==i?new Color(0.4f,0.7f,0.9f):Color.white;if(GUI.Button(new Rect(px+10+i*62,80,58,23),formats[i])){Commit();Call(main,"SetFormat",Enum.ToObject(Get(main,"selectedFormat").GetType(),i));Commit();}}GUI.backgroundColor=Color.white;
            for(int i=0;i<4;i++) {
                string f=i==0?"propRed":i==1?"propGreen":"propBlue";int v=i==3?Alpha:Convert.ToInt32(Get(main,f));
                GUI.Label(new Rect(px+12,112+i*31,24,25),new string[]{"R:","G:","B:","A:"}[i]);
                string label=(English?AlphaEn:AlphaZh)[Mathf.Clamp(v,0,6)];if(i<3&&v==0)label=T("无（黑色）","None (black)");
                if(GUI.Button(new Rect(px+40,110+i*31,250,27),label+"  ▾")){openChannel=openChannel==i?-1:i;openInput=-1;openRecent=false;channelWindow=new Rect(px-220,78,210,258);}
                if(GUI.Button(new Rect(px+296,110+i*31,24,27),new GUIContent("↶",T("重置此通道","Reset channel"))))ChooseChannel(i,0);
            }
            if(GUI.Button(new Rect(px+10,241,155,28),T("保存属性贴图","Save Property Map")))SaveMap(8,false);
            GUI.enabled=!string.IsNullOrEmpty(Get(main,"QuicksavePathProperty") as string);
            if(GUI.Button(new Rect(px+173,241,147,28),T("快速保存属性图","Quick Save Property")))SaveMap(8,true);GUI.enabled=true;
            float ax=350,ay=302;
            if(GUI.Button(new Rect(ax,ay,100,32),T("后处理","Post Process"))){GameObject o=(GameObject)Get(main,"PostProcessGuiObject");o.SetActive(!o.activeSelf);}
            if(GUI.Button(new Rect(ax+108,ay,132,32),T("显示完整材质","Full Material")))Call(main,"ShowFullMaterial");
            if(GUI.Button(new Rect(ax+248,ay,105,32),T("下一张环境图","Next Cubemap"))){Commit();Array cubes=(Array)Get(main,"CubeMaps");Set(main,"selectedCubemap",(Convert.ToInt32(Get(main,"selectedCubemap"))+1)%cubes.Length);Call(main,"SetMaterialValues");Call(Get(main,"reflectionProbe"),"RenderProbe");Commit();}
            GUI.enabled=Get(main,"_HeightMap")!=null;
            if(GUI.Button(new Rect(ax+361,ay,82,32),T("纹理平铺","Tile Maps"))){Call(main,"CloseWindows");Call(main,"FixSize");((GameObject)Get(main,"TilingTextureMakerGuiObject")).SetActive(true);Call(Get(main,"TilingTextureMakerGuiScript"),"Initialize");}GUI.enabled=true;
            if(GUI.Button(new Rect(ax+451,ay,90,32),T("调整对齐","Alignment"))){Call(main,"CloseWindows");Call(main,"FixSize");Call(Get(main,"AlignmentGuiScript"),"Initialize");}
            GUI.enabled=Get(main,"_NormalMap")!=null;if(GUI.Button(new Rect(ax+549,ay,105,32),T("翻转法线 Y","Flip Normal Y"))){Commit();Call(main,"FlipNormalMapY");Commit();}GUI.enabled=true;
            if(GUI.Button(new Rect(ax+662,ay,128,32),T("清除全部贴图","Clear All Maps")))confirmClear=!confirmClear;
            if(GUI.Button(new Rect(12,302,320,32),T("纹理尺寸：","Texture Size: ")+(TextureWidth==0?T("原始尺寸","Source sizes"):TextureWidth+" × "+TextureHeight)+" ▾")){openResolution=!openResolution;openRecent=false;openAbout=false;openChannel=-1;openInput=-1;windowOpen=false;}
            if(confirmClear){GUI.Box(new Rect(ax+662,ay+38,150,70),T("清除全部贴图？","Clear all maps?"));if(GUI.Button(new Rect(ax+672,ay+70,60,25),T("确定","Yes"))){Commit();Call(main,"ClearAllTextures");Call(main,"CloseWindows");Call(main,"SetMaterialValues");Commit();confirmClear=false;}if(GUI.Button(new Rect(ax+740,ay+70,60,25),T("取消","No")))confirmClear=false;}
            if(openChannel>=0){channelWindow=GUI.Window(8180,channelWindow,DrawChannels,T("选择通道来源","Channel Source"),solidWindow);GUI.BringWindowToFront(8180);}
            if(openInput>=0){inputWindow=GUI.Window(8181,inputWindow,DrawInputChannels,(English?LabelsEn:LabelsZh)[openInput]+T("：来源通道"," / Source Channel"),solidWindow);GUI.BringWindowToFront(8181);}
            if(windowOpen){window.x=Mathf.Clamp(window.x,0,Mathf.Max(0,w-window.width));window.y=Mathf.Clamp(window.y,54,Mathf.Max(54,Screen.height/uiScale-window.height));window=GUI.Window(8178,window,DrawWindow,T("Materialize 增强设置","Materialize Enhancements"),solidWindow);GUI.BringWindowToFront(8178);}
            if(openRecent)DrawRecentProjects(w,Screen.height/uiScale);
            if(openAbout)DrawAbout(w,Screen.height/uiScale);
            if(openResolution)DrawResolution(w,Screen.height/uiScale);
            GUI.matrix=matrix;
            string projectLabel=T("当前工程：","Project: ")+(CurrentProjectPath.Length==0?T("未命名工程","Untitled"):CurrentProjectPath)+(ProjectDirty?T("  * 未保存","  * Unsaved"):"");
            GUI.Label(new Rect(12,Screen.height-90,Screen.width-24,22),new GUIContent(FitRecentText(projectLabel,Screen.width-40,GUI.skin.label),projectLabel));
            if(Status.Length>0)GUI.Label(new Rect(12,Screen.height-64,Mathf.Min(900,Screen.width-20),24),Status);
            if(GUI.Button(new Rect(Screen.width-190,Screen.height-40,100,30),T("窗口 / 全屏","Window / Full")))Call(main,"Fullscreen");
            if(GUI.Button(new Rect(Screen.width-80,Screen.height-40,70,30),T("退出","Quit")))RequestClose();
        }catch(Exception e){Error("UI",e);}finally{GUI.matrix=matrix;GUI.depth=depth;GUI.enabled=enabled;GUI.backgroundColor=Color.white;}
    }
    static void DrawChannels(int id) {
        for(int i=0;i<7;i++)if(GUI.Button(new Rect(10,28+i*29,190,26),(openChannel<3&&i==0)?T("无（黑色）","None (black)"):(English?AlphaEn:AlphaZh)[i]))ChooseChannel(openChannel,i);
        if(GUI.Button(new Rect(10,233,190,20),T("取消","Cancel")))openChannel=-1;
    }
    static void DrawInputChannels(int id) {
        int index=openInput;if(index<0)return;
        for(int i=0;i<5;i++) {
            GUI.backgroundColor=InputModes[index]==i?new Color(0.4f,0.7f,0.9f):Color.white;
            string label=i==0?T("整图（保留 RGBA）","Full image (keep RGBA)"):InputChannelNames[i]+T(" 通道 → 灰度"," channel to grayscale");
            if(GUI.Button(new Rect(10,27+i*29,210,26),label))SetInputChannel(index,i,InputInvert[index]);
        }
        GUI.backgroundColor=Color.white;
        bool inverse=GUI.Toggle(new Rect(12,178,206,25),InputInvert[index],T("反相（粗糙度 → 平滑度）","Invert (1 - value)"));
        if(inverse!=InputInvert[index])SetInputChannel(index,InputModes[index],inverse);
        if(GUI.Button(new Rect(10,211,210,26),T("完成","Done")))openInput=-1;
    }
    static void DrawWindow(int id) {
        FreeRanges=GUI.Toggle(new Rect(20,35,480,25),FreeRanges,T("允许参数超出原范围（数值框可直接输入）","Allow values outside original ranges"));
        GUI.Label(new Rect(20,72,480,25),T("贴图名称：支持 {project}；不填扩展名","Texture names: use {project}; omit extensions"));
        GUI.Label(new Rect(20,99,480,25),T("以下划线开头时，自动加工程名称作为前缀","Names starting with _ use the project prefix"));
        if(editNames==null)editNames=(string[])Names.Clone();
        for(int i=0;i<8;i++){GUI.Label(new Rect(20,134+i*33,130,25),(English?LabelsEn:LabelsZh)[i]);editNames[i]=GUI.TextField(new Rect(160,134+i*33,330,27),editNames[i]);}
        if(GUI.Button(new Rect(20,412,150,32),T("保存并应用","Save and Apply"))){try{ValidateNames(editNames);Commit();Names=(string[])editNames.Clone();Commit();SaveConfig();}catch(Exception e){Error("Names",e);}}
        if(GUI.Button(new Rect(180,412,140,32),T("关闭","Close")))windowOpen=false;
        GUI.Label(new Rect(20,459,480,42),T("Ctrl+Z 撤销 / Ctrl+Y 重做；↶ 恢复单项默认\n保存项目仅写入 .mtz；导出贴图需单独点击","Ctrl+Z undo / Ctrl+Y redo; ↶ resets one parameter\nSaving a project writes only .mtz; export maps separately"));
        GUI.DragWindow(new Rect(0,0,520,28));
    }
}
