using UnityEngine;

public enum PlayerMode
{
    Side,
    Isometric
}

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class PrototypePlayer : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float sideSpeed = 4f;
    [SerializeField, Min(0.1f)] private float groundSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;
    [SerializeField] private LayerMask groundLayers;

    [SerializeField, Min(0f)] private float pushStrength = 2f;

    private PrototypePushBox[] boxes;
    private bool grounded;
    private Rigidbody body;
    private CapsuleCollider capsule;
    private Vector3 spawnPosition;
    private Vector2 moveInput;
    private bool jumpRequested;
    private bool switchRequested;
    private bool resetRequested;
    private float switchBlockedUntil;

    public PlayerMode Mode { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        spawnPosition = body.position;
        boxes = FindObjectsOfType<PrototypePushBox>();
        SetMode(PlayerMode.Side);
    }

    private void Update()
    {
        moveInput = Vector2.ClampMagnitude(new Vector2(
            Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
        jumpRequested |= Input.GetKeyDown(KeyCode.Space);
        switchRequested |= Input.GetKeyDown(KeyCode.Tab);
        resetRequested |= Input.GetKeyDown(KeyCode.R);
    }

    private void FixedUpdate()
    {
        if (resetRequested || body.position.y < -4f)
        {
            body.position = spawnPosition;
            body.velocity = Vector3.zero;
            foreach (PrototypePushBox box in boxes)
                box.ResetPosition();
            SetMode(PlayerMode.Side);
            switchBlockedUntil = 0f;
            resetRequested = switchRequested = jumpRequested = false;
            return;
        }

        grounded = IsGrounded();
        if (switchRequested)
        {
            if (grounded)
                SetMode(Mode == PlayerMode.Side ? PlayerMode.Isometric : PlayerMode.Side);
            else
                switchBlockedUntil = Time.time + 1.2f;
        }

        Vector3 velocity = body.velocity;
        if (Mode == PlayerMode.Side)
        {
            velocity.x = moveInput.x * sideSpeed;
            velocity.z = 0f;
            if (jumpRequested && !switchRequested && grounded)
                velocity.y = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
        }
        else
        {
            velocity.x = moveInput.x * groundSpeed;
            velocity.z = moveInput.y * groundSpeed;
        }

        body.velocity = velocity;
        jumpRequested = switchRequested = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (Mode != PlayerMode.Isometric || !grounded || moveInput == Vector2.zero ||
            !collision.collider.TryGetComponent(out PrototypePushBox box))
            return;

        Vector3 intent = new Vector3(moveInput.x, 0f, moveInput.y);
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (Mathf.Abs(normal.y) > 0.2f || Vector3.Dot(intent, normal) >= -0.1f)
                continue;

            Vector3 direction = Mathf.Abs(normal.x) > Mathf.Abs(normal.z)
                ? -Mathf.Sign(normal.x) * Vector3.right
                : -Mathf.Sign(normal.z) * Vector3.forward;
            if (Vector3.Dot(intent, direction) > 0.1f)
                box.TryPush(direction, pushStrength, groundLayers);
            break;
        }
    }

    private bool IsGrounded()
    {
        if (body.velocity.y > 0.05f)
            return false;

        Vector3 center = body.position + capsule.center;
        float radius = capsule.radius * 0.85f;
        float distance = capsule.height * 0.5f - radius + 0.06f;
        return Physics.SphereCast(center, radius, Vector3.down, out RaycastHit hit,
            distance, groundLayers, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.5f;
    }

    private void SetMode(PlayerMode mode)
    {
        Mode = mode;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        if (mode == PlayerMode.Side)
            body.constraints |= RigidbodyConstraints.FreezePositionZ;
    }

    private void OnGUI()
    {
        string controls = Mode == PlayerMode.Side
            ? "SIDE / A D or Left Right / Space: Jump"
            : "ISOMETRIC / WASD or Arrows / Walk into orange box";
        GUI.Box(new Rect(12, 12, 360, 65),
            controls + "\nTab: Switch     R: Reset" +
            (Time.time < switchBlockedUntil ? "\nLand before switching." : ""));
    }
}
