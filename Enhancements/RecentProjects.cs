using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    const int RecentLimit=12;
    static readonly List<string> recentProjects=new List<string>();
    static bool openRecent;
    static Vector2 recentScroll;
    static Rect recentWindow;
    internal static string RecentStorageOverride;
    static string RecentPath {get{return RecentStorageOverride??Path.Combine(Application.dataPath,"recent_projects.txt");}}
    internal static string[] RecentProjects {get{return recentProjects.ToArray();}}

    internal static void ReadRecentProjects() {
        recentProjects.Clear();
        try {
            if(!File.Exists(RecentPath))return;
            foreach(string line in File.ReadAllLines(RecentPath,Encoding.UTF8)) {
                if(recentProjects.Count==RecentLimit)break;
                try {
                    string path=Path.GetFullPath(Encoding.UTF8.GetString(Convert.FromBase64String(line)));
                    if(!path.EndsWith(".mtz",StringComparison.OrdinalIgnoreCase))continue;
                    if(!recentProjects.Exists(delegate(string p){return string.Equals(p,path,StringComparison.OrdinalIgnoreCase);}))recentProjects.Add(path);
                }catch(ArgumentException){}catch(FormatException){}catch(NotSupportedException){}
            }
        }catch(Exception e){Error("Read recent projects",e);}
    }
    static void RememberProject(string path) {
        try {
            path=Path.GetFullPath(path);
            if(!File.Exists(path)||!path.EndsWith(".mtz",StringComparison.OrdinalIgnoreCase))return;
            recentProjects.RemoveAll(delegate(string p){return string.Equals(p,path,StringComparison.OrdinalIgnoreCase);});
            recentProjects.Insert(0,path);
            if(recentProjects.Count>RecentLimit)recentProjects.RemoveRange(RecentLimit,recentProjects.Count-RecentLimit);
            string[] lines=recentProjects.ConvertAll(delegate(string p){return Convert.ToBase64String(Encoding.UTF8.GetBytes(p));}).ToArray();
            string file=RecentPath,temp=file+".writing";
            File.WriteAllLines(temp,lines,new UTF8Encoding(false));
            if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
        }catch(Exception e){Error("Save recent projects",e);}
    }

    // Existing MonoBehaviour method delegates here; its serialized layout stays intact.
    public static void LoadProject(object sl,string path) {
        CloseMapEditor();openExport=false;
        if(string.IsNullOrEmpty(path))return;
        try {
            if(Convert.ToBoolean(Get(sl,"busy"))||importing){Status=T("正在加载，请稍候","Loading in progress");return;}
            path=Path.GetFullPath(path);
            object po;
            using(FileStream stream=File.OpenRead(path))po=new XmlSerializer(sl.GetType().Assembly.GetType("ProjectObject")).Deserialize(stream);
            if(po==null)throw new InvalidDataException("Invalid project");
            RestoreResolution(po);
            Set(sl,"thisProject",po);
            foreach(string field in GuiFields)Call(Get(sl,field),"SetValues",po);
            Call(Get(sl,"mainGui"),"ClearAllTextures");
            Set(sl,"busy",true);openRecent=false;
            ((MonoBehaviour)sl).StartCoroutine(LoadProjectRoutine(sl,path));
        }catch(Exception e){Error("Load project",e);}
    }
    static System.Collections.IEnumerator LoadProjectRoutine(object sl,string path) {
        try {yield return ((MonoBehaviour)sl).StartCoroutine(LoadTextures(sl,path));}
        finally {Set(sl,"busy",false);}
    }
    internal static void OpenRecentProject(string path) {LoadProject(Get(Main,"SaveLoadProjectScript"),path);}

    static void DrawRecentProjects(float width,float height) {
        if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){openRecent=false;Event.current.Use();return;}
        float h=Mathf.Min(490,Mathf.Max(180,height-75));
        recentWindow=new Rect(Mathf.Min(552,width-630),50,620,h);
        GUI.Window(8182,recentWindow,DrawRecentWindow,T("最近项目","Recent Projects"),solidWindow);
        GUI.BringWindowToFront(8182);
    }
    static string FitRecentText(string text,float width,GUIStyle style) {
        if(style.CalcSize(new GUIContent(text)).x<=width)return text;
        int left=0,right=text.Length;
        while(left<right){int middle=(left+right+1)/2;if(style.CalcSize(new GUIContent(text.Substring(0,middle)+"…")).x<=width)left=middle;else right=middle-1;}
        return text.Substring(0,left)+"…";
    }
    static void DrawRecentWindow(int id) {
        GUI.Label(new Rect(12,27,590,24),T("最近打开的项目（最新在前，最多 12 个）","Recently opened projects (newest first, up to 12)"));
        float h=recentWindow.height;
        if(recentProjects.Count==0)GUI.Label(new Rect(18,72,580,50),T("还没有记录。成功加载工程后会显示在这里。","No recent projects yet. Successfully opened projects appear here."));
        else {
            GUIStyle row=new GUIStyle(GUI.skin.button);row.alignment=TextAnchor.MiddleLeft;row.wordWrap=false;
            recentScroll=GUI.BeginScrollView(new Rect(10,55,600,h-108),recentScroll,new Rect(0,0,578,recentProjects.Count*60));
            string selected=null;
            for(int i=0;i<recentProjects.Count;i++) {
                string path=recentProjects[i];bool exists=File.Exists(path);
                string name=(i+1)+". "+Path.GetFileName(path)+(exists?"":T("  [文件不存在]","  [Missing file]"));
                GUI.enabled=exists&&!Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy"));
                if(GUI.Button(new Rect(2,i*60,574,54),new GUIContent(FitRecentText(name,548,row)+"\n"+FitRecentText(path,548,row),path),row))selected=path;
            }
            GUI.enabled=true;GUI.EndScrollView();
            if(selected!=null)OpenRecentProject(selected);
        }
        if(GUI.Button(new Rect(12,h-42,290,29),T("浏览其他项目…","Browse Other Projects…")))Browse(T("加载项目","Load Project"),"LoadProject",true);
        if(GUI.Button(new Rect(314,h-42,294,29),T("关闭","Close")))openRecent=false;
    }
}
