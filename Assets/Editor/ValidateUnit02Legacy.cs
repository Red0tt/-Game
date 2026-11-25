using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Differential traces: execute identical legacy inputs against the pre-change and post-change projects.
[InitializeOnLoad]
public static class ValidateUnit02Legacy
{
    static ValidateUnit02Legacy(){EditorApplication.playModeStateChanged+=Changed;}
    [Serializable] public sealed class Frame {public string scene;public int tick,mode,history;public Vector3 position;public string objects;}
    [Serializable] public sealed class Report {public List<Frame> frames=new List<Frame>();}
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run(){SessionState.SetBool("Unit02Legacy",true);EditorApplication.isPlaying=true;}
    static void Changed(PlayModeStateChange state)
    {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("Unit02Legacy",false)){SessionState.SetBool("Unit02Legacy",false);EditorApplication.delayCall+=Test;}}
    static Report report;
    static Queue<string> pending;
    static string current;
    static void Test()
    {
        try
        {
            Physics.autoSimulation=false;Physics2D.simulationMode=SimulationMode2D.Script;
            report=new Report();var paths=new List<string>();
            foreach(string path in Directory.GetFiles("Assets/Level","*.unity",SearchOption.AllDirectories))
                if(!path.Contains("Water")&&!path.Contains("Unit02"))paths.Add(path.Replace('\\','/'));
            paths.Sort(StringComparer.Ordinal);pending=new Queue<string>(paths);
            SceneManager.sceneLoaded+=Loaded;Next();
        }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    static void Next()
    {
        if(pending.Count==0)
        {
            if(report.frames.Count==0)throw new Exception("No legacy frames recorded");
            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(),"legacy-trace.json"),JsonUtility.ToJson(report,true));
            Debug.Log("LEGACY_TRACE_SUCCESS "+report.frames.Count);EditorApplication.Exit(0);return;
        }
        current=pending.Dequeue();EditorSceneManager.LoadSceneInPlayMode(current,new LoadSceneParameters(LoadSceneMode.Single));
    }
    static void Loaded(Scene scene,LoadSceneMode mode)
    {
        var p=UnityEngine.Object.FindObjectOfType<PrototypePlayer>();if(p!=null)p.enabled=false;
        foreach(var c in UnityEngine.Object.FindObjectsOfType<PrototypeCamera>())c.enabled=false;
        EditorApplication.delayCall+=Trace;
    }
    static void Trace()
    {
        try
        {
            var player=UnityEngine.Object.FindObjectOfType<PrototypePlayer>();
            if(player==null)throw new Exception("Legacy player missing in "+current);
            Action<string,object> set=(n,v)=>typeof(PrototypePlayer).GetField(n,Flags).SetValue(player,v);
            Func<string,object> get=n=>typeof(PrototypePlayer).GetField(n,Flags).GetValue(player);
            Action<string> call=n=>typeof(PrototypePlayer).GetMethod(n,Flags).Invoke(player,null);
            if(get("body")==null)call("Start");var rb=(Rigidbody2D)get("platformBody");
            for(int i=0;i<120;i++)
            {
                Vector2 input=i%32<12?Vector2.right:i%32<18?Vector2.up:i%32<27?Vector2.left:Vector2.down;
                set("moveInput",input);set("waitForMoveRelease",false);set("nextGridStep",0f);
                set("queuedGridDirection",new Vector3(input.x,0,input.y));
                if(i%17==4)set("switchRequested",true);if(i%13==6)set("jumpRequested",true);
                if(i%23==8)set("fanhuaInteractionRequested",true);if(i==75)set("undoRequested",true);if(i==99)set("resetRequested",true);
                call("FixedUpdate");Physics2D.Simulate(.02f);if(player.Mode==PlayerMode.Platform)call("CopyPlatformPosition");
                Physics.SyncTransforms();Physics.Simulate(.02f);if(i%5!=0)continue;
                var sb=new System.Text.StringBuilder();
                foreach(var block in UnityEngine.Object.FindObjectsOfType<PrototypeFallingBlock>())
                    sb.Append(block.CurrentState.Position.ToString("F3")).Append(block.CurrentState.Lost).Append(block.CurrentState.Plugged);
                foreach(var box in UnityEngine.Object.FindObjectsOfType<PrototypePushBox>())sb.Append(box.Position.ToString("F3"));
                var history=(System.Collections.ICollection)get("history");
                report.frames.Add(new Frame{scene=current,tick=i,mode=(int)player.Mode,history=history.Count,position=player.GetComponent<Rigidbody>().position,objects=sb.ToString()});
            }
            Debug.Log("LEGACY_SCENE_PASS "+current);Next();
        }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
