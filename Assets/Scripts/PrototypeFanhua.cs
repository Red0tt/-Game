using System.Collections.Generic;
using UnityEngine;

// The test grid is an area, including cells without floor support.
internal sealed class PrototypeFanhua
{
    internal sealed class State
    {
        public Vector2Int End;
        public Vector2Int Anchor;
        public bool Released;
        public HashSet<Vector2Int> Cooled, Shattered;
        public bool AlternatePalette;
        public int PaletteMode;
        public Vector2Int Direction;
        public PrototypeFallingBlock.State[] Blue;
        public Vector2Int[] BlueOrigins;
        public bool[] Filled;
        public Dictionary<int,int> Offsets;
        public bool[] Broken;
    }

    private readonly Vector2Int anchor;
    private readonly RectInt grid;
    private readonly Transform world;
    private readonly Transform platform;
    private readonly Material material;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<BoxCollider> walls = new List<BoxCollider>();
    private readonly HashSet<BoxCollider> blueWalls = new HashSet<BoxCollider>();
    private readonly List<PrototypeFallingBlock> blue = new List<PrototypeFallingBlock>();
    private readonly Dictionary<PrototypeFallingBlock,Vector2Int> blueOrigins=new Dictionary<PrototypeFallingBlock,Vector2Int>();
    private readonly List<GameObject> red = new List<GameObject>();
    private readonly Dictionary<GameObject,BoxCollider2D> redProjections = new Dictionary<GameObject,BoxCollider2D>();
    private readonly HashSet<GameObject> filled = new HashSet<GameObject>();
    private readonly Dictionary<int,int> offsets = new Dictionary<int,int>();
    private readonly Dictionary<Vector2Int,Vector2Int> originalCells = new Dictionary<Vector2Int,Vector2Int>();
    private readonly Dictionary<GameObject,Vector2Int> redGridCells=new Dictionary<GameObject,Vector2Int>();
    private readonly Dictionary<GameObject, Vector2Int> redOriginal = new Dictionary<GameObject,Vector2Int>();
    private readonly HashSet<GameObject> broken = new HashSet<GameObject>();
    private readonly Dictionary<GameObject,TileColor> ribbonColors=new Dictionary<GameObject,TileColor>();
    private readonly HashSet<Vector2Int> yellowWaterCells=new HashSet<Vector2Int>();
    private readonly List<Collider2D> yellowShapes=new List<Collider2D>();
    private readonly List<GameObject> yellowRibbonProjections=new List<GameObject>();
    private readonly List<GameObject> ribbonViews=new List<GameObject>();
    private readonly List<Mesh> ribbonMeshes=new List<Mesh>();
    private static Mesh paperMesh;
    private static Mesh cubeMesh;
    private readonly Dictionary<GameObject,Mesh> spikeMeshes = new Dictionary<GameObject,Mesh>();
    private readonly List<GameObject> pleats = new List<GameObject>();
    private bool platformMode;
    private bool released;
    private readonly HashSet<Vector2Int> cooledStrips=new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> shatteredStrips=new HashSet<Vector2Int>();
    private Vector2Int end;
    private Vector2Int expansionDirection;
    private bool alternatePalette;
    private int paletteMode;

    public PrototypeFanhua(Transform world, Transform platform, Material material,
        Vector2Int anchor, RectInt grid)
    {
        this.world = world;
        this.platform = platform;
        this.material = material;
        this.anchor = anchor;
        this.grid = grid;
        end = anchor;
        Build();
    }

    public State Capture()
    {
        return new State
        {
            End = end, Anchor = anchor, Released = released,
            Cooled=new HashSet<Vector2Int>(cooledStrips), Shattered=new HashSet<Vector2Int>(shatteredStrips),
            AlternatePalette = alternatePalette,
            PaletteMode = paletteMode,
            Direction = expansionDirection,
            Blue = blue.ConvertAll(block => block.CurrentState).ToArray(),
            BlueOrigins=blue.ConvertAll(block=>blueOrigins[block]).ToArray(),
            Filled = red.ConvertAll(tile => filled.Contains(tile)).ToArray(),
            Offsets = new Dictionary<int,int>(offsets),
            Broken = red.ConvertAll(tile => broken.Contains(tile)).ToArray()
        };
    }

    public IEnumerable<Vector2Int> YellowCells
    {
        get { if(yellowWaterCells.Count>0){foreach(var cell in yellowWaterCells)yield return cell;yield break;}
            if (Axis != 0) foreach (var cell in Footprint(end))
            if (ColorAt(cell) == TileColor.Yellow) yield return cell; }
    }
    public IEnumerable<Vector2Int> FireCells
    {
        get { foreach(var tile in red) if(!broken.Contains(tile)&&!filled.Contains(tile))yield return redGridCells[tile]; }
    }
    public void CoolCell(Vector2Int cell)
    {
        foreach(var tile in red)
            if(redGridCells[tile]==cell && !filled.Contains(tile)) Fill(tile);
    }
    public IEnumerable<Vector2Int> BrokenFireCells {get {foreach(var tile in broken)yield return redGridCells[tile];}}
    public bool WaterRules => world!=null && world.GetComponentInChildren<PrototypeWaterLevel>() != null;
    public RectInt Grid => grid;
    public IReadOnlyList<PrototypeFallingBlock> BlueBlocks => blue;
    public int Axis => expansionDirection==Vector2Int.zero ? 0 : expansionDirection.x!=0?1:2;

    public bool PressesPlate(Vector2Int cell) =>
        Axis != 0 && Contains(cell, end) && ColorAt(cell) == TileColor.Yellow;

    public Vector2Int AnchorCell => anchor;
    public Vector2Int EndCell => end;
    public string LastEditFailure {get;private set;}
    public int Extension => LengthFor(end);
    private int LengthFor(Vector2Int target)
    {
        if(target==anchor && Axis==0)return 0;
        var area=Area(target);bool horizontal=Axis==0?target.y==anchor.y:Axis==1;
        int min=horizontal?area.xMin:area.yMin,max=horizontal?area.xMax:area.yMax;
        int length=Mathf.Max(0,(horizontal?area.width:area.height)-2);
        for(int n=min+1;n<max;n++)
        {int a=ExtensionOffset(n-1),b=ExtensionOffset(n);length+=Mathf.Abs(a-b);}
        return length;
    }
    public string PaletteHint => new[]{"A · directional","B · reversed","C · red/yellow","D · blue above red"}[paletteMode];

    public bool IsInitial => Axis==0 && paletteMode==0;

    public void Restore(State state)
    {
        released = state.Released;
        cooledStrips.Clear();shatteredStrips.Clear();
        if(state.Cooled!=null)cooledStrips.UnionWith(state.Cooled);if(state.Shattered!=null)shatteredStrips.UnionWith(state.Shattered);
        end = state.End;
        alternatePalette = state.AlternatePalette;
        paletteMode = state.PaletteMode;
        expansionDirection = state.Direction;
        offsets.Clear();
        if(state.Offsets!=null)foreach(var pair in state.Offsets)offsets.Add(pair.Key,pair.Value);
        Build();
        if(released) foreach(var saved in state.Blue) CreateBlue(Vector2Int.RoundToInt(saved.Position));
        for (int i = 0; i < blue.Count; i++)
        {blue[i].RestoreWorldPosition(state.Blue[i]);if(state.BlueOrigins!=null)blueOrigins[blue[i]]=state.BlueOrigins[i];}
        for (int i = 0; i < red.Count; i++)
            if (state.Filled[i])
                Fill(red[i]);
        for(int i=0;i<red.Count;i++)if(state.Broken!=null && state.Broken[i])Break(red[i]);
    }

    public bool CanSetEnd(Vector2Int target, LayerMask solids, Collider pullingPlayer)
    {
        if (target == end || !grid.Contains(target))
            return false;
        if (target == anchor)
            return false;

        Vector2Int delta = target - anchor;
        if (LengthFor(target) > 10)
            return false;
        if(Axis!=0 && Mathf.Abs(delta.x)+Mathf.Abs(delta.y)<Mathf.Abs(end.x-anchor.x)+Mathf.Abs(end.y-anchor.y)) return false;
        if (delta.x != 0 && delta.y != 0)
            return false;
        if (end != anchor && (end.x == anchor.x) != (target.x == anchor.x))
            return false;

        foreach (Vector2Int cell in Footprint(target,true))
        {
            if (!grid.Contains(cell))
                return false;
            foreach (Collider obstacle in Physics.OverlapBox(World(cell), Vector3.one * 0.44f,
                Quaternion.identity, solids, QueryTriggerInteraction.Ignore))
                if (obstacle != pullingPlayer && !(obstacle is BoxCollider wall && walls.Contains(wall)) &&
                    !blue.Exists(block => block.gameObject == obstacle.gameObject))
                    return false;
        }
        return true;
    }

    public int DistanceTo(Vector2Int cell)
    {
        int distance=int.MaxValue;
        foreach(var tile in Footprint(end,true))distance=Mathf.Min(distance,Mathf.Abs(cell.x-tile.x)+Mathf.Abs(cell.y-tile.y));
        return distance;
    }

    public bool TryPush(Vector2Int player,Vector2Int step,LayerMask solids,Collider actor,bool apply)
    {
        if(platformMode)return false;
        if(TryBendDetachedBlue(player,step,solids,actor,apply,out bool blueContact))return true;
        if(blueContact)return false;
        if(!Contains(player+step,end)&&!TouchesBondedBlue(player+step))return false;
        if(Axis==0)return false;
        var axis=PullAxis(end);
        if(step==axis)return false; // The base cannot translate the flower.
        if(step==-axis)
        {
            // Compress from the moving end, without returning to the folded base.
            int station=Axis==1?end.x:end.y;
            var contact=originalCells.TryGetValue(player+step,out var hitCell)?hitCell:player+step;
            if((Axis==1?contact.x:contact.y)!=station)return false;
            var target=end+step;
            if(Area(target).size!=Area(end).size- (Axis==1?Vector2Int.right:Vector2Int.up))return false;
            bool bondedBlue=HasBondedBlueRibbon;
            var before=Capture();var oldEnd=end;var previous=new Dictionary<int,int>(offsets);
            int tipOffset=OffsetAt(station);end=target;
            int newTip=Axis==1?end.x:end.y;
            bool valid=!IsBaseStation(newTip)||tipOffset==0;
            offsets.Remove(station);offsets[newTip]=tipOffset;
            var area=Area(end);int min=Axis==1?area.xMin:area.yMin,max=Axis==1?area.xMax:area.yMax;
            valid=valid&&PropagateOffsets(newTip,min,max)&&LengthFor(end)<=10&&ValidateEdit(player+step,solids,actor,bondedBlue);
            if(valid&&apply){RebuildKeepingMaterials(before);return true;}
            end=oldEnd;offsets.Clear();foreach(var pair in previous)offsets.Add(pair.Key,pair.Value);
            return valid;
        }
        return TryShiftStrip(player,step,solids,actor,apply);
    }

    // Shift a two-colour cross-section. Adjacent strips must still share an edge.
    public bool TryShiftStrip(Vector2Int player,Vector2Int step,LayerMask solids,Collider actor,bool apply)
    {
        if(Axis==0 || platformMode || (Axis==1 ? step.x!=0 : step.y!=0))return false;
        Vector2Int chosen=default;int nearest=int.MaxValue;
        foreach(var cell in Footprint(end,true))
        {int d=Mathf.Abs(cell.x-player.x)+Mathf.Abs(cell.y-player.y);if(d<nearest){nearest=d;chosen=cell;}}
        if(Contains(player+step,end)){chosen=player+step;nearest=1;}
        if(nearest>1&&TouchesBondedBlue(player+step))
        {
            foreach(var cell in new List<Vector2Int>(Footprint(end,true)))
                if((Axis==1?cell.x==player.x+step.x:cell.y==player.y+step.y)&&ColorAt(cell)==TileColor.Blue){chosen=cell;nearest=1;break;}
        }
        if(nearest>1)return false;
        var original=originalCells[chosen];int station=Axis==1?original.x:original.y;
        // The original two-cell base is permanently anchored.
        if(IsBaseStation(station))return false;
        bool bondedBlue=HasBondedBlueRibbon;
        int old=offsets.TryGetValue(station,out int v)?v:0;
        int next=old+(Axis==1?step.y:step.x);
        var area=Area(end);int min=Axis==1?area.xMin:area.yMin;int max=Axis==1?area.xMax:area.yMax;
        var previousOffsets=new Dictionary<int,int>(offsets);
        offsets[station]=next;
        bool valid=true;
        if(Mathf.Abs(next)<Mathf.Abs(old))
        {
            var crest=new Queue<int>();crest.Enqueue(station);var visited=new HashSet<int>{station};
            while(crest.Count>0&&valid)
            {
                int current=crest.Dequeue();
                foreach(int neighbour in new[]{current-1,current+1})
                {
                    if(neighbour<min||neighbour>=max||!visited.Add(neighbour))continue;
                    int value=OffsetAt(neighbour);if(value!=old)continue;
                    if(IsBaseStation(neighbour)){valid=false;break;}offsets[neighbour]=next;crest.Enqueue(neighbour);
                }
            }
        }
        valid=valid&&PropagateOffsets(station,min,max)&&LengthFor(end)<=10&&ValidateEdit(player+step,solids,actor,bondedBlue);
        var proposedOffsets=new Dictionary<int,int>(offsets);
        offsets.Clear();foreach(var item in previousOffsets)offsets.Add(item.Key,item.Value);
        if(!valid||!apply)return valid;
        var before=Capture();offsets.Clear();foreach(var item in proposedOffsets)offsets.Add(item.Key,item.Value);
        RebuildKeepingMaterials(before);
        return true;
    }

    private bool PropagateOffsets(int station,int min,int max)
    {
        var pending=new Queue<int>();pending.Enqueue(station);
        while(pending.Count>0)
        {
            int current=pending.Dequeue(),value=OffsetAt(current);
            for(int neighbour=current-1;neighbour<=current+1;neighbour+=2)
            {
                if(neighbour<min||neighbour>=max||Mathf.Abs(value-OffsetAt(neighbour))<=1)continue;
                if(IsBaseStation(neighbour))return false;
                offsets[neighbour]=value-System.Math.Sign(value-OffsetAt(neighbour));pending.Enqueue(neighbour);
            }
        }
        return true;
    }

    public bool OverlapsPaper(Vector2Int actorCell) => OverlapsPaperShape(actorCell,false);
    private bool OverlapsPaperShape(Vector2Int actorCell,bool previewBondedBlue)
    {
        var cells=new List<Vector2Int>(Footprint(end,true,previewBondedBlue));
        var area=Area(end);int min=Axis==1?area.xMin:area.yMin,max=Axis==1?area.xMax:area.yMax;
        var bounds=new Bounds(new Vector3(actorCell.x,actorCell.y,0),new Vector3(.62f,.62f,1));
        foreach(var cell in cells)
        {
            var original=originalCells[cell];int station=Axis==1?original.x:original.y;
            int left=BoundaryShift(station,min,max),right=BoundaryShift(station+1,min,max);
            Vector2 a,b,c,d;
            if(Axis==1){a=new Vector2(station-.5f,original.y-.5f+left);b=new Vector2(station+.5f,original.y-.5f+right);c=b+Vector2.up;d=a+Vector2.up;}
            else{a=new Vector2(original.x-.5f+left,station-.5f);b=new Vector2(original.x-.5f+right,station+.5f);c=b+Vector2.right;d=a+Vector2.right;}
            if(TriangleHits(a,b,c,bounds)||TriangleHits(a,c,d,bounds))return true;
        }
        return false;
    }

    private bool ValidateEdit(Vector2Int nextPlayer,LayerMask solids,Collider actor,bool previewBondedBlue=false)
    {
        if(OverlapsPaperShape(nextPlayer,previewBondedBlue)){LastEditFailure="paper overlaps actor";return false;}
        foreach(var cell in Footprint(end,true))
        {
            if(!grid.Contains(cell)){LastEditFailure="edge "+cell;return false;}
            foreach(var hit in Physics.OverlapBox(World(cell),Vector3.one*.44f,Quaternion.identity,solids,QueryTriggerInteraction.Ignore))
                if(hit!=actor&&!(hit is BoxCollider wall&&walls.Contains(wall))&&!blue.Exists(block=>block.gameObject==hit.gameObject)){LastEditFailure=hit.name;return false;}
        }
        foreach(var hit in Physics.OverlapBox(World(nextPlayer),Vector3.one*.35f,Quaternion.identity,solids,QueryTriggerInteraction.Ignore))
            if(hit!=actor&&!(hit is BoxCollider wall&&walls.Contains(wall))&&!blue.Exists(block=>block.gameObject==hit.gameObject))return false;
        return true;
    }

    private bool HasYellowPartner(PrototypeFallingBlock block)
    {
        if(Axis==0||block.CurrentState.Lost)return false;
        var original=blueOrigins[block];var mate=original;
        if(Axis==1)mate.y=original.y==anchor.y?anchor.y+1:anchor.y;
        else mate.x=original.x==anchor.x?anchor.x+1:anchor.x;
        // Yellow exists only in palette C or the directional blue/yellow pair.
        if(paletteMode==2)return true;
        if(paletteMode==3)return false;
        var direction=PullAxis(end);if(alternatePalette)direction=-direction;
        if(Axis==1)return direction.x>0&&mate.y==anchor.y;
        return direction.y<0&&mate.x==anchor.x+1;

    }
    private bool IsBondedBlue(PrototypeFallingBlock block)
    {
        if(!HasYellowPartner(block))return false;
        var original=blueOrigins[block];int station=Axis==1?original.x:original.y;
        return Vector2.Distance(block.CurrentState.Position,original+(Axis==1?Vector2Int.up:Vector2Int.right)*OffsetAt(station))<.01f;
    }
    public IEnumerable<PrototypeFallingBlock> BondedBlue
    {get {foreach(var block in blue)if(IsBondedBlue(block))yield return block;}}
    private bool HasBondedBlueRibbon => blue.Count>0&&blue.TrueForAll(IsBondedBlue);

    private bool AttachedBlueAt(Vector2Int original,Vector2Int position)
    {
        foreach(var block in blue)if(!block.CurrentState.Lost&&blueOrigins[block]==original&&Vector2.Distance(block.CurrentState.Position,position)<.01f)return true;
        return HasBondedBlueRibbon&&Area(end).Contains(original);
    }

    private static bool OnBlueSpan(Vector2Int cell,Vector2 a,Vector2 b)
    {
        int length=Mathf.CeilToInt(Mathf.Max(Mathf.Abs(a.x-b.x),Mathf.Abs(a.y-b.y)));
        for(int i=0;i<=length;i++)if(Vector2Int.RoundToInt(Vector2.Lerp(a,b,length==0?0:i/(float)length))==cell)return true;
        return false;
    }
    private bool TouchesBondedBlue(Vector2Int cell)
    {
        if(!HasBondedBlueRibbon)return false;
        var origin=blueOrigins[blue[0]];bool positive=Axis==1?origin.y>anchor.y:origin.x==anchor.x;
        var side=Axis==1?(positive?Vector2.up:Vector2.down):(positive?Vector2.left:Vector2.right);
        var centers=PaperCenters();var bounds=new Bounds(new Vector3(cell.x,cell.y,0),new Vector3(.62f,.62f,1));
        for(int i=1;i<centers.Count;i++)
        {
            var a=new Vector2(centers[i-1].x,centers[i-1].z);var b=new Vector2(centers[i].x,centers[i].z);
            if(TriangleHits(a,b,b+side,bounds)||TriangleHits(a,b+side,a+side,bounds))return true;
        }
        return false;
    }

    public bool HasBlueCell(Vector2Int cell)
    {
        if(Axis==0)return false;
        if(TouchesBondedBlue(cell))return true;
        var chain=new List<PrototypeFallingBlock>(blue);chain.Sort((a,b)=>(Axis==1?blueOrigins[a].x:blueOrigins[a].y).CompareTo(Axis==1?blueOrigins[b].x:blueOrigins[b].y));
        for(int i=0;i<chain.Count;i++)
        {
            if(chain[i].CurrentState.Lost||!chain[i].CurrentState.Water)continue;
            if(Vector2Int.RoundToInt(chain[i].CurrentState.Position)==cell)return true;
            if(i>0&&!chain[i-1].CurrentState.Lost&&OnBlueSpan(cell,chain[i-1].CurrentState.Position,chain[i].CurrentState.Position))return true;
        }
        return false;
    }

    private bool TryBendDetachedBlue(Vector2Int player,Vector2Int step,LayerMask solids,Collider actor,bool apply,out bool contact)
    {
        contact=false;if(Axis==0)return false;
        var chain=new List<PrototypeFallingBlock>(blue);chain.Sort((a,b)=>(Axis==1?blueOrigins[a].x:blueOrigins[a].y).CompareTo(Axis==1?blueOrigins[b].x:blueOrigins[b].y));
        int chosen=chain.FindIndex(b=>b.CurrentState.Water&&!b.CurrentState.Lost&&Vector2Int.RoundToInt(b.CurrentState.Position)==player+step);
        int bridgeOther=-1;
        if(chosen<0)
            for(int i=1;i<chain.Count;i++)
                if(!chain[i-1].CurrentState.Lost&&!chain[i].CurrentState.Lost&&OnBlueSpan(player+step,chain[i-1].CurrentState.Position,chain[i].CurrentState.Position))
                {chosen=i;bridgeOther=i-1;break;}
        if(chosen<0)return false;
        var origin=blueOrigins[chain[chosen]];int station=Axis==1?origin.x:origin.y;
        if(bridgeOther<0&&AttachedBlueAt(origin,origin+(Axis==1?Vector2Int.up:Vector2Int.right)*OffsetAt(station)))return false;
        contact=true;if(IsBaseStation(station))return false;
        var positions=chain.ConvertAll(b=>Vector2Int.RoundToInt(b.CurrentState.Position));positions[chosen]+=step;
        var pending=new Queue<int>();pending.Enqueue(chosen);
        if(bridgeOther>=0)
        {var key=blueOrigins[chain[bridgeOther]];if(IsBaseStation(Axis==1?key.x:key.y))return false;positions[bridgeOther]+=step;pending.Enqueue(bridgeOther);}
        while(pending.Count>0)
        {
            int index=pending.Dequeue();
            foreach(int neighbour in new[]{index-1,index+1})
            {
                if(neighbour<0||neighbour>=chain.Count||chain[neighbour].CurrentState.Lost)continue;
                var delta=positions[index]-positions[neighbour];
                if(Mathf.Abs(delta.x)<=1&&Mathf.Abs(delta.y)<=1)continue;
                var key=blueOrigins[chain[neighbour]];if(IsBaseStation(Axis==1?key.x:key.y))return false;
                positions[neighbour]+=new Vector2Int(Mathf.Abs(delta.x)>1?System.Math.Sign(delta.x):0,Mathf.Abs(delta.y)>1?System.Math.Sign(delta.y):0);pending.Enqueue(neighbour);
            }
        }
        int length=0;
        for(int i=0;i<chain.Count;i++)
        {
            if(chain[i].CurrentState.Lost)continue;
            var cell=positions[i];if(!grid.Contains(cell)||cell==player+step)return false;
            if(i>0&&!chain[i-1].CurrentState.Lost)length+=Mathf.Abs(cell.x-positions[i-1].x)+Mathf.Abs(cell.y-positions[i-1].y);
            foreach(var hit in Physics.OverlapBox(World(cell),Vector3.one*.44f,Quaternion.identity,solids,QueryTriggerInteraction.Ignore))
                if(hit!=actor&&!chain.Exists(b=>b.gameObject==hit.gameObject))return false;
        }
        if(length>11)return false;
        for(int i=1;i<chain.Count;i++)
            if(!chain[i-1].CurrentState.Lost&&!chain[i].CurrentState.Lost&&OnBlueSpan(player+step,positions[i-1],positions[i]))return false;
        if(!apply)return true;
        for(int i=0;i<chain.Count;i++)if(!chain[i].CurrentState.Lost){var state=chain[i].CurrentState;state.Position=positions[i];state.Velocity=Vector2.zero;chain[i].RestoreWorldPosition(state);chain[i].SetWaterView(false);}
        return true;
    }

    public IEnumerable<KeyValuePair<PrototypeFallingBlock,PrototypeFallingBlock>> BlueLinks
    {
        get
        {
            var chain=new List<PrototypeFallingBlock>(blue);chain.Sort((a,b)=>(Axis==1?blueOrigins[a].x:blueOrigins[a].y).CompareTo(Axis==1?blueOrigins[b].x:blueOrigins[b].y));
            for(int i=1;i<chain.Count;i++)
            {if(!chain[i-1].CurrentState.Lost&&!chain[i].CurrentState.Lost)yield return new KeyValuePair<PrototypeFallingBlock,PrototypeFallingBlock>(chain[i-1],chain[i]);}
        }
    }

    public void AppendBluePipe(List<Vector3> vertices,List<int> triangles)
    {
        if(HasBondedBlueRibbon)
        {
            var original=blueOrigins[blue[0]];bool side=Axis==1?original.y>anchor.y:original.x==anchor.x;
            var mesh=FanhuaPaperMesh.Ribbon(PaperCenters(),side,Axis==1);int first=vertices.Count;
            vertices.AddRange(mesh.vertices);foreach(int index in mesh.triangles)triangles.Add(first+index);Object.Destroy(mesh);return;
        }
        var chain=new List<PrototypeFallingBlock>(blue);chain.Sort((a,b)=>(Axis==1?blueOrigins[a].x:blueOrigins[a].y).CompareTo(Axis==1?blueOrigins[b].x:blueOrigins[b].y));
        var run=new List<Vector3>();bool positive=true;
        void Flush()
        {
            if(run.Count==0)return;
            var centers=new List<Vector3>();var axis=Axis==1?Vector3.right:Vector3.forward;
            float baseline=Axis==1?run[0].z:run[0].x;
            for(int edge=0;edge<=run.Count;edge++)
            {
                var point=edge==run.Count?run[edge-1]+axis*.5f:run[edge]-axis*.5f;
                if(edge>0&&edge<run.Count)
                {
                    float a=Axis==1?run[edge-1].z:run[edge-1].x,b=Axis==1?run[edge].z:run[edge].x;
                    float value=Mathf.Abs(a-baseline)>=Mathf.Abs(b-baseline)?a:b;
                    if(Axis==1)point.z=value;else point.x=value;
                }
                centers.Add(point);
            }
            var mesh=FanhuaPaperMesh.Ribbon(centers,positive,Axis==1);int offset=vertices.Count;vertices.AddRange(mesh.vertices);foreach(int index in mesh.triangles)triangles.Add(offset+index);Object.Destroy(mesh);run.Clear();
        }
        foreach(var block in chain)
        {
            var original=blueOrigins[block];
            if(block.CurrentState.Lost||!block.CurrentState.Water){Flush();continue;}
            positive=Axis==1?original.y>anchor.y:original.x==anchor.x;
            var p=block.CurrentState.Position;
            var center=Axis==1?new Vector3(p.x,.5f,p.y+(positive?-.5f:.5f)):new Vector3(p.x+(positive?.5f:-.5f),.5f,p.y);
            if(run.Count==0||run[run.Count-1]!=center)run.Add(center);
        }
        Flush();
    }

    private void RebuildKeepingMaterials(State before)
    {
        // Fluid sources stay in world space, with stable order and spent volume.
        var cooled=cooledStrips;var shattered=shatteredStrips;
        foreach(var tile in red){if(filled.Contains(tile))cooled.Add(redOriginal[tile]-before.Anchor);if(broken.Contains(tile))shattered.Add(redOriginal[tile]-before.Anchor);}
        var area=Area(end);int min=Axis==1?area.xMin:area.yMin,max=Axis==1?area.xMax:area.yMax;
        foreach(var block in blue)
        {
            if(!block.CurrentState.Water||block.CurrentState.Lost)continue;
            var original=blueOrigins[block];int station=Axis==1?original.x:original.y;
            int oldShift=before.Offsets.TryGetValue(station,out int saved)?saved:0;
            var expected=original+(Axis==1?Vector2Int.up:Vector2Int.right)*oldShift;
            if(Vector2.Distance(block.CurrentState.Position,expected)>.01f)continue;
            int mapped=Mathf.Clamp(station,min,max-1);
            // The active end keeps its blue material when compressed or stretched.
            int oldTip=Axis==1?before.End.x:before.End.y;
            if(station==oldTip)mapped=Axis==1?end.x:end.y;
            if(Axis==1)original.x=mapped;else original.y=mapped;
            var state=block.CurrentState;state.Position=original+(Axis==1?Vector2Int.up:Vector2Int.right)*OffsetAt(mapped);state.Velocity=Vector2.zero;
            blueOrigins[block]=original;block.RestoreWorldPosition(state);
        }
        released=before.Released || blue.Exists(b=>b.CurrentState.Water||b.CurrentState.Lost||b.CurrentState.Gray);
        Build(released);
        foreach(var tile in red){var key=redOriginal[tile]-anchor;if(cooled.Contains(key))Fill(tile);if(shattered.Contains(key))Break(tile);}
        SetMode(false);
    }

    public bool IsAtPullEdge(Vector2Int player, Vector2Int step)
    {
        if (Axis!=0 && PullAxis(end) != step)
            return false;
        return !Contains(player, end) && Contains(player - step, end);
    }

    public Vector2Int EndFor(Vector2Int player, Vector2Int step)
    {
        return step.x == 0 ? new Vector2Int(anchor.x, player.y) : new Vector2Int(player.x, anchor.y);
    }

    public void TogglePalette()
    {
        if(Axis!=0)return;
        paletteMode=(paletteMode+1)%4;
        alternatePalette=paletteMode==1;
        Build();
        SetMode(false);
    }

    public void SetEnd(Vector2Int target)
    {
        if(expansionDirection==Vector2Int.zero&&target!=anchor)expansionDirection=new Vector2Int(System.Math.Sign(target.x-anchor.x),System.Math.Sign(target.y-anchor.y));
        var before=Capture();
        var area=Area(target);bool horizontal=Axis==1;
        int min=horizontal?area.xMin:area.yMin,max=horizontal?area.xMax:area.yMax;
        var newOffsets=new Dictionary<int,int>();
        for(int station=min;station<max;station++)newOffsets[station]=ExtensionOffset(station);
        offsets.Clear();foreach(var pair in newOffsets)offsets.Add(pair.Key,pair.Value);
        end = target;
        RebuildKeepingMaterials(before);
    }

    private Vector2Int PullAxis(Vector2Int target)
    {
        if(expansionDirection!=Vector2Int.zero)return expansionDirection;
        Vector2Int delta = target - anchor;
        return new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
    }

    private RectInt Area(Vector2Int target)
    {
        Vector2Int min = Vector2Int.Min(anchor, target);
        Vector2Int max = Vector2Int.Max(anchor + Vector2Int.one, target);
        return new RectInt(min, max - min + Vector2Int.one);
    }

    private bool Contains(Vector2Int cell, Vector2Int target) {foreach(var candidate in Footprint(target,true))if(candidate==cell)return true;return false;}

    private IEnumerable<Vector2Int> Footprint(Vector2Int target,bool materialOnly=false,bool previewBondedBlue=false)
    {
        originalCells.Clear();
        foreach (Vector2Int cell in Area(target).allPositionsWithin)
        {
            bool horizontal=Axis==0?target.y==anchor.y:Axis==1;
            int station=horizontal?cell.x:cell.y;
            int shift=ExtensionOffset(station);
            var mapped=cell+(horizontal?Vector2Int.up:Vector2Int.right)*shift;
            originalCells[mapped]=cell;
            if(materialOnly && Axis!=0)
            {
                var color=ColorAt(mapped);var key=cell-anchor;
                if(((released||blue.Exists(b=>b.CurrentState.Water||b.CurrentState.Lost||b.CurrentState.Gray))&&color==TileColor.Blue&&!previewBondedBlue&&!AttachedBlueAt(cell,mapped))||(color==TileColor.Red&&(cooledStrips.Contains(key)||shatteredStrips.Contains(key))))continue;
            }
            yield return mapped;
        }
    }

    private enum TileColor { Red, Yellow, Blue }

    private TileColor ColorAt(Vector2Int cell)
    {
        Vector2Int direction = PullAxis(end);
        if (alternatePalette)
            direction = -direction;

        if(originalCells.TryGetValue(cell,out var original))cell=original;
        bool upper = cell.y > anchor.y;
        bool right = cell.x > anchor.x;
        if(paletteMode>=2)
        {
            bool first=Axis==1?upper:!right;
            return paletteMode==2 ? (first?TileColor.Red:TileColor.Yellow) : (first?TileColor.Blue:TileColor.Red);
        }
        if (direction == Vector2Int.right)
            return upper ? TileColor.Blue : TileColor.Yellow;
        if (direction == Vector2Int.left)
            return upper ? TileColor.Red : TileColor.Blue;
        if (direction == Vector2Int.up)
            return right ? TileColor.Blue : TileColor.Red;
        return right ? TileColor.Yellow : TileColor.Blue;
    }

    private static Vector3 World(Vector2Int cell) => new Vector3(cell.x, 0.5f, cell.y);

    private GameObject Tile(Vector2Int cell, Color color, float mark = 0f)
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if(cubeMesh==null)cubeMesh=tile.GetComponent<MeshFilter>().sharedMesh;
        tile.transform.SetParent(world, false);
        tile.transform.position = World(cell);
        var renderer = tile.GetComponent<Renderer>();
        if (material != null)
            renderer.sharedMaterial = material;
        Tint(renderer, color, mark);
        objects.Add(tile);
        return tile;
    }

    private static void Tint(Renderer renderer, Color color, float mark)
    {
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_Color", color);
        properties.SetColor("_BaseColor", color);
        properties.SetFloat("_Mark", mark);
        renderer.SetPropertyBlock(properties);
    }

    private void CreateBlue(Vector2Int cell)
    {
        var block=Tile(cell,new Color(.24f,.49f,.81f),1f).AddComponent<PrototypeFallingBlock>();
        block.CreateFootprint(platform,canHangInGrid:true);block.HideSolidInGrid=true;block.EnterableBlue=WaterRules;block.BelongsToFlower=true;blue.Add(block);blueOrigins[block]=originalCells.TryGetValue(cell,out var original)?original:cell;
    }

    private void Build(bool keepFluid=false)
    {
        if(!keepFluid) foreach (PrototypeFallingBlock block in blue)
            block.DestroyFootprint();
        foreach(var mesh in ribbonMeshes)Object.Destroy(mesh);ribbonMeshes.Clear();ribbonViews.Clear();ribbonColors.Clear();yellowWaterCells.Clear();yellowShapes.Clear();yellowRibbonProjections.Clear();
        foreach(var mesh in spikeMeshes.Values)Object.Destroy(mesh);
        spikeMeshes.Clear();
        foreach (GameObject obj in objects)
        {
            if(keepFluid && blue.Exists(b=>b.gameObject==obj))continue;
            obj.SetActive(false);
            Object.Destroy(obj);
        }
        objects.Clear();
        if(keepFluid)foreach(var source in blue)objects.Add(source.gameObject);
        walls.Clear();
        blueWalls.Clear();
        if(!keepFluid){blue.Clear();blueOrigins.Clear();}
        red.Clear();
        redProjections.Clear();
        filled.Clear();
        broken.Clear();redOriginal.Clear();redGridCells.Clear();pleats.Clear();
        var buildCells=new List<Vector2Int>(Footprint(end));
        foreach (Vector2Int cell in buildCells)
        {
            bool folded = Axis==0;
            GameObject wall = Tile(cell, folded && alternatePalette ? new Color(0.63f, 0.50f, 0.67f) : new Color(0.77f, 0.67f, 0.78f));
            if(!folded)
            {
                if(paperMesh==null)paperMesh=FanhuaPaperMesh.Pleats();
                wall.GetComponent<MeshFilter>().sharedMesh=paperMesh;
            }
            wall.name = folded ? "Fanhua · folded 2×2" : "Fanhua · barrier";
            wall.transform.localScale = new Vector3(0.96f, folded ? 1f : 0.8f, 0.96f);
            var wallCollider = wall.GetComponent<BoxCollider>();
            walls.Add(wallCollider);
            if (folded)
                continue;

            TileColor color = ColorAt(cell);
            if (color == TileColor.Blue)
            {
                if(!released) CreateBlue(cell);
                blueWalls.Add(wallCollider);
                continue;
            }

            var tile = Tile(cell, color == TileColor.Red ? new Color(0.79f, 0.27f, 0.33f)
                : new Color(0.98f, 0.79f, 0.30f), color == TileColor.Red ? 2f : 3f);
            tile.GetComponent<BoxCollider>().enabled = false;
            if (color == TileColor.Red)
            {
                red.Add(tile);redOriginal[tile]=originalCells[cell];redGridCells[tile]=cell;
                // Palette C's partner belongs to this same cross-section.
                // Do not attach to a neighbouring strip when an elbow brings them together.
                Vector2 outward=Vector2.up;
                if(paletteMode==2)
                {
                    int station=Axis==1?cell.x:cell.y;
                    var area=Area(end);int min=Axis==1?area.xMin:area.yMin,max=Axis==1?area.xMax:area.yMax;
                    int here=BoundaryShift(station,min,max),next=BoundaryShift(station+1,min,max);
                    outward=Axis==1?Vector2.up:Vector2.left;
                    Vector2 seam=Axis==1?new Vector2(station,anchor.y+.5f+(here+next)*.5f):new Vector2(anchor.x+.5f+(here+next)*.5f,station);
                    Vector2 center=seam+outward*.5f;tile.transform.position=new Vector3(center.x,.5f,center.y);
                    Vector2 edge=Axis==1?new Vector2(1,next-here):new Vector2(next-here,1);
                    spikeMeshes[tile]=FanhuaPaperMesh.Spikes(outward,edge);
                }
                if(!spikeMeshes.ContainsKey(tile))spikeMeshes[tile]=FanhuaPaperMesh.Spikes(outward);
                tile.GetComponent<MeshFilter>().sharedMesh=spikeMeshes[tile];
                if(WaterRules)
                {
                    var redProxy = new GameObject("Unit02 red footprint");
                    redProxy.transform.SetParent(platform,false); redProxy.transform.position=(Vector2)cell;
                    var redShape=redProxy.AddComponent<BoxCollider2D>(); redShape.size=Vector2.one; redShape.isTrigger=true;
                    redProjections[tile]=redShape; objects.Add(redProxy);
                }
                continue;
            }
            var proxy = new GameObject("Yellow footprint");
            proxy.transform.SetParent(platform, false);
            proxy.transform.position = (Vector2)cell;
            // Leave side clearance so neighboring blue blocks can fall past the yellow column.
            // Keep the platform's full height for blue blocks and players landing on top.
            var shape = proxy.AddComponent<BoxCollider2D>();
            shape.size = new Vector2(0.94f, 1f);yellowShapes.Add(shape);
            objects.Add(proxy);
        }
    }

    private bool IsBaseStation(int station)
    {
        int first=Axis==1?anchor.x:anchor.y;
        return station==first||station==first+1;
    }
    private int ExtensionOffset(int station)
    {
        var area=Area(end);bool horizontal=Axis==0?end.y==anchor.y:Axis==1;
        int min=horizontal?area.xMin:area.yMin,max=horizontal?area.xMax:area.yMax;
        return OffsetAt(Mathf.Clamp(station,min,max-1));
    }
    private int OffsetAt(int station) => offsets.TryGetValue(station,out int value)?value:0;
    private int BoundaryShift(int boundary,int min,int max)
    {
        if(boundary<=min)return OffsetAt(min);
        if(boundary>=max)return OffsetAt(max-1);
        int a=OffsetAt(boundary-1),b=OffsetAt(boundary);
        // A downward fold must move both edges, just as an upward fold does.
        return Mathf.Abs(a)>=Mathf.Abs(b)?a:b;
    }

    private List<Vector3> PaperCenters()
    {
        var area=Area(end);var centers=new List<Vector3>();
        int min=Axis==1?area.xMin:area.yMin,max=Axis==1?area.xMax:area.yMax;
        for(int boundary=min;boundary<=max;boundary++)
        {int shift=BoundaryShift(boundary,min,max);
         centers.Add(Axis==1?new Vector3(boundary-.5f,.5f,anchor.y+.5f+shift):new Vector3(anchor.x+.5f+shift,.5f,boundary-.5f));}
        return centers;
    }

    private void BuildRibbonView()
    {
        if(Axis==0)return;
        var area=Area(end);var centers=PaperCenters();
        int min=Axis==1?area.xMin:area.yMin;
        for(int band=0;band<2;band++)
        {
            var obj=new GameObject("Continuous paper band",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(world,false);
            var mesh=FanhuaPaperMesh.Ribbon(centers,band==0);obj.GetComponent<MeshFilter>().sharedMesh=mesh;ribbonMeshes.Add(mesh);
            obj.GetComponent<Renderer>().sharedMaterial=material;
            var sample=Axis==1?new Vector2Int(min,anchor.y+1-band):new Vector2Int(anchor.x+band,min);
            int shift=offsets.TryGetValue(min,out int v)?v:0;sample+= (Axis==1?Vector2Int.up:Vector2Int.right)*shift;
            var color=ColorAt(sample);Tint(obj.GetComponent<Renderer>(),color==TileColor.Blue?new Color(.24f,.49f,.81f):color==TileColor.Yellow?new Color(.98f,.79f,.3f):new Color(.79f,.27f,.33f),0);
            objects.Add(obj);ribbonViews.Add(obj);ribbonColors[obj]=color;
            if(color==TileColor.Yellow)
            {
                var proxy=new GameObject("Continuous yellow silhouette");proxy.transform.SetParent(platform,false);
                var polygon=proxy.AddComponent<PolygonCollider2D>();
                var vertices=mesh.vertices;int rings=vertices.Length/2/9;
                var outline=new List<Vector2>();
                for(int ring=0;ring<rings;ring++){var point=vertices[ring*9+4];outline.Add(new Vector2(point.x,point.z));}
                for(int ring=rings-1;ring>=0;ring--){var point=(vertices[ring*9]+vertices[ring*9+8])*.5f;outline.Add(new Vector2(point.x,point.z));}
                polygon.points=outline.ToArray();objects.Add(proxy);yellowRibbonProjections.Add(proxy);
                for(int strip=0;strip<centers.Count-1;strip++)
                {
                    int a=strip*24*9,b=(strip+1)*24*9;
                    var quad=new List<Vector2>{new Vector2(vertices[a+4].x,vertices[a+4].z),new Vector2(vertices[b+4].x,vertices[b+4].z),
                        new Vector2(vertices[b].x,vertices[b].z),new Vector2(vertices[a].x,vertices[a].z)};
                    float xMin=float.PositiveInfinity,xMax=float.NegativeInfinity,yMin=xMin,yMax=xMax;
                    foreach(var vtx in quad){xMin=Mathf.Min(xMin,vtx.x);xMax=Mathf.Max(xMax,vtx.x);yMin=Mathf.Min(yMin,vtx.y);yMax=Mathf.Max(yMax,vtx.y);}
                    for(int x=Mathf.FloorToInt(xMin+.5f);x<=Mathf.FloorToInt(xMax+.5f);x++)
                    for(int y=Mathf.FloorToInt(yMin+.5f);y<=Mathf.FloorToInt(yMax+.5f);y++)
                    {var cell=new Vector2Int(x,y);if(OverlapArea(quad,cell)>.0001f)yellowWaterCells.Add(cell);}
                }
            }
        }
    }

    private static float OverlapArea(List<Vector2> polygon,Vector2Int cell)
    {
        var result=new List<Vector2>(polygon);
        foreach(var plane in new[]{new Vector3(1,0,cell.x-.5f),new Vector3(-1,0,-cell.x-.5f),new Vector3(0,1,cell.y-.5f),new Vector3(0,-1,-cell.y-.5f)})
        {
            var input=result;result=new List<Vector2>();if(input.Count==0)return 0;
            var previous=input[input.Count-1];float pd=previous.x*plane.x+previous.y*plane.y-plane.z;
            foreach(var point in input)
            {float d=point.x*plane.x+point.y*plane.y-plane.z;
             if((d>=0)!=(pd>=0))result.Add(Vector2.Lerp(previous,point,pd/(pd-d)));
             if(d>=0)result.Add(point);previous=point;pd=d;}
        }
        float area=0;for(int i=0;i<result.Count;i++){var a=result[i];var b=result[(i+1)%result.Count];area+=a.x*b.y-a.y*b.x;}
        return Mathf.Abs(area)*.5f;
    }

    public void SetMode(bool platformMode)
    {
        this.platformMode=platformMode;
        if(Axis!=0&&ribbonViews.Count==0)BuildRibbonView();
        foreach(var view in ribbonViews)
        {
            var color=ribbonColors[view];
            bool visible=color==TileColor.Yellow || (!platformMode && (color==TileColor.Blue ? !blue.Exists(b=>b.CurrentState.Water||b.CurrentState.Lost) : filled.Count==0&&broken.Count==0));
            view.SetActive(visible);
        }
        foreach(var projection in yellowRibbonProjections)projection.SetActive(platformMode);
        foreach(var shape in yellowShapes)shape.enabled=yellowRibbonProjections.Count==0;
        if(Axis!=0)
            foreach(var obj in objects)
            {
                var mesh=obj.GetComponent<MeshFilter>();
                if(mesh==null||ribbonViews.Contains(obj)||walls.Contains(obj.GetComponent<BoxCollider>()))continue;
                obj.GetComponent<Renderer>().enabled=platformMode;
                mesh.sharedMesh=platformMode ? (spikeMeshes.TryGetValue(obj,out var spikes)?spikes:cubeMesh) : paperMesh;
            }
        foreach(var p in pleats)p.SetActive(!platformMode);
        foreach (BoxCollider wall in walls)
        {
            bool consumed=false;
            foreach(var tile in red)
                if((filled.Contains(tile)||broken.Contains(tile))&&World(redGridCells[tile])==wall.transform.position){consumed=true;break;}
            bool showWall = !platformMode && !blueWalls.Contains(wall) && !consumed;
            wall.enabled = showWall;
            wall.GetComponent<Renderer>().enabled = showWall && Axis==0;
        }
        foreach (GameObject tile in red)
            tile.GetComponent<Renderer>().enabled = !filled.Contains(tile) && !broken.Contains(tile);

        // The 3D renderer is shared by both cameras. grid blue tiles are pushable.
        foreach (PrototypeFallingBlock block in blue)
        {
            block.GetComponent<BoxCollider>().enabled = !platformMode && (!block.CurrentState.Water||block.EnterableBlue) && !block.CurrentState.Plugged && !block.CurrentState.Lost;
            if (platformMode)
                block.ResumePhysics();
            else
                block.StopPhysics();
        }
        if(Axis!=0)
            foreach(var obj in objects)
            {
                if(obj.GetComponent<MeshFilter>()==null||ribbonViews.Contains(obj))continue;
                if(!platformMode || (yellowRibbonProjections.Count>0 && !red.Contains(obj) && !blue.Exists(b=>b.gameObject==obj)))obj.GetComponent<Renderer>().enabled=false;
            }
    }

    public static bool TryPlaceFallingBlocks(PrototypeFallingBlock[] sceneBlocks, PrototypeFanhua[] flowers,
        LayerMask solids, RectInt grid, Vector2Int playerCell)
    {
        var blocks = new List<PrototypeFallingBlock>();
        foreach (PrototypeFanhua flower in flowers)
            blocks.AddRange(flower.blue);
        blocks.AddRange(sceneBlocks);
        blocks.RemoveAll(block => block.CurrentState.Lost);
        blocks.Sort((left, right) => left.CurrentState.Position.y.CompareTo(right.CurrentState.Position.y));

        var occupied = new HashSet<Vector2Int> { playerCell };
        foreach (PrototypeFallingBlock block in blocks)
        {
            if (block.CurrentState.Plugged)
                occupied.Add(Vector2Int.RoundToInt(block.CurrentState.Position));
            else
                block.GetComponent<BoxCollider>().enabled = false;
        }
        Physics.SyncTransforms();

        foreach (PrototypeFallingBlock block in blocks)
        {
            if (block.CurrentState.Plugged)
                continue;

            var state = block.CurrentState;
            var shape = block.GetComponent<BoxCollider>();
            Vector2Int nearest = Vector2Int.RoundToInt(state.Position);
            Vector3 centerOffset = block.transform.TransformVector(shape.center) +
                Vector3.up * block.transform.position.y;
            Vector3 halfSize = Vector3.Scale(shape.size, shape.transform.lossyScale) * 0.5f -
                Vector3.one * 0.01f;

            // Only scene blocks need floor support when returning to the grid.
            Vector3 nearestCenter = new Vector3(nearest.x, 0f, nearest.y) + centerOffset;
            if (!grid.Contains(nearest) || state.Position.y < grid.yMin - 0.5f ||
                (!block.CanHangInGrid && !PrototypePushBox.HasFloorSupport(nearestCenter, halfSize, solids)))
            {
                block.Discard();
                continue;
            }

            // Settle stacked blocks from the bottom up, keeping their 2D column when possible.
            int bestDistance = int.MaxValue;
            Vector2Int destination = nearest;
            foreach (Vector2Int cell in grid.allPositionsWithin)
            {
                int distance = Mathf.Abs(cell.x - nearest.x) * grid.height + Mathf.Abs(cell.y - nearest.y);
                if (distance >= bestDistance || occupied.Contains(cell))
                    continue;

                Vector3 center = new Vector3(cell.x, 0f, cell.y) + centerOffset;
                if (Physics.CheckBox(center, halfSize, Quaternion.identity, solids, QueryTriggerInteraction.Ignore) ||
                    (!block.CanHangInGrid && !PrototypePushBox.HasFloorSupport(center, halfSize, solids)))
                    continue;

                bestDistance = distance;
                destination = cell;
            }
            if (bestDistance == int.MaxValue)
            {
                // The caller restores the switch snapshot if the grid is full.
                foreach (PrototypeFallingBlock sceneBlock in sceneBlocks)
                    sceneBlock.GetComponent<BoxCollider>().enabled = !sceneBlock.CurrentState.Lost;
                return false;
            }

            state.Position = destination;
            block.RestoreWorldPosition(state);
            shape.enabled = true;
            occupied.Add(destination);
        }
        Physics.SyncTransforms();
        return true;
    }

    private void Fill(GameObject tile)
    {
        filled.Add(tile);
        tile.GetComponent<Renderer>().enabled = false;
        if (WaterRules)
        {
            Tint(tile.GetComponent<Renderer>(), Color.gray, 0f);
            if(redProjections.TryGetValue(tile,out var projection)) projection.isTrigger=true;
        }
    }

    public static void SyncFallingBlock(PrototypeFallingBlock block, PrototypeFanhua[] flowers, float bottomEdge)
    {
        if (block.CurrentState.Lost || block.CurrentState.Plugged)
            return;

        Vector2 before = block.CurrentState.Position;
        block.SyncFromPhysics();
        Vector2 after = block.CurrentState.Position;
        if (after.y >= before.y)
        {
            block.DiscardBelow(bottomEdge);
            return;
        }

        if(block.CurrentState.Gray){block.DiscardBelow(bottomEdge);return;}
        PrototypeFanhua owner = null;
        GameObject hit = null;
        foreach (PrototypeFanhua flower in flowers)
        foreach (GameObject tile in flower.red)
        {
            Vector3 target = tile.transform.position;
            if (!flower.filled.Contains(tile) && !flower.broken.Contains(tile) && Mathf.Abs(target.x - after.x) < tile.transform.lossyScale.x * 0.5f &&
                before.y > target.z && after.y <= target.z &&
                (hit == null || target.z > hit.transform.position.z))
            {
                owner = flower;
                hit = tile;
            }
        }
        if (hit != null)
        {
            owner.Fill(hit);
            block.Plug(new Vector2(hit.transform.position.x, hit.transform.position.z));
            if (owner.WaterRules) Tint(block.GetComponent<Renderer>(), Color.gray, 0f);
        }
        else
            block.DiscardBelow(bottomEdge);
    }

    public bool IsDangerous(CapsuleCollider2D player, ContactPoint2D[] contacts)
    {
        Bounds bounds = player.bounds;
        foreach (GameObject tile in red)
        {
            Vector3 p = tile.transform.position;
            Vector3 size = tile.transform.lossyScale;
            if(filled.Contains(tile)||broken.Contains(tile))continue;
            var vertices=spikeMeshes[tile].vertices;
            for(int i=0;i<vertices.Length/2;i+=3)
            {
                Vector3 a=tile.transform.TransformPoint(vertices[i]),b=tile.transform.TransformPoint(vertices[i+1]),c=tile.transform.TransformPoint(vertices[i+2]);
                if(TriangleHits(new Vector2(a.x,a.z),new Vector2(b.x,b.z),new Vector2(c.x,c.z),bounds))return true;
            }
        }
        int count = player.GetContacts(contacts);
        for (int i = 0; i < count; i++)
            if (contacts[i].normal.y < -0.5f)
                foreach (PrototypeFallingBlock block in blue)
                    if (block.FellOnto(contacts[i]))
                        return true;
        return false;
    }

    private void Break(GameObject tile)
    {
        broken.Add(tile);tile.GetComponent<Renderer>().enabled=false;
        if(redProjections.TryGetValue(tile,out var shape))shape.enabled=false;
    }

    private static bool TriangleHits(Vector2 a,Vector2 b,Vector2 c,Bounds bounds)
    {
        var center=(Vector2)bounds.center;var extents=(Vector2)bounds.extents;
        var axes=new[]{Vector2.right,Vector2.up,new Vector2(a.y-b.y,b.x-a.x),new Vector2(b.y-c.y,c.x-b.x),new Vector2(c.y-a.y,a.x-c.x)};
        foreach(var axis in axes)
        {
            float x=Vector2.Dot(a,axis),y=Vector2.Dot(b,axis),z=Vector2.Dot(c,axis),middle=Vector2.Dot(center,axis);
            float radius=Mathf.Abs(axis.x)*extents.x+Mathf.Abs(axis.y)*extents.y;
            if(Mathf.Max(x,Mathf.Max(y,z))<=middle-radius||Mathf.Min(x,Mathf.Min(y,z))>=middle+radius)return false;
        }
        return true;
    }

    public void ResolveUnsupportedRed()
    {
        if(!platformMode)return;
        foreach(var tile in red)
        {
            if(filled.Contains(tile)||broken.Contains(tile))continue;
            var p=tile.transform.position;
            bool yellow=false;
            foreach(var cell in YellowCells)if(Mathf.Abs(cell.x-p.x)+Mathf.Abs(cell.y-p.z)==1){yellow=true;break;}
            if(yellow)continue;
            // Red above a same-flower water strip loses its support once it flows away.
            foreach(var block in blue)
                if(block.CurrentState.Water && Mathf.Abs(block.CurrentState.Position.x-p.x)<.1f && block.CurrentState.Position.y<p.z)
                {Break(tile);break;}
        }
    }

    public void RecordBeforePhysicsStep()
    {
        foreach (PrototypeFallingBlock block in blue)
            block.RecordBeforePhysicsStep();
    }

    public void Dispose()
    {
        foreach(var mesh in ribbonMeshes)Object.Destroy(mesh);ribbonMeshes.Clear();
        foreach(var block in blue)if(block!=null)block.DestroyFootprint();
        foreach(var mesh in spikeMeshes.Values)Object.Destroy(mesh);
        spikeMeshes.Clear();
        foreach(var obj in objects)if(obj!=null)Object.Destroy(obj);
        blue.Clear();red.Clear();objects.Clear();
    }

    public void StopPhysics()
    {
        foreach (PrototypeFallingBlock block in blue)
            block.StopPhysics();
    }
}
