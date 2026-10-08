using System;
using System.IO;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    static bool openAbout;
    static Texture2D aboutAvatar;
    const string AuthorName="DannyAnbo";
    const string AuthorHomepage="https://space.bilibili.com/413324822";
    const string EnhancementRepository="https://github.com/DannyAnbo/Materialize-Enhanced";
    static void DrawAbout(float width,float height) {
        GUI.Window(8184,new Rect((width-540)/2,Mathf.Max(55,(height-420)/2),540,420),DrawAboutWindow,T("关于我与这个版本","About the Author and This Version"),solidWindow);GUI.BringWindowToFront(8184);
    }
    static void DrawAboutWindow(int id) {
        if(aboutAvatar==null) {
            using(Stream stream=typeof(MaterializeEnhancements).Assembly.GetManifestResourceStream("author-avatar"))if(stream!=null) {
                byte[] bytes=new byte[stream.Length];int read=0;while(read<bytes.Length){int n=stream.Read(bytes,read,bytes.Length-read);if(n==0)break;read+=n;}
                aboutAvatar=new Texture2D(2,2,TextureFormat.RGBA32,false);aboutAvatar.LoadImage(bytes);
            }
        }
        if(aboutAvatar!=null)GUI.DrawTexture(new Rect(24,43,126,126),aboutAvatar,ScaleMode.ScaleToFit);
        GUI.Label(new Rect(174,43,335,35),AuthorName);
        GUI.Label(new Rect(174,84,340,46),T("Materialize 增强版本维护\n材质工具、工作流与创作","Materialize enhanced edition maintainer\nMaterial tools, workflows and art"));
        if(GUI.Button(new Rect(174,138,335,31),T("打开我的哔哩哔哩主页","Visit My Bilibili Homepage")))Application.OpenURL(AuthorHomepage);
        if(GUI.Button(new Rect(174,178,335,31),T("打开我的 GitHub 主页","Visit My GitHub Profile")))Application.OpenURL("https://github.com/DannyAnbo");
        GUI.Label(new Rect(24,229,490,58),T("此版本包含中文界面、撤销、来源通道、\n内嵌工程、最近项目、纹理尺寸与保存改进。","Chinese UI, undo, source channels, embedded projects,\nrecent projects, texture size and improved saving."));
        GUI.Label(new Rect(24,291,490,24),T("原作：Bounding Box Software  •  GPL-3.0","Original: Bounding Box Software  •  GPL-3.0"));
        GUI.Label(new Rect(24,319,490,24),T("本软件不提供担保；许可证允许修改与再分发。","No warranty. Modification and redistribution under GPL-3.0."));
        if(GUI.Button(new Rect(24,365,152,30),T("增强版源码","Enhanced Source")))Application.OpenURL(EnhancementRepository);
        if(GUI.Button(new Rect(190,365,152,30),T("原项目 / 许可","Original / License")))Application.OpenURL("https://github.com/BoundingBoxSoftware/Materialize");
        if(GUI.Button(new Rect(356,365,152,30),T("关闭","Close")))openAbout=false;
    }
}
