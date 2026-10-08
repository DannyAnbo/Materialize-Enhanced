using System;
using System.IO;
using System.Collections;
using UnityEngine;

public static class SessionSelfTest
{
    static object Get(object o,string n){return MaterializeEnhancements.Get(o,n);}
    static object Call(object o,string n,params object[] args){return MaterializeEnhancements.Call(o,n,args);}
    static void Set(object o,string n,object v){MaterializeEnhancements.Set(o,n,v);}
    public static IEnumerator Run(object main,string output,Action<bool,string> check) {
        string folder=Path.Combine(output,"session");Directory.CreateDirectory(folder);
        object sl=Get(main,"SaveLoadProjectScript"),fb=Get(main,"fileBrowser");
        string file=Path.Combine(folder,"working-project.mtz");MaterializeEnhancements.SaveProject(sl,file,2);
        check(MaterializeEnhancements.CurrentProjectPath==file&&!MaterializeEnhancements.ProjectDirty,"Save sets current project path and clean state");
        object mat=Get(Get(sl,"materailGui"),"MatS");float before=(float)Get(mat,"Metallic");
        Set(mat,"Metallic",before+0.23f);MaterializeEnhancements.Commit();
        check(MaterializeEnhancements.ProjectDirty,"Changing a material parameter marks project unsaved");
        MaterializeEnhancements.Undo();check(!MaterializeEnhancements.ProjectDirty,"Undo back to the saved state clears unsaved marker");
        MaterializeEnhancements.Redo();check(MaterializeEnhancements.ProjectDirty,"Redo after the saved state restores unsaved marker");
        MaterializeEnhancements.SaveCurrentProject(false);
        check(!MaterializeEnhancements.ProjectDirty&&!(bool)Get(fb,"isActive")&&File.ReadAllText(file).Contains("<zhSessionValues>"),"Save current overwrites tracked project without a browser and saves extra settings");
        MaterializeEnhancements.SaveCurrentProject(true);check((bool)Get(fb,"isActive")&&(bool)Get(fb,"overwriteWarn"),"Save As always opens a filename dialog");Call(fb,"Close");
        string second=Path.Combine(folder,"saved-as.mtz");MaterializeEnhancements.SaveProject(sl,second,2);
        check(MaterializeEnhancements.CurrentProjectPath==second&&File.Exists(file),"Save As changes current path and preserves previous project");
        MaterializeEnhancements.CurrentProjectPath="";MaterializeEnhancements.SaveCurrentProject(false);check((bool)Get(fb,"isActive"),"Save on untitled project opens a filename dialog");Call(fb,"Close");
        Call(sl,"LoadProject",second);yield return new WaitForSeconds(0.2f);
        check(MaterializeEnhancements.CurrentProjectPath==second&&!MaterializeEnhancements.ProjectDirty,"Successful load sets active path and clean baseline");
        object pp=Get(main,"PostProcessGuiScript");float bloom=(float)Get(pp,"BloomAmount");Set(pp,"BloomAmount",bloom+0.37f);MaterializeEnhancements.Commit();
        check(MaterializeEnhancements.ProjectDirty,"Post-process changes mark project unsaved");
        MaterializeEnhancements.SaveCurrentProject(false);Set(pp,"BloomAmount",bloom);MaterializeEnhancements.Commit();Call(sl,"LoadProject",second);yield return new WaitForSeconds(0.2f);
        check(Math.Abs((float)Get(pp,"BloomAmount")-(bloom+0.37f))<0.0001f&&!MaterializeEnhancements.ProjectDirty,"Saved project restores post-process settings and is clean");
        mat=Get(Get(sl,"materailGui"),"MatS");Set(mat,"Metallic",(float)Get(mat,"Metallic")+0.2f);MaterializeEnhancements.Commit();MaterializeEnhancements.RequestClose();
        check(MaterializeEnhancements.ClosePromptVisible&&MaterializeEnhancements.ProjectDirty,"Closing a modified project shows unsaved warning");
        MaterializeEnhancements.CancelClose();check(!MaterializeEnhancements.ClosePromptVisible&&MaterializeEnhancements.ProjectDirty,"Cancel close preserves unsaved project and app state");
        string current=MaterializeEnhancements.CurrentProjectPath;MaterializeEnhancements.SaveProject(sl,Path.Combine(Path.Combine(folder,"missing-directory"),"fail.mtz"),2);
        check(MaterializeEnhancements.ProjectDirty&&MaterializeEnhancements.CurrentProjectPath==current,"Failed save preserves current path and dirty state");
        MaterializeEnhancements.CurrentProjectPath="";MaterializeEnhancements.RequestClose();MaterializeEnhancements.SaveAndClose();
        check((bool)Get(fb,"isActive"),"Save and Close on untitled project waits for save dialog");
        Call(main,"SaveProject",(object)null);Call(fb,"Close");MaterializeEnhancements.CancelClose();
        check(MaterializeEnhancements.ProjectDirty&&!MaterializeEnhancements.ClosePromptVisible,"Cancelling close-save does not discard work or quit");
        MaterializeEnhancements.CurrentProjectPath=current;MaterializeEnhancements.SaveCurrentProject(false);
        MaterializeEnhancements.FreeRanges=!MaterializeEnhancements.FreeRanges;MaterializeEnhancements.Commit();
        check(!MaterializeEnhancements.ProjectDirty,"Global slider-range preference does not dirty a saved project");
        using(Stream avatar=typeof(MaterializeEnhancements).Assembly.GetManifestResourceStream("author-avatar"))check(avatar!=null&&avatar.Length>0,"Author avatar is embedded in runtime assembly");
        check(MaterializeEnhancements.DropReady,"Native close and file-drop window hook is installed");
    }
}
