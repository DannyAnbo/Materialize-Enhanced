using System;
using System.Globalization;
using UnityEngine;

public static partial class MaterializeEnhancements
{
    static bool ResetButton(Rect rect,string type,string field) {
        bool clicked=field.Length>0&&GUI.Button(rect,new GUIContent("↶",T("恢复此参数的默认值","Reset this parameter to its default")));
        if(clicked)Status=T("已恢复此参数的默认值（可撤销）","Parameter default restored (undo available)");return clicked;
    }
    static bool ParameterNumber(Rect rect,string title,float value,string text,out float result,out string resultText,float min,float max,string type,string field,bool integer,bool labelled) {
        float y=rect.y+(labelled?20:0);bool changed=false;
        if(labelled)GUI.Label(new Rect(rect.x,rect.y,rect.width-25,20),title);
        string control="parameter-"+type+"-"+field+"-"+rect.x+"-"+rect.y;
        float slider=FreeSlider(new Rect(rect.x,y,rect.width-62,16),value,min,max);
        if(integer)slider=Mathf.Round(slider);
        if(slider!=value){value=slider;text=value.ToString("G6",CultureInfo.InvariantCulture);changed=true;}
        bool keyDown=Event.current.type==EventType.KeyDown;string previousText=text;
        GUI.SetNextControlName(control);text=GUI.TextField(new Rect(rect.x+rect.width-52,y-4,52,22),text??value.ToString(CultureInfo.InvariantCulture));
        if((text!=previousText||keyDown)&&GUI.GetNameOfFocusedControl()==control) {
            float parsed;if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed)&&!float.IsNaN(parsed)&&!float.IsInfinity(parsed)) {
                parsed=ClampInput(parsed,min,max);if(integer)parsed=Mathf.Round(parsed);changed|=parsed!=value;value=parsed;
            }
        }
        Rect reset=labelled?new Rect(rect.x+rect.width-22,rect.y-2,22,20):new Rect(rect.x+rect.width-22,rect.y-25,22,20);
        if(ResetButton(reset,type,field)){Commit();value=Convert.ToSingle(DefaultParameter(type,field));text=value.ToString("G6",CultureInfo.InvariantCulture);changed=true;GUIUtility.keyboardControl=0;}
        result=value;resultText=text;return changed;
    }
    public static bool ParameterFloat(Rect r,string title,float v,string text,out float result,out string rt,float min,float max,string type,string field){return ParameterNumber(r,title,v,text,out result,out rt,min,max,type,field,false,true);}
    public static bool ParameterInt(Rect r,string title,int v,string text,out int result,out string rt,int min,int max,string type,string field){float value;bool changed=ParameterNumber(r,title,v,text,out value,out rt,min,max,type,field,true,true);result=(int)value;return changed;}
    public static bool ParameterFloatBare(Rect r,float v,string text,out float result,out string rt,float min,float max,string type,string field){return ParameterNumber(r,"",v,text,out result,out rt,min,max,type,field,false,false);}
    public static bool ParameterIntBare(Rect r,int v,string text,out int result,out string rt,int min,int max,string type,string field){float value;bool changed=ParameterNumber(r,"",v,text,out value,out rt,min,max,type,field,true,false);result=(int)value;return changed;}
    public static float ParameterHorizontal(Rect r,float v,float min,float max,string type,string field) {
        Rect reset=new Rect(r.xMax-18,r.y-4,18,20);if(field.Length>0)r.width-=23;
        float value=FreeSlider(r,v,min,max);if(ResetButton(reset,type,field)){Commit();value=Convert.ToSingle(DefaultParameter(type,field));}return value;
    }
    public static float ParameterVertical(Rect r,float v,float min,float max,string type,string field) {
        Rect reset=new Rect(r.x-2,r.yMax-18,Mathf.Max(16,r.width),18);if(field.Length>0)r.height-=23;
        float value=FreeVerticalSlider(r,v,min,max);if(ResetButton(reset,type,field)){Commit();value=Convert.ToSingle(DefaultParameter(type,field));}return value;
    }
    public static bool ParameterVerticalHelper(Rect r,float v,out float result,float min,float max,bool dirty,string type,string field){result=ParameterVertical(r,v,min,max,type,field);return dirty||v!=result;}
    public static bool ParameterToggle(Rect r,bool value,string title,string type,string field) {
        Rect reset=new Rect(r.xMax-20,r.y,20,20);if(field.Length>0)r.width-=22;
        bool result=GUI.Toggle(r,value,title);if(ResetButton(reset,type,field)){Commit();result=Convert.ToBoolean(DefaultParameter(type,field));}return result;
    }
    public static bool ParameterToggleHelper(Rect r,bool value,out bool result,string title,bool dirty,string type,string field){result=ParameterToggle(r,value,title,type,field);return dirty||result!=value;}
}
