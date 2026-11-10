using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class PrototypeCamera : MonoBehaviour
{
    [SerializeField] private PrototypePlayer player;
    [SerializeField] private Vector3 focus = new Vector3(0f, 0.5f, 0f);
    [SerializeField, Range(0f, 89f)] private float gridPitch = 45f;

    [SerializeField] private bool followRows;
    [SerializeField] private float minFocusZ;
    [SerializeField] private float maxFocusZ;

    private void LateUpdate()
    {
        float pitch = player.Mode == PlayerMode.Platform ? 90f : gridPitch;
        Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -16f);
        Vector3 target = focus;
        if (followRows)
            target.z = Mathf.Clamp(player.transform.position.z, minFocusZ, maxFocusZ);
        transform.SetPositionAndRotation(target + offset, rotation);
    }
}
