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
   if(source.CurrentState.Water && !source.CurrentState.Lost && (source.GetComponent<Renderer>().enabled || source.GetComponent<BoxCollider>().enabled))
    throw new Exception("liquid source returned as a grid cube");
  var frozen=water.Capture();
  for(int i=0;i<20;i++)typeof(PrototypePlayer).GetMethod("FixedUpdate",flags).Invoke(p,null);
  if(water.Simulation.TickCount!=frozen.Fluid.Tick || Mathf.Abs(water.Simulation.TotalLost-frozen.Fluid.Lost.Sum())>.00001f)
   throw new Exception("grid view did not freeze water");
  Debug.Log("GRAY_PLAYMODE_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
