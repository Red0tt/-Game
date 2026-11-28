using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class ValidateConnectedCampaign
{
 static ValidateConnectedCampaign(){EditorApplication.playModeStateChanged+=OnPlay;}
 public static void Run(){
 var all=new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
 for(int i=1;i<=9;i++){string path="Assets/Level/try/Connected0"+i+".unity";all.RemoveAll(x=>x.path==path);all.Add(new EditorBuildSettingsScene(path,true));}
 EditorBuildSettings.scenes=all.ToArray();
EditorSceneManager.OpenScene("Assets/Level/try/Connected01.unity");SessionState.SetInt("ConnectedStage",1);EditorApplication.isPlaying=true;}
 static void OnPlay(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetInt("ConnectedStage",0)>0)EditorApplication.delayCall+=Test;}
 static void Test(){try{
  int stage=SessionState.GetInt("ConnectedStage",1);
  if(SceneManager.GetActiveScene().name!="Connected0"+stage){EditorApplication.delayCall+=Test;return;}
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  var player=UnityEngine.Object.FindObjectOfType<PrototypePlayer>();player.enabled=false;
  if(typeof(PrototypePlayer).GetField("platformBody",flags).GetValue(player)==null)typeof(PrototypePlayer).GetMethod("Start",flags).Invoke(player,null);
  var flowers=(Array)typeof(PrototypePlayer).GetField("flowers",flags).GetValue(player);var f=flowers.GetValue(0);var ft=f.GetType();
  var gates=UnityEngine.Object.FindObjectsOfType<PrototypePressureGate>();foreach(var g in gates)if(g.IsOpen)throw new Exception("gate begins open");
  if(stage<=3||stage>=7)
  {
    ft.GetMethod("SetEnd").Invoke(f,new object[]{new Vector2Int(stage==9?8:(stage==3||stage>=7)?3:1,5)});
    if(stage>=2)Shift(new Vector2Int(stage==9?8:1,4),Vector2Int.up);
    if(stage==3||stage==8)Shift(new Vector2Int(3,7),Vector2Int.down);
  }
  if(stage>=4)
  {
    foreach(var block in UnityEngine.Object.FindObjectsOfType<PrototypeFallingBlock>())
        if(!block.CanHangInGrid&&!block.TryPushCell(Vector3.right,2,(LayerMask)1,new RectInt(-7,0,22,13)))throw new Exception("source cannot be pushed");
    typeof(PrototypePlayer).GetMethod("SetMode",flags).Invoke(player,new object[]{PlayerMode.Platform});
    var water=player.GetComponent<PrototypeWaterLevel>();
    for(int i=0;i<200;i++)typeof(PrototypeWaterLevel).GetMethod("Advance",flags).Invoke(water,new object[]{.06f,flowers});
    typeof(PrototypePlayer).GetMethod("SetMode",flags).Invoke(player,new object[]{PlayerMode.Grid});
  }
  typeof(PrototypePlayer).GetMethod("RefreshGates",flags).Invoke(player,null);
  foreach(var g in gates)if(!g.IsOpen)throw new Exception("reference solution failed stage "+stage);
  // Verify a physical walking route around the paper, rather than teleporting through it.
  var body=player.GetComponent<Rigidbody>();body.position=new Vector3(0,.7f,11);
  for(int x=0;x<=(stage==9?14:9);x++)
  {var c=new Vector3(x,.7f,11);foreach(var hit in Physics.OverlapBox(c,new Vector3(.25f,.4f,.25f),Quaternion.identity,1))if(hit.gameObject!=player.gameObject)throw new Exception("exit approach blocked by "+hit.name);}
  body.position=new Vector3(stage==9?14:9,.7f,6);
  if(!(bool)typeof(PrototypePlayer).GetMethod("TryCompleteLevel",flags).Invoke(player,null))throw new Exception("exit not reachable");
  Debug.Log("CONNECTED_STAGE_PASSED "+stage);
  void Shift(Vector2Int cell,Vector2Int step){if(!(bool)ft.GetMethod("TryShiftStrip").Invoke(f,new object[]{cell,step,(LayerMask)1,player.GetComponent<Collider>(),true}))throw new Exception("strip edit failed");}
  if(stage<9){SessionState.SetInt("ConnectedStage",stage+1);SceneManager.LoadScene("Connected0"+(stage+1));EditorApplication.delayCall+=Test;}
  else{SessionState.SetInt("ConnectedStage",0);Debug.Log("CONNECTED_CAMPAIGN_PASSED");EditorApplication.Exit(0);}
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
