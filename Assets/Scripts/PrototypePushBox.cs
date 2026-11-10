using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class PrototypePushBox : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float weight = 1f;

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

    public bool TryPushCell(Vector3 direction, float strength, LayerMask solidLayers)
    {
        if (weight > strength)
            return false;

        Bounds bounds = shape.bounds;
        Vector3 halfExtents = bounds.extents - Vector3.one * Clearance;
        Vector3 target = bounds.center + direction;
        if (Physics.CheckBox(target, halfExtents, Quaternion.identity,
                solidLayers, QueryTriggerInteraction.Ignore))
            return false;

        // All corners need support on the destination cell.
        Vector3 bottom = target - Vector3.up * halfExtents.y;
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = bottom + new Vector3(x * halfExtents.x, 0f, z * halfExtents.z);
            if (!Physics.Raycast(corner, Vector3.down, 3f * Clearance,
                    solidLayers, QueryTriggerInteraction.Ignore))
                return false;
        }

        body.position += direction;
        return true;
    }

    public void ResetPosition()
    {
        body.position = spawnPosition;
    }
}
