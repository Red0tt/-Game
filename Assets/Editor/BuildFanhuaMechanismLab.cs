using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildFanhuaMechanismLab
{
    public const string ScenePath="Assets/Level/try/FanhuaMechanismLab.unity";
    [MenuItem("Fanhua/Playtest/Open Fanhua Mechanism Lab")]
    public static void Open(){EditorSceneManager.OpenScene(ScenePath);}
    public static void Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Fanhua mechanism laboratory");var solids=new List<BoxCollider>();
        GameObject Cube(string name,Vector3 position,Vector3 scale,Color color)
        {var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(root.transform);o.transform.position=position;o.transform.localScale=scale;var m=new Material(Shader.Find("Standard"));m.color=color;o.GetComponent<Renderer>().sharedMaterial=m;return o;}
        for(int x=-14;x<=14;x++)for(int z=0;z<=14;z++)Cube("Grid floor",new Vector3(x,-.1f,z),new Vector3(1,.2f,1),(x+z)%2==0?new Color(.85f,.87f,.8f):new Color(.9f,.91f,.85f));
        for(int x=-14;x<=14;x++)solids.Add(Cube("2D landing floor",new Vector3(x,.5f,0),Vector3.one,new Color(.72f,.67f,.57f)).GetComponent<BoxCollider>());
        for(int i=0;i<4;i++)solids.Add(Cube("Silhouette landing stair",new Vector3(11-i,.5f,1+i),Vector3.one,new Color(.68f,.67f,.73f)).GetComponent<BoxCollider>());
        var obj=GameObject.CreatePrimitive(PrimitiveType.Capsule);obj.name="Player";obj.transform.SetParent(root.transform);obj.transform.position=new Vector3(-1,.7f,5);obj.GetComponent<CapsuleCollider>().height=1.4f;obj.GetComponent<CapsuleCollider>().radius=.32f;obj.AddComponent<Rigidbody>();
        var p=obj.AddComponent<PrototypePlayer>();obj.AddComponent<FanhuaMechanismLab>();var water=obj.AddComponent<PrototypeWaterLevel>();water.flowInterval=.12f;water.switchPosition=new Vector2(99,99);water.returnShore=new Vector2Int(12,2);
        var so=new SerializedObject(p);so.FindProperty("groundLayers").intValue=1;so.FindProperty("enableFanhuaTest").boolValue=true;so.FindProperty("initialFanhuaPalette").intValue=2;
        so.FindProperty("fanhuaAnchor").vector2IntValue=new Vector2Int(-3,5);so.FindProperty("fanhuaGridOrigin").vector2IntValue=new Vector2Int(-14,0);so.FindProperty("fanhuaGridSize").vector2IntValue=new Vector2Int(29,15);so.FindProperty("fallLimit").floatValue=-3;
        var a=so.FindProperty("platformSolids");a.arraySize=solids.Count;for(int i=0;i<solids.Count;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=solids[i];so.FindProperty("fallingBlocks").arraySize=0;so.ApplyModifiedPropertiesWithoutUndo();
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(PrototypeCamera));camera.tag="MainCamera";camera.GetComponent<Camera>().orthographic=true;camera.GetComponent<Camera>().orthographicSize=11;camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=new Color(.92f,.95f,.9f);
        so=new SerializedObject(camera.GetComponent<PrototypeCamera>());so.FindProperty("player").objectReferenceValue=p;so.FindProperty("focus").vector3Value=new Vector3(0,.5f,6);so.ApplyModifiedPropertiesWithoutUndo();
        var light=new GameObject("Light",typeof(Light));light.GetComponent<Light>().type=LightType.Directional;light.transform.rotation=Quaternion.Euler(50,-30,0);RenderSettings.ambientLight=Color.white*.7f;
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
        var scenes=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);scenes.RemoveAll(s=>s.path==ScenePath);scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();Debug.Log("FANHUA_MECHANISM_LAB_BUILT");
    }
}
