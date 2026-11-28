using System;
using System.Collections.Generic;
using UnityEngine;

// Finite-volume, source-labelled 2D cells. No 3D floor queries occur here.
public sealed class Unit02WaterSimulation
{
    public sealed class State
    {
        public Dictionary<Vector2Int, float[]> Cells = new Dictionary<Vector2Int, float[]>();
        public Vector2Int[] Anchors;
        public float[] Spent, Lost;
        public Dictionary<Vector2Int, float> Fires = new Dictionary<Vector2Int, float>();
        public Dictionary<Vector2Int, Vector2Int> Stones = new Dictionary<Vector2Int, Vector2Int>();
        public int Tick;
    }
    public readonly RectInt Bounds;
    public readonly int SourceCount;
    private State state;
    private readonly HashSet<Vector2Int> solid = new HashSet<Vector2Int>();
    private const float Epsilon = .000001f;
    public IEnumerable<Vector2Int> Cells => state.Cells.Keys;
    public IEnumerable<Vector2Int> FireCells => state.Fires.Keys;
    public IEnumerable<Vector2Int> StoneCells => state.Stones.Values;
    public int TickCount => state.Tick;

    public Unit02WaterSimulation(RectInt bounds, Vector2Int[] anchors)
    {
        Bounds = bounds; SourceCount = anchors.Length;
        state = new State { Anchors = (Vector2Int[])anchors.Clone(), Spent = new float[SourceCount], Lost = new float[SourceCount] };
        for(int i=0;i<SourceCount;i++) Add(anchors[i],i,1f);
    }
    public void SetSolids(IEnumerable<Vector2Int> cells)
    { solid.Clear(); foreach(var cell in cells) solid.Add(cell); }
    public void AddFire(Vector2Int cell) { if(!state.Fires.ContainsKey(cell)) state.Fires.Add(cell,0); }
    public void RemoveFire(Vector2Int cell) {state.Fires.Remove(cell);}
    public float FireProgress(Vector2Int cell) => state.Fires.TryGetValue(cell,out float amount) ? amount : 0;
    public bool Cooled(Vector2Int cell) => FireProgress(cell)>=1f;
    public float Remaining(int source) => Mathf.Max(0,1f-state.Spent[source]-state.Lost[source]);
    public Vector2Int Anchor(int source) => state.Anchors[source];
    public float Depth(Vector2Int cell)
    {
        if(!state.Cells.TryGetValue(cell,out var amounts)) return 0;
        float total=0; foreach(float amount in amounts) total+=amount; return total;
    }
    public float TotalWater { get { float total=0; foreach(var cell in state.Cells.Keys) total+=Depth(cell); return total; } }
    public float TotalSpent { get { float total=0; foreach(float v in state.Spent) total+=v; return total; } }
    public float TotalLost { get { float total=0; foreach(float v in state.Lost) total+=v; return total; } }
    public void MoveSource(int source, Vector2Int anchor)
    {
        if(state.Anchors[source]==anchor) return;
        foreach(var amounts in state.Cells.Values) amounts[source]=0;
        state.Anchors[source]=anchor;
        Add(anchor,source,Remaining(source));
        Clean();
    }
    private bool Blocked(Vector2Int cell) => solid.Contains(cell) || (state.Fires.ContainsKey(cell) && !Cooled(cell)) || state.Stones.ContainsValue(cell);
    private void Add(Vector2Int cell,int source,float amount)
    {
        if(amount<=0) return;
        if(!Bounds.Contains(cell)) { state.Lost[source]+=amount; return; }
        if(!state.Cells.TryGetValue(cell,out var amounts)) state.Cells.Add(cell,amounts=new float[SourceCount]);
        amounts[source]+=amount;
    }
    private void Transfer(Vector2Int from,Vector2Int to,float amount)
    {
        float total=Depth(from);
        if(total<=0f || amount<=0f) return;
        amount=Mathf.Min(total,amount);
        var amounts=state.Cells[from];
        for(int i=0;i<SourceCount;i++)
        {
            float part=amount*amounts[i]/total;
            amounts[i]=Mathf.Max(0,amounts[i]-part);
            Add(to,i,part);
        }
    }
    private void Absorb(Vector2Int from,Vector2Int fire,float requested)
    {
        float total=Depth(from), amount=Mathf.Min(total,requested);
        if(amount<Epsilon) return;
        var amounts=state.Cells[from];
        for(int i=0;i<SourceCount;i++)
        {
            float part=amount*amounts[i]/total;
            amounts[i]=Mathf.Max(0,amounts[i]-part);
            state.Spent[i]+=part;
        }
        state.Fires[fire]+=amount;
    }
    private void Burn()
    {
        var fires=new List<Vector2Int>(state.Fires.Keys);
        fires.Sort((a,b)=>a.y==b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        foreach(var fire in fires)
        {
            if(Cooled(fire)) continue;
            // Only an attempted downward fall into the fire can form stone.
            // Side/bottom contacts remain blocking and cannot spend water.
            var above=fire+Vector2Int.up;
            if(solid.Contains(above)) continue;
            Absorb(above,fire,1f-state.Fires[fire]);
            // Numerical rounding must never pull water from unrelated cells.
            if(state.Fires[fire]>=1f-Epsilon)
            { state.Fires[fire]=1f; state.Stones[fire]=fire; }
        }
    }
    public bool HasSupport(Vector2Int cell) => Supported(cell);
    private bool Supported(Vector2Int cell)
    {
        var below=cell+Vector2Int.down;
        while(Bounds.Contains(below))
        {
            if(Blocked(below)) return true;
            if(Depth(below)<.999f) return false;
            below+=Vector2Int.down;
        }
        return false;
    }
    private bool LowerPocket(Vector2Int start,out Vector2Int target)
    {
        var queue=new Queue<Vector2Int>();var visited=new HashSet<Vector2Int>{start};queue.Enqueue(start);
        while(queue.Count>0)
        {
            var cell=queue.Dequeue();
            if(cell.y<start.y && Depth(cell)<.999f){target=cell;return true;}
            foreach(var d in new[]{Vector2Int.down,Vector2Int.left,Vector2Int.right})
            {
                var next=cell+d;
                if(Bounds.Contains(next)&&!Blocked(next)&&visited.Add(next))queue.Enqueue(next);
            }
        }
        target=default;return false;
    }
    public void Step()
    {
        Burn();
        var cells=new List<Vector2Int>(state.Cells.Keys);
        cells.Sort((a,b)=>a.y==b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        foreach(var cell in cells)
        {
            var below=cell+Vector2Int.down;
            if(!Blocked(below)) Transfer(cell,below,Mathf.Max(0,1f-Depth(below)));
        }
        Burn(); // First contact cools before lateral spreading can split the incoming drop.
        // Resolve pressure into nearby lower pockets before building a higher layer.
        // This prevents a falling column from spilling merely because lateral updates lag.
        foreach(var cell in cells)
        {
            if(!Supported(cell)) continue;
            int guard=0;
            while(Depth(cell)>Epsilon && guard++<Bounds.width*Bounds.height && LowerPocket(cell,out var pocket))
                Transfer(cell,pocket,Mathf.Min(Depth(cell),1f-Depth(pocket)));
        }
        // Equalise neighbouring columns at this row. Unsupported edges spill down next tick.
        for(int y=Bounds.yMin;y<Bounds.yMax;y++)
        for(int n=Bounds.xMin;n<Bounds.xMax-1;n++)
        {
            int x=(state.Tick&1)==0 ? n : Bounds.xMax-2-(n-Bounds.xMin);
            var a=new Vector2Int(x,y); var b=a+Vector2Int.right;
            if(Blocked(a)||Blocked(b)) continue;
            float difference=Depth(a)-Depth(b);
            var from=difference>0?a:b; var to=difference>0?b:a;
            if(!Supported(from)) continue;
            Transfer(from,to,Mathf.Abs(difference)*.5f);
        }
        // Boundary cells are genuine outflows, not invisible retaining walls.
        for(int y=Bounds.yMin;y<Bounds.yMax;y++)
        {
            foreach(int x in new[]{Bounds.xMin,Bounds.xMax-1})
            {
                var edge=new Vector2Int(x,y);
                if(!Blocked(edge)) Transfer(edge,new Vector2Int(x==Bounds.xMin ? x-1:x+1,y),Depth(edge)*.5f);
            }
        }
        Burn();
        // Bottom-up order permits a stack to fall without interpenetrating.
        var origins=new List<Vector2Int>(state.Stones.Keys);
        origins.Sort((a,b)=>state.Stones[a].y.CompareTo(state.Stones[b].y));
        foreach(var origin in origins)
        {
            var cell=state.Stones[origin]; var below=cell+Vector2Int.down;
            if(Blocked(below) || Depth(below)>.000001f) continue;
            if(Bounds.Contains(below)) state.Stones[origin]=below;
            else state.Stones.Remove(origin);
        }
        Clean(); state.Tick++;
    }
    private void Clean()
    {
        var empty=new List<Vector2Int>();
        foreach(var cell in state.Cells.Keys)
            if(Depth(cell)<Epsilon)
            {
                // Account for numerical residue instead of leaving an undrainable drop.
                var amounts=state.Cells[cell];
                for(int i=0;i<SourceCount;i++)state.Lost[i]+=amounts[i];
                empty.Add(cell);
            }
        foreach(var cell in empty) state.Cells.Remove(cell);
    }
    public State Capture() => Copy(state);
    public void Restore(State saved) { state=Copy(saved); }
    private static State Copy(State input)
    {
        var result=new State { Anchors=(Vector2Int[])input.Anchors.Clone(), Spent=(float[])input.Spent.Clone(),
            Lost=(float[])input.Lost.Clone(), Fires=new Dictionary<Vector2Int,float>(input.Fires), Stones=new Dictionary<Vector2Int,Vector2Int>(input.Stones), Tick=input.Tick };
        foreach(var pair in input.Cells) result.Cells.Add(pair.Key,(float[])pair.Value.Clone());
        return result;
    }
}
