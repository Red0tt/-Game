using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Only writes the Unit 02 scene. Unit 01 scenes and their serialized settings are untouched.
public static class BuildWaterLevel
{
    public const string ScenePath="Assets/Level/Unit02/Unit02Water01.unity";
    [MenuItem("Fanhua/Unit 02/Rebuild Water 01")]
    public static void Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject("Unit 02 · 导水潜流");
        var solids=new List<BoxCollider>(); var floors=new HashSet<Vector2Int>();
        var stone=new Color(.65f,.59f,.71f); var yellow=new Color(.98f,.79f,.3f);
        GameObject Cube(string name,Vector3 pos,Vector3 size,Color color)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
            obj.transform.SetParent(root.transform,false); obj.transform.position=pos; obj.transform.localScale=size;
            var mat=new Material(Shader.Find("Standard")); mat.color=color; obj.GetComponent<Renderer>().sharedMaterial=mat;
            return obj;
        }
        void Floor(int x,int z) { floors.Add(new Vector2Int(x,z)); }
        void Solid(int x,int z,string name,Color color)
        { solids.Add(Cube(name,new Vector3(x,.5f,z),Vector3.one,color).GetComponent<BoxCollider>()); }
        // A needs a genuine back-pull runway; its floor is background in 2D.
        for(int z=9;z<=12;z++) Floor(1,z);
        Floor(2,12); Floor(3,12);
        for(int z=12;z<=18;z++) { Floor(2,z); Floor(3,z); }
        Floor(2,9); Floor(3,9);
        // A separate right-bank grid region; neither row crosses the pool.
        for(int z=6;z<=11;z++) Floor(7,z);
        for(int x=7;x<=13;x++) Floor(x,11);
        for(int x=7;x<=10;x++) Floor(x,8);
        foreach(var cell in floors)
            Cube($"Cell {cell.x}, {cell.y}",new Vector3(cell.x,-.1f,cell.y),new Vector3(1,.2f,1),new Color(.87f,.84f,.79f));
        // Nine-cell basin minus a one-cell roof = eight cells of capacity to the lip.
        for(int x=3;x<=5;x++) Solid(x,2,"Yellow basin floor",yellow);
        for(int z=3;z<=8;z++) Solid(2,z,"Left retaining wall",stone);
        for(int z=3;z<=5;z++) Solid(6,z,"Right spill lip",stone);
        Solid(5,4,"Submerged alcove roof",stone);
        // Tray keeps the spare water away from the fire until deliberately pushed.
        Solid(9,9,"Spare water tray",yellow);
        Solid(8,10,"Tray left wall",stone); Solid(10,10,"Tray right wall",stone);
        // Same-height approach and wide fire require a cooled midpoint, not a long jump.
        Solid(9,7,"Fire approach",yellow); Solid(10,7,"Fire approach",yellow);
        for(int x=11;x<=15;x++)
        {
            for(int z=0;z<=6;z++) Solid(x,z,"Fire ridge base",stone);
            Cube($"Fire {x}, 7",new Vector3(x,.5f,7),Vector3.one,new Color(.9f,.22f,.16f)).AddComponent<Unit02FireTile>();
        }
        // A full-height wall opens from a physical submerged switch. No task flags at the exit.
        var door=Cube("Submerged switch door",new Vector3(8,.5f,13),new Vector3(1,1,15),Color.gray);
        solids.Add(door.GetComponent<BoxCollider>());
        var lever=Cube("Dive switch",new Vector3(5,.5f,3),new Vector3(.32f,.2f,.32f),Color.cyan);
        Object.DestroyImmediate(lever.GetComponent<Collider>());
        var wire=Cube("Switch link",new Vector3(6.5f,-.015f,5),new Vector3(3,.025f,.05f),Color.cyan);
        Object.DestroyImmediate(wire.GetComponent<Collider>());
        var source=Cube("One unit spare water",new Vector3(9,.5f,11),new Vector3(.96f,1,.96f),new Color(.24f,.49f,.81f));
        source.AddComponent<PrototypeFallingBlock>();
        var exit=Cube("Unit02 exit",new Vector3(17.6f,.5f,8),new Vector3(.6f,.12f,.6f),Color.green);
        Object.DestroyImmediate(exit.GetComponent<Collider>());
        var objPlayer=GameObject.CreatePrimitive(PrimitiveType.Capsule); objPlayer.name="Player · Unit02";
        objPlayer.transform.SetParent(root.transform,false); objPlayer.transform.position=new Vector3(1,.7f,10);
        var capsule=objPlayer.GetComponent<CapsuleCollider>(); capsule.height=1.4f; capsule.radius=.32f;
        objPlayer.AddComponent<Rigidbody>(); var player=objPlayer.AddComponent<PrototypePlayer>();
        var water=objPlayer.AddComponent<PrototypeWaterLevel>(); water.gate=door.GetComponent<BoxCollider>();
        water.gateVisual=door.GetComponent<Renderer>(); water.switchPosition=new Vector2(5,3); water.returnShore=new Vector2Int(7,6);
        var so=new SerializedObject(player);
        so.FindProperty("groundLayers").intValue=1; so.FindProperty("levelExit").objectReferenceValue=exit.transform;
        so.FindProperty("enableFanhuaTest").boolValue=true; so.FindProperty("fanhuaAnchor").vector2IntValue=new Vector2Int(3,10);
        so.FindProperty("fanhuaGridOrigin").vector2IntValue=Vector2Int.zero; so.FindProperty("fanhuaGridSize").vector2IntValue=new Vector2Int(19,22);
        so.FindProperty("fallLimit").floatValue=-2; so.FindProperty("additionalFanhuaAnchors").arraySize=0;
        var array=so.FindProperty("platformSolids"); array.arraySize=solids.Count;
        for(int i=0;i<solids.Count;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=solids[i];
        array=so.FindProperty("fallingBlocks"); array.arraySize=1; array.GetArrayElementAtIndex(0).objectReferenceValue=source.GetComponent<PrototypeFallingBlock>();
        so.ApplyModifiedPropertiesWithoutUndo();
        var camObj=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener),typeof(PrototypeCamera)); camObj.tag="MainCamera";
        var cam=camObj.GetComponent<Camera>(); cam.orthographic=true; cam.orthographicSize=11.6f;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.94f,.91f,.85f);
        so=new SerializedObject(camObj.GetComponent<PrototypeCamera>()); so.FindProperty("player").objectReferenceValue=player;
        so.FindProperty("focus").vector3Value=new Vector3(9,.5f,10); so.ApplyModifiedPropertiesWithoutUndo();
        camObj.transform.SetPositionAndRotation(new Vector3(9,17,10),Quaternion.Euler(90,0,0));
        var light=new GameObject("Directional Light",typeof(Light)); light.GetComponent<Light>().type=LightType.Directional;
        light.transform.rotation=Quaternion.Euler(50,-30,0); RenderSettings.ambientLight=new Color(.7f,.7f,.7f);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath); AssetDatabase.SaveAssets();
        Debug.Log("UNIT02_BUILT "+ScenePath);
    }
}
