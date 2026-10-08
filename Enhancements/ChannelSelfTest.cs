using System;
using System.IO;
using System.Collections;
using System.Xml;
using UnityEngine;

public static class ChannelSelfTest
{
    static object Get(object o,string n){return MaterializeEnhancements.Get(o,n);}
    static object Call(object o,string n,params object[] args){return MaterializeEnhancements.Call(o,n,args);}
    static Texture2D Map(object main,int index){return Get(main,MaterializeEnhancements.MapFields[index]) as Texture2D;}
    static bool Matches(Texture2D map,Color32[] original,int mode,bool inverse) {
        if(map==null)return false;Color32[] pixels=map.GetPixels32();if(pixels.Length!=original.Length)return false;
        for(int i=0;i<pixels.Length;i++) {
            Color32 expected=original[i];
            if(mode!=0){byte value=mode==1?expected.r:mode==2?expected.g:mode==3?expected.b:expected.a;if(inverse)value=(byte)(255-value);expected=new Color32(value,value,value,255);}
            else if(inverse)expected=new Color32((byte)(255-expected.r),(byte)(255-expected.g),(byte)(255-expected.b),expected.a);
            if(!pixels[i].Equals(expected))return false;
        }
        return true;
    }
    public static IEnumerator Run(object main,string output,Action<bool,string> check) {
        string folder=Path.Combine(output,"channels");Directory.CreateDirectory(folder);
        object sl=Get(main,"SaveLoadProjectScript");
        Texture2D original=new Texture2D(12,9,TextureFormat.RGBA32,false);Color32[] pixels=new Color32[108];
        for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32((byte)((i*13+7)%256),(byte)((i*19+41)%256),(byte)((i*23+89)%256),(byte)((i*29+17)%256));
        original.SetPixels32(pixels);original.Apply();string source=Path.Combine(folder,"合并_RGBA.png");File.WriteAllBytes(source,original.EncodeToPNG());UnityEngine.Object.Destroy(original);
        MaterializeEnhancements.Commit();Call(main,"CloseWindows");Call(main,"ClearAllTextures");MaterializeEnhancements.Commit();
        string[] types={"height","diffuse","diffuseOriginal","normal","metallic","smoothness","edge","ao"};Type mapType=main.GetType().Assembly.GetType("MapType");
        for(int i=0;i<8;i++) {
            yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Convert.ToInt32(Enum.Parse(mapType,types[i])),source));
            int mode=i%4+1;MaterializeEnhancements.SetInputChannel(i,mode,false);
            check(Matches(Map(main,i),pixels,mode,false)&&MaterializeEnhancements.InputModes[i]==mode,"Independent source channel for "+types[i]);
        }
        for(int mode=1;mode<=4;mode++) {
            MaterializeEnhancements.SetInputChannel(4,mode,false);check(Matches(Map(main,4),pixels,mode,false),"Switch to "+new string[]{"","R","G","B","A"}[mode]+" always samples original packed texture");
            File.WriteAllBytes(Path.Combine(folder,"metallic_"+new string[]{"","R","G","B","A"}[mode]+".png"),Map(main,4).EncodeToPNG());
        }
        MaterializeEnhancements.SetInputChannel(4,0,false);check(Matches(Map(main,4),pixels,0,false),"Full image restores all original RGBA bytes");
        Material fullMaterial=Get(main,"FullMaterial") as Material;
        check(object.ReferenceEquals(fullMaterial.GetTexture("_MetallicMap"),Map(main,4)),"Full material uses the newly selected texture channel");
        foreach(string extension in new string[]{"tga","tiff"}) {
            Texture2D packed=Map(main,4);string file=Path.Combine(folder,"合并_RGBA_"+extension);
            yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.SaveTexture(sl,extension,packed,file));
            MaterializeEnhancements.SetInputChannel(4,4,false);
            yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Convert.ToInt32(Enum.Parse(mapType,"metallic")),file+"."+extension));
            check(Matches(Map(main,4),pixels,4,false),"Alpha extraction survives Unicode "+extension+" import");
            MaterializeEnhancements.SetInputChannel(4,0,false);
        }
        MaterializeEnhancements.SetInputChannel(5,2,true);check(Matches(Map(main,5),pixels,2,true),"Inverted G yields exact roughness-to-smoothness values");
        File.WriteAllBytes(Path.Combine(folder,"smoothness_G_inverted.png"),Map(main,5).EncodeToPNG());
        MaterializeEnhancements.SetInputChannel(4,4,false);MaterializeEnhancements.Undo();check(MaterializeEnhancements.InputModes[4]==0&&Matches(Map(main,4),pixels,0,false),"Undo restores source-channel choice and pixels");
        yield return new WaitForSeconds(0.4f);
        MaterializeEnhancements.Redo();check(MaterializeEnhancements.InputModes[4]==4&&Matches(Map(main,4),pixels,4,false),"Source-channel redo survives later frames");
        MaterializeEnhancements.SetInputChannel(4,1,false);check(Matches(Map(main,4),pixels,1,false),"Original packed channels remain selectable after undo and redo");
        MaterializeEnhancements.ChooseChannel(0,2);Texture2D property=Map(main,8);Color32[] propertyPixels=property.GetPixels32();bool good=propertyPixels.Length==pixels.Length;
        for(int i=0;i<propertyPixels.Length&&good;i++)good=propertyPixels[i].r==pixels[i].r;
        check(good,"Property-map packing reads the extracted source channel");
        MaterializeEnhancements.Commit();Call(main,"ClearTexture",Enum.Parse(mapType,"ao"));MaterializeEnhancements.Commit();
        MaterializeEnhancements.SetInputChannel(7,2,true);
        yield return ((MonoBehaviour)sl).StartCoroutine(MaterializeEnhancements.ImportTexture(sl,Convert.ToInt32(Enum.Parse(mapType,"ao")),source));
        check(Matches(Map(main,7),pixels,2,true),"Import uses the channel selected before loading a texture");
        MaterializeEnhancements.Undo();check(Map(main,7)==null&&MaterializeEnhancements.InputModes[7]==2&&MaterializeEnhancements.InputInvert[7],"Undo import preserves the preselected source channel");
        MaterializeEnhancements.Redo();check(Matches(Map(main,7),pixels,2,true),"Redo import restores raw source and extracted pixels");
        for(int i=0;i<8;i++)MaterializeEnhancements.SetInputChannel(i,i%4+1,i==5);
        string savedDir=Path.Combine(folder,"saved");Directory.CreateDirectory(savedDir);string project=Path.Combine(savedDir,"channels.mtz");MaterializeEnhancements.SaveProject(sl,project,2);
        check(Directory.GetFiles(savedDir).Length==1&&File.ReadAllText(project).Contains("<zhInputSources>"),"Channel project embeds original sources without exporting sidecar textures");
        string portable=Path.Combine(folder,"portable");Directory.CreateDirectory(portable);string moved=Path.Combine(portable,"channels.mtz");File.Copy(project,moved,true);
        for(int i=0;i<8;i++)MaterializeEnhancements.SetInputChannel(i,0,false);Call(sl,"LoadProject",moved);
        yield return new WaitForSeconds(0.7f);
        bool restored=true;
        for(int i=0;i<8;i++)restored&=MaterializeEnhancements.InputModes[i]==i%4+1&&MaterializeEnhancements.InputInvert[i]==(i==5)&&Matches(Map(main,i),pixels,i%4+1,i==5);
        check(restored,"Portable project restores all eight channel choices, inversions and pixels");
        for(int i=0;i<8;i++){MaterializeEnhancements.SetInputChannel(i,0,false);restored&=Matches(Map(main,i),pixels,0,false);}
        check(restored,"Portable project retains every original channel for later reselection");
        MaterializeEnhancements.SetInputChannel(4,3,true);MaterializeEnhancements.Commit();Texture2D generated=new Texture2D(12,9,TextureFormat.RGBA32,false);Color32[] edited=(Color32[])pixels.Clone();edited[0]=new Color32(211,23,87,192);generated.SetPixels32(edited);generated.Apply();MaterializeEnhancements.Set(main,"_MetallicMap",generated);MaterializeEnhancements.Commit();
        check(MaterializeEnhancements.InputModes[4]==0&&!MaterializeEnhancements.InputInvert[4],"New generated texture becomes a fresh full-image source");
        MaterializeEnhancements.Undo();MaterializeEnhancements.SetInputChannel(4,2,false);check(Matches(Map(main,4),pixels,2,false),"Undo generation restores the previous packed original");
        Texture2D live=Map(main,3);MaterializeEnhancements.Commit();live.SetPixels32(edited);live.Apply();MaterializeEnhancements.ImageApplied(live);MaterializeEnhancements.Commit();
        MaterializeEnhancements.SetInputChannel(3,1,false);check(Matches(Map(main,3),edited,1,false),"In-place pixel edits become the source for later channel selection");
        XmlDocument oldDocument=new XmlDocument();oldDocument.Load(Path.Combine(Path.Combine(output,"isolated"),"portable.mtz"));
        foreach(string field in new string[]{"zhInputModes","zhInputInvert","zhInputSources"}){XmlNode node=oldDocument.DocumentElement.SelectSingleNode(field);if(node!=null)oldDocument.DocumentElement.RemoveChild(node);}
        string older=Path.Combine(folder,"v2-project.mtz");oldDocument.Save(older);Call(sl,"LoadProject",older);
        yield return new WaitForSeconds(0.7f);
        bool oldProject=true;for(int i=0;i<8;i++)oldProject&=MaterializeEnhancements.InputModes[i]==0&&!MaterializeEnhancements.InputInvert[i];
        check(oldProject,"Earlier embedded projects load with full-image source defaults");
    }
}
