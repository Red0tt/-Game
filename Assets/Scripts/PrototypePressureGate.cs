using UnityEngine;

public sealed class PrototypePressureGate : MonoBehaviour
{
    [SerializeField] private Transform plate;
    [SerializeField] private Transform flowerPlate;
    [SerializeField] private BoxCollider door;
    [SerializeField] private Renderer doorVisual;

    private static readonly int Pressed = Shader.PropertyToID("_Pressed");
    private MaterialPropertyBlock plateProperties;

    public bool IsOpen => !door.enabled;

    internal void Refresh(PrototypePushBox[] boxes, PrototypeFanhua[] flowers)
    {
        bool boxPressed = false;
        foreach (PrototypePushBox box in boxes)
        {
            Vector3 offset = box.Position - plate.position;
            if (Mathf.Abs(offset.x) < 0.15f && Mathf.Abs(offset.z) < 0.15f)
            {
                boxPressed = true;
                break;
            }
        }
        bool flowerPressed = false;
        if (flowerPlate != null)
        {
            var cell = Vector2Int.RoundToInt(new Vector2(flowerPlate.position.x, flowerPlate.position.z));
            flowerPressed = System.Array.Exists(flowers, flower => flower.PressesPlate(cell));
        }
        bool open = boxPressed && (flowerPlate == null || flowerPressed);
        door.enabled = !open;
        doorVisual.enabled = !open;

        UpdatePlateVisual(plate, boxPressed);
        if (flowerPlate != null)
            UpdatePlateVisual(flowerPlate, flowerPressed);
    }

    private void UpdatePlateVisual(Transform target, bool pressed)
    {
        if (!target.TryGetComponent(out Renderer visual) ||
            visual.sharedMaterial == null || !visual.sharedMaterial.HasProperty(Pressed))
            return;

        if (plateProperties == null)
            plateProperties = new MaterialPropertyBlock();
        visual.GetPropertyBlock(plateProperties);
        plateProperties.SetFloat(Pressed, pressed ? 1f : 0f);
        visual.SetPropertyBlock(plateProperties);
    }
}
