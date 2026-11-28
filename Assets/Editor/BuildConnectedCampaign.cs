using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildConnectedCampaign
{
    [MenuItem("Fanhua/Playtest/Open World Map")]
    public static void Open(){EditorSceneManager.OpenScene("Assets/Scenes/FanhuaWorldMap.unity");}
    public static void BuildAll(){BuildGrayPlaytest.Build();Build();}
    public static void Build()
    {
        System.IO.Directory.CreateDirectory("Assets/Level/Connected");
        for(int stage=1;stage<=9;stage++)Room(stage);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("World",typeof(FanhuaWorldMap));
        var cam=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));cam.tag="MainCamera";cam.transform.SetPositionAndRotation(new Vector3(0,20,2),Quaternion.Euler(90,0,0));cam.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;cam.GetComponent<Camera>().backgroundColor=new Color(.94f,.96f,.92f);cam.GetComponent<Camera>().orthographic=true;cam.GetComponent<Camera>().orthographicSize=11;cam.GetComponent<Camera>().backgroundColor=new Color(.9f,.95f,.9f);
        Lighting();EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/FanhuaWorldMap.unity");
        AssetDatabase.SaveAssets();Debug.Log("CONNECTED_CAMPAIGN_BUILT");
    }
    static void Lighting(){var o=new GameObject("Light",typeof(Light));o.GetComponent<Light>().type=LightType.Directional;o.transform.rotation=Quaternion.Euler(50,-30,0);RenderSettings.ambientLight=Color.white*.7f;}
    static void Room(int stage)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Connected " + stage);var solids=new List<BoxCollider>();
        GameObject Cube(string name,Vector3 p,Vector3 size,Color color)
        {var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(root.transform);o.transform.position=p;o.transform.localScale=size;var m=new Material(Shader.Find("Standard"));m.color=color;o.GetComponent<Renderer>().sharedMaterial=m;return o;}
        for(int x=-7;x<=14;x++)for(int z=0;z<=12;z++)Cube("Floor",new Vector3(x,-.1f,z),new Vector3(1,.2f,1),new Color(.85f,.82f,.75f));
        // Tall cross-board doors stop both grid walking and 2D jumping.
        var gates=new List<PrototypePressureGate>();
        for(int n=0;n<((stage==3||stage==6||stage==8)?2:1);n++)
        {
            var door=Cube("Paper gate",new Vector3((stage==9?11:6)+n,.5f,6),new Vector3(1,1,30),new Color(.65f,.45f,.6f));solids.Add(door.GetComponent<BoxCollider>());
            var target=new Vector2Int(stage==9?8:n==0?1:3,n==0?(stage==1?5:6):4);
            var plate=Cube("Flower pressure target",new Vector3(target.x,.015f,target.y),new Vector3(.8f,.03f,.8f),Color.yellow);Object.DestroyImmediate(plate.GetComponent<Collider>());
            var gate=door.AddComponent<PrototypePressureGate>();var gs=new SerializedObject(gate);gs.FindProperty(stage>=4&&stage<=6?"stonePlate":"flowerPlate").objectReferenceValue=plate.transform;gs.FindProperty("door").objectReferenceValue=door.GetComponent<BoxCollider>();gs.FindProperty("doorVisual").objectReferenceValue=door.GetComponent<Renderer>();gs.ApplyModifiedPropertiesWithoutUndo();gates.Add(gate);
            if(stage>=4&&stage<=6)plate.transform.position=new Vector3(n==0?1:3,.015f,stage==5?5:1);
            if(stage>=7)
            {
                var grayTarget=Cube("Gray pressure target",new Vector3(stage==9?10:4,.015f,1),new Vector3(.8f,.03f,.8f),Color.gray);Object.DestroyImmediate(grayTarget.GetComponent<Collider>());
                gs.Update();gs.FindProperty("stonePlate").objectReferenceValue=grayTarget.transform;gs.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        var exit=Cube("Exit",new Vector3(stage==9?14:9,.03f,6),new Vector3(.7f,.06f,.7f),Color.green);Object.DestroyImmediate(exit.GetComponent<Collider>());
        var sources=new List<PrototypeFallingBlock>();
        if(stage>=4)
        {
            for(int n=0;n<(stage==6?2:1);n++)
            {
                int x=stage>=7?(stage==9?10:4):(n==0?1:3);
                var source=Cube("Move water before release",new Vector3(x-1,.5f,10),new Vector3(.94f,1f,.94f),Color.blue);sources.Add(source.AddComponent<PrototypeFallingBlock>());
                Cube("Red · water from above",new Vector3(x,.5f,stage>=7?8:6),Vector3.one,Color.red).AddComponent<Unit02FireTile>();
                solids.Add(Cube("Stone landing support",new Vector3(x,.5f,stage==5?4:0),Vector3.one,Color.yellow).GetComponent<BoxCollider>());
            }
        }
        var obj=GameObject.CreatePrimitive(PrimitiveType.Capsule);obj.name="Player";obj.transform.SetParent(root.transform);obj.transform.position=new Vector3(0,.7f,5);obj.GetComponent<CapsuleCollider>().height=1.4f;obj.GetComponent<CapsuleCollider>().radius=.32f;obj.AddComponent<Rigidbody>();var player=obj.AddComponent<PrototypePlayer>();var water=obj.AddComponent<PrototypeWaterLevel>();water.switchPosition=new Vector2(50,50);water.returnShore=new Vector2Int(0,8);obj.AddComponent<FanhuaReturnToMap>();
        var so=new SerializedObject(player);so.FindProperty("groundLayers").intValue=1;so.FindProperty("enableFanhuaTest").boolValue=true;so.FindProperty("fanhuaAnchor").vector2IntValue=new Vector2Int(-2,5);so.FindProperty("fanhuaGridOrigin").vector2IntValue=new Vector2Int(-7,0);so.FindProperty("fanhuaGridSize").vector2IntValue=new Vector2Int(22,13);so.FindProperty("levelExit").objectReferenceValue=exit.transform;so.FindProperty("requireAllGatesAtExit").boolValue=true;
        var ar=so.FindProperty("platformSolids");ar.arraySize=solids.Count;for(int i=0;i<solids.Count;i++)ar.GetArrayElementAtIndex(i).objectReferenceValue=solids[i];var fa=so.FindProperty("fallingBlocks");fa.arraySize=sources.Count;for(int i=0;i<sources.Count;i++)fa.GetArrayElementAtIndex(i).objectReferenceValue=sources[i];var ga=so.FindProperty("gates");ga.arraySize=gates.Count;for(int i=0;i<gates.Count;i++)ga.GetArrayElementAtIndex(i).objectReferenceValue=gates[i];so.ApplyModifiedPropertiesWithoutUndo();
        var cam=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(PrototypeCamera));cam.tag="MainCamera";cam.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;cam.GetComponent<Camera>().backgroundColor=new Color(.94f,.96f,.92f);cam.GetComponent<Camera>().orthographic=true;cam.GetComponent<Camera>().orthographicSize=13;
        so=new SerializedObject(cam.GetComponent<PrototypeCamera>());so.FindProperty("player").objectReferenceValue=player;so.FindProperty("focus").vector3Value=new Vector3(3.5f,.5f,6);so.ApplyModifiedPropertiesWithoutUndo();Lighting();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),$"Assets/Level/Connected/Connected0{stage}.unity");
    }
}
