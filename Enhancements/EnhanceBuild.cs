using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

if(args[0]=="--data") {
    var source=ModuleDefMD.Load(args[1]);
    var output=new System.Text.StringBuilder("using System;\npublic static class FactoryDefaults { public static object Get(string type,string field) { switch(type+\".\"+field) {\n");
    foreach(var t in source.Types) {
        var ctor=t.Methods.FirstOrDefault(x=>x.IsInstanceConstructor&&x.MethodSig.Params.Count==0&&x.Body!=null);
        foreach(var f in t.Fields.Where(f=>!f.IsStatic&&new[]{"System.Single","System.Int32","System.Boolean"}.Contains(f.FieldType.FullName))) {
            string value=f.FieldType.FullName=="System.Boolean"?"false":f.FieldType.FullName=="System.Single"?"0f":"0";
            if(ctor!=null)for(int i=1;i<ctor.Body.Instructions.Count;i++)if(ctor.Body.Instructions[i].OpCode==OpCodes.Stfld&&ctor.Body.Instructions[i].Operand is IField target&&target.Name==f.Name){
                var previous=ctor.Body.Instructions[i-1];
                if(previous.OpCode==OpCodes.Ldc_R4)value=((float)previous.Operand).ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"f";
                else if(previous.IsLdcI4())value=f.FieldType.FullName=="System.Boolean"?(previous.GetLdcI4Value()!=0?"true":"false"):previous.GetLdcI4Value().ToString()+(f.FieldType.FullName=="System.Single"?"f":"");
            }
            output.AppendLine("case \""+t.Name+"."+f.Name+"\": return "+value+";");
        }
    }
    output.AppendLine("default: throw new ArgumentException(type+\".\"+field); } } }");File.WriteAllText(args[2],output.ToString(),new System.Text.UTF8Encoding(true));return;
}
// Always build from the preserved original. The helper stays in its own assembly.
var mod = ModuleDefMD.Load(args[0]);
var helper = ModuleDefMD.Load(args[1]);
var imp = new Importer(mod);
var ht = helper.Types.Single(t => t.Name == "MaterializeEnhancements");
IMethod H(string name) => imp.Import(ht.Methods.Single(m => m.Name == name));
MethodDef M(string type, string method) => mod.Types.Single(t => t.Name == type).Methods.Single(m => m.Name == method);
void Prefix(MethodDef method, params Instruction[] instructions) {
    method.Body.SimplifyBranches();
    for (int i=0; i<instructions.Length; i++) method.Body.Instructions.Insert(i,instructions[i]);
}
void Replace(MethodDef method, string helperName) {
    method.Body = new CilBody { InitLocals = true };
    for(int i=0;i<method.Parameters.Count;i++) method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg,method.Parameters[i]));
    method.Body.Instructions.Add(Instruction.Create(OpCodes.Call,H(helperName)));
    method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
}
var po = mod.Types.Single(t => t.Name == "ProjectObject");
po.Fields.Add(new FieldDefUser("zhEmbedded",new FieldSig(new SZArraySig(mod.CorLibTypes.String)),FieldAttributes.Public));
po.Fields.Add(new FieldDefUser("zhMapNames",new FieldSig(new SZArraySig(mod.CorLibTypes.String)),FieldAttributes.Public));
po.Fields.Add(new FieldDefUser("zhChannels",new FieldSig(new SZArraySig(mod.CorLibTypes.Int32)),FieldAttributes.Public));
po.Fields.Add(new FieldDefUser("zhInputModes",new FieldSig(new SZArraySig(mod.CorLibTypes.Int32)),FieldAttributes.Public));
po.Fields.Add(new FieldDefUser("zhInputInvert",new FieldSig(new SZArraySig(mod.CorLibTypes.Boolean)),FieldAttributes.Public));
po.Fields.Add(new FieldDefUser("zhInputSources",new FieldSig(new SZArraySig(mod.CorLibTypes.String)),FieldAttributes.Public));
po.Fields.Add(new FieldDefUser("zhSessionValues",new FieldSig(new SZArraySig(mod.CorLibTypes.String)),FieldAttributes.Public));
po.Fields.Add(new FieldDefUser("zhTextureSize",new FieldSig(new SZArraySig(mod.CorLibTypes.Int32)),FieldAttributes.Public));
Prefix(M("MainGui","Update"),Instruction.Create(OpCodes.Ldarg_0),Instruction.Create(OpCodes.Call,H("Tick")));
Replace(M("MainGui","OnGUI"),"Draw");
Replace(M("MainGui","OpenFile"),"OpenFile");
Replace(M("MainGui","PasteFile"),"PasteFile");
Replace(M("SaveLoadProject","SaveProject"),"SaveProject");
Replace(M("SaveLoadProject","LoadProject"),"LoadProject");
Replace(M("SaveLoadProject","SaveAllFiles"),"ExportAll");
Replace(M("SaveLoadProject","LoadTexture"),"ImportTexture");
Replace(M("SaveLoadProject","SaveTexture"),"SaveTexture");
Replace(M("SaveLoadProject","SaveFile"),"SaveFile");
// Keep the original external-texture loader for projects without embedded images.
M("SaveLoadProject","LoadAllTextures").Name="LoadAllTexturesLegacy";
int sliderCount=0,clampCount=0,resetCount=0;
var noTranslate=new HashSet<Instruction>();
foreach(var t in mod.GetTypes()) foreach(var m in t.Methods) {
    if(m.Body==null)continue;
    m.Body.SimplifyBranches();
    foreach(var i in m.Body.Instructions.ToArray()) {
        if(!(i.Operand is IMethod r))continue;
        int at=m.Body.Instructions.IndexOf(i);
        IField parameter=null;
        // Out-value arguments identify the exact field, even when several sliders share a label.
        if(r.DeclaringType.Name=="GuiHelper"&&(r.Name=="Slider"||r.Name=="VerticalSlider"||r.Name=="Toggle")) {
            for(int k=at-1;k>=Math.Max(0,at-35);k--)if(m.Body.Instructions[k].OpCode==OpCodes.Ldflda&&m.Body.Instructions[k].Operand is IField f&&!f.Name.String.EndsWith("Text")){parameter=f;break;}
        } else if(r.DeclaringType.Name=="GUI"&&r.Name=="Toggle") {
            for(int k=at-1;k>=Math.Max(0,at-12);k--)if(m.Body.Instructions[k].OpCode==OpCodes.Ldfld&&m.Body.Instructions[k].Operand is IField f&&f.FieldSig.Type.FullName=="System.Boolean"){parameter=f;break;}
        } else if(r.DeclaringType.Name=="GUI"&&(r.Name=="HorizontalSlider"||r.Name=="VerticalSlider")) {
            for(int k=at+1;k<Math.Min(m.Body.Instructions.Count,at+8);k++){var next=m.Body.Instructions[k];if(next.OpCode==OpCodes.Stfld&&next.Operand is IField f){parameter=f;break;}if(next.OpCode.FlowControl==FlowControl.Cond_Branch||next.OpCode.FlowControl==FlowControl.Branch)break;}
        }
        if(parameter!=null&&t.Name!="GuiHelper") {
            string replacement=null;
            if(r.DeclaringType.Name=="GuiHelper") {
                if(r.Name=="Slider") {bool labeled=r.MethodSig.Params[1].FullName=="System.String";bool integer=r.MethodSig.Params[labeled?2:1].FullName=="System.Int32";replacement=integer?(labeled?"ParameterInt":"ParameterIntBare"):(labeled?"ParameterFloat":"ParameterFloatBare");}
                else if(r.Name=="VerticalSlider")replacement="ParameterVerticalHelper";else if(r.Name=="Toggle")replacement="ParameterToggleHelper";
            }else if(r.DeclaringType.Name=="GUI") {if(r.Name=="HorizontalSlider")replacement="ParameterHorizontal";else if(r.Name=="VerticalSlider")replacement="ParameterVertical";else if(r.Name=="Toggle"&&r.MethodSig.Params.Count==3&&r.MethodSig.Params[2].FullName=="System.String")replacement="ParameterToggle";}
            if(replacement!=null){var typeString=Instruction.Create(OpCodes.Ldstr,parameter.DeclaringType.Name);var fieldString=Instruction.Create(OpCodes.Ldstr,parameter.Name);noTranslate.Add(typeString);noTranslate.Add(fieldString);m.Body.Instructions.Insert(at,typeString);m.Body.Instructions.Insert(at+1,fieldString);i.OpCode=OpCodes.Call;i.Operand=H(replacement);resetCount++;continue;}
        }
        if(r.DeclaringType.Namespace!="UnityEngine")continue;
        if(t.Name=="FileBrowser"&&m.Name=="GuiWindow"&&r.DeclaringType.Name=="GUILayout"&&r.Name=="TextField"&&r.MethodSig.Params.Count==2)i.Operand=H("FilenameTextField");
        if(r.Name=="HorizontalSlider" && r.MethodSig.Params.Count==4) {
            if(r.DeclaringType.Name=="GUI") { i.Operand=H("FreeSlider");sliderCount++; }
            else if(r.DeclaringType.Name=="GUILayout") { i.Operand=H("FreeSliderL");sliderCount++; }
        }
        if(r.Name=="VerticalSlider" && r.MethodSig.Params.Count==4) {
            if(r.DeclaringType.Name=="GUI") { i.Operand=H("FreeVerticalSlider");sliderCount++; }
            else if(r.DeclaringType.Name=="GUILayout") { i.Operand=H("FreeVerticalSliderL");sliderCount++; }
        }
        if(t.Name=="GuiHelper" && r.Name=="Clamp" && r.MethodSig.Params.Count==3) { i.Operand=H("ClampInput");clampCount++; }
        if(r.DeclaringType.Name=="Texture2D"&&r.Name=="Apply"&&r.MethodSig.Params.Count==0) {
            // Duplicate the texture receiver so the post-Apply hook sees in-place edits too.
            at=m.Body.Instructions.IndexOf(i);m.Body.Instructions.Insert(at,Instruction.Create(OpCodes.Dup));m.Body.Instructions.Insert(at+2,Instruction.Create(OpCodes.Call,H("ImageApplied")));
        }
        if(r.DeclaringType.Name=="GUI") {
            string[] simple={"Label","Box","Button","Toggle","Window"};
            if(simple.Contains(r.Name.String)&&r.MethodSig.Params.Last().FullName=="System.String"&&((r.Name=="Window"&&r.MethodSig.Params.Count==4)||(r.Name=="Toggle"&&r.MethodSig.Params.Count==3)||(new[]{"Label","Box","Button"}.Contains(r.Name.String)&&r.MethodSig.Params.Count==2)))i.Operand=H(r.Name);
        }
    }
}
var pm=M("MainGui","ProcessPropertyMap");
// TextureFormat.RGB24 (3) -> RGBA32 (4) for the property map only.
foreach(var i in pm.Body.Instructions.ToArray()) {
    if(i.OpCode==OpCodes.Newobj && i.Operand is IMethod ctor && ctor.DeclaringType.Name=="Texture2D") {
        int at=pm.Body.Instructions.IndexOf(i);
        var format=pm.Body.Instructions[at-2];
        if(!format.IsLdcI4() || format.GetLdcI4Value()!=3)throw new Exception("Unexpected property texture constructor");
        format.OpCode=OpCodes.Ldc_I4_4;format.Operand=null;
    }
}
// Modify ret itself so branches targeting the old ret also execute the hook.
foreach(var ret in pm.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray()) {
    int at=pm.Body.Instructions.IndexOf(ret);ret.OpCode=OpCodes.Ldarg_0;
    pm.Body.Instructions.Insert(at+1,Instruction.Create(OpCodes.Call,H("ApplyAlpha")));
    pm.Body.Instructions.Insert(at+2,Instruction.Create(OpCodes.Ret));
}
if(args.Length>3 && args[3]!="NONE") {
    var map=new Dictionary<string,string>();
    foreach(var n in JsonNode.Parse(File.ReadAllText(args[3])).AsArray()) if(n["zh"]!=null && !string.IsNullOrWhiteSpace((string)n["zh"]))map[(string)n["s"]]=(string)n["zh"];
    foreach(var kv in JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(args[3]),"overrides.json"))).AsObject())map[kv.Key]=(string)kv.Value;
    foreach(var t in mod.GetTypes())foreach(var m in t.Methods)if(m.Body!=null)foreach(var i in m.Body.Instructions.ToArray())if(!noTranslate.Contains(i)&&i.OpCode==OpCodes.Ldstr && i.Operand is string s && map.TryGetValue(s,out var zh)&&s!=zh){int at=m.Body.Instructions.IndexOf(i);m.Body.Instructions.Insert(at+1,Instruction.Create(OpCodes.Ldstr,zh));m.Body.Instructions.Insert(at+2,Instruction.Create(OpCodes.Call,H("Localize")));}
}
// Recalculate stack sizes. Preserve the IDs of scene-serialized original types.
var opts=new ModuleWriterOptions(mod);opts.MetadataOptions.Flags|=MetadataFlags.PreserveRids;
mod.Write(args[2],opts);
Console.WriteLine("Built "+args[2]+"; sliders="+sliderCount+"; parameter resets="+resetCount+"; input clamps="+clampCount);
