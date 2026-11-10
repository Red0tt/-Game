using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class PrototypePushBox : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float weight = 1f;
    [SerializeField, Min(0.1f)] private float pushSpeed = 2f;

    private const float Clearance = 0.01f;
    private Rigidbody body;
    private BoxCollider shape;
    private Vector3 spawnPosition;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        shape = GetComponent<BoxCollider>();
        spawnPosition = body.position;
    }

    public void TryPush(Vector3 direction, float strength, LayerMask solidLayers)
    {
        if (weight > strength)
            return;

        Bounds bounds = shape.bounds;
        Vector3 halfExtents = bounds.extents;
        halfExtents.y -= Clearance;
        float distance = pushSpeed * Time.fixedDeltaTime;
        if (Physics.BoxCast(bounds.center, halfExtents, direction, out RaycastHit hit,
                Quaternion.identity, distance + Clearance, solidLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(0f, hit.distance - Clearance);

        Vector3 displacement = direction * distance;
        Vector3 bottom = bounds.center + displacement - Vector3.up * halfExtents.y;
        // Each corner needs support so a kinematic box cannot be pushed out over an edge.
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = bottom + new Vector3(x * (halfExtents.x - Clearance),
                0f, z * (halfExtents.z - Clearance));
            if (!Physics.Raycast(corner, Vector3.down, 3f * Clearance, solidLayers,
                    QueryTriggerInteraction.Ignore))
                return;
        }

        if (distance > 0f)
            body.MovePosition(body.position + displacement);
    }

    public void ResetPosition()
    {
        body.position = spawnPosition;
    }
}
