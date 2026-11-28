using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit, resettable experiments; these controls do not alter campaign rules.
public sealed class FanhuaMechanismLab : MonoBehaviour
{
    private static int experiment;
    private static readonly int[] Palettes={2,0,1,3};
    private static readonly string[] Names={"红黄：纸花保形与外侧刺","蓝黄：蓄水／泄水与承托","红上蓝下：水落、红失托碎裂","蓝上红下：合成一格下落灰石"};
    private Font font;private GUIStyle label,button;
    private void Awake(){GetComponent<PrototypePlayer>().ConfigureLabPalette(Palettes[experiment]);}
    private void OnGUI()
    {
        if(label==null){font=PrototypePlayer.CreateChineseHudFont(16);label=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};label.normal.textColor=new Color(.15f,.2f,.2f);button=new GUIStyle(GUI.skin.button){font=font,fontSize=15};}
        float width=Mathf.Min(Screen.width-24,920);var area=new Rect(12,Screen.height-170,width,158);
        PrototypePlayer.DrawPanel(area);
        GUI.Label(new Rect(area.x+12,area.y+8,width-180,28),"翻花专项实验 · 初始组合："+Names[experiment],label);
        var player=GetComponent<PrototypePlayer>();
        if(GUI.Button(new Rect(area.xMax-154,area.y+6,142,28),player.Mode==PlayerMode.Grid?"切换到 2D":"切换到 2.5D",button))player.RequestLabModeSwitch();
        GUI.Label(new Rect(area.x+12,area.y+34,width-24,54),"起点按D拉开；面朝纸花移动推回；绕到中段上／下侧，朝纸花走来推弯。右Shift切2D观察：黄色保持原形、蓝色化水、红色为刺。Z撤回，R重置。",label);
        float w=(width-30)/4;
        for(int i=0;i<Names.Length;i++)if(GUI.Button(new Rect(area.x+9+i*w,area.y+94,w-4,34),(i+1)+" · "+new[]{"红黄","蓝黄","红上蓝下","蓝上红下"}[i],button))
        {experiment=i;SceneManager.LoadScene("FanhuaMechanismLab");}
        GUI.Label(new Rect(area.x+12,area.y+130,width-24,22),"切换实验会重置本关；3D中水冻结。无通关门，先专注形状与组合。",label);
    }
    private void OnDestroy(){if(font!=null)Destroy(font);}
}
