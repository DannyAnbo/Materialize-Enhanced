using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    delegate IntPtr WindowProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    delegate bool EnumWindowProc(IntPtr hwnd,IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int x,y;}
    [StructLayout(LayoutKind.Sequential)]struct NativeRect{public int left,top,right,bottom;}
    [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindowProc callback,IntPtr value);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr hwnd,StringBuilder name,int count);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW",SetLastError=true)]static extern IntPtr SetWindowLong32(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll")]static extern IntPtr CallWindowProc(IntPtr previous,IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")]static extern bool GetClientRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")]static extern short GetKeyState(int key);
    [DllImport("shell32.dll")]static extern void DragAcceptFiles(IntPtr hwnd,bool accept);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern uint DragQueryFile(IntPtr drop,uint index,StringBuilder file,uint count);
    [DllImport("shell32.dll")]static extern bool DragQueryPoint(IntPtr drop,out NativePoint point);
    [DllImport("shell32.dll")]static extern void DragFinish(IntPtr drop);
    [DllImport("FreeImage",EntryPoint="FreeImage_Load",CharSet=CharSet.Ansi)]static extern IntPtr DecodeImage(int format,string path,int flags);
    [DllImport("FreeImage",EntryPoint="FreeImage_Save",CharSet=CharSet.Ansi)]static extern bool ConvertImage(int format,IntPtr image,string path,int flags);
    [DllImport("FreeImage",EntryPoint="FreeImage_Unload")]static extern void UnloadImage(IntPtr image);
    static IntPtr dropWindow,oldWindowProc;
    static WindowProc dropProc;
    static bool dropTried,importing;
    sealed class DropRequest { public string[] files; public float x,y; }
    static readonly Queue<DropRequest> drops=new Queue<DropRequest>();
    public static bool DropReady {get{return dropWindow!=IntPtr.Zero;}}
    static void InstallDrops() {
        if(dropTried)return;dropTried=true;
        try {
            uint current=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr hwnd,IntPtr value){uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid!=current)return true;StringBuilder cls=new StringBuilder(128);GetClassName(hwnd,cls,128);if(cls.ToString()!="UnityWndClass")return true;dropWindow=hwnd;return false;},IntPtr.Zero);
            if(dropWindow==IntPtr.Zero){dropTried=false;return;}
            dropProc=DropWindowProc;IntPtr pointer=Marshal.GetFunctionPointerForDelegate(dropProc);
            oldWindowProc=IntPtr.Size==8?SetWindowLongPtr(dropWindow,-4,pointer):SetWindowLong32(dropWindow,-4,pointer);
            if(oldWindowProc==IntPtr.Zero){dropWindow=IntPtr.Zero;throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}
            DragAcceptFiles(dropWindow,true);
        }catch(Exception e){Error("File drop",e);}
    }
    static IntPtr DropWindowProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam) {
        // Windows queues key events even when a fast chord falls between Unity frames.
        if(message==0x100&&((long)lParam&0x40000000)==0&&(GetKeyState(0x11)&0x8000)!=0) {
            int key=(int)wParam;
            if(key==0x53||key==0x5a||key==0x59)nativeShortcut=key|((GetKeyState(0x10)&0x8000)!=0?0x100:0);
        }
        if(!quitting&&(message==0x10||(message==0x112&&((long)wParam&0xfff0)==0xf060))){nativeCloseRequested=true;return IntPtr.Zero;}
        if(message==0x233) {
            try {
                uint count=DragQueryFile(wParam,uint.MaxValue,null,0);List<string> paths=new List<string>();
                for(uint i=0;i<count;i++){uint size=DragQueryFile(wParam,i,null,0);StringBuilder path=new StringBuilder((int)size+1);DragQueryFile(wParam,i,path,size+1);paths.Add(path.ToString());}
                NativePoint p;DragQueryPoint(wParam,out p);NativeRect rect;GetClientRect(hwnd,out rect);
                lock(drops)drops.Enqueue(new DropRequest{files=paths.ToArray(),x=p.x,y=p.y});
            }catch(Exception e){Debug.LogError("Drop files: "+e.Message);}finally{DragFinish(wParam);}return IntPtr.Zero;
        }
        if(message==0x82){DragAcceptFiles(hwnd,false);IntPtr previous=oldWindowProc;dropWindow=IntPtr.Zero;return CallWindowProc(previous,hwnd,message,wParam,lParam);}
        return CallWindowProc(oldWindowProc,hwnd,message,wParam,lParam);
    }
    static void PollDrops() {
        InstallDrops();DropRequest request=null;lock(drops)if(drops.Count>0)request=drops.Dequeue();
        if(request!=null){NativeRect rect;GetClientRect(dropWindow,out rect);float factor=Screen.width/(float)Math.Max(1,rect.right-rect.left);HandleDrop(request.files,new Vector2(request.x*factor/uiScale,request.y*factor/uiScale));}
    }
    static void UninstallDrops() {
        if(dropWindow==IntPtr.Zero||oldWindowProc==IntPtr.Zero)return;
        DragAcceptFiles(dropWindow,false);
        if(IntPtr.Size==8)SetWindowLongPtr(dropWindow,-4,oldWindowProc);else SetWindowLong32(dropWindow,-4,oldWindowProc);
        dropWindow=IntPtr.Zero;oldWindowProc=IntPtr.Zero;
        lock(drops)drops.Clear();
    }
    public static int DropTarget(Vector2 point){for(int i=0;i<8;i++)if(dropRects[i].Contains(point))return i;return -1;}
    public static void HandleDrop(string[] files,Vector2 logicalPoint) {
        if(files==null||files.Length==0)return;
        if(Convert.ToBoolean(Get(Main,"hideGui"))||windowOpen||openRecent||openResolution||closePrompt||openAbout||openChannel>=0||openInput>=0||Convert.ToBoolean(Get(Get(Main,"fileBrowser"),"isActive"))){Status=T("请先关闭对话框，再拖入贴图","Close the dialog before dropping textures");return;}
        if(files.Length!=1){Status=T("每次请拖入一张贴图到目标缩略图区","Drop one texture at a time onto its thumbnail");return;}
        if(files[0].EndsWith(".mtz",StringComparison.OrdinalIgnoreCase)){Call(Get(Main,"SaveLoadProjectScript"),"LoadProject",files[0]);return;}
        int index=DropTarget(logicalPoint);if(index<0){Status=T("请将文件拖到对应贴图的缩略图区","Drop the file onto the target texture thumbnail");return;}
        ((MonoBehaviour)Main).StartCoroutine(ImportTexture(Get(Main,"SaveLoadProjectScript"),Convert.ToInt32(MapEnum(index)),files[0]));
    }
    public static void OpenFile(object main,string path){if(string.IsNullOrEmpty(path))return;Main=main;((MonoBehaviour)main).StartCoroutine(ImportTexture(Get(main,"SaveLoadProjectScript"),Convert.ToInt32(Get(main,"mapTypeToLoad")),path));}
    public static void PasteFile(object main){Main=main;Call(Get(main,"SaveLoadProjectScript"),"PasteFile",Get(main,"mapTypeToLoad"));}
    public static IEnumerator ImportTexture(object sl,int mapType,string path) {
        if(importing){Status=T("正在导入，请稍候","Import in progress");yield break;}
        importing=true;Set(sl,"busy",true);string temporary=null,converted=null;Texture2D texture=null;IntPtr native=IntPtr.Zero;
        try {
            Main=Get(sl,"mainGui");Bind();Commit();
            string typeName=Enum.GetName(Main.GetType().Assembly.GetType("MapType"),mapType);int index=Array.IndexOf(MapTypes,typeName);if(index<0)throw new ArgumentException("Unknown texture target");
            string ext=Path.GetExtension(path).ToLowerInvariant();
            string decodePath=path;
            if(ext!=".png"&&ext!=".jpg"&&ext!=".jpeg") {
                if(ext!=".bmp"&&ext!=".tga"&&ext!=".tif"&&ext!=".tiff")throw new InvalidDataException(T("不支持此图片格式","Unsupported image format"));
                temporary=Path.Combine(Application.dataPath,"enhance-import-"+Guid.NewGuid().ToString("N")+ext);File.Copy(path,temporary);
                converted=temporary+".png";int format=ext==".bmp"?0:ext==".tga"?17:18;
                native=DecodeImage(format,temporary,0);if(native==IntPtr.Zero||!ConvertImage(13,native,converted,0))throw new InvalidDataException("Cannot decode "+path);decodePath=converted;
            }
            texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            if(!texture.LoadImage(File.ReadAllBytes(decodePath)))throw new InvalidDataException("Cannot decode "+path);
            texture.anisoLevel=9;
            if(index<8)AdoptImportedTexture(index,texture);else {Set(Main,MapFields[index],texture);Call(Main,"SetLoadedTexture",MapEnum(index));}
            Commit();Status=T("贴图已导入（可撤销）","Texture imported (undo available)");texture=null;
        }catch(Exception e){DestroyUnusedTexture(texture);Error("Import",e);}finally {
            if(native!=IntPtr.Zero)UnloadImage(native);
            foreach(string file in new string[]{temporary,converted})if(file!=null&&File.Exists(file))File.Delete(file);
            Set(sl,"busy",false);importing=false;
        }
        yield break;
    }
}
