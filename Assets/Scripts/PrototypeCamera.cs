using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class PrototypeCamera : MonoBehaviour
{
    [SerializeField] private PrototypePlayer player;
    [SerializeField] private Vector3 focus = new Vector3(0f, 0.5f, 0f);

    private void LateUpdate()
    {
        Vector3 offset = player.Mode == PlayerMode.Side
            ? new Vector3(0f, 0f, -16f)
            : new Vector3(9f, 9f, -9f);
        transform.SetPositionAndRotation(focus + offset, Quaternion.LookRotation(-offset));
    }
}
