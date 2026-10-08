using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Xml;
using UnityEngine;

public static class RecentSelfTest
{
    static object Get(object o,string n){return MaterializeEnhancements.Get(o,n);}
    static object Call(object o,string n,params object[] args){return MaterializeEnhancements.Call(o,n,args);}
    static bool SameList(string[] a,string[] b){return string.Join("\n",a)==string.Join("\n",b);}
    public static IEnumerator Run(object main,string output,Action<bool,string> check) {
        string folder=Path.Combine(output,"recent");Directory.CreateDirectory(folder);
        string storage=Path.Combine(folder,"entries.txt");MaterializeEnhancements.RecentStorageOverride=storage;MaterializeEnhancements.ReadRecentProjects();
        check(MaterializeEnhancements.RecentProjects.Length==0,"Recent list starts empty and uses isolated test storage");
        object sl=Get(main,"SaveLoadProjectScript");
        string source=Path.Combine(Path.Combine(Path.Combine(output,"channels"),"portable"),"channels.mtz");
        string first=Path.Combine(folder,"材质.版本一.mtz"),second=Path.Combine(folder,"材质.版本二.mtz");File.Copy(source,first,true);File.Copy(source,second,true);
        string saved=Path.Combine(folder,"saved-only.mtz");MaterializeEnhancements.SaveProject(sl,saved,2);
        check(MaterializeEnhancements.RecentProjects.Length==0,"Saving without opening does not enter recently opened list");
        Call(sl,"LoadProject",first);yield return new WaitForSeconds(0.2f);
        string[] entries=MaterializeEnhancements.RecentProjects;
        check(entries.Length==1&&entries[0]==first,"Successful load records absolute Unicode path with dots");
        MaterializeEnhancements.HandleDrop(new string[]{second},Vector2.zero);yield return new WaitForSeconds(0.2f);
        entries=MaterializeEnhancements.RecentProjects;check(entries.Length==2&&entries[0]==second&&entries[1]==first,"Project drag loading enters newest-first recent list");
        MaterializeEnhancements.SetInputChannel(4,0,false);MaterializeEnhancements.OpenRecentProject(first);yield return new WaitForSeconds(0.2f);
        entries=MaterializeEnhancements.RecentProjects;
        Texture2D texture=Get(main,"_MetallicMap") as Texture2D;
        check(entries.Length==2&&entries[0]==first&&entries[1]==second,"Reopening from recent list moves project to top without duplicate");
        check(MaterializeEnhancements.InputModes[4]==1&&texture!=null&&texture.width==12&&texture.GetPixels32()[0].r==7,"Recent entry restores embedded pixels and original channel selection");
        Call(sl,"LoadProject",first.ToUpperInvariant());yield return new WaitForSeconds(0.2f);
        check(MaterializeEnhancements.RecentProjects.Length==2,"Path deduplication is case-insensitive on Windows");
        entries=MaterializeEnhancements.RecentProjects;texture=Get(main,"_MetallicMap") as Texture2D;Call(sl,"LoadProject",(object)null);Call(sl,"LoadProject","");
        check(SameList(entries,MaterializeEnhancements.RecentProjects)&&object.ReferenceEquals(texture,Get(main,"_MetallicMap")),"Cancelled load leaves recent list and current texture unchanged");
        texture=Get(main,"_MetallicMap") as Texture2D;
        string invalid=Path.Combine(folder,"invalid.mtz");File.WriteAllText(invalid,"not a project");Call(sl,"LoadProject",invalid);
        check(SameList(entries,MaterializeEnhancements.RecentProjects)&&object.ReferenceEquals(texture,Get(main,"_MetallicMap")),"Invalid XML is not recorded and leaves current texture intact");
        MaterializeEnhancements.OpenRecentProject(Path.Combine(folder,"missing.mtz"));
        check(SameList(entries,MaterializeEnhancements.RecentProjects)&&object.ReferenceEquals(texture,Get(main,"_MetallicMap")),"Missing recent project fails safely without modifying list or texture");
        MaterializeEnhancements.ReadRecentProjects();check(SameList(entries,MaterializeEnhancements.RecentProjects),"Recent paths and order survive reloading persisted storage");
        XmlDocument doc=new XmlDocument();doc.Load(source);doc.DocumentElement.SelectSingleNode("zhEmbedded/string").InnerText="not-base64";
        string broken=Path.Combine(folder,"broken-embedded.mtz");doc.Save(broken);Call(sl,"LoadProject",broken);yield return new WaitForSeconds(0.2f);
        check(SameList(entries,MaterializeEnhancements.RecentProjects)&&!Convert.ToBoolean(Get(sl,"busy")),"Failed embedded-image load is not recorded and releases loading state");
        string legacy=Path.Combine(Path.Combine(output,"folder.with.dots"),"legacy.mtz");Call(sl,"LoadProject",legacy);yield return new WaitForSeconds(0.3f);
        check(MaterializeEnhancements.RecentProjects[0]==legacy,"Completed legacy external-texture project enters recent list");
        for(int i=0;i<14;i++) {
            string file=Path.Combine(folder,"project-"+i.ToString("D2")+".mtz");File.Copy(source,file,true);Call(sl,"LoadProject",file);yield return new WaitForSeconds(0.08f);
        }
        entries=MaterializeEnhancements.RecentProjects;
        check(entries.Length==12&&entries[0].EndsWith("project-13.mtz")&&entries[11].EndsWith("project-02.mtz"),"Recent list retains newest 12 successful opens");
        File.Delete(entries[2]);MaterializeEnhancements.ReadRecentProjects();
        check(SameList(entries,MaterializeEnhancements.RecentProjects)&&!File.Exists(entries[2]),"Missing files retain their recorded position for the missing-file UI");
        File.AppendAllText(storage,"\ninvalid base64\n"+Convert.ToBase64String(Encoding.UTF8.GetBytes(entries[0]))+"\n");
        MaterializeEnhancements.ReadRecentProjects();check(SameList(entries,MaterializeEnhancements.RecentProjects),"Malformed history rows and duplicate entries do not prevent reading valid history");
        check(!File.Exists(storage+".writing"),"Persisted recent list leaves no incomplete staging file");
        File.WriteAllLines(storage,new string[]{"invalid base64",Convert.ToBase64String(Encoding.UTF8.GetBytes(first)),Convert.ToBase64String(Encoding.UTF8.GetBytes(first.ToUpperInvariant())),Convert.ToBase64String(Encoding.UTF8.GetBytes(saved+".png")),Convert.ToBase64String(Encoding.UTF8.GetBytes(second))});
        MaterializeEnhancements.ReadRecentProjects();entries=MaterializeEnhancements.RecentProjects;
        check(entries.Length==2&&entries[0]==first&&entries[1]==second,"History reader skips bad rows, nonprojects and duplicates while preserving order");
        MaterializeEnhancements.OpenRecentProject(first);yield return new WaitForSeconds(0.2f);
        check(!Convert.ToBoolean(Get(sl,"busy")),"Successful recent load returns to ready state");
    }
}
