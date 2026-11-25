using System.Collections.Generic;
using UnityEngine;

// Unit 02 only: finite-volume 2D fluid. Absence of this component selects legacy rules.
public sealed class PrototypeWaterLevel : MonoBehaviour
{
    public BoxCollider gate;
    public Renderer gateVisual;
    public Vector2 switchPosition = new Vector2(5,3);
    public Vector2Int returnShore = new Vector2Int(7,6);
    [Min(.02f)] public float flowInterval=.06f;
    public bool SwitchOpen { get; private set; }
    public sealed class State
    {
        public bool Open;
        public float Clock;
        public Unit02WaterSimulation.State Fluid;
    }
    private Unit02WaterSimulation simulation;
    private PrototypePlayer player;
    private PrototypeFanhua[] flowers;
    private readonly List<PrototypeFallingBlock> sources=new List<PrototypeFallingBlock>();
    private readonly List<Rect> pools=new List<Rect>();
    private readonly List<GameObject> visuals=new List<GameObject>();
    private Unit02FireTile[] sceneFire;
    private Material waterMaterial,markerMaterial,previewMaterial;
    private readonly List<LineRenderer> sourcePreviews=new List<LineRenderer>();
    private GameObject marker;
    private bool platformMode;
    private float clock, hintUntil;
    private string message;
    private Font hintFont;
    private GUIStyle hintStyle;
    public Unit02WaterSimulation Simulation => simulation;

    private void Start()
    {
        marker=new GameObject("Unit02 · safe return shore"); marker.transform.SetParent(transform.parent,false);
        markerMaterial=new Material(Shader.Find("Standard")); markerMaterial.color=new Color(.15f,.75f,.5f);
        var center=new Vector3(returnShore.x,.025f,returnShore.y+.5f);
        Mark(center+new Vector3(-.43f,0,0),new Vector3(.06f,.025f,1.86f));
        Mark(center+new Vector3(.43f,0,0),new Vector3(.06f,.025f,1.86f));
        Mark(center+new Vector3(0,0,-.9f),new Vector3(.86f,.025f,.06f));
        Mark(center+new Vector3(0,0,.9f),new Vector3(.86f,.025f,.06f));
    }
    private void Mark(Vector3 position,Vector3 scale)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name="Return shore border";
        obj.transform.SetParent(marker.transform,false); obj.transform.position=position; obj.transform.localScale=scale;
        obj.GetComponent<Collider>().enabled=false; Destroy(obj.GetComponent<Collider>());
        obj.GetComponent<Renderer>().sharedMaterial=markerMaterial;
    }
    internal void Initialize(Transform physicsRoot)
    {
        player=GetComponent<PrototypePlayer>();
        sceneFire=FindObjectsOfType<Unit02FireTile>();
        foreach(var fire in sceneFire) fire.CreateProjection(physicsRoot);
    }
    internal bool CanEditFlower(int index)
    {
        if(simulation==null) return true;
        Notify("本轮已开始放水；按Z撤回到首次放水前，可重新调整翻花"); return false;
    }
    public State Capture() => new State { Open=SwitchOpen, Clock=clock, Fluid=simulation?.Capture() };
    public void Restore(State saved)
    {
        SwitchOpen=saved!=null && saved.Open; clock=saved?.Clock??0;
        if(saved?.Fluid==null) simulation=null;
        else
        {
            simulation=new Unit02WaterSimulation(player.WaterGrid,saved.Fluid.Anchors);
            simulation.Restore(saved.Fluid);
        }
        ApplyGate();
        if(sceneFire!=null) foreach(var fire in sceneFire) fire.SetCooling(simulation!=null ? simulation.FireProgress(fire.Cell) : 0);
        pools.Clear(); foreach(var visual in visuals) visual.SetActive(false);
        foreach(var preview in sourcePreviews) preview.gameObject.SetActive(false);
    }
    private void Bind(PrototypeFanhua[] activeFlowers)
    {
        flowers=activeFlowers;
        sources.Clear(); sources.AddRange(player.WaterSceneBlocks);
        foreach(var flower in flowers) sources.AddRange(flower.BlueBlocks);
        if(simulation==null)
        {
            var anchors=new Vector2Int[sources.Count];
            for(int i=0;i<sources.Count;i++) anchors[i]=Vector2Int.RoundToInt(sources[i].CurrentState.Position);
            simulation=new Unit02WaterSimulation(player.WaterGrid,anchors);
        }
        for(int i=0;i<sources.Count;i++)
        {
            var source=sources[i];
            simulation.MoveSource(i,Vector2Int.RoundToInt(source.CurrentState.Position));
            if(simulation.Remaining(i)<.00001f) source.Discard();
            else if(!source.CurrentState.Lost) { source.SetWater(true); source.StopPhysics(); }
        }
    }
    private void RebuildTerrain()
    {
        var solid=new HashSet<Vector2Int>();
        foreach(var shape in player.WaterStaticSolids)
        {
            if(shape==null || !shape.enabled) continue;
            Bounds b=shape.bounds;
            for(int x=Mathf.CeilToInt(b.min.x+.01f);x<=Mathf.FloorToInt(b.max.x-.01f);x++)
            for(int z=Mathf.CeilToInt(b.min.z+.01f);z<=Mathf.FloorToInt(b.max.z-.01f);z++) solid.Add(new Vector2Int(x,z));
        }
        foreach(var flower in flowers)
        {
            foreach(var cell in flower.YellowCells) solid.Add(cell);
            foreach(var cell in flower.FireCells) simulation.AddFire(cell);
        }
        foreach(var fire in sceneFire) simulation.AddFire(fire.Cell);
        simulation.SetSolids(solid);
    }
    internal void Advance(float seconds,PrototypeFanhua[] activeFlowers)
    {
        Bind(activeFlowers); RebuildTerrain(); clock+=seconds;
        while(clock>=flowInterval) { simulation.Step(); clock-=flowInterval; }
        ApplyCooling();
        for(int i=0;i<sources.Count;i++) if(simulation.Remaining(i)<.00001f) sources[i].Discard();
    }
    private void ApplyCooling()
    {
        foreach(var fire in sceneFire) fire.SetCooling(simulation.FireProgress(fire.Cell));
        foreach(var flower in flowers)
            foreach(var cell in flower.FireCells) if(simulation.Cooled(cell)) flower.CoolCell(cell);
    }
    internal void Step(Rigidbody2D body,PrototypeFanhua[] activeFlowers)
    {
        if(player==null) return;
        Bind(activeFlowers); RebuildTerrain(); ApplyCooling();
        foreach(var source in sources)
            Physics2D.IgnoreCollision(body.GetComponent<Collider2D>(),source.WaterShape,true);
        pools.Clear();
        foreach(var cell in simulation.Cells)
        {
            float depth=simulation.Depth(cell);
            if(depth>.0001f) pools.Add(new Rect(cell.x-.5f,cell.y-.5f,1,Mathf.Min(1,depth)));
        }
        RenderPools();
        RenderSources();
        if(!SwitchOpen && Wet(body.position) && Vector2.Distance(body.position,switchPosition)<.45f)
        { SwitchOpen=true; ApplyGate(); }
    }
    private void ApplyGate()
    {
        if(gate!=null) gate.enabled=!SwitchOpen;
        if(gateVisual!=null) gateVisual.enabled=!SwitchOpen;
        if(player!=null) player.RefreshWaterProjections();
    }
    private void RenderPools()
    {
        if(waterMaterial==null)
        {
            waterMaterial=new Material(Shader.Find("Standard")); waterMaterial.color=new Color(.12f,.5f,.9f,.6f);
            waterMaterial.SetFloat("_Mode",3); waterMaterial.SetInt("_SrcBlend",5); waterMaterial.SetInt("_DstBlend",10);
            waterMaterial.SetInt("_ZWrite",0); waterMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON"); waterMaterial.renderQueue=3000;
        }
        while(visuals.Count<pools.Count)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name="Unit02 · 2D water cell";
            obj.transform.SetParent(transform.parent,false); obj.GetComponent<Collider>().enabled=false;
            Destroy(obj.GetComponent<Collider>()); obj.GetComponent<Renderer>().sharedMaterial=waterMaterial; visuals.Add(obj);
        }
        for(int i=0;i<visuals.Count;i++)
        {
            visuals[i].SetActive(platformMode && i<pools.Count);
            if(i>=pools.Count) continue;
            var pool=pools[i]; visuals[i].transform.position=new Vector3(pool.center.x,.5f,pool.center.y);
            visuals[i].transform.localScale=new Vector3(pool.width,.55f,pool.height);
        }
    }
    private void RenderSources()
    {
        if(previewMaterial==null) previewMaterial=new Material(Shader.Find("Sprites/Default"));
        while(sourcePreviews.Count<sources.Count)
        {
            var obj=new GameObject("Unit02 · source return preview");obj.transform.SetParent(transform.parent,false);
            var line=obj.AddComponent<LineRenderer>();line.useWorldSpace=true;line.loop=true;line.positionCount=4;
            line.startWidth=.035f;line.endWidth=.035f;line.sharedMaterial=previewMaterial;
            line.startColor=line.endColor=new Color(.15f,.4f,.9f,.85f);sourcePreviews.Add(line);
        }
        for(int i=0;i<sourcePreviews.Count;i++)
        {
            bool show=platformMode && i<sources.Count && simulation.Remaining(i)>.00001f;
            sourcePreviews[i].gameObject.SetActive(show);if(!show)continue;
            var cell=simulation.Anchor(i);
            sourcePreviews[i].SetPositions(new[]{new Vector3(cell.x-.43f,.85f,cell.y-.43f),new Vector3(cell.x+.43f,.85f,cell.y-.43f),
                new Vector3(cell.x+.43f,.85f,cell.y+.43f),new Vector3(cell.x-.43f,.85f,cell.y+.43f)});
        }
    }
    internal void SetMode(bool platform)
    {
        platformMode=platform;
        for(int i=0;i<visuals.Count;i++) visuals[i].SetActive(platform && i<pools.Count);
        foreach(var preview in sourcePreviews) preview.gameObject.SetActive(false);
    }
    private bool Wet(Vector2 point) { foreach(var pool in pools) if(pool.Contains(point)) return true; return false; }
    internal bool Swim(Rigidbody2D body,Vector2 input,bool jump)
    {
        if(!Wet(body.position)) return false;
        float surface=float.NegativeInfinity;
        // Follow only a contiguous wet column; a disconnected falling droplet above
        // cannot turn deep water into an artificial surface launch point.
        var column=new Vector2Int(Mathf.FloorToInt(body.position.x+.5f),Mathf.FloorToInt(body.position.y+.5f));
        for(int n=0;n<player.WaterGrid.height;n++)
        {
            float depth=simulation.Depth(column);
            surface=column.y-.5f+depth;
            if(depth<.999f || simulation.Depth(column+Vector2Int.up)<.0001f) break;
            column+=Vector2Int.up;
        }
        if(body.velocity.y>3f && body.position.y>surface-.3f) return false;
        body.gravityScale=0;
        body.velocity=new Vector2(input.x*3f,Mathf.Min(input.y!=0?input.y*2.5f:1.2f,(surface-.05f-body.position.y)/Time.fixedDeltaTime));
        if(jump && simulation.HasSupport(column) && body.position.y>surface-.3f)
        { body.gravityScale=1; body.velocity=new Vector2(input.x*4f,Mathf.Sqrt(-2*Physics2D.gravity.y*1.2f)); }
        return true;
    }
    internal bool IsDangerous(Bounds body)
    {
        foreach(var fire in sceneFire)
            if(!fire.Cooled && body.Intersects(new Bounds(new Vector3(fire.Cell.x,fire.Cell.y,0),new Vector3(.98f,.98f,1)))) return true;
        return false;
    }
    internal void RejectReturn(bool occupied) => Notify(occupied?"这里会与蓝块或墙重叠，请游到岸边再切换":"这里没有地板，请先游到绿色岸边标记");
    private void Notify(string text) { message=text; hintUntil=Time.unscaledTime+3; }
    internal bool TryMapBlocksToGrid(PrototypeFallingBlock[] sceneBlocks,PrototypeFanhua[] activeFlowers,
        LayerMask solids,RectInt grid,Vector2Int playerCell)
    {
        // The liquid is paused; each surviving cube returns to its own emission anchor.
        var blocks=new List<PrototypeFallingBlock>(sceneBlocks);
        foreach(var flower in activeFlowers) blocks.AddRange(flower.BlueBlocks);
        foreach(var block in blocks) block.GetComponent<BoxCollider>().enabled=false;
        Physics.SyncTransforms(); var occupied=new HashSet<Vector2Int>();
        foreach(var block in blocks)
        {
            var state=block.CurrentState; if(state.Lost) continue;
            var cell=Vector2Int.RoundToInt(state.Position); var shape=block.GetComponent<BoxCollider>();
            var center=new Vector3(cell.x,block.transform.position.y,cell.y)+block.transform.TransformVector(shape.center);
            var half=Vector3.Scale(shape.size,block.transform.lossyScale)*.5f-Vector3.one*.01f;
            if(!grid.Contains(cell) || cell==playerCell || !occupied.Add(cell) ||
                Physics.CheckBox(center,half,Quaternion.identity,solids,QueryTriggerInteraction.Ignore)) return false;
            block.SetWaterView(false);
        }
        Physics.SyncTransforms(); return true;
    }
    private void OnGUI()
    {
        if(hintStyle==null)
        {
            hintFont=PrototypePlayer.CreateChineseHudFont(16);
            hintStyle=new GUIStyle(GUI.skin.label){font=hintFont,fontSize=16,alignment=TextAnchor.MiddleCenter};
            hintStyle.normal.textColor=PrototypePlayer.HudInk;
        }
        string hint=platformMode?"第二单元 · W上浮 / S下潜 · 水面Space跃出 · 绿色岸边可切回2.5D":"第二单元 · 先背身拉翻花 / Q换色，再切入2D放水 · 每蓝格一份水";
        GUI.Label(new Rect(0,10,Screen.width,28),hint,hintStyle);
        if(simulation!=null) GUI.Label(new Rect(0,38,Screen.width,26),$"水量 {simulation.TotalWater:F1}　冷却消耗 {simulation.TotalSpent:F1}　流失 {simulation.TotalLost:F1}",hintStyle);
        if(!platformMode && simulation!=null && Camera.main!=null)
            for(int i=0;i<sources.Count;i++)
            {
                float amount=simulation.Remaining(i);if(amount<=.00001f||amount>=.999f)continue;
                Vector3 screen=Camera.main.WorldToScreenPoint(sources[i].transform.position+Vector3.up*.7f);
                GUI.Label(new Rect(screen.x-35,Screen.height-screen.y-15,70,25),$"{amount:F2}份",hintStyle);
            }
        if(Time.unscaledTime<hintUntil) GUI.Label(new Rect(0,66,Screen.width,26),message,hintStyle);
    }
    private void OnDestroy()
    {
        if(marker!=null) Destroy(marker); if(markerMaterial!=null) Destroy(markerMaterial);
        if(waterMaterial!=null) Destroy(waterMaterial); if(previewMaterial!=null) Destroy(previewMaterial);
        foreach(var preview in sourcePreviews) if(preview!=null) Destroy(preview.gameObject);
        if(hintFont!=null) Destroy(hintFont);
        foreach(var obj in visuals) if(obj!=null) Destroy(obj);
    }
}
