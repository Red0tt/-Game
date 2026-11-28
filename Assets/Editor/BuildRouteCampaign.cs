using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildRouteCampaign
{
    public static void Build()
    {
        for(int stage=1;stage<=7;stage++)
        {
            EditorSceneManager.OpenScene($"Assets/Level/Level0{stage}.unity");
            var player=Object.FindObjectOfType<PrototypePlayer>();var so=new SerializedObject(player);
            if(stage<=3)AddEarlyFlower(player,so,stage);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),$"Assets/Level/try/Route0{stage}.unity");
        }
        var scenes=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        for(int i=1;i<=7;i++){string path=$"Assets/Level/try/Route0{i}.unity";scenes.RemoveAll(s=>s.path==path);scenes.Add(new EditorBuildSettingsScene(path,true));}
        EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();Debug.Log("ROUTE_CAMPAIGN_BUILT");
    }
    static void AddEarlyFlower(PrototypePlayer player,SerializedObject so,int stage)
    {
        var root=player.transform.parent;Vector2Int spawn=Vector2Int.RoundToInt(new Vector2(player.transform.position.x,player.transform.position.z));
        var alcove=new RectInt(spawn.x-5,spawn.y-1,5,4);var solids=new List<BoxCollider>();
        var array=so.FindProperty("platformSolids");for(int i=0;i<array.arraySize;i++)solids.Add((BoxCollider)array.GetArrayElementAtIndex(i).objectReferenceValue);
        GameObject Cube(string name,Vector3 pos,Vector3 scale,Color color)
        {var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root,false);obj.transform.position=pos;obj.transform.localScale=scale;var mat=new Material(Shader.Find("Standard"));mat.color=color;obj.GetComponent<Renderer>().sharedMaterial=mat;return obj;}
        // Subtract only the new alcove from merged wall meshes; keep the rest of the original route.
        foreach(var shape in solids.ToArray())
        {
            if(shape==null)continue;Bounds b=shape.bounds;
            if(b.max.x<=alcove.xMin-.5f||b.min.x>=alcove.xMax-.5f||b.max.z<=alcove.yMin-.5f||b.min.z>=alcove.yMax-.5f)continue;
            var color=shape.GetComponent<Renderer>().sharedMaterial.color;solids.Remove(shape);
            for(int x=Mathf.CeilToInt(b.min.x+.01f);x<=Mathf.FloorToInt(b.max.x-.01f);x++)
            for(int z=Mathf.CeilToInt(b.min.z+.01f);z<=Mathf.FloorToInt(b.max.z-.01f);z++)
                if(!alcove.Contains(new Vector2Int(x,z)))solids.Add(Cube("Preserved original wall",new Vector3(x,b.center.y,z),new Vector3(1,b.size.y,1),color).GetComponent<BoxCollider>());
            shape.enabled=false;shape.GetComponent<Renderer>().enabled=false;
        }
        foreach(var cell in alcove.allPositionsWithin)Cube("Flower alcove floor",new Vector3(cell.x,-.1f,cell.y),new Vector3(1,.2f,1),new Color(.86f,.84f,.76f));
        var plate=Cube("Yellow opens the original route",new Vector3(spawn.x-1,.02f,spawn.y+(stage==2?1:stage==3?-1:0)),new Vector3(.75f,.04f,.75f),Color.yellow);Object.DestroyImmediate(plate.GetComponent<Collider>());
        var door=Cube("Entry paper gate",new Vector3(spawn.x+1,.5f,spawn.y),new Vector3(1,1,32),new Color(.62f,.48f,.64f));solids.Add(door.GetComponent<BoxCollider>());
        var gate=door.AddComponent<PrototypePressureGate>();var gs=new SerializedObject(gate);gs.FindProperty("flowerPlate").objectReferenceValue=plate.transform;gs.FindProperty("door").objectReferenceValue=door.GetComponent<BoxCollider>();gs.FindProperty("doorVisual").objectReferenceValue=door.GetComponent<Renderer>();gs.ApplyModifiedPropertiesWithoutUndo();
        var gates=so.FindProperty("gates");int count=gates.arraySize;gates.arraySize=count+1;gates.GetArrayElementAtIndex(count).objectReferenceValue=gate;
        array=so.FindProperty("platformSolids");array.arraySize=solids.Count;for(int i=0;i<solids.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=solids[i];
        so.FindProperty("enableFanhuaTest").boolValue=true;so.FindProperty("initialFanhuaPalette").intValue=2;so.FindProperty("fanhuaAnchor").vector2IntValue=new Vector2Int(spawn.x-4,spawn.y);
        Bounds bounds=new Bounds(player.transform.position,Vector3.one);foreach(var shape in Object.FindObjectsOfType<BoxCollider>())bounds.Encapsulate(shape.bounds);
        // The long gate is not used for the map bounds: retain sensible vertical limits.
        int xmin=Mathf.Min(alcove.xMin-1,Mathf.FloorToInt(bounds.min.x)-1),xmax=Mathf.Max(alcove.xMax+1,Mathf.CeilToInt(bounds.max.x)+1);
        so.FindProperty("fanhuaGridOrigin").vector2IntValue=new Vector2Int(xmin,spawn.y-20);so.FindProperty("fanhuaGridSize").vector2IntValue=new Vector2Int(xmax-xmin,40);so.ApplyModifiedPropertiesWithoutUndo();
        var camera=Object.FindObjectOfType<PrototypeCamera>();if(camera!=null){var cs=new SerializedObject(camera);var focus=cs.FindProperty("focus").vector3Value;focus.x-=1.5f;cs.FindProperty("focus").vector3Value=focus;cs.ApplyModifiedPropertiesWithoutUndo();Camera.main.orthographicSize=Mathf.Max(Camera.main.orthographicSize,6.5f);}
    }
}
