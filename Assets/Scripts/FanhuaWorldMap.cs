using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The overworld has traversable geography, loops, landmarks and persistent return points.
public sealed class FanhuaWorldMap : MonoBehaviour
{
    public static readonly string[] Rooms={"Route01","Route02","Route03","Route04","Route05","Route06","Route07"};
    public static readonly string[] Names={"初见 · 跨洞","流向 · 让路","停留 · 留出时间","借力 · 登台","回程 · 留路","换位 · 开门","合奏 · 综合"};
    private readonly Vector2Int[] entries={new Vector2Int(0,-8),new Vector2Int(0,-1),new Vector2Int(-8,5),new Vector2Int(-8,13),new Vector2Int(8,5),new Vector2Int(8,13),new Vector2Int(0,20)};
    private readonly HashSet<Vector2Int> floor=new HashSet<Vector2Int>();
    private readonly List<Material> materials=new List<Material>();
    private readonly List<GameObject> scenery=new List<GameObject>();
    private Transform avatar;private Vector2Int position;private float nextMove;private Font font;private GUIStyle style;
    private void Start()
    {
        font=PrototypePlayer.CreateChineseHudFont(20);style=new GUIStyle{font=font,fontSize=20,alignment=TextAnchor.MiddleCenter};style.normal.textColor=new Color(.15f,.2f,.2f);
        foreach(var center in entries)for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)floor.Add(center+new Vector2Int(x,z));
        void Road(Vector2Int a,Vector2Int b){while(a.x!=b.x){floor.Add(a);a.x+=System.Math.Sign(b.x-a.x);}while(a.y!=b.y){floor.Add(a);a.y+=System.Math.Sign(b.y-a.y);}floor.Add(b);}
        Road(entries[0],entries[1]);Road(entries[1],new Vector2Int(0,5));Road(entries[2],entries[4]);Road(entries[2],entries[3]);Road(entries[4],entries[5]);Road(entries[3],new Vector2Int(0,13));Road(entries[5],new Vector2Int(0,13));Road(new Vector2Int(0,13),entries[6]);
        foreach(var cell in floor)
        {
            bool waterDistrict=cell.x>3;Color color=waterDistrict?new Color(.67f,.83f,.85f):new Color(.91f,.86f,.71f);
            Cube("Walkable paper tile",new Vector3(cell.x,-.15f,cell.y),new Vector3(.98f,.3f,.98f),color);
        }
        // Water and ridges are visible landmarks, not traversable empty background.
        Cube("Eastern pool",new Vector3(8,-.65f,9),new Vector3(10,.2f,18),new Color(.2f,.45f,.65f));
        Cube("Western paper ridge",new Vector3(-8,-.6f,9),new Vector3(10,.4f,18),new Color(.56f,.47f,.6f));
        for(int i=0;i<entries.Length;i++)
        {
            bool solved=PlayerPrefs.GetInt("Fanhua.Completed.Route0"+(i+1),0)==1;
            var center=entries[i];Cube("Entrance · "+Names[i],new Vector3(center.x,.08f,center.y),new Vector3(1.3f,.16f,1.3f),solved?new Color(.9f,.7f,.2f):new Color(.48f,.7f,.52f));
            // Different silhouettes: arch, basin, tall folded tower.
            for(int side=-1;side<=1;side+=2)Cube("Paper portal pillar",new Vector3(center.x+side*1.6f,.8f,center.y+1),new Vector3(.3f,1.6f,.3f),new Color(.83f,.7f,.72f));
            Cube("Portal lintel",new Vector3(center.x,1.65f,center.y+1),new Vector3(3.5f,.2f,.35f),new Color(.83f,.7f,.72f));
        }
        position=new Vector2Int(PlayerPrefs.GetInt("Fanhua.MapX",0),PlayerPrefs.GetInt("Fanhua.MapZ",-10));if(!floor.Contains(position))position=new Vector2Int(0,-10);
        avatar=GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;avatar.name="World map character";avatar.position=new Vector3(position.x,.7f,position.y);
        if(Camera.main!=null){Camera.main.orthographic=true;Camera.main.orthographicSize=20;Camera.main.clearFlags=CameraClearFlags.SolidColor;Camera.main.backgroundColor=new Color(.9f,.94f,.9f);var rotation=Quaternion.Euler(75,0,0);Camera.main.transform.SetPositionAndRotation(new Vector3(0,0,5)-rotation*Vector3.forward*32,rotation);}
    }
    private void Cube(string name,Vector3 pos,Vector3 size,Color color)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(transform,false);obj.transform.position=pos;obj.transform.localScale=size;
        var mat=new Material(Shader.Find("Standard"));mat.color=color;materials.Add(mat);obj.GetComponent<Renderer>().sharedMaterial=mat;scenery.Add(obj);
    }
    private void Update()
    {
        var input=new Vector2Int((int)Input.GetAxisRaw("Horizontal"),(int)Input.GetAxisRaw("Vertical"));
        if(Time.time>=nextMove&&input!=Vector2Int.zero)
        {
            var step=Mathf.Abs(input.x)>0?new Vector2Int(input.x,0):new Vector2Int(0,input.y);
            if(floor.Contains(position+step)){position+=step;avatar.position=new Vector3(position.x,.7f,position.y);}nextMove=Time.time+.15f;
        }
        if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))
            for(int i=0;i<entries.Length;i++)if((entries[i]-position).sqrMagnitude<=2)
            {PlayerPrefs.SetInt("Fanhua.MapX",position.x);PlayerPrefs.SetInt("Fanhua.MapZ",position.y);PlayerPrefs.Save();SceneManager.LoadScene(Rooms[i]);break;}
    }
    private void OnGUI()
    {
        if(style==null)return;GUI.Label(new Rect(0,12,Screen.width,32),"纸花群岛 · WASD沿路探索 · 走近门廊Enter进入 · Esc返回原地",style);
        for(int i=0;i<entries.Length;i++){var p=Camera.main.WorldToScreenPoint(new Vector3(entries[i].x,0,entries[i].y));GUI.Label(new Rect(p.x-110,Screen.height-p.y+20,220,30),(i+1)+"  "+Names[i],style);}
    }
    private void OnDestroy(){if(font!=null)Destroy(font);foreach(var mat in materials)Destroy(mat);if(avatar!=null)Destroy(avatar.gameObject);}
}
