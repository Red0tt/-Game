using UnityEngine;
public static class GrayRuleChecks {
 public static void Run() {
  var sim=new Unit02WaterSimulation(new RectInt(-3,-4,7,10),new[]{new Vector2Int(0,2)});
  sim.AddFire(new Vector2Int(0,1)); sim.SetSolids(new[]{new Vector2Int(0,-2)});
  sim.Step(); var saved=sim.Capture();
  Require(sim.Cooled(new Vector2Int(0,1)),"red consumed");
  Require(Mathf.Abs(sim.TotalSpent-1)<.0001f && sim.TotalWater<.0001f,"one unit consumed");
  Require(saved.Stones.Count==1 && saved.Stones[new Vector2Int(0,1)]==new Vector2Int(0,0),"one falling solid");
  sim.Step(); sim.Step(); Require(sim.Capture().Stones[new Vector2Int(0,1)]==new Vector2Int(0,-1),"lands on floor");
  sim.Restore(saved); Require(sim.Capture().Stones[new Vector2Int(0,1)]==new Vector2Int(0,0),"undo position");
  Require(saved.Stones[new Vector2Int(0,1)]==new Vector2Int(0,0),"snapshot independent");
  var anchors=new Vector2Int[10];for(int i=0;i<10;i++)anchors[i]=new Vector2Int(i,6);
  var drain=new Unit02WaterSimulation(new RectInt(-2,0,15,9),anchors);
  var ledge=new System.Collections.Generic.List<Vector2Int>();for(int x=0;x<10;x++)ledge.Add(new Vector2Int(x,2));
  drain.SetSolids(ledge);
  for(int i=0;i<2000;i++)drain.Step();
  Require(drain.TotalWater==0f,"ten-cell open ledge drains completely");
  Require(Mathf.Abs(drain.TotalLost-10)<.0005f,"drained water budget conserved");
  var pool=new Unit02WaterSimulation(new RectInt(-3,0,7,9),new[]{new Vector2Int(0,5)});
  pool.SetSolids(new[]{new Vector2Int(0,1),new Vector2Int(-1,2),new Vector2Int(1,2)});
  for(int i=0;i<300;i++)pool.Step();
  Require(Mathf.Abs(pool.TotalWater-1)<.0001f,"sealed basin retains normal water");
  var shelf=new Unit02WaterSimulation(new RectInt(-20,0,41,8),new[]{new Vector2Int(0,4)});
  var wide=new System.Collections.Generic.List<Vector2Int>();for(int x=-20;x<=20;x++)wide.Add(new Vector2Int(x,1));shelf.SetSolids(wide);
  for(int i=0;i<80;i++)shelf.Step();Require(shelf.TotalWater==0,"wide continuous floor drains through open ends");
  var displaced=new Unit02WaterSimulation(new RectInt(-3,0,7,8),new[]{new Vector2Int(0,3)});
  displaced.SetSolids(new[]{new Vector2Int(0,3)});Require(displaced.Depth(new Vector2Int(0,3))==0&&Mathf.Abs(displaced.TotalWater-1)<.0001f,"paper edit displaces water without overlap or deletion");
  typeof(ValidateWaterLevel).GetMethod("SolverTests",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
  Debug.Log("GRAY_RULE_CHECKS_PASSED");
 }
 static void Require(bool value,string message){if(!value)throw new System.Exception(message);}
}
