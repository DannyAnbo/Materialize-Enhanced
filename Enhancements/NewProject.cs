using System;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    static EditState newProjectDefaults;
    static bool newPrompt,newQueued;
    public static void RequestNewProject() {
        if(!Bind()||quitting||importing||Convert.ToBoolean(Get(Get(Main,"SaveLoadProjectScript"),"busy")))return;
        Commit();
        if(ProjectDirty){newPrompt=true;closePrompt=true;windowOpen=false;openRecent=false;openResolution=false;openAbout=false;openInput=-1;openChannel=-1;}
        else NewProjectNow();
    }
    static void NewProjectNow() {
        if(newProjectDefaults==null)return;
        Call(Main,"CloseWindows");CancelClose();
        windowOpen=openRecent=openResolution=openAbout=confirmClear=false;openInput=openChannel=-1;previewIndex=-1;
        string[] names=(string[])Names.Clone();
        EditState blank=new EditState{values=newProjectDefaults.values,extras=newProjectDefaults.extras,
            maps=new ImageState[9],sources=new ImageState[8],inputModes=new int[8],inputInvert=new bool[8],sourceNames=new string[8],
            alpha=newProjectDefaults.alpha,ranges=FreeRanges,names=names,width=0,height=0};
        Apply(blank);ClearInputSources();ClearSurfaceDisplay();
        Material sample=Get(Main,"SampleMaterial") as Material;if(sample!=null)sample.SetTexture("_MainTex",Get(Main,"_TextureGrey") as Texture);
        foreach(string field in QuickFields)Set(Main,field,"");Set(Main,"QuicksavePathProperty","");Set(Main,"QuicksavePath","");Set(Main,"textureToSave",null);
        Set(Get(Main,"SaveLoadProjectScript"),"thisProject",null);editNames=null;CurrentProjectPath="";
        ResetHistory();savedProject=last;sessionReady=true;ProjectDirty=false;UpdateProjectTitle();
        Status=T("已新建空白工程（Ctrl+S 保存）","New blank project (Ctrl+S to save)");
    }
}
