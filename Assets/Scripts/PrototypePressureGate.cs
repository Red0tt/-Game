using UnityEngine;

public sealed class PrototypePressureGate : MonoBehaviour
{
    [SerializeField] private Transform plate;
    [SerializeField] private BoxCollider door;
    [SerializeField] private Renderer doorVisual;

    public bool IsOpen => !door.enabled;

    public void Refresh(PrototypePushBox[] boxes)
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
        door.enabled = !open;
        doorVisual.enabled = !open;
    }
}
