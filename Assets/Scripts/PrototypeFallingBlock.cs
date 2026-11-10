using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class PrototypeFallingBlock : MonoBehaviour
{
    public struct State
    {
        public Vector2 Position;
        public Vector2 Velocity;
    }

    private Rigidbody2D footprint;
    private float worldHeight;
    public State CurrentState { get; private set; }

    public void CreateFootprint(Transform parent)
    {
        // The level places each block as a centered, axis-aligned box.
        BoxCollider shape = GetComponent<BoxCollider>();
        worldHeight = transform.position.y;
        CurrentState = new State { Position = new Vector2(transform.position.x, transform.position.z) };

        var proxy = new GameObject(name + " footprint");
        proxy.transform.SetParent(parent);
        footprint = proxy.AddComponent<Rigidbody2D>();
        footprint.constraints = RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
        footprint.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        footprint.mass = 100f;
        var projectedShape = proxy.AddComponent<BoxCollider2D>();
        Vector3 size = Vector3.Scale(shape.size, transform.lossyScale);
        projectedShape.size = new Vector2(size.x, size.z);
    }

    public void SyncFromPhysics()
    {
        RestoreWorldPosition(new State { Position = footprint.position, Velocity = footprint.velocity });
    }

    public void RestoreWorldPosition(State state)
    {
        CurrentState = state;
        transform.position = new Vector3(state.Position.x, worldHeight, state.Position.y);
    }

    public void ResumePhysics()
    {
        footprint.simulated = true;
        footprint.position = CurrentState.Position;
        footprint.velocity = CurrentState.Velocity;
    }

    public void StopPhysics()
    {
        footprint.simulated = false;
    }
}
