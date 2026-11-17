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
    }

    [SerializeField, Min(0f)] private float gravityScale = 1f;

    private Rigidbody2D footprint;
    private float worldHeight;
    private float platformHalfHeight;
    private float heightBeforeStep;
    private float speedBeforeStep;
    public State CurrentState { get; private set; }
    public bool CanHangInGrid { get; private set; }

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
            RestoreWorldPosition(new State { Position = footprint.position, Velocity = footprint.velocity });
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
        transform.position = new Vector3(state.Position.x, worldHeight, state.Position.y);
    }

    public void ResumePhysics()
    {
        footprint.simulated = !CurrentState.Lost;
        if (CurrentState.Lost)
            return;

        footprint.position = CurrentState.Position;
        footprint.velocity = CurrentState.Velocity;
        footprint.bodyType = CurrentState.Plugged ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
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

    public void Plug(Vector2 cell)
    {
        RestoreWorldPosition(new State { Position = cell, Plugged = true });
        ResumePhysics();
    }

    public void DestroyFootprint()
    {
        footprint.simulated = false;
        Destroy(footprint.gameObject);
    }

    public void StopPhysics()
    {
        footprint.simulated = false;
    }
}
