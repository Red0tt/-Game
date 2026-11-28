using UnityEngine;

// Authored fire for Unit 02. Has no effect in scenes without PrototypeWaterLevel.
[RequireComponent(typeof(BoxCollider),typeof(Renderer))]
public sealed class Unit02FireTile : MonoBehaviour
{
    private BoxCollider2D projection;
    private Mesh spikeMesh;
    public Vector2Int Cell => Vector2Int.RoundToInt(new Vector2(transform.position.x,transform.position.z));
    public bool Cooled { get; private set; }
    internal void CreateProjection(Transform parent)
    {
        if(spikeMesh==null){spikeMesh=FanhuaPaperMesh.Spikes(Vector2.up);GetComponent<MeshFilter>().sharedMesh=spikeMesh;}
        var obj=new GameObject(name+" · Unit02 fire footprint"); obj.transform.SetParent(parent,false);
        obj.transform.position=(Vector2)Cell;
        projection=obj.AddComponent<BoxCollider2D>(); projection.size=Vector2.one;
        Apply(false);
    }
    private void OnDestroy(){if(spikeMesh!=null)Destroy(spikeMesh);}
    internal void Apply(bool cooled) => SetCooling(cooled?1f:0f);
    internal void SetCooling(float progress)
    {
        Cooled=progress>=1f;
        if(projection!=null) projection.isTrigger=true;
        GetComponent<Renderer>().enabled=!Cooled;
        GetComponent<BoxCollider>().enabled=!Cooled;
        var properties=new MaterialPropertyBlock();
        GetComponent<Renderer>().GetPropertyBlock(properties);
        var color=Cooled?Color.gray:Color.Lerp(new Color(.9f,.22f,.16f),new Color(.45f,.28f,.22f),Mathf.Clamp01(progress));
        properties.SetColor("_Color",color); properties.SetColor("_BaseColor",color);
        GetComponent<Renderer>().SetPropertyBlock(properties);
    }
}
