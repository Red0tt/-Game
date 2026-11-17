using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class PrototypePushBox : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float weight = 1f;

    private const float Clearance = 0.01f;
    private Rigidbody body;
    private BoxCollider shape;
    public Vector3 Position
    {
        get => body.position;
        set => body.position = value;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        shape = GetComponent<BoxCollider>();
    }

    public bool TryPushCell(Vector3 direction, float strength, LayerMask solidLayers)
    {
        if (weight > strength ||
            !TryGetPushDestination(shape, direction, solidLayers, out bool supported) || !supported)
            return false;

        body.position += direction;
        return true;
    }

    internal static bool TryGetPushDestination(BoxCollider shape, Vector3 direction, LayerMask solidLayers,
        out bool supported)
    {
        Bounds bounds = shape.bounds;
        Vector3 halfExtents = bounds.extents - Vector3.one * Clearance;
        Vector3 target = bounds.center + direction;
        // An occupied destination blocks the push. Never push a chain of blocks.
        supported = false;
        if (Physics.CheckBox(target, halfExtents, Quaternion.identity,
                solidLayers, QueryTriggerInteraction.Ignore))
            return false;

        supported = HasFloorSupport(target, halfExtents, solidLayers);
        return true;
    }

    internal static bool HasFloorSupport(Vector3 center, Vector3 halfExtents, LayerMask solidLayers)
    {
        // All corners need support on the destination cell.
        Vector3 bottom = center - Vector3.up * halfExtents.y;
        for (int x = -1; x <= 1; x += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 corner = bottom + new Vector3(x * halfExtents.x, 0f, z * halfExtents.z);
            if (!Physics.Raycast(corner, Vector3.down, 3f * Clearance,
                    solidLayers, QueryTriggerInteraction.Ignore))
                return false;
        }

        return true;
    }
}
