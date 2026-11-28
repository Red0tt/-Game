using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class ValidateGrayPlaytest
{
 static ValidateGrayPlaytest(){EditorApplication.playModeStateChanged+=OnPlay;}
 public static void Run(){EditorSceneManager.OpenScene(BuildGrayPlaytest.Path);SessionState.SetBool("GrayPlaytest",true);EditorApplication.isPlaying=true;}
 static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("GrayPlaytest",false)){SessionState.SetBool("GrayPlaytest",false);EditorApplication.delayCall+=Test;}}
 static void Test(){try{
  GrayRuleChecks.Run();
  var p=UnityEngine.Object.FindObjectOfType<PrototypePlayer>();p.enabled=false;
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var editable=(System.Array)typeof(PrototypePlayer).GetField("flowers",flags).GetValue(p);
  var flower=editable.GetValue(0);var ft=flower.GetType();
  // Exact regression: pull one cell and immediately reverse, without resetting grip.
  p.GetComponent<Rigidbody>().position=new Vector3(0,.7f,10);Physics.SyncTransforms();
  typeof(PrototypePlayer).GetField("queuedGridDirection",flags).SetValue(p,Vector3.right);
  typeof(PrototypePlayer).GetField("nextGridStep",flags).SetValue(p,0f);
  typeof(PrototypePlayer).GetMethod("UpdateGridMovement",flags).Invoke(p,null);
  typeof(PrototypePlayer).GetField("queuedGridDirection",flags).SetValue(p,Vector3.left);
  typeof(PrototypePlayer).GetField("nextGridStep",flags).SetValue(p,0f);
  typeof(PrototypePlayer).GetMethod("UpdateGridMovement",flags).Invoke(p,null);
  if(p.GetComponent<Rigidbody>().position.x!=0||(int)ft.GetProperty("Axis").GetValue(flower)==0)throw new Exception("one-cell pull/reverse compression failed");
  typeof(PrototypePlayer).GetMethod("RestoreState",flags).Invoke(p,new[]{typeof(PrototypePlayer).GetField("initialState",flags).GetValue(p)});
  ft.GetMethod("SetEnd").Invoke(flower,new object[]{new Vector2Int(2,10)});
  p.GetComponent<Rigidbody>().position=new Vector3(3,.7f,10);Physics.SyncTransforms();
  typeof(PrototypePlayer).GetField("queuedGridDirection",flags).SetValue(p,Vector3.left);
  typeof(PrototypePlayer).GetField("moveInput",flags).SetValue(p,Vector2.left);
  typeof(PrototypePlayer).GetField("nextGridStep",flags).SetValue(p,0f);
  typeof(PrototypePlayer).GetMethod("UpdateGridMovement",flags).Invoke(p,null);
  if((Vector2Int)ft.GetProperty("EndCell").GetValue(flower)!=new Vector2Int(1,10)||p.GetComponent<Rigidbody>().position.x!=2)
    throw new Exception("normal movement did not push flower back");
  ft.GetMethod("SetEnd").Invoke(flower,new object[]{new Vector2Int(2,10)});
  var pushArgs=new object[]{new Vector2Int(3,10),Vector2Int.left,(LayerMask)1,p.GetComponent<Collider>(),true};
  if(!(bool)ft.GetMethod("TryPush").Invoke(flower,pushArgs))throw new Exception("horizontal compression failed");
  if((Vector2Int)ft.GetProperty("EndCell").GetValue(flower)!=new Vector2Int(1,10))throw new Exception("compressed end wrong");
  ft.GetMethod("SetEnd").Invoke(flower,new object[]{new Vector2Int(2,10)});
  if(!(bool)ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(0,9),Vector2Int.up,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("contact push bend failed");
  ft.GetMethod("TryPush").Invoke(flower,new object[]{new Vector2Int(0,12),Vector2Int.down,(LayerMask)1,p.GetComponent<Collider>(),true});
  // Restore the original undeformed fixture for the remaining checks.
  var initialPush=typeof(PrototypePlayer).GetField("initialState",flags).GetValue(p);
  typeof(PrototypePlayer).GetMethod("RestoreState",flags).Invoke(p,new[]{initialPush});
  ft.GetMethod("SetEnd").Invoke(flower,new object[]{new Vector2Int(2,10)});
  var snapshot=ft.GetMethod("Capture").Invoke(flower,null);
  object[] shift={new Vector2Int(0,9),Vector2Int.up,(LayerMask)1,p.GetComponent<Collider>(),true};
  if(!(bool)ft.GetMethod("TryShiftStrip").Invoke(flower,shift))throw new Exception("connected shift rejected");
  if((bool)ft.GetMethod("TryShiftStrip").Invoke(flower,shift))throw new Exception("disconnected shift accepted");
  ft.GetMethod("Restore").Invoke(flower,new[]{snapshot});
  // Restore the initial folded scene so the fluid fixtures stay independent.
  var initial=typeof(PrototypePlayer).GetField("initialState",flags).GetValue(p);
  typeof(PrototypePlayer).GetMethod("RestoreState",flags).Invoke(p,new[]{initial});
  typeof(PrototypePlayer).GetMethod("SetMode",flags).Invoke(p,new object[]{PlayerMode.Platform});
  var flowers=typeof(PrototypePlayer).GetField("flowers",flags).GetValue(p);
  var water=p.GetComponent<PrototypeWaterLevel>();
  for(int i=0;i<60;i++)typeof(PrototypeWaterLevel).GetMethod("Advance",flags).Invoke(water,new object[]{.2f,flowers});
  var cells=water.Simulation.StoneCells.ToArray();
  if(!cells.Contains(new Vector2Int(-4,1))||!cells.Contains(new Vector2Int(0,5)))throw new Exception("gray landing mismatch");
  var hit=Physics2D.OverlapPoint(new Vector2(-4,1));if(hit==null||hit.isTrigger)throw new Exception("gray platform has no solid projection");
  if(Physics2D.OverlapPoint(new Vector2(-4,5)) is Collider2D old && !old.isTrigger)throw new Exception("old platform remains");
  var saved=water.Capture();water.Restore(saved);
  typeof(PrototypePlayer).GetMethod("SetMode",flags).Invoke(p,new object[]{PlayerMode.Grid});
  foreach(var c in UnityEngine.Object.FindObjectsOfType<BoxCollider2D>())if(c.name=="Gray solid footprint"&&c.enabled&&c.gameObject.activeInHierarchy)throw new Exception("2D stone projection not paused");
  foreach(var source in UnityEngine.Object.FindObjectsOfType<PrototypeFallingBlock>())
   if(source.CurrentState.Water && !source.CurrentState.Lost && (source.GetComponent<Renderer>().enabled || source.GetComponent<BoxCollider>().enabled!=source.EnterableBlue))
    throw new Exception("blue frozen collision or visual mismatch");
  var frozen=water.Capture();
  for(int i=0;i<20;i++)typeof(PrototypePlayer).GetMethod("FixedUpdate",flags).Invoke(p,null);
  if(water.Simulation.TickCount!=frozen.Fluid.Tick || Mathf.Abs(water.Simulation.TotalLost-frozen.Fluid.Lost.Sum())>.00001f)
   throw new Exception("grid view did not freeze water");
  Debug.Log("GRAY_PLAYMODE_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
