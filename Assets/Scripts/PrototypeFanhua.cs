using System.Collections.Generic;
using UnityEngine;

// The test grid is an area, including cells without floor support.
internal sealed class PrototypeFanhua
{
    internal sealed class State
    {
        public Vector2Int End;
        public bool AlternatePalette;
        public int PaletteMode;
        public PrototypeFallingBlock.State[] Blue;
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
    private readonly List<GameObject> red = new List<GameObject>();
    private readonly Dictionary<GameObject,BoxCollider2D> redProjections = new Dictionary<GameObject,BoxCollider2D>();
    private readonly HashSet<GameObject> filled = new HashSet<GameObject>();
    private readonly Dictionary<int,int> offsets = new Dictionary<int,int>();
    private readonly Dictionary<Vector2Int,Vector2Int> originalCells = new Dictionary<Vector2Int,Vector2Int>();
    private readonly Dictionary<GameObject, Vector2Int> redOriginal = new Dictionary<GameObject,Vector2Int>();
    private readonly HashSet<GameObject> broken = new HashSet<GameObject>();
    private static Mesh paperMesh;
    private static Mesh cubeMesh;
    private readonly Dictionary<GameObject,Mesh> spikeMeshes = new Dictionary<GameObject,Mesh>();
    private readonly List<GameObject> pleats = new List<GameObject>();
    private bool platformMode;
    private Vector2Int end;
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
            End = end,
            AlternatePalette = alternatePalette,
            PaletteMode = paletteMode,
            Blue = blue.ConvertAll(block => block.CurrentState).ToArray(),
            Filled = red.ConvertAll(tile => filled.Contains(tile)).ToArray(),
            Offsets = new Dictionary<int,int>(offsets),
            Broken = red.ConvertAll(tile => broken.Contains(tile)).ToArray()
        };
    }

    public IEnumerable<Vector2Int> YellowCells
    {
        get { if (Axis != 0) foreach (var cell in Footprint(end))
            if (ColorAt(cell) == TileColor.Yellow) yield return cell; }
    }
    public IEnumerable<Vector2Int> FireCells
    {
        get { foreach(var tile in red) if(!broken.Contains(tile))yield return new Vector2Int(Mathf.RoundToInt(tile.transform.position.x),Mathf.RoundToInt(tile.transform.position.z)); }
    }
    public void CoolCell(Vector2Int cell)
    {
        foreach(var tile in red)
            if(Mathf.RoundToInt(tile.transform.position.x)==cell.x && Mathf.RoundToInt(tile.transform.position.z)==cell.y && !filled.Contains(tile)) Fill(tile);
    }
    public IEnumerable<Vector2Int> BrokenFireCells {get {foreach(var tile in broken)yield return Vector2Int.RoundToInt(new Vector2(tile.transform.position.x,tile.transform.position.z));}}
    public bool WaterRules => world.GetComponentInChildren<PrototypeWaterLevel>() != null;
    public RectInt Grid => grid;
    public IReadOnlyList<PrototypeFallingBlock> BlueBlocks => blue;
    public int Axis => end == anchor ? 0 : end.y == anchor.y ? 1 : 2;

    public bool PressesPlate(Vector2Int cell) =>
        Axis != 0 && Contains(cell, end) && ColorAt(cell) == TileColor.Yellow;

    public Vector2Int AnchorCell => anchor;
    public Vector2Int EndCell => end;
    public int Extension => LengthFor(end);
    private int LengthFor(Vector2Int target)
    {
        if(target==anchor)return 0;
        var area=Area(target);bool horizontal=target.y==anchor.y;
        int min=horizontal?area.xMin:area.yMin,max=horizontal?area.xMax:area.yMax;
        int length=Mathf.Max(0,(horizontal?area.width:area.height)-2);
        for(int n=min+1;n<max;n++)
        {int a=offsets.TryGetValue(n-1,out int av)?av:0,b=offsets.TryGetValue(n,out int bv)?bv:0;length+=Mathf.Abs(a-b);}
        return length;
    }
    public string PaletteHint => new[]{"A · directional","B · reversed","C · red/yellow","D · blue above red"}[paletteMode];

    public bool IsInitial => end == anchor && paletteMode==0;

    public void Restore(State state)
    {
        end = state.End;
        alternatePalette = state.AlternatePalette;
        paletteMode = state.PaletteMode;
        offsets.Clear();
        if(state.Offsets!=null)foreach(var pair in state.Offsets)offsets.Add(pair.Key,pair.Value);
        Build();
        for (int i = 0; i < blue.Count; i++)
            blue[i].RestoreWorldPosition(state.Blue[i]);
        for (int i = 0; i < red.Count; i++)
            if (state.Filled[i])
                Fill(red[i]);
        for(int i=0;i<red.Count;i++)if(state.Broken!=null && state.Broken[i])Break(red[i]);
    }

    public bool CanSetEnd(Vector2Int target, LayerMask solids, Collider pullingPlayer)
    {
        if(filled.Count>0||broken.Count>0||blue.Exists(b=>b.CurrentState.Water||b.CurrentState.Gray||b.CurrentState.Lost))return false;
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

        foreach (Vector2Int cell in Footprint(target))
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
        foreach(var tile in Footprint(end))distance=Mathf.Min(distance,Mathf.Abs(cell.x-tile.x)+Mathf.Abs(cell.y-tile.y));
        return distance;
    }

    // Shift a two-colour cross-section. Adjacent strips must still share an edge.
    public bool TryShiftStrip(Vector2Int player,Vector2Int step,LayerMask solids,Collider actor,bool apply)
    {
        if(filled.Count>0||broken.Count>0||blue.Exists(b=>b.CurrentState.Water||b.CurrentState.Gray||b.CurrentState.Lost))return false;
        if(Axis==0 || platformMode || (Axis==1 ? step.x!=0 : step.y!=0))return false;
        Vector2Int chosen=default;int nearest=int.MaxValue;
        foreach(var cell in Footprint(end))
        {int d=Mathf.Abs(cell.x-player.x)+Mathf.Abs(cell.y-player.y);if(d<nearest){nearest=d;chosen=cell;}}
        if(nearest>1)return false;
        var original=originalCells[chosen];int station=Axis==1?original.x:original.y;
        // The base is fixed. Every other strip keeps its material identity.
        if(station==(Axis==1?anchor.x:anchor.y))return false;
        int old=offsets.TryGetValue(station,out int v)?v:0;
        int next=old+(Axis==1?step.y:step.x);
        var area=Area(end);int min=Axis==1?area.xMin:area.yMin;int max=Axis==1?area.xMax:area.yMax;
        for(int n=station-1;n<=station+1;n+=2)
            if(n>=min&&n<max&&Mathf.Abs(next-(offsets.TryGetValue(n,out int other)?other:0))>1)return false;
        offsets[station]=next;
        bool valid=LengthFor(end)<=10;
        foreach(var cell in Footprint(end))
        {
            if(!grid.Contains(cell)||cell==player+step){valid=false;break;}
            foreach(var hit in Physics.OverlapBox(World(cell),Vector3.one*.44f,Quaternion.identity,solids,QueryTriggerInteraction.Ignore))
                if(hit!=actor&&!(hit is BoxCollider wall&&walls.Contains(wall))&&!blue.Exists(block=>block.gameObject==hit.gameObject)){valid=false;break;}
        }
        foreach(var hit in Physics.OverlapBox(World(player+step),Vector3.one*.35f,Quaternion.identity,solids,QueryTriggerInteraction.Ignore))
            if(hit!=actor&&!(hit is BoxCollider wall&&walls.Contains(wall))&&!blue.Exists(block=>block.gameObject==hit.gameObject))valid=false;
        offsets[station]=old;
        if(!valid||!apply)return valid;
        var before=Capture(); offsets[station]=next;
        RebuildKeepingMaterials(before);
        return true;
    }

    private void RebuildKeepingMaterials(State before)
    {
        // Strip edits preserve blue identities; no edit is allowed after water starts.
        Build();
        for(int i=0;i<blue.Count&&i<before.Blue.Length;i++)
        {var state=before.Blue[i];state.Position=blue[i].CurrentState.Position;blue[i].RestoreWorldPosition(state);}
        for(int i=0;i<red.Count&&i<before.Filled.Length;i++)
        {if(before.Filled[i])Fill(red[i]);if(before.Broken!=null&&before.Broken[i])Break(red[i]);}
        SetMode(false);
    }

    public bool IsAtPullEdge(Vector2Int player, Vector2Int step)
    {
        if (end != anchor && PullAxis(end) != step)
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
        end = target;
        Build();
        SetMode(false);
    }

    private Vector2Int PullAxis(Vector2Int target)
    {
        Vector2Int delta = target - anchor;
        return new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
    }

    private RectInt Area(Vector2Int target)
    {
        Vector2Int min = Vector2Int.Min(anchor, target);
        Vector2Int max = Vector2Int.Max(anchor + Vector2Int.one, target);
        return new RectInt(min, max - min + Vector2Int.one);
    }

    private bool Contains(Vector2Int cell, Vector2Int target) {foreach(var candidate in Footprint(target))if(candidate==cell)return true;return false;}

    private IEnumerable<Vector2Int> Footprint(Vector2Int target)
    {
        originalCells.Clear();
        foreach (Vector2Int cell in Area(target).allPositionsWithin)
        {
            int station=target.y==anchor.y?cell.x:cell.y;
            int shift=offsets.TryGetValue(station,out int v)?v:0;
            var mapped=cell+(target.y==anchor.y?Vector2Int.up:Vector2Int.right)*shift;
            originalCells[mapped]=cell;
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

    private void Build()
    {
        foreach (PrototypeFallingBlock block in blue)
            block.DestroyFootprint();
        foreach(var mesh in spikeMeshes.Values)Object.Destroy(mesh);
        spikeMeshes.Clear();
        foreach (GameObject obj in objects)
        {
            obj.SetActive(false);
            Object.Destroy(obj);
        }
        objects.Clear();
        walls.Clear();
        blueWalls.Clear();
        blue.Clear();
        red.Clear();
        redProjections.Clear();
        filled.Clear();
        broken.Clear();redOriginal.Clear();pleats.Clear();
        var buildCells=new List<Vector2Int>(Footprint(end));
        foreach (Vector2Int cell in buildCells)
        {
            bool folded = end == anchor;
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
                var block = Tile(cell, new Color(0.24f, 0.49f, 0.81f), 1f).AddComponent<PrototypeFallingBlock>();
                block.CreateFootprint(platform, canHangInGrid: true);
                blue.Add(block);
                blueWalls.Add(wallCollider);
                continue;
            }

            var tile = Tile(cell, color == TileColor.Red ? new Color(0.79f, 0.27f, 0.33f)
                : new Color(0.98f, 0.79f, 0.30f), color == TileColor.Red ? 2f : 3f);
            tile.GetComponent<BoxCollider>().enabled = false;
            if (color == TileColor.Red)
            {
                red.Add(tile);redOriginal[tile]=originalCells[cell];
                // Palette C's partner belongs to this same cross-section.
                // Do not attach to a neighbouring strip when an elbow brings them together.
                Vector2 outward=paletteMode==2 ? (Axis==1?Vector2.up:Vector2.left) : Vector2.up;
                spikeMeshes[tile]=FanhuaPaperMesh.Spikes(outward);
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
            shape.size = new Vector2(0.94f, 1f);
            objects.Add(proxy);
        }
    }

    public void SetMode(bool platformMode)
    {
        this.platformMode=platformMode;
        if(Axis!=0)
            foreach(var obj in objects)
            {
                var mesh=obj.GetComponent<MeshFilter>();
                if(mesh==null||walls.Contains(obj.GetComponent<BoxCollider>()))continue;
                mesh.sharedMesh=platformMode ? (spikeMeshes.TryGetValue(obj,out var spikes)?spikes:cubeMesh) : paperMesh;
            }
        foreach(var p in pleats)p.SetActive(!platformMode);
        foreach (BoxCollider wall in walls)
        {
            bool consumed=false;
            foreach(var tile in red)
                if((filled.Contains(tile)||broken.Contains(tile))&&tile.transform.position==wall.transform.position){consumed=true;break;}
            bool showWall = !platformMode && !blueWalls.Contains(wall) && !consumed;
            wall.enabled = showWall;
            wall.GetComponent<Renderer>().enabled = showWall && Axis==0;
        }
        foreach (GameObject tile in red)
            tile.GetComponent<Renderer>().enabled = !filled.Contains(tile) && !broken.Contains(tile);

        // The 3D renderer is shared by both cameras. grid blue tiles are pushable.
        foreach (PrototypeFallingBlock block in blue)
        {
            block.GetComponent<BoxCollider>().enabled = !platformMode && !block.CurrentState.Plugged && !block.CurrentState.Lost;
            if (platformMode)
                block.ResumePhysics();
            else
                block.StopPhysics();
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
            if (!filled.Contains(tile) && !broken.Contains(tile) &&
                bounds.Intersects(new Bounds(new Vector3(p.x, p.z, 0f),
                    new Vector3(size.x, size.z, 1f) * 0.98f)))
                return true;
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
