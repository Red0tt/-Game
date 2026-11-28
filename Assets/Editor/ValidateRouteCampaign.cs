using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class ValidateRouteCampaign
{
 static ValidateRouteCampaign(){EditorApplication.playModeStateChanged+=OnPlay;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/Level/try/Route01.unity");SessionState.SetInt("RouteTest",1);EditorApplication.isPlaying=true;}
 static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetInt("RouteTest",0)>0)EditorApplication.delayCall+=Test;}
 static void Test(){try{
  int stage=SessionState.GetInt("RouteTest",1);if(SceneManager.GetActiveScene().name!="Route0"+stage){EditorApplication.delayCall+=Test;return;}
  var p=UnityEngine.Object.FindObjectOfType<PrototypePlayer>();p.enabled=false;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  if(typeof(PrototypePlayer).GetField("platformBody",flags).GetValue(p)==null)typeof(PrototypePlayer).GetMethod("Start",flags).Invoke(p,null);
  if(stage<=3)
  {
   var spawn=Vector2Int.RoundToInt(new Vector2(p.transform.position.x,p.transform.position.z));
   var flowers=(Array)typeof(PrototypePlayer).GetField("flowers",flags).GetValue(p);var flower=flowers.GetValue(0);
   if(!(bool)flower.GetType().GetMethod("CanSetEnd").Invoke(flower,new object[]{new Vector2Int(spawn.x-1,spawn.y),(LayerMask)1,p.GetComponent<Collider>()}))throw new Exception("early flower cannot actually extend");
   flower.GetType().GetMethod("SetEnd").Invoke(flower,new object[]{new Vector2Int(spawn.x-1,spawn.y)});
   if(stage>=2)
   {
    var actor=new Vector2Int(spawn.x-1,spawn.y+(stage==2?-1:2));var step=stage==2?Vector2Int.up:Vector2Int.down;
    if(!(bool)flower.GetType().GetMethod("TryPush").Invoke(flower,new object[]{actor,step,(LayerMask)1,p.GetComponent<Collider>(),true}))throw new Exception("early pipe bend failed: "+flower.GetType().GetProperty("LastEditFailure").GetValue(flower));
   }
   typeof(PrototypePlayer).GetMethod("RefreshGates",flags).Invoke(p,null);Physics.SyncTransforms();
   foreach(var gate in UnityEngine.Object.FindObjectsOfType<PrototypePressureGate>())if(gate.name=="Entry paper gate"&&!gate.IsOpen)throw new Exception("early flower cannot open original route");
   var exit=(Transform)typeof(PrototypePlayer).GetField("levelExit",flags).GetValue(p);var target=Vector2Int.RoundToInt(new Vector2(exit.position.x,exit.position.z));
   var seen=new HashSet<Vector2Int>{spawn};var queue=new Queue<Vector2Int>();queue.Enqueue(spawn);
   while(queue.Count>0&&seen.Count<6000)
   {
    var cell=queue.Dequeue();if(cell==target)throw new Exception("single grid form bypasses original switching challenge, stage "+stage);
    foreach(var step in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
    {
     var next=cell+step;if(Mathf.Abs(next.x-spawn.x)>40||Mathf.Abs(next.y-spawn.y)>40||seen.Contains(next))continue;
     if(!Physics.Raycast(new Vector3(next.x,.08f,next.y),Vector3.down,.16f,1,QueryTriggerInteraction.Ignore))continue;
     bool occupied=false;foreach(var hit in Physics.OverlapBox(new Vector3(next.x,.7f,next.y),new Vector3(.29f,.6f,.29f),Quaternion.identity,1))if(hit.gameObject!=p.gameObject){occupied=true;break;}
     if(!occupied){seen.Add(next);queue.Enqueue(next);}
    }
   }
  }
  Debug.Log("ROUTE_STAGE_PASSED "+stage);
  if(stage<7){SessionState.SetInt("RouteTest",stage+1);SceneManager.LoadScene("Route0"+(stage+1));EditorApplication.delayCall+=Test;}else{SessionState.SetInt("RouteTest",0);Debug.Log("ROUTE_CAMPAIGN_PASSED");EditorApplication.Exit(0);}
 }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}}
}
