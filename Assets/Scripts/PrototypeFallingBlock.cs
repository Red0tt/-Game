using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class PrototypeFallingBlock : MonoBehaviour
{
    public struct State
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public bool Plugged;
        public bool Lost;
        public bool Water;
        public bool Gray;
    }

    [SerializeField, Min(0f)] private float gravityScale = 1f;

    private Rigidbody2D footprint;
    private float worldHeight;
    private float platformHalfHeight;
    private float heightBeforeStep;
    private float speedBeforeStep;
    private bool usesWaterViews;
    private bool waterView;
    public State CurrentState { get; private set; }
    public bool CanHangInGrid { get; private set; }
    public bool HideSolidInGrid { get; set; }

    public void CreateFootprint(Transform parent, bool canHangInGrid = false)
    {
        // The level places each block as a centered, axis-aligned box.
        BoxCollider shape = GetComponent<BoxCollider>();
        CanHangInGrid = canHangInGrid;
        worldHeight = transform.position.y;
        CurrentState = new State { Position = new Vector2(transform.position.x, transform.position.z) };

        var proxy = new GameObject(name + " footprint");
        proxy.transform.SetParent(parent);
        footprint = proxy.AddComponent<Rigidbody2D>();
        footprint.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        footprint.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        footprint.mass = 100f;
        footprint.gravityScale = gravityScale;
        var projectedShape = proxy.AddComponent<BoxCollider2D>();
        Vector3 size = Vector3.Scale(shape.size, transform.lossyScale);
        platformHalfHeight = size.z * 0.5f;
        projectedShape.size = new Vector2(size.x, size.z);
    }

    public void SyncFromPhysics()
    {
        if (!CurrentState.Plugged && !CurrentState.Lost)
            RestoreWorldPosition(new State { Position = footprint.position, Velocity = footprint.velocity, Water = CurrentState.Water, Gray = CurrentState.Gray });
    }

    public void RestoreWorldPosition(State state)
    {
        if (CurrentState.Lost != state.Lost)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                renderer.enabled = !state.Lost;
            GetComponent<BoxCollider>().enabled = !state.Lost;
        }
        CurrentState = state;
        if (footprint != null) footprint.GetComponent<BoxCollider2D>().isTrigger = state.Water;
        GetComponent<Renderer>().enabled = !state.Lost &&
            (usesWaterViews ? (!state.Water && (!waterView || state.Plugged || state.Gray)) : !state.Water);
        if (transform.parent != null && transform.parent.GetComponentInChildren<PrototypeWaterLevel>() != null)
        {
            var color = (state.Plugged || state.Gray) ? Color.gray : new Color(.24f,.49f,.81f);
            var properties = new MaterialPropertyBlock();
            var renderer = GetComponent<Renderer>();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_Color",color);
            properties.SetColor("_BaseColor",color);
            renderer.SetPropertyBlock(properties);
        }
        transform.position = new Vector3(state.Position.x, worldHeight, state.Position.y);
    }

    public void ResumePhysics()
    {
        footprint.simulated = !CurrentState.Lost;
        if (CurrentState.Lost)
            return;

        footprint.position = CurrentState.Position;
        footprint.velocity = CurrentState.Velocity;
        footprint.GetComponent<BoxCollider2D>().isTrigger = CurrentState.Water;
        footprint.bodyType = CurrentState.Plugged || CurrentState.Water ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        RecordBeforePhysicsStep();
    }

    public void RecordBeforePhysicsStep()
    {
        heightBeforeStep = footprint.position.y;
        speedBeforeStep = footprint.velocity.y;
    }

    public bool FellOnto(ContactPoint2D contact)
    {
        // The collision solver can stop the block before the player reads the contact.
        return (contact.rigidbody == footprint || contact.otherRigidbody == footprint) &&
            (speedBeforeStep < -0.01f || footprint.position.y < heightBeforeStep - 0.001f);
    }

    public bool TryPushCell(Vector3 direction, float strength, LayerMask solids, RectInt? grid)
    {
        Vector2 target = CurrentState.Position + new Vector2(direction.x, direction.z);
        if (CurrentState.Lost || CurrentState.Plugged || strength < 1f ||
            !PrototypePushBox.TryGetPushDestination(GetComponent<BoxCollider>(), direction, solids, out bool supported) ||
            (!supported && !CanHangInGrid) ||
            (grid.HasValue && !grid.Value.Contains(Vector2Int.RoundToInt(target))))
            return false;

        State state = CurrentState;
        state.Position = target;
        // Moving the solid cube breaks its old reservoir binding. Re-entering
        // 2D must let it fall and find support at the new location.
        if (state.Water)
        {
            state.Water = false;
            state.Velocity = Vector2.zero;
        }
        RestoreWorldPosition(state);
        return true;
    }

    public void DiscardBelow(float bottomEdge)
    {
        State state = CurrentState;
        if (state.Plugged || state.Lost || state.Position.y + platformHalfHeight > bottomEdge)
            return;

        Discard();
    }

    public void Discard()
    {
        State state = CurrentState;
        state.Lost = true;
        RestoreWorldPosition(state);
        StopPhysics();
    }

    public void SetWaterView(bool platformMode)
    {
        usesWaterViews = true;
        waterView = platformMode;
        GetComponent<Renderer>().enabled = !CurrentState.Lost && (!CurrentState.Water && (!platformMode || CurrentState.Plugged || CurrentState.Gray)) && (platformMode || !HideSolidInGrid || CurrentState.Gray);
        GetComponent<BoxCollider>().enabled = !platformMode && !CurrentState.Lost && !CurrentState.Water;
    }

    internal Collider2D WaterShape => footprint.GetComponent<Collider2D>();

    public void SetWater(bool value)
    {
        if (CurrentState.Gray && value) return;
        State state = CurrentState;
        state.Water = value;
        state.Velocity = Vector2.zero;
        RestoreWorldPosition(state);
        footprint.GetComponent<BoxCollider2D>().isTrigger = value;
        footprint.bodyType = value ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        GetComponent<Renderer>().enabled = !CurrentState.Lost &&
            (usesWaterViews ? (!CurrentState.Water && (!waterView || CurrentState.Plugged || CurrentState.Gray)) : !value);
    }

    public void Plug(Vector2 cell)
    {
        RestoreWorldPosition(new State { Position = cell, Gray = true });
        ResumePhysics();
    }

    public void DestroyFootprint()
    {
        if(footprint==null)return;
        footprint.simulated = false;
        Destroy(footprint.gameObject);
    }

    public void StopPhysics()
    {
        footprint.simulated = false;
    }
}
