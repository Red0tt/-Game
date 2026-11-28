using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class ValidateFanhuaCombinations
{
 static ValidateFanhuaCombinations(){EditorApplication.playModeStateChanged+=OnPlay;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Level/try/Connected09.unity");SessionState.SetBool("CombinationTest",true);EditorApplication.isPlaying=true;}
 static void OnPlay(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("CombinationTest",false)){SessionState.SetBool("CombinationTest",false);EditorApplication.delayCall+=Test;}}
 static void Test(){try{
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;var p=UnityEngine.Object.FindObjectOfType<PrototypePlayer>();p.enabled=false;
  var pt=typeof(PrototypePlayer);var init=pt.GetField("initialState",flags).GetValue(p);
  object f=null;Type ft=null;Array flowers=null;var water=p.GetComponent<PrototypeWaterLevel>();
  void Reset(){pt.GetMethod("RestoreState",flags).Invoke(p,new[]{init});flowers=(Array)pt.GetField("flowers",flags).GetValue(p);f=flowers.GetValue(0);ft=f.GetType();}
  void Call(string name,params object[] args){ft.GetMethod(name).Invoke(f,args);}
  void Mode(){pt.GetMethod("SetMode",flags).Invoke(p,new object[]{PlayerMode.Platform});}
  Reset();
  bool Can(Vector2Int target)=>(bool)ft.GetMethod("CanSetEnd").Invoke(f,new object[]{target,(LayerMask)1,p.GetComponent<Collider>()});
  if(!Can(new Vector2Int(9,5))||Can(new Vector2Int(10,5)))throw new Exception("10/11 boundary invalid");
  Call("TogglePalette");Call("TogglePalette");Call("SetEnd",new Vector2Int(1,5));Mode();
  foreach(var tile in (IEnumerable)ft.GetField("red",flags).GetValue(f))
  {var obj=(GameObject)tile;var vertices=obj.GetComponent<MeshFilter>().sharedMesh.vertices;if(vertices.Length!=24)throw new Exception("spike geometry absent");foreach(var normal in obj.GetComponent<MeshFilter>().sharedMesh.normals)if(normal.sqrMagnitude<.9f)throw new Exception("paper normals cancelled");if(vertices[0].z!=-.5f)throw new Exception("spike base not attached to paired yellow");}
  Reset();Call("SetEnd",new Vector2Int(-4,5));Mode();
  typeof(PrototypeWaterLevel).GetMethod("Advance",flags).Invoke(water,new object[]{.06f,flowers});
  if((int)ft.GetField("broken",flags).GetValue(f).GetType().GetProperty("Count").GetValue(ft.GetField("broken",flags).GetValue(f))==0)throw new Exception("red above water did not break");
  Reset();Call("TogglePalette");Call("TogglePalette");Call("TogglePalette");Call("SetEnd",new Vector2Int(1,5));Mode();
  if(water.Simulation==null)throw new Exception("water not initialized");
  typeof(PrototypeWaterLevel).GetMethod("Advance",flags).Invoke(water,new object[]{.06f,flowers});
  int count=0;foreach(var cell in water.Simulation.StoneCells)count++;
  if(count==0||water.Simulation.TotalSpent<.99f)throw new Exception("blue above red did not solidify");
  Reset();
  Debug.Log("FANHUA_COMBINATIONS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
