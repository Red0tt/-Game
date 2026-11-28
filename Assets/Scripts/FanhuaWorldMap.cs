using UnityEngine;
using UnityEngine.SceneManagement;

// An explorable paper-fan map; all discovered rules can be tested without unlock flags.
public sealed class FanhuaWorldMap : MonoBehaviour
{
    public static readonly string[] Rooms={"Assets/Level/Connected/Connected01","Assets/Level/Connected/Connected02","Assets/Level/Connected/Connected03","Assets/Level/Connected/Connected04","Assets/Level/Connected/Connected05","Assets/Level/Connected/Connected06","Assets/Level/Connected/Connected07","Assets/Level/Connected/Connected08","Assets/Level/Connected/Connected09"};
    public static readonly string[] Names={"展纸","折角","两端照应","落水成石","支撑的高度","双石落位","先折后落","回程留路","十格之内"};
    private Transform avatar;
    private readonly Vector3[] doors=new Vector3[Rooms.Length];
    private readonly System.Collections.Generic.List<Mesh> meshes=new System.Collections.Generic.List<Mesh>();
    private readonly System.Collections.Generic.List<Material> materials=new System.Collections.Generic.List<Material>();
    private Font font;
    private GUIStyle style;
    private void Start()
    {
        font=PrototypePlayer.CreateChineseHudFont(22);
        style=new GUIStyle{font=font,fontSize=22,alignment=TextAnchor.MiddleCenter};style.normal.textColor=Color.black;
        avatar=GameObject.CreatePrimitive(PrimitiveType.Capsule).transform;avatar.name="World map character";avatar.position=new Vector3(0,.7f,-3);
        for(int i=0;i<doors.Length;i++)
        {
            float angle=Mathf.Lerp(165,15,i/(float)(doors.Length-1))*Mathf.Deg2Rad;
            doors[i]=new Vector3(Mathf.Cos(angle)*8,.1f,Mathf.Sin(angle)*6);
            var gate=GameObject.CreatePrimitive(PrimitiveType.Cylinder);gate.name=Names[i];gate.transform.position=doors[i];gate.transform.localScale=new Vector3(1,.1f,1);
            gate.GetComponent<Renderer>().material.color=Color.HSVToRGB(i/(float)doors.Length,.45f,.85f);
            materials.Add(gate.GetComponent<Renderer>().material);
            float a=(Mathf.Lerp(174,6,i/(float)doors.Length))*Mathf.Deg2Rad;
            float b=(Mathf.Lerp(174,6,(i+1)/(float)doors.Length))*Mathf.Deg2Rad;
            var mesh=new Mesh{name="Paper fan room silhouette"};
            mesh.vertices=new[]{new Vector3(0,-.04f,-2.5f),new Vector3(Mathf.Cos(a)*9,-.04f,Mathf.Sin(a)*7),new Vector3(Mathf.Cos(b)*9,-.04f,Mathf.Sin(b)*7)};
            var front=mesh.vertices;mesh.vertices=new[]{front[0],front[1],front[2],front[0],front[1],front[2]};mesh.triangles=new[]{0,1,2,5,4,3};mesh.RecalculateNormals();meshes.Add(mesh);
            var petal=new GameObject("Room footprint · "+Names[i],typeof(MeshFilter),typeof(MeshRenderer));petal.transform.SetParent(transform);
            petal.GetComponent<MeshFilter>().sharedMesh=mesh;
            var mat=new Material(Shader.Find("Standard"));mat.color=Color.HSVToRGB(i/(float)doors.Length,.18f,.96f);petal.GetComponent<MeshRenderer>().sharedMaterial=mat;materials.Add(mat);
        }
    }
    private void Update()
    {
        var delta=new Vector3(Input.GetAxisRaw("Horizontal"),0,Input.GetAxisRaw("Vertical"));avatar.position+=Vector3.ClampMagnitude(delta,1)*4*Time.deltaTime;
        avatar.position=new Vector3(Mathf.Clamp(avatar.position.x,-10,10),.7f,Mathf.Clamp(avatar.position.z,-4,8));
        if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))
            for(int i=0;i<doors.Length;i++)if(Vector3.Distance(avatar.position,doors[i])<1.4f){SceneManager.LoadScene(System.IO.Path.GetFileName(Rooms[i]));break;}
    }
    private void OnGUI()
    {
        if(style==null)return;
        GUI.Label(new Rect(0,12,Screen.width,35),"翻花庭院 · WASD走近花瓣，Enter进入 · 关卡内Esc返回",style);
        for(int i=0;i<doors.Length;i++)
        {var screen=Camera.main.WorldToScreenPoint(doors[i]);GUI.Label(new Rect(screen.x-70,Screen.height-screen.y+14,140,30),(i+1)+" · "+Names[i],style);}
    }
    private void OnDestroy(){if(font!=null)Destroy(font);foreach(var m in meshes)Destroy(m);foreach(var m in materials)Destroy(m);}
}
