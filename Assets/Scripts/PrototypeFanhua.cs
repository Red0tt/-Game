using System.Collections.Generic;
using UnityEngine;

// The test grid is an area, including cells without floor support.
internal sealed class PrototypeFanhua
{
    internal sealed class State
    {
        public Vector2Int End;
        public bool AlternatePalette;
        public PrototypeFallingBlock.State[] Blue;
        public bool[] Filled;
    }

    private readonly Vector2Int anchor;
    private readonly RectInt grid;
    private readonly Transform world;
    private readonly Transform platform;
    private readonly Material material;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<BoxCollider> walls = new List<BoxCollider>();
    private readonly Dictionary<BoxCollider, PrototypeFallingBlock> blueWalls =
        new Dictionary<BoxCollider, PrototypeFallingBlock>();
    private readonly List<PrototypeFallingBlock> blue = new List<PrototypeFallingBlock>();
    private readonly List<GameObject> red = new List<GameObject>();
    private readonly HashSet<GameObject> filled = new HashSet<GameObject>();
    private Vector2Int end;
    private bool alternatePalette;
    private bool isPlatform;

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
            Blue = blue.ConvertAll(block => block.CurrentState).ToArray(),
            Filled = red.ConvertAll(tile => filled.Contains(tile)).ToArray()
        };
    }

    public Vector2Int AnchorCell => anchor;
    public Vector2Int EndCell => end;
    public string PaletteHint => alternatePalette ? "Palette B" : "Palette A";

    public bool IsInitial => end == anchor && !alternatePalette;

    public void Restore(State state)
    {
        end = state.End;
        alternatePalette = state.AlternatePalette;
        Build();
        for (int i = 0; i < blue.Count; i++)
            blue[i].RestoreWorldPosition(state.Blue[i]);
        for (int i = 0; i < red.Count; i++)
            if (state.Filled[i])
                Fill(red[i]);
    }

    public bool CanSetEnd(Vector2Int target, LayerMask solids, Collider pullingPlayer)
    {
        if (target == end || !grid.Contains(target))
            return false;
        if (target == anchor)
            return true;

        Vector2Int delta = target - anchor;
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

    public bool IsBeside(Vector2Int player)
    {
        foreach (Vector2Int direction in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
            if (Contains(player + direction, end) && !Contains(player, end))
                return true;
        return false;
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
        alternatePalette = !alternatePalette;
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

    private bool Contains(Vector2Int cell, Vector2Int target) => Area(target).Contains(cell);

    private IEnumerable<Vector2Int> Footprint(Vector2Int target)
    {
        foreach (Vector2Int cell in Area(target).allPositionsWithin)
            yield return cell;
    }

    private enum TileColor { Red, Yellow, Blue }

    private TileColor ColorAt(Vector2Int cell)
    {
        Vector2Int direction = PullAxis(end);
        if (alternatePalette)
            direction = -direction;

        bool upper = cell.y > anchor.y;
        bool right = cell.x > anchor.x;
        if (direction == Vector2Int.right)
            return upper ? TileColor.Blue : TileColor.Yellow;
        if (direction == Vector2Int.left)
            return upper ? TileColor.Red : TileColor.Blue;
        if (direction == Vector2Int.up)
            return right ? TileColor.Blue : TileColor.Red;
        return right ? TileColor.Yellow : TileColor.Blue;
    }

    private static Vector3 World(Vector2Int cell) => new Vector3(cell.x, 0.5f, cell.y);

    private GameObject Tile(Vector2Int cell, Color color)
    {
        GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tile.transform.SetParent(world, false);
        tile.transform.position = World(cell);
        var renderer = tile.GetComponent<Renderer>();
        if (material != null)
            renderer.sharedMaterial = material;
        Tint(renderer, color);
        objects.Add(tile);
        return tile;
    }

    private static void Tint(Renderer renderer, Color color)
    {
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_Color", color);
        properties.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(properties);
    }

    private void Build()
    {
        foreach (PrototypeFallingBlock block in blue)
            block.DestroyFootprint();
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
        filled.Clear();
        foreach (Vector2Int cell in Footprint(end))
        {
            bool folded = end == anchor;
            GameObject wall = Tile(cell, folded && alternatePalette ? new Color(0.58f, 0.45f, 0.67f) : Color.gray);
            wall.name = folded ? "Fanhua · folded 2×2" : "Fanhua · barrier";
            wall.transform.localScale = new Vector3(0.96f, folded ? 1f : 0.8f, 0.96f);
            var wallCollider = wall.GetComponent<BoxCollider>();
            walls.Add(wallCollider);
            if (folded)
                continue;

            TileColor color = ColorAt(cell);
            if (color == TileColor.Blue)
            {
                var block = Tile(cell, new Color(0.08f, 0.36f, 0.97f)).AddComponent<PrototypeFallingBlock>();
                block.CreateFootprint(platform);
                blue.Add(block);
                blueWalls.Add(wallCollider, block);
                continue;
            }

            var tile = Tile(cell, color == TileColor.Red ? Color.red : Color.yellow);
            tile.GetComponent<BoxCollider>().enabled = false;
            if (color == TileColor.Red)
            {
                red.Add(tile);
                continue;
            }
            var proxy = new GameObject("Yellow footprint");
            proxy.transform.SetParent(platform, false);
            proxy.transform.position = (Vector2)cell;
            proxy.AddComponent<BoxCollider2D>();
            objects.Add(proxy);
        }
    }

    public void SetMode(bool platformMode)
    {
        isPlatform = platformMode;
        foreach (BoxCollider wall in walls)
        {
            bool showWall = !platformMode;
            if (showWall && blueWalls.TryGetValue(wall, out PrototypeFallingBlock block))
            {
                Vector3 origin = wall.transform.position;
                var originCell = new Vector2(origin.x, origin.z);
                showWall = !block.CurrentState.Plugged && block.CurrentState.Position == originCell;
            }
            wall.enabled = showWall;
            wall.GetComponent<Renderer>().enabled = showWall;
        }
        foreach (GameObject tile in red)
            tile.GetComponent<Renderer>().enabled = !filled.Contains(tile);

        // The 3D renderer is shared by both cameras. grid blue tiles are pushable.
        foreach (PrototypeFallingBlock block in blue)
        {
            block.GetComponent<BoxCollider>().enabled = !platformMode && !block.CurrentState.Plugged;
            if (platformMode)
                block.ResumePhysics();
            else
                block.StopPhysics();
        }
    }

    private void Fill(GameObject tile)
    {
        filled.Add(tile);
        tile.GetComponent<Renderer>().enabled = false;
    }

    public void Sync()
    {
        foreach (PrototypeFallingBlock block in blue)
            SyncFallingBlock(block);
    }

    public void SyncFallingBlock(PrototypeFallingBlock block)
    {
        if (!isPlatform)
            return;

        Vector2 before = block.CurrentState.Position;
        block.SyncFromPhysics();
        Vector2 after = block.CurrentState.Position;
        if (after.y >= before.y)
            return;

        GameObject hit = null;
        foreach (GameObject tile in red)
        {
            Vector3 target = tile.transform.position;
            if (!filled.Contains(tile) && Mathf.Abs(target.x - after.x) < tile.transform.lossyScale.x * 0.5f &&
                before.y > target.z && after.y <= target.z &&
                (hit == null || target.z > hit.transform.position.z))
                hit = tile;
        }
        if (hit != null)
        {
            Fill(hit);
            block.Plug(new Vector2(hit.transform.position.x, hit.transform.position.z));
        }
    }

    public bool IsDangerous(CapsuleCollider2D player, ContactPoint2D[] contacts)
    {
        Bounds bounds = player.bounds;
        foreach (GameObject tile in red)
        {
            Vector3 p = tile.transform.position;
            Vector3 size = tile.transform.lossyScale;
            if (!filled.Contains(tile) &&
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

    public void RecordBeforePhysicsStep()
    {
        foreach (PrototypeFallingBlock block in blue)
            block.RecordBeforePhysicsStep();
    }

    public void StopPhysics()
    {
        foreach (PrototypeFallingBlock block in blue)
            block.StopPhysics();
    }
}
