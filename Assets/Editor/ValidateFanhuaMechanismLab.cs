using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class ValidateFanhuaMechanismLab
{
    static ValidateFanhuaMechanismLab(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run(){EditorSceneManager.OpenScene(BuildFanhuaMechanismLab.ScenePath);SessionState.SetBool("MechanismLabTest",true);EditorApplication.isPlaying=true;}
    static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("MechanismLabTest",false)){SessionState.SetBool("MechanismLabTest",false);EditorApplication.delayCall+=Test;}}
    static void Test(){try{
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var p=UnityEngine.Object.FindObjectOfType<PrototypePlayer>();p.enabled=false;
        var pt=typeof(PrototypePlayer);var initial=pt.GetField("initialState",flags).GetValue(p);
        var flower=((Array)pt.GetField("flowers",flags).GetValue(p)).GetValue(0);var ft=flower.GetType();var water=p.GetComponent<PrototypeWaterLevel>();
        void Mode(PlayerMode value){pt.GetMethod("SetMode",flags).Invoke(p,new object[]{value});}
        void Reset(int palette)
        {
            pt.GetMethod("RestoreState",flags).Invoke(p,new[]{initial});
            var snapshot=ft.GetMethod("Capture").Invoke(flower,null);int current=(int)snapshot.GetType().GetField("PaletteMode").GetValue(snapshot);
            for(int i=0;i<(palette-current+4)%4;i++)ft.GetMethod("TogglePalette").Invoke(flower,null);
            if(!(bool)ft.GetMethod("CanSetEnd").Invoke(flower,new object[]{new Vector2Int(2,5),(LayerMask)1,p.GetComponent<Collider>()}))throw new Exception("lab runway blocked");
            ft.GetMethod("SetEnd").Invoke(flower,new object[]{new Vector2Int(2,5)});
        }
        void Tick(){typeof(PrototypeWaterLevel).GetMethod("Advance",flags).Invoke(water,new object[]{.12f,pt.GetField("flowers",flags).GetValue(p)});}
        GameObject Yellow()
        {foreach(DictionaryEntry pair in (IDictionary)ft.GetField("ribbonColors",flags).GetValue(flower))if(pair.Value.ToString()=="Yellow")return (GameObject)pair.Key;throw new Exception("continuous yellow missing");}
        Reset(2);
        if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(0,4),Vector2Int.up,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("bend blocked");
        var yellow=Yellow();var mesh=yellow.GetComponent<MeshFilter>().sharedMesh;var before=mesh.vertices;
        if(Mathf.Abs(mesh.bounds.min.z-4.5f)>.001f)throw new Exception("yellow band flipped to the red side");
        Mode(PlayerMode.Platform);Tick();
        if(!yellow.activeInHierarchy||yellow.GetComponent<MeshFilter>().sharedMesh!=mesh)throw new Exception("2D replaced the original yellow shape");
        var after=mesh.vertices;for(int i=0;i<before.Length;i++)if(before[i]!=after[i])throw new Exception("yellow silhouette changed");
        bool projection=false;foreach(var shape in UnityEngine.Object.FindObjectsOfType<PolygonCollider2D>())if(shape.name=="Continuous yellow silhouette"&&shape.enabled)projection=true;
        if(!projection)throw new Exception("yellow outline has no physical projection");
        Mode(PlayerMode.Grid);if(!yellow.activeInHierarchy)throw new Exception("yellow disappeared on return");
        if(Mathf.Abs(mesh.bounds.min.x+3.5f)>.001f||Mathf.Abs(mesh.bounds.max.x-2.5f)>.001f)throw new Exception("half-cell endpoints remain");
        if(!(bool)typeof(PrototypeWaterLevel).GetMethod("CanEditFlower",flags).Invoke(water,new object[]{0}))throw new Exception("return edit locked");
        int budget=(int)ft.GetProperty("Extension").GetValue(flower);
        if(budget<=4)throw new Exception("bend did not spend length");
        if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(-3,4),Vector2Int.up,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("origin cannot bend after 2D return");
        Reset(2);
        if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(-4,5),Vector2Int.right,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("origin cannot translate");
        if((Vector2Int)ft.GetProperty("AnchorCell").GetValue(flower)!=new Vector2Int(-2,5))throw new Exception("origin translation missing");
        // Identical left/right pushes must produce mirror-identical grid boundaries.
        Reset(2);
        foreach(int x in new[]{-3,2})if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(x,4),Vector2Int.up,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("symmetric end push blocked");
        var mirrored=Yellow().GetComponent<MeshFilter>().sharedMesh.vertices;int rings=mirrored.Length/18;
        for(int i=0;i<rings;i++)if(Mathf.Abs(mirrored[i*9+4].z-mirrored[(rings-1-i)*9+4].z)>.001f)throw new Exception("end shapes are not mirrored");
        var meshMap=(IDictionary)ft.GetField("spikeMeshes",flags).GetValue(flower);
        foreach(DictionaryEntry pair in meshMap)
        {
            var tile=(GameObject)pair.Key;var verts=((Mesh)pair.Value).vertices;
            for(int i=0;i<verts.Length/2;i++)
            {
                if(i%3==1)continue;var point=tile.transform.TransformPoint(verts[i]);int station=Mathf.Clamp(Mathf.FloorToInt(point.x+.50001f),-3,2);
                int left=(int)ft.GetMethod("BoundaryShift",flags).Invoke(flower,new object[]{station,-3,3});
                int right=(int)ft.GetMethod("BoundaryShift",flags).Invoke(flower,new object[]{station+1,-3,3});
                float seam=5.5f+Mathf.Lerp(left,right,point.x-(station-.5f));
                if(Mathf.Abs(point.z-seam)>.001f)throw new Exception("spike base detached from paper seam");
            }
        }
        Reset(2);p.GetComponent<Rigidbody>().position=new Vector3(-4,.7f,5);Physics.SyncTransforms();
        pt.GetField("queuedGridDirection",flags).SetValue(p,Vector3.left);pt.GetField("moveInput",flags).SetValue(p,Vector2.left);pt.GetField("nextGridStep",flags).SetValue(p,0f);
        pt.GetMethod("UpdateGridMovement",flags).Invoke(p,null);
        if((Vector2Int)ft.GetProperty("AnchorCell").GetValue(flower)!=new Vector2Int(-4,5)||p.GetComponent<Rigidbody>().position.x!=-5)throw new Exception("normal movement cannot pull base");
        Reset(0);Mode(PlayerMode.Platform);Tick();
        foreach(var source in (IEnumerable)ft.GetProperty("BlueBlocks").GetValue(flower))
        {var block=(PrototypeFallingBlock)source;if(!block.CurrentState.Water||block.GetComponent<Renderer>().enabled)throw new Exception("blue failed to become water");}
        if(!Yellow().activeInHierarchy)throw new Exception("blue/yellow lost paper body");
        Mode(PlayerMode.Grid);
        if((bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(2,7),Vector2Int.down,(LayerMask)1,p.GetComponent<Collider>(),false}))throw new Exception("vanished blue still accepts push contact");
        if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(2,6),Vector2Int.down,(LayerMask)1,p.GetComponent<Collider>(),false}))throw new Exception("cannot contact yellow directly after water conversion");
        float volume=water.Simulation.TotalWater+water.Simulation.TotalSpent+water.Simulation.TotalLost;
        int sourceCount=water.Simulation.SourceCount;
        Mode(PlayerMode.Grid);
        if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(2,4),Vector2Int.up,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("wet flower cannot bend");
        foreach(var source in (IEnumerable)ft.GetProperty("BlueBlocks").GetValue(flower))if(((PrototypeFallingBlock)source).GetComponent<Collider>().enabled)throw new Exception("water ghost collider returned after editing");
        ft.GetMethod("SetEnd").Invoke(flower,new object[]{new Vector2Int(3,5)});
        foreach(var source in (IEnumerable)ft.GetProperty("BlueBlocks").GetValue(flower))if(!((PrototypeFallingBlock)source).CurrentState.Water&&!((PrototypeFallingBlock)source).CurrentState.Lost)throw new Exception("water regenerated solid");
        var wet=ft.GetMethod("Capture").Invoke(flower,null);ft.GetMethod("Restore").Invoke(flower,new[]{wet});
        Mode(PlayerMode.Platform);Tick();
        if(water.Simulation.SourceCount!=sourceCount||Mathf.Abs(volume-water.Simulation.TotalWater-water.Simulation.TotalSpent-water.Simulation.TotalLost)>.001f)throw new Exception("editing duplicated water");
        Reset(1);Mode(PlayerMode.Platform);Tick();
        var broken=ft.GetField("broken",flags).GetValue(flower);if((int)broken.GetType().GetProperty("Count").GetValue(broken)==0)throw new Exception("red above blue did not break");
        Reset(3);Mode(PlayerMode.Platform);Tick();int stones=0;foreach(var cell in water.Simulation.StoneCells)stones++;
        if(stones==0||Mathf.Abs(water.Simulation.TotalSpent-stones)>.001f)throw new Exception("two-to-one gray conversion failed");
        Debug.Log("FANHUA_MECHANISM_LAB_PASSED");EditorApplication.Exit(0);
    }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}}
}
