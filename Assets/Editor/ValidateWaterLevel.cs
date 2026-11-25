using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ValidateWaterLevel
{
    static ValidateWaterLevel(){EditorApplication.playModeStateChanged+=OnPlay;}
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static PrototypePlayer player;static Rigidbody2D body;static PrototypeWaterLevel water;
    static object Get(string name)=>typeof(PrototypePlayer).GetField(name,Flags).GetValue(player);
    static void Set(string name,object value)=>typeof(PrototypePlayer).GetField(name,Flags).SetValue(player,value);
    static object Call(string name,params object[] args)=>typeof(PrototypePlayer).GetMethod(name,Flags).Invoke(player,args);
    static void Check(bool value,string message){if(!value)throw new Exception(message+" @ "+(body!=null?body.position.ToString():"solver"));Debug.Log("UNIT02_PASS "+message);}
    public static void Run(){BuildWaterLevel.Build();SessionState.SetBool("Unit02Test",true);EditorApplication.isPlaying=true;}
    static void OnPlay(PlayModeStateChange state)
    {if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("Unit02Test",false)){SessionState.SetBool("Unit02Test",false);EditorApplication.delayCall+=Test;}}
    static void Tick(Vector2 input=default,bool jump=false)
    {
        Set("moveInput",input);Set("waitForMoveRelease",false);Set("jumpRequested",jump);Call("FixedUpdate");
        Physics2D.Simulate(.02f);if(player.Mode==PlayerMode.Platform)Call("CopyPlatformPosition");
        Physics.SyncTransforms();Physics.Simulate(.02f);
    }
    static void Walk(int x,int z){Set("nextGridStep",0f);Set("queuedGridDirection",new Vector3(x,0,z));Tick();}
    static void Switch(){Set("switchRequested",true);Tick();}
    static void Q(){Set("fanhuaInteractionRequested",true);Tick();}
    static void Wait(int n){for(int i=0;i<n;i++)Tick();}
    static void Balance(Unit02WaterSimulation sim,string label)
    {Check(Mathf.Abs(sim.SourceCount-sim.TotalWater-sim.TotalSpent-sim.TotalLost)<.0005f,label+" conserves water");}
    static HashSet<Vector2Int> Basin()
    {
        var s=new HashSet<Vector2Int>();for(int x=3;x<=5;x++)s.Add(new Vector2Int(x,2));
        for(int z=3;z<=8;z++)s.Add(new Vector2Int(2,z));for(int z=3;z<=5;z++)s.Add(new Vector2Int(6,z));
        s.Add(new Vector2Int(5,4));return s;
    }
    static void SolverTests()
    {
        var anchors=new Vector2Int[8];for(int i=0;i<8;i++)anchors[i]=new Vector2Int(3,10+i);
        var sim=new Unit02WaterSimulation(new RectInt(0,0,19,22),anchors);sim.SetSolids(Basin());
        for(int i=0;i<600;i++)sim.Step();Balance(sim,"closed basin");
        Check(sim.Depth(new Vector2Int(5,3))>.99f,"water flows sideways under alcove roof");
        Check(sim.Depth(new Vector2Int(3,5))>.99f&&sim.Depth(new Vector2Int(5,5))>.99f,"eight units fill eight-cell basin");
        Check(sim.Depth(new Vector2Int(5,4))==0,"water never occupies solid roof");
        var saved=sim.Capture();sim.MoveSource(0,new Vector2Int(7,18));for(int i=0;i<300;i++)sim.Step();
        Check(sim.TotalLost>.9f,"moving source does not create another unit");Balance(sim,"moved source");
        sim.MoveSource(0,new Vector2Int(3,9));Check(sim.Remaining(0)<.001f&&sim.TotalWater<7.01f,"repositioning spent or lost source never refills it");
        sim.Restore(saved);Check(Mathf.Abs(sim.TotalWater-8)<.001f,"fluid snapshot restores distribution and budget");
        var open=new Unit02WaterSimulation(new RectInt(0,0,12,12),new[]{new Vector2Int(5,6),new Vector2Int(5,7)});
        open.SetSolids(new[]{new Vector2Int(4,2),new Vector2Int(5,2),new Vector2Int(6,2)});
        for(int i=0;i<600;i++)open.Step();Balance(open,"open platform");Check(open.TotalLost>1.9f,"open platform spills from sides instead of retaining water");
        var falling=new Unit02WaterSimulation(new RectInt(0,0,9,9),new[]{new Vector2Int(3,6)});
        Check(!falling.HasSupport(new Vector2Int(3,6)),"airborne water does not grant a surface double jump");
        var side=new Unit02WaterSimulation(new RectInt(0,0,9,9),new[]{new Vector2Int(3,4)});
        side.AddFire(new Vector2Int(4,4));side.AddFire(new Vector2Int(5,4));side.Step();
        Check(side.Cooled(new Vector2Int(4,4))&&!side.Cooled(new Vector2Int(5,4)),"side contact cools exactly one fire per unit");Balance(side,"fire consumption");
        var shallow=new Unit02WaterSimulation(new RectInt(0,0,19,22),new[]{new Vector2Int(3,10),new Vector2Int(3,11)});shallow.SetSolids(Basin());
        for(int i=0;i<500;i++)shallow.Step();Check(shallow.Depth(new Vector2Int(3,5))<.01f,"two sources cannot manufacture three-deep water");
    }
    static void Test()
    {
        try
        {
            SolverTests();player=UnityEngine.Object.FindObjectOfType<PrototypePlayer>();player.enabled=false;
            if(Get("platformBody")==null)Call("Start");body=(Rigidbody2D)Get("platformBody");water=player.GetComponent<PrototypeWaterLevel>();
            Physics.autoSimulation=false;Physics2D.simulationMode=SimulationMode2D.Script;
            // Prepare blue/yellow A. The runway is ordinary grid-only floor.
            Walk(0,1);Walk(0,1);Walk(1,0);Walk(1,0);Q();for(int i=0;i<6;i++)Walk(0,1);
            Check(player.GetComponent<Rigidbody>().position==new Vector3(3,.7f,18),"six back-pulls supply eight blue units");
            Walk(-1,0);for(int i=0;i<6;i++)Walk(0,-1);Walk(-1,0);for(int i=0;i<3;i++)Walk(0,-1);Walk(1,0);Walk(1,0);
            Check(player.GetComponent<Rigidbody>().position.x==3&&player.GetComponent<Rigidbody>().position.z==9,"entry is above real 2D basin");
            object beforeRelease=Call("CaptureState");Switch();Wait(600);
            Check(player.Mode==PlayerMode.Platform,"water remains playable while basin fills");Balance(water.Simulation,"scene");
            Check(water.Simulation.Depth(new Vector2Int(5,3))>.98f,"scene pool includes submerged side chamber");
            Debug.Log("UNIT02_POSITION float "+body.position);
            object floating=Call("CaptureState");Switch();Check(player.Mode==PlayerMode.Platform&&!(bool)Get("switchRequested"),"unsupported water return is consumed once");
            float y=body.position.y;for(int i=0;i<10;i++)Tick(Vector2.down);Check(body.position.y<y-.3f,"can dive after rejected return");Call("RestoreState",floating);
            // Dive in x=4, then enter the chamber underneath its roof.
            for(int i=0;i<17;i++)Tick(Vector2.right);
            for(int i=0;i<70;i++)Tick(Vector2.down);
            for(int i=0;i<17;i++)Tick(new Vector2(1,-1));
            Check(water.SwitchOpen,"submerged physical switch opens gate");
            for(int i=0;i<14;i++)Tick(Vector2.left);for(int i=0;i<85;i++)Tick(Vector2.up);
            for(int i=0;i<15;i++)Tick(Vector2.right);
            Debug.Log("UNIT02_POSITION lip "+body.position);
            Tick(Vector2.right,true);for(int i=0;i<24;i++)Tick(Vector2.right);Wait(3);
            Debug.Log("UNIT02_POSITION bank "+body.position);Switch();
            Check(player.Mode==PlayerMode.Grid&&Mathf.RoundToInt(player.GetComponent<Rigidbody>().position.x)==7,"swim and jump reach other grid plane");
            foreach(var source in player.GetType().GetField("fallingBlocks",Flags).GetValue(player) as PrototypeFallingBlock[])
                Check(source.GetComponent<Renderer>().enabled&&source.GetComponent<BoxCollider>().enabled,"2.5D restores solid spare source");
            Check(!(bool)typeof(PrototypeWaterLevel).GetMethod("CanEditFlower",Flags).Invoke(water,new object[]{0}),"released flower cannot duplicate water by rebuilding");
            int pausedTick=water.Simulation.TickCount;
            object bankState=Call("CaptureState");
            int guard=0;while(player.GetComponent<Rigidbody>().position.z<11&&guard++<10)Walk(0,1);
            Check(water.Simulation.TickCount==pausedTick,"2.5D pauses the same water distribution");
            Walk(1,0);for(int i=0;i<4;i++)Walk(1,0);
            var spare=((PrototypeFallingBlock[])Get("fallingBlocks"))[0];Check(spare.CurrentState.Position==new Vector2(13,11),"push spare into middle fire column");
            // Withdraw along row 11, then down to the approach deck.
            guard=0;while(player.GetComponent<Rigidbody>().position.x>7&&guard++<10)Walk(-1,0);
            guard=0;while(player.GetComponent<Rigidbody>().position.z>8&&guard++<10)Walk(0,-1);
            Walk(1,0);Walk(1,0);Walk(1,0);object beforeFire=Call("CaptureState");Switch();Wait(200);
            var fires=UnityEngine.Object.FindObjectsOfType<Unit02FireTile>();Unit02FireTile middle=null;
            foreach(var fire in fires)if(fire.Cell.x==13)middle=fire;
            Check(middle!=null&&middle.Cooled&&spare.CurrentState.Lost,"one source is spent on one fixed grey midpoint");
            Check(Mathf.Abs(water.Simulation.TotalSpent-1)<.001f,"cooling costs one actual water unit");Balance(water.Simulation,"cooled scene");
            Debug.Log("UNIT02_POSITION approach "+body.position);
            Tick(Vector2.right,true);for(int i=0;i<32;i++)Tick(Vector2.right);Wait(30);
            Debug.Log("UNIT02_POSITION grey "+body.position);Check(body.position.x>12.5f&&body.position.x<13.5f&&body.position.y>7.7f,"land on grey midpoint");
            for(int i=0;i<9;i++)Tick(Vector2.right);Tick(Vector2.right,true);
            for(int i=0;i<60&&!(bool)Get("levelComplete");i++)Tick(Vector2.right);
            Check((bool)Get("levelComplete"),"actual exit reached without operation checklist");
            Set("resetRequested",true);Tick();Check(water.Simulation==null&&!water.SwitchOpen,"R clears water distribution consumption and gate");
            Set("undoRequested",true);Tick();Check(water.Simulation!=null&&water.SwitchOpen&&middle.Cooled,"Z restores fluid and cooled terrain after R");
            Call("RestoreState",beforeFire);Switch();Wait(180);body.position=new Vector2(11,7);Tick();
            Check(player.Mode==PlayerMode.Grid,"uncool fire still causes respawn");
            Call("RestoreState",beforeRelease);Switch();Wait(120);Call("RestoreState",beforeRelease);
            Check(water.Simulation==null,"undo before release allows redesign without duplicating water");
            // Counterexamples use the same actual terrain and player physics.
            for(float launch=9.1f;launch<=10.41f;launch+=.1f)
            {
                Call("RestoreState",bankState);
                player.GetComponent<Rigidbody>().position=new Vector3(10,.7f,8);
                Call("SetMode",PlayerMode.Platform);Wait(100);
                body.position=new Vector2(launch,7.9f);body.velocity=new Vector2(4,Mathf.Sqrt(2*9.81f*1.2f));
                Set("activeAction",Enum.Parse(typeof(PrototypePlayer).GetNestedType("ActionKind",BindingFlags.NonPublic),"Air"));
                for(int i=0;i<90;i++)Tick(Vector2.right);
                Check(!(bool)Get("levelComplete"),"uncool fire cannot be bypassed from x="+launch.ToString("F1"));
            }
            for(int cooledX=11;cooledX<=12;cooledX++)
            {
                Call("RestoreState",bankState);
                var state=spare.CurrentState;state.Position=new Vector2(cooledX,11);spare.RestoreWorldPosition(state);
                player.GetComponent<Rigidbody>().position=new Vector3(10,.7f,8);Call("SetMode",PlayerMode.Platform);Wait(180);
                body.position=new Vector2(cooledX+.7f,7.9f);body.velocity=new Vector2(4,Mathf.Sqrt(2*9.81f*1.2f));
                Set("activeAction",Enum.Parse(typeof(PrototypePlayer).GetNestedType("ActionKind",BindingFlags.NonPublic),"Air"));
                for(int i=0;i<90;i++)Tick(Vector2.right);
                Check(!(bool)Get("levelComplete"),"cooling short midpoint x="+cooledX+" cannot reach exit");
            }
            Call("RestoreState",bankState);
            player.GetComponent<Rigidbody>().position=new Vector3(13,.7f,11);Call("SetMode",PlayerMode.Platform);
            for(int i=0;i<90;i++)Tick(Vector2.right);
            Check(!(bool)Get("levelComplete"),"dropping from preparation runway cannot skip fire ridge");
            Debug.Log("UNIT02_VALIDATION_SUCCESS");EditorApplication.Exit(0);
        }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
