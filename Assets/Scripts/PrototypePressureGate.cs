using UnityEngine;

public sealed class PrototypePressureGate : MonoBehaviour
{
    [SerializeField] private Transform plate;
    [SerializeField] private Transform flowerPlate;
    [SerializeField] private BoxCollider door;
    [SerializeField] private Renderer doorVisual;

    public bool IsOpen => !door.enabled;

    internal void Refresh(PrototypePushBox[] boxes, PrototypeFanhua[] flowers)
    {
        bool open = false;
        foreach (PrototypePushBox box in boxes)
        {
            Vector3 offset = box.Position - plate.position;
            if (Mathf.Abs(offset.x) < 0.15f && Mathf.Abs(offset.z) < 0.15f)
            {
                open = true;
                break;
            }
        }
        if (flowerPlate != null)
        {
            var cell = Vector2Int.RoundToInt(new Vector2(flowerPlate.position.x, flowerPlate.position.z));
            open &= System.Array.Exists(flowers, flower => flower.PressesPlate(cell));
        }
        door.enabled = !open;
        doorVisual.enabled = !open;
    }
}
