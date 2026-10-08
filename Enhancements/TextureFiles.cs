using System;
using System.IO;
using System.Collections;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    // FreeImage uses its UTF-16 APIs; .NET stages the final Unicode filename atomically.
    public static IEnumerator SaveTexture(object sl,string extension,Texture2D texture,string path) {
        if(texture==null)yield break;
        Set(sl,"busy",true);string input=null,converted=null,staged=null;IntPtr image=IntPtr.Zero;Texture2D original=texture;
        try {
            texture=WorkflowTexture(texture);
            texture=ResizeTexture(texture,TextureWidth,TextureHeight);
            extension=extension.ToLowerInvariant();if(extension=="jpeg")extension="jpg";if(extension=="tif")extension="tiff";
            string destination=Path.GetFullPath(path+"."+extension);Directory.CreateDirectory(Path.GetDirectoryName(destination));
            byte[] bytes;
            if(extension=="png")bytes=texture.EncodeToPNG();
            else if(extension=="jpg")bytes=texture.EncodeToJPG();
            else {
                if(extension!="tga"&&extension!="tiff"&&extension!="bmp")throw new InvalidDataException("Unsupported export format: "+extension);
                string token=Path.Combine(Application.dataPath,"enhance-export-"+Guid.NewGuid().ToString("N"));input=token+".png";converted=token+"."+extension;
                File.WriteAllBytes(input,texture.EncodeToPNG());image=DecodeImage(13,input,0);
                if(image==IntPtr.Zero||!ConvertImage(extension=="bmp"?0:extension=="tga"?17:18,image,converted,extension=="tiff"?2048:0))throw new IOException("Cannot encode "+extension);
                bytes=File.ReadAllBytes(converted);
            }
            staged=destination+".writing";File.WriteAllBytes(staged,bytes);
            if(File.Exists(destination))File.Replace(staged,destination,null);else File.Move(staged,destination);staged=null;
            RememberSave(original,destination);Status=T("贴图已保存：","Texture saved: ")+destination;
        }catch(Exception e){Error("Save texture",e);}finally {
            if(image!=IntPtr.Zero)UnloadImage(image);
            if(!object.ReferenceEquals(texture,original)&&!object.ReferenceEquals(texture,roughnessDisplay))UnityEngine.Object.Destroy(texture);
            foreach(string file in new string[]{input,converted,staged})if(file!=null&&File.Exists(file))File.Delete(file);
            Set(sl,"busy",false);
        }
        yield break;
    }
    static void RememberSave(Texture2D texture,string path) {
        if(Main==null)return;
        for(int i=0;i<8;i++)if(object.ReferenceEquals(Get(Main,MapFields[i]),texture))Set(Main,QuickFields[i],path);
        if(object.ReferenceEquals(Get(Main,"_PropertyMap"),texture))Set(Main,"QuicksavePathProperty",path);
    }
    public static void SaveFile(object sl,string path,int format,Texture2D texture,string suffix) {
        if(string.IsNullOrEmpty(path)||texture==null)return;
        // Only remove a real texture extension. A dot in a directory or asset name is preserved.
        string ext=Path.GetExtension(path).ToLowerInvariant();
        if(Array.IndexOf(new string[]{".png",".jpg",".jpeg",".tga",".bmp",".tif",".tiff"},ext)>=0)path=path.Substring(0,path.Length-ext.Length);
        ((MonoBehaviour)sl).StartCoroutine(SaveTexture(sl,Extension(format),texture,path+suffix));
    }
}
