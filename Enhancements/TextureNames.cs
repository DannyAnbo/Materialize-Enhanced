using System;
using System.IO;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    // Leaf file names only: portable projects do not need the source computer's full paths.
    public static readonly string[] SourceFileNames=new string[8];
    static void RestoreSourceNames(object project,bool embedded) {
        string[] saved=Get(project,"zhSourceNames") as string[];
        string[] fields={"heightMapPath","diffuseMapPath","diffuseMapOriginalPath","normalMapPath","metallicMapPath","smoothnessMapPath","edgeMapPath","aoMapPath"};
        for(int i=0;i<8;i++) {
            string value=saved!=null&&saved.Length==8?saved[i]:embedded?"":Get(project,fields[i]) as string;
            SourceFileNames[i]=string.IsNullOrEmpty(value)||value=="null"?"":Path.GetFileName(value.Replace('\\','/'));
            sourceTextures[i]=Get(Main,MapFields[i]) as Texture2D;if(sourceImages[i]==null)sourceImages[i]=SnapshotImage(sourceTextures[i]);
        }
    }
    static void DrawTextureName(Rect rect,int index,Texture2D texture) {
        if(texture==null)return;
        string name=index==8?Path.GetFileName(Get(Main,"QuicksavePathProperty") as string??""):SourceFileNames[index];bool known=!string.IsNullOrEmpty(name);
        string label=known?name:T("未记录文件名","Name unavailable");
        string hint=known?(index==8?T("导出文件：","Export file: "):T("来源文件：","Source file: "))+name:T("此贴图由软件生成，或旧工程未记录原始文件名。","Generated texture, or an earlier project without recorded source names.");
        if(index==8&&!known){label=T("生成贴图（尚未导出）","Generated map (not exported)");hint=label;}
        GUI.Label(rect,new GUIContent(FitRecentText(label,rect.width,GUI.skin.label),hint));
        if(rect.Contains(Event.current.mousePosition))textureNameHint=hint;
    }
    static string textureNameHint;
    static void DrawTextureNameHint(float width,float height) {
        if(string.IsNullOrEmpty(textureNameHint))return;
        GUIStyle style=new GUIStyle(GUI.skin.box);style.wordWrap=true;style.alignment=TextAnchor.MiddleLeft;
        float w=Mathf.Min(width-24,Mathf.Max(260,GUI.skin.label.CalcSize(new GUIContent(textureNameHint)).x+24));
        float h=Mathf.Max(32,style.CalcHeight(new GUIContent(textureNameHint),w)+12);
        Vector2 p=Event.current.mousePosition;GUI.Box(new Rect(Mathf.Clamp(p.x+12,8,width-w-8),Mathf.Clamp(p.y+22,8,height-h-8),w,h),textureNameHint,style);
    }
}
