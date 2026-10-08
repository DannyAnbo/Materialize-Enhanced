using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    public static string CurrentProjectPath="";
    public static bool ProjectDirty;
    static EditState savedProject;
    static bool sessionReady,closePrompt,saveBeforeClose,exitQueued,quitting;
    static volatile bool nativeCloseRequested;
    static volatile int nativeShortcut;
    static string nativeTitle="";
    static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool SetWindowText(IntPtr hwnd,string title);
    internal static bool ClosePromptVisible {get{return closePrompt;}}

    static bool SameProject(EditState a,EditState b) {
        if(a==null||b==null||a.alpha!=b.alpha||a.width!=b.width||a.height!=b.height||!SameValues(a.values,b.values)||!SameValues(a.extras,b.extras))return false;
        for(int i=0;i<9;i++)if(!object.ReferenceEquals(a.maps[i],b.maps[i]))return false;
        for(int i=0;i<8;i++)if(a.names[i]!=b.names[i]||a.inputModes[i]!=b.inputModes[i]||a.inputInvert[i]!=b.inputInvert[i]||!object.ReferenceEquals(a.sources[i],b.sources[i]))return false;
        return true;
    }
    static void UpdateProjectDirty(EditState state) {
        if(!sessionReady){sessionReady=true;savedProject=state;}
        ProjectDirty=!SameProject(state,savedProject);
    }
    static void ProjectSaved(string path) {
        CurrentProjectPath=Path.GetFullPath(path);savedProject=Capture();sessionReady=true;ProjectDirty=false;
        if(saveBeforeClose){exitQueued=true;saveBeforeClose=false;}
    }
    static void ProjectLoaded(string path) {
        CurrentProjectPath=Path.GetFullPath(path);savedProject=last;sessionReady=true;ProjectDirty=false;
    }
    static void UpdateProjectTitle() {
        string name=CurrentProjectPath.Length==0?T("未命名工程","Untitled"):Path.GetFileName(CurrentProjectPath);
        string title="Materialize — "+name+(ProjectDirty?" *":"");
        if(dropWindow!=IntPtr.Zero&&nativeTitle!=title){SetWindowText(dropWindow,title);nativeTitle=title;}
    }
    public static void SaveCurrentProject(bool saveAs) {
        if(quitting||importing||Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy"))){Status=T("请等待当前操作完成后再保存","Wait for the current operation before saving");return;}
        if(saveAs||CurrentProjectPath.Length==0) {
            Browse(T("项目另存为","Save Project As"),"SaveProject",true);
            object fb=Get(Main,"fileBrowser");
            if(CurrentProjectPath.Length>0)Set(fb,"typedFilename",Path.GetFileName(CurrentProjectPath));
        }else SaveProject(Get(Main,"SaveLoadProjectScript"),CurrentProjectPath,Convert.ToInt32(Get(Main,"selectedFormat")));
    }
    internal static void RequestClose() {
        if(quitting)return;
        if(importing||Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy"))){Status=T("请等待加载或导入完成后再关闭","Wait for loading or import to finish before closing");return;}
        Commit();
        if(ProjectDirty){closePrompt=true;windowOpen=false;openRecent=false;openInput=-1;openChannel=-1;}
        else BeginQuit();
    }
    internal static void CancelClose() {closePrompt=false;saveBeforeClose=false;exitQueued=false;}
    internal static void SaveAndClose() {saveBeforeClose=true;SaveCurrentProject(false);}
    internal static void DiscardAndClose() {BeginQuit();}
    static void BeginQuit() {
        if(quitting)return;quitting=true;closePrompt=false;
        // Stop per-frame snapshots and detach the managed window callback before Unity tears down.
        History.Clear();imageCache.Clear();dirtyImages.Clear();last=null;savedProject=null;ClearInputSources();
        UninstallDrops();Application.Quit();
    }
    static void SessionTick(bool browsing) {
        UpdateProjectTitle();
        int shortcut=nativeShortcut;nativeShortcut=0;
        if(shortcut!=0&&!browsing&&!windowOpen&&!closePrompt&&!quitting&&!openAbout&&!openResolution) {
            int key=shortcut&0xff;
            if(key==0x53)SaveCurrentProject((shortcut&0x100)!=0);
            else if(GUIUtility.keyboardControl==0){if(key==0x5a)Undo();else if(key==0x59)Redo();}
        }
        if(nativeCloseRequested){nativeCloseRequested=false;if(browsing){Status=T("请先完成或取消文件对话框","Finish or cancel the file dialog first");}else RequestClose();}
        if(exitQueued&&!browsing)BeginQuit();
    }
    static void DrawClosePrompt(float width,float height) {
        GUI.Window(8183,new Rect((width-520)/2,Mathf.Max(65,(height-190)/2),520,190),DrawCloseWindow,T("尚未保存的修改","Unsaved Changes"),solidWindow);GUI.BringWindowToFront(8183);
    }
    static void DrawCloseWindow(int id) {
        GUI.Label(new Rect(18,34,484,55),T("当前工程有尚未保存的修改。关闭前要保存吗？","The current project has unsaved changes. Save before closing?"));
        GUI.Label(new Rect(18,91,484,24),FitRecentText(CurrentProjectPath.Length==0?T("未命名工程","Untitled"):CurrentProjectPath,460,GUI.skin.label));
        if(GUI.Button(new Rect(18,139,150,32),T("保存并关闭","Save and Close")))SaveAndClose();
        if(GUI.Button(new Rect(185,139,150,32),T("不保存，关闭","Don't Save")))DiscardAndClose();
        if(GUI.Button(new Rect(352,139,150,32),T("取消","Cancel")))CancelClose();
    }

    // Only known editable fields are serialized; no arbitrary reflection targets from project files.
    static string[] PackSessionValues() {
        BindExtras();List<string> result=new List<string>();
        for(int i=0;i<extraObjects.Length;i++)foreach(FieldInfo f in extraFields[i]) {
            object value=f.GetValue(extraObjects[i]);string text;
            if(value is Vector2){Vector2 v=(Vector2)value;text=v.x.ToString("R",Invariant)+","+v.y.ToString("R",Invariant);}
            else if(value is Vector3){Vector3 v=(Vector3)value;text=v.x.ToString("R",Invariant)+","+v.y.ToString("R",Invariant)+","+v.z.ToString("R",Invariant);}
            else text=Convert.ToString(value,Invariant);
            result.Add(i+"|"+f.Name+"|"+text);
        }
        return result.ToArray();
    }
    static void RestoreSessionValues(object po) {
        string[] lines=Get(po,"zhSessionValues") as string[];if(lines==null)return;
        BindExtras();
        foreach(string line in lines) {
            string[] p=line.Split('|');int index;if(p.Length!=3||!int.TryParse(p[0],out index)||index<0||index>=extraObjects.Length)continue;
            foreach(FieldInfo f in extraFields[index])if(f.Name==p[1]) {
                try {
                    Type t=f.FieldType;object value;
                    if(t.IsEnum)value=Enum.Parse(t,p[2]);
                    else if(t==typeof(Vector2)){string[] v=p[2].Split(',');value=new Vector2(float.Parse(v[0],Invariant),float.Parse(v[1],Invariant));}
                    else if(t==typeof(Vector3)){string[] v=p[2].Split(',');value=new Vector3(float.Parse(v[0],Invariant),float.Parse(v[1],Invariant),float.Parse(v[2],Invariant));}
                    else value=Convert.ChangeType(p[2],t,Invariant);
                    f.SetValue(extraObjects[index],value);
                }catch(Exception e){Error("Project display settings",e);}
                break;
            }
        }
        Call(Main,"SetFormat",Get(Main,"selectedFormat"));Refresh(false);
        Shader.SetGlobalInt("_FlipNormalY",Convert.ToBoolean(Get(extraObjects[5],"normalMapMayaStyle"))?1:0);
        if(Convert.ToBoolean(Get(guis[7],"planeShown")))Shader.DisableKeyword("TOP_PROJECTION");else Shader.EnableKeyword("TOP_PROJECTION");
        Call(Get(Main,"reflectionProbe"),"RenderProbe");
    }
}
