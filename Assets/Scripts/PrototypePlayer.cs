using UnityEngine;

public enum PlayerMode
{
    Side,
    Grid
}

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class PrototypePlayer : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float sideSpeed = 4f;
    [SerializeField, Min(0.05f)] private float gridStepInterval = 0.18f;
    [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;
    [SerializeField] private LayerMask groundLayers;

    [SerializeField, Min(0f)] private float pushStrength = 2f;

    private PrototypePushBox[] boxes;
    private float nextGridStep;
    private Rigidbody body;
    private CapsuleCollider capsule;
    private Vector3 spawnPosition;
    private Vector2 moveInput;
    private Vector3 queuedGridDirection;
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
        if (Mode == PlayerMode.Grid)
        {
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                queuedGridDirection = Vector3.right;
            else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                queuedGridDirection = Vector3.left;
            else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                queuedGridDirection = Vector3.forward;
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                queuedGridDirection = Vector3.back;
        }
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

        bool grounded = IsGrounded();
        if (switchRequested)
        {
            if (grounded && TrySwitchMode())
            {
                jumpRequested = switchRequested = false;
                return;
            }
            switchBlockedUntil = Time.time + 1.2f;
        }

        if (Mode == PlayerMode.Grid)
        {
            UpdateGridMovement();
        }
        else
        {
            Vector3 velocity = body.velocity;
            velocity.x = moveInput.x * sideSpeed;
            velocity.z = 0f;
            if (jumpRequested && !switchRequested && grounded)
                velocity.y = Mathf.Sqrt(-2f * Physics.gravity.y * jumpHeight);
            body.velocity = velocity;
        }

        jumpRequested = switchRequested = false;
    }

    private bool TrySwitchMode()
    {
        if (Mode == PlayerMode.Side)
        {
            Vector3 cell = body.position;
            cell.x = Mathf.Round(cell.x);
            cell.z = Mathf.Round(cell.z);
            if (!HasSupport(cell) || HasObstacle(cell, out PrototypePushBox box) || box != null)
                return false;
            body.position = cell;
            body.velocity = Vector3.zero;
            SetMode(PlayerMode.Grid);
        }
        else
        {
            SetMode(PlayerMode.Side);
        }
        return true;
    }

    private void UpdateGridMovement()
    {
        Vector3 direction = queuedGridDirection;
        if (direction == Vector3.zero)
        {
            if (moveInput == Vector2.zero)
            {
                nextGridStep = 0f;
                return;
            }
            direction = Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y)
                ? Mathf.Sign(moveInput.x) * Vector3.right
                : Mathf.Sign(moveInput.y) * Vector3.forward;
        }

        if (Time.fixedTime < nextGridStep)
            return;

        queuedGridDirection = Vector3.zero;
        nextGridStep = Time.fixedTime + gridStepInterval;

        Vector3 cell = body.position + direction;
        if (!HasSupport(cell) || HasObstacle(cell, out PrototypePushBox box))
            return;

        if (box != null && !box.TryPushCell(direction, pushStrength, groundLayers))
            return;

        body.position = cell;
    }

    private bool HasSupport(Vector3 position)
    {
        Vector3 foot = position + capsule.center - Vector3.up * (capsule.height * 0.5f);
        return Physics.Raycast(foot + Vector3.up * 0.08f, Vector3.down, 0.16f,
            groundLayers, QueryTriggerInteraction.Ignore);
    }

    // A single box can occupy the target cell and be pushed. Any other obstacle blocks it.
    private bool HasObstacle(Vector3 position, out PrototypePushBox box)
    {
        box = null;
        Vector3 center = position + capsule.center;
        float halfSegment = capsule.height * 0.5f - capsule.radius;
        Vector3 offset = Vector3.up * halfSegment;
        Collider[] overlaps = Physics.OverlapCapsule(center - offset, center + offset,
            capsule.radius - 0.025f, groundLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider obstacle in overlaps)
        {
            if (!obstacle.TryGetComponent(out PrototypePushBox candidate))
                return true;
            if (box != null)
                return true;
            box = candidate;
        }
        return false;
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
        queuedGridDirection = Vector3.zero;
        nextGridStep = 0f;
        body.useGravity = mode == PlayerMode.Side;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        if (mode == PlayerMode.Side)
            body.constraints |= RigidbodyConstraints.FreezePositionZ;
        else
            body.constraints |= RigidbodyConstraints.FreezePositionY;
    }

    private void OnGUI()
    {
        string controls = Mode == PlayerMode.Side
            ? "SIDE / A D or Left Right / Space: Jump"
            : "GRID / WASD or Arrows / Push orange box";
        GUI.Box(new Rect(12, 12, 360, 65),
            controls + "\nTab: Switch     R: Reset" +
            (Time.time < switchBlockedUntil ? "\nSwitch requires a clear, supported cell." : ""));
    }
}
