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
        if((bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(-4,5),Vector2Int.right,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("folded base moved");
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
        if((bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(-3,4),Vector2Int.up,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("origin moved sideways");
        if((bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(-4,5),Vector2Int.right,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("whole flower translated");
        Reset(2);
        foreach(int x in new[]{0,2})if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(x,4),Vector2Int.up,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("free strip push blocked");
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
        if((Vector2Int)ft.GetProperty("AnchorCell").GetValue(flower)!=new Vector2Int(-3,5)||p.GetComponent<Rigidbody>().position.x!=-5)throw new Exception("walking away moved fixed base");
        Reset(2);
        if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(2,7),Vector2Int.down,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("tip down bend failed");
        p.GetComponent<Rigidbody>().position=new Vector3(3,.7f,4);Physics.SyncTransforms();
        for(int i=0;i<2;i++)
        {
            pt.GetField("queuedGridDirection",flags).SetValue(p,Vector3.right);pt.GetField("moveInput",flags).SetValue(p,Vector2.right);pt.GetField("nextGridStep",flags).SetValue(p,0f);
            pt.GetMethod("UpdateGridMovement",flags).Invoke(p,null);
        }
        if((Vector2Int)ft.GetProperty("EndCell").GetValue(flower)!=new Vector2Int(4,5))throw new Exception("straight movement did not extend bent tip");
        var straight=Yellow().GetComponent<MeshFilter>().sharedMesh.vertices;int count=straight.Length/18;
        for(int ring=count-49;ring<count;ring++)if(Mathf.Abs(straight[ring*9+4].z-3.5f)>.001f)throw new Exception("new strips sloped away from walking direction");
        if((int)ft.GetProperty("Extension").GetValue(flower)>10)throw new Exception("inherited tip exceeded budget");
        var offsetsState=ft.GetMethod("Capture").Invoke(flower,null);var savedOffsets=(IDictionary)offsetsState.GetType().GetField("Offsets").GetValue(offsetsState);
        if((int)savedOffsets[3]!=-1||(int)savedOffsets[4]!=-1)throw new Exception("new tip lost its original height");
        // Reproduce both reported directions through the real character movement path.
        void Walk(Vector3 direction)
        {
            pt.GetField("queuedGridDirection",flags).SetValue(p,direction);pt.GetField("moveInput",flags).SetValue(p,new Vector2(direction.x,direction.z));pt.GetField("nextGridStep",flags).SetValue(p,0f);
            pt.GetMethod("UpdateGridMovement",flags).Invoke(p,null);
            var at=p.GetComponent<Rigidbody>().position;
            if((bool)ft.GetMethod("OverlapsPaper").Invoke(flower,new object[]{Vector2Int.RoundToInt(new Vector2(at.x,at.z))}))throw new Exception("character entered visible paper after push");
        }
        Reset(2);p.GetComponent<Rigidbody>().position=new Vector3(0,.7f,7);Physics.SyncTransforms();
        Walk(Vector3.back);Walk(Vector3.back);
        if(p.GetComponent<Rigidbody>().position.z!=5)throw new Exception("top-to-bottom pushes failed");
        if((Vector2Int)ft.GetProperty("AnchorCell").GetValue(flower)!=new Vector2Int(-3,5))throw new Exception("downward pushes moved base");
        Reset(2);
        if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(2,7),Vector2Int.down,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("bent compression fixture failed");
        p.GetComponent<Rigidbody>().position=new Vector3(3,.7f,4);Physics.SyncTransforms();
        Walk(Vector3.left);Walk(Vector3.left);
        if((Vector2Int)ft.GetProperty("EndCell").GetValue(flower)!=new Vector2Int(0,5)||p.GetComponent<Rigidbody>().position.x!=1)throw new Exception("right-to-left bent compression failed");
        var compressed=ft.GetMethod("Capture").Invoke(flower,null);var compressedOffsets=(IDictionary)compressed.GetType().GetField("Offsets").GetValue(compressed);
        if((int)compressedOffsets[0]!=-1)throw new Exception("compression lost bent tip height");
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
