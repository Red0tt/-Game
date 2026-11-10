using UnityEngine;

public enum PlayerMode
{
    Platform,
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
    [SerializeField] private BoxCollider[] platformSolids = System.Array.Empty<BoxCollider>();

    private GameObject platformPhysics;
    private Rigidbody2D platformBody;
    private CapsuleCollider2D platformShape;
    private BoxCollider2D[] projectedSolids;
    private readonly ContactPoint2D[] platformContacts = new ContactPoint2D[8];

    public PlayerMode Mode { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        spawnPosition = body.position;
        boxes = FindObjectsOfType<PrototypePushBox>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        CreatePlatformPhysics();
        SetMode(PlayerMode.Grid);
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
        if (resetRequested || (Mode == PlayerMode.Platform && platformBody.position.y < -4f))
        {
            body.position = spawnPosition;
            foreach (PrototypePushBox box in boxes)
                box.ResetPosition();
            SetMode(PlayerMode.Grid);
            resetRequested = switchRequested = jumpRequested = false;
            return;
        }

        if (switchRequested)
        {
            if (Mode == PlayerMode.Platform)
                CopyPlatformPosition();
            SetMode(Mode == PlayerMode.Grid ? PlayerMode.Platform : PlayerMode.Grid);
            jumpRequested = switchRequested = false;
            return;
        }

        if (Mode == PlayerMode.Grid)
            UpdateGridMovement();
        else
            UpdatePlatformMovement();

        jumpRequested = false;
    }

    private void CreatePlatformPhysics()
    {
        platformPhysics = new GameObject("Top View Physics");
        platformPhysics.SetActive(false);
        projectedSolids = new BoxCollider2D[platformSolids.Length];
        for (int i = 0; i < platformSolids.Length; i++)
        {
            var projection = new GameObject(platformSolids[i].name + " footprint");
            projection.transform.SetParent(platformPhysics.transform);
            projectedSolids[i] = projection.AddComponent<BoxCollider2D>();
        }

        var proxy = new GameObject("Player footprint");
        proxy.transform.SetParent(platformPhysics.transform);
        platformBody = proxy.AddComponent<Rigidbody2D>();
        platformBody.freezeRotation = true;
        platformBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        platformShape = proxy.AddComponent<CapsuleCollider2D>();
        platformShape.size = new Vector2(0.64f, 0.8f);
    }

    private void UpdatePlatformMovement()
    {
        Vector2 velocity = platformBody.velocity;
        velocity.x = moveInput.x * sideSpeed;
        if (jumpRequested && velocity.y <= 0.05f)
        {
            int count = platformShape.GetContacts(platformContacts);
            for (int i = 0; i < count; i++)
            {
                if (platformContacts[i].normal.y < 0.5f)
                    continue;
                velocity.y = Mathf.Sqrt(-2f * Physics2D.gravity.y * jumpHeight);
                break;
            }
        }
        platformBody.velocity = velocity;
    }

    private void LateUpdate()
    {
        if (Mode == PlayerMode.Platform)
            CopyPlatformPosition();
    }

    private void CopyPlatformPosition()
    {
        Vector2 position = platformBody.position;
        body.position = new Vector3(position.x, spawnPosition.y, position.y);
    }

    private void OnDestroy()
    {
        if (platformPhysics != null)
            Destroy(platformPhysics);
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

        Vector3 position = body.position;
        Vector3 cell = new Vector3(Mathf.Round(position.x), position.y, Mathf.Round(position.z));
        // An airborne switch can leave the player between centers. Move to the
        // first center ahead on the requested axis.
        float distanceToCenter = Vector3.Dot(cell - position, direction);
        if (distanceToCenter <= 0.001f)
            cell += direction;
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

    private void SetMode(PlayerMode mode)
    {
        Mode = mode;
        queuedGridDirection = Vector3.zero;
        nextGridStep = 0f;

        bool platformMode = mode == PlayerMode.Platform;
        capsule.enabled = !platformMode;
        platformPhysics.SetActive(platformMode);
        if (!platformMode)
            return;

        Physics.SyncTransforms();
        for (int i = 0; i < platformSolids.Length; i++)
        {
            Bounds bounds = platformSolids[i].bounds;
            projectedSolids[i].transform.position = new Vector3(bounds.center.x, bounds.center.z, 0f);
            projectedSolids[i].size = new Vector2(bounds.size.x, bounds.size.z);
        }
        platformBody.position = new Vector2(body.position.x, body.position.z);
        platformBody.velocity = Vector2.zero;
        Physics2D.SyncTransforms();
    }

    private void OnGUI()
    {
        string controls = Mode == PlayerMode.Platform
            ? "TOP VIEW / A D or Left Right / Space: Jump"
            : "GRID / WASD or Arrows / Push orange box";
        GUI.Box(new Rect(12, 12, 360, 50), controls + "\nTab: Switch     R: Reset");
    }
}
