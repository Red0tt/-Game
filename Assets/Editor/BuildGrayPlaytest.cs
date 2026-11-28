using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildGrayPlaytest
{
    public const string Path="Assets/Scenes/GrayFallPlaytest.unity";
    [MenuItem("Fanhua/Playtest/Open Gray Fall Test")]
    public static void Open() { EditorSceneManager.OpenScene(Path); }
    public static void Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Gray fall test");
        var solids=new List<BoxCollider>();
        GameObject Cube(string name,Vector3 pos,Vector3 scale,Color color)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(root.transform);
            o.transform.position=pos;o.transform.localScale=scale;
            var m=new Material(Shader.Find("Standard"));m.color=color;o.GetComponent<Renderer>().sharedMaterial=m;return o;
        }
        // Grid floors are background in 2D, giving an unrestricted setup area.
        for(int x=-7;x<=7;x++)for(int z=0;z<=11;z++)
            Cube("Floor",new Vector3(x,-.1f,z),new Vector3(1,.2f,1),new Color(.85f,.82f,.75f));
        for(int x=-7;x<=7;x++)solids.Add(Cube("Landing floor",new Vector3(x,.5f,0),Vector3.one,new Color(.9f,.73f,.28f)).GetComponent<BoxCollider>());
        // Left: fall three cells. Middle: immediate support. Right: two stacked drops.
        var blocks=new List<PrototypeFallingBlock>();
        foreach(var cell in new[]{new Vector2Int(-4,8),new Vector2Int(0,8),new Vector2Int(4,8),new Vector2Int(4,9)})
            blocks.Add(Cube("Water · one unit",new Vector3(cell.x,.5f,cell.y),new Vector3(.94f,1f,.94f),Color.blue).AddComponent<PrototypeFallingBlock>());
        foreach(var cell in new[]{new Vector2Int(-4,5),new Vector2Int(0,5),new Vector2Int(4,5),new Vector2Int(4,4)})
            Cube("Red reaction cell",new Vector3(cell.x,.5f,cell.y),Vector3.one,Color.red).AddComponent<Unit02FireTile>();
        solids.Add(Cube("Immediate support",new Vector3(0,.5f,4),Vector3.one,Color.yellow).GetComponent<BoxCollider>());
        var p=GameObject.CreatePrimitive(PrimitiveType.Capsule);p.name="Player";p.transform.SetParent(root.transform);p.transform.position=new Vector3(-6,.7f,2);
        var c=p.GetComponent<CapsuleCollider>();c.height=1.4f;c.radius=.32f;p.AddComponent<Rigidbody>();
        var player=p.AddComponent<PrototypePlayer>();var water=p.AddComponent<PrototypeWaterLevel>();
        water.flowInterval=.2f;water.returnShore=new Vector2Int(-6,2);water.switchPosition=new Vector2(50,50);
        var so=new SerializedObject(player);so.FindProperty("groundLayers").intValue=1;
        so.FindProperty("enableFanhuaTest").boolValue=true;so.FindProperty("fanhuaAnchor").vector2IntValue=new Vector2Int(-2,10);
        so.FindProperty("fanhuaGridOrigin").vector2IntValue=new Vector2Int(-7,0);so.FindProperty("fanhuaGridSize").vector2IntValue=new Vector2Int(15,13);
        var arr=so.FindProperty("platformSolids");arr.arraySize=solids.Count;for(int i=0;i<solids.Count;i++)arr.GetArrayElementAtIndex(i).objectReferenceValue=solids[i];
        arr=so.FindProperty("fallingBlocks");arr.arraySize=blocks.Count;for(int i=0;i<blocks.Count;i++)arr.GetArrayElementAtIndex(i).objectReferenceValue=blocks[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        var cam=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(PrototypeCamera));cam.tag="MainCamera";
        cam.GetComponent<Camera>().orthographic=true;cam.GetComponent<Camera>().orthographicSize=9;
        so=new SerializedObject(cam.GetComponent<PrototypeCamera>());so.FindProperty("player").objectReferenceValue=player;so.FindProperty("focus").vector3Value=new Vector3(0,.5f,5);so.ApplyModifiedPropertiesWithoutUndo();
        cam.transform.SetPositionAndRotation(new Vector3(0,17,5),Quaternion.Euler(90,0,0));
        var light=new GameObject("Light",typeof(Light));light.GetComponent<Light>().type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);RenderSettings.ambientLight=Color.white*.7f;
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),Path);
        Debug.Log("GRAY_PLAYTEST_SCENE_BUILT");
    }
}
