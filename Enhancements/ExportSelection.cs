using System;
using UnityEngine;
public static partial class MaterializeEnhancements {
    static bool openExport;
    static readonly bool[] exportSelected=new bool[9];
    static void OpenExportSelection(){for(int i=0;i<9;i++)exportSelected[i]=i==8||Get(Main,MapFields[i])!=null;openExport=true;openRecent=openAbout=openResolution=windowOpen=false;}
    public static void ExportSelected(object sl,string path,int format,bool[] selection){if(string.IsNullOrEmpty(path)||selection==null||selection.Length!=9)return;try{ValidateNames(Names);((MonoBehaviour)sl).StartCoroutine(ExportRoutine(sl,ProjectBase(path),format,(bool[])selection.Clone()));}catch(Exception e){Error("Export",e);}}
    static void DrawExportSelection(float width,float height){GUI.Window(8187,new Rect((width-470)/2,Mathf.Max(60,(height-405)/2),470,405),DrawExportWindow,T("选择导出的贴图","Choose Maps to Export"),solidWindow);GUI.BringWindowToFront(8187);}
    static void DrawExportWindow(int id){
        GUI.Label(new Rect(18,32,434,28),T("导出当前工作尺寸的所选贴图","Export selected maps at the current working resolution"));
        bool any=false;
        for(int i=0;i<9;i++){
            bool available=i==8||Get(Main,MapFields[i])!=null;GUI.enabled=available;
            string label=i==8?T("属性 RGBA","Property RGBA"):SurfaceName(i);
            exportSelected[i]=GUI.Toggle(new Rect(22+(i%2)*215,73+(i/2)*38,210,30),available&&exportSelected[i],label+(available?"":T("（空）"," (empty)")));any|=available&&exportSelected[i];
        }
        GUI.enabled=true;
        if(GUI.Button(new Rect(18,278,210,30),T("全选","Select All")))for(int i=0;i<9;i++)exportSelected[i]=i==8||Get(Main,MapFields[i])!=null;
        if(GUI.Button(new Rect(242,278,210,30),T("取消全选","Deselect All")))Array.Clear(exportSelected,0,9);
        GUI.Label(new Rect(18,315,434,24),T("上方“保存格式”决定输出格式；文件名作为统一前缀","Export format follows the main panel; filename is the common prefix"));
        GUI.enabled=any;
        if(GUI.Button(new Rect(18,358,210,30),T("选择位置并导出","Choose Location and Export"))){openExport=false;Browse(T("导出所选贴图","Export Selected Maps"),"ExportFiles",true);}
        GUI.enabled=true;if(GUI.Button(new Rect(242,358,210,30),T("取消","Cancel")))openExport=false;
    }
}
