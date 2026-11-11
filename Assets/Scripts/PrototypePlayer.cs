using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [SerializeField] private Transform levelExit;
    [SerializeField] private PrototypePressureGate exitGate;
    private bool levelComplete;

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
    private bool undoRequested;
    private bool waitForMoveRelease;
    private bool fallingInGrid;
    private float gridFallSpeed;
    private const float GridFallDistance = 1.5f;

    private enum ActionKind { None, GridWalk, Push, PlatformWalk, Air }

    private sealed class Snapshot
    {
        public PlayerMode Mode;
        public Vector3 Position;
        public Vector2 PlatformVelocity;
        public Vector3[] BoxPositions;
        public PrototypeFallingBlock.State[] FallingBlocks;
        public bool LevelComplete;
    }

    private readonly Stack<Snapshot> history = new Stack<Snapshot>();
    private Snapshot initialState;
    private Snapshot pendingPlatformWalk;
    private float platformXBeforeStep;
    private ActionKind activeAction;
    private PrototypePushBox activePushBox;
    [SerializeField] private BoxCollider[] platformSolids = System.Array.Empty<BoxCollider>();

    [SerializeField] private PrototypeFallingBlock[] fallingBlocks = System.Array.Empty<PrototypeFallingBlock>();

    [SerializeField] private PrototypePressureGate[] gates = System.Array.Empty<PrototypePressureGate>();
    [SerializeField] private float fallLimit = -4f;

    private GameObject platformPhysics;
    private BoxCollider2D[] projectedBoxes;
    private Rigidbody2D platformBody;
    private CapsuleCollider2D platformShape;
    private BoxCollider2D[] projectedSolids;
    [SerializeField] private BoxCollider spikeBed;
    private BoxCollider2D projectedSpikeBed;
    private readonly ContactPoint2D[] platformContacts = new ContactPoint2D[8];

    public PlayerMode Mode { get; private set; }

    private void Start()
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
        initialState = CaptureState();
    }

    private int NextLevelIndex
    {
        get
        {
            int current = SceneManager.GetActiveScene().buildIndex;
            return current >= 0 && current + 1 < SceneManager.sceneCountInBuildSettings
                ? current + 1 : -1;
        }
    }

    private void Update()
    {
        if (levelComplete && Input.GetKeyDown(KeyCode.Return) && NextLevelIndex >= 0)
        {
            SceneManager.LoadScene(NextLevelIndex);
            return;
        }

        moveInput = Vector2.ClampMagnitude(new Vector2(
            Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
        if (waitForMoveRelease && moveInput == Vector2.zero)
            waitForMoveRelease = false;

        if (waitForMoveRelease)
            moveInput = Vector2.zero;
        else if (Mode == PlayerMode.Grid)
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
        switchRequested |= Input.GetKeyDown(KeyCode.RightShift);
        resetRequested |= Input.GetKeyDown(KeyCode.R);
        undoRequested |= Input.GetKeyDown(KeyCode.Z);
    }

    private void FixedUpdate()
    {
        if (Mode == PlayerMode.Platform)
        {
            CopyPlatformPosition();
            FinishPlatformStep();
        }

        if (undoRequested)
        {
            undoRequested = false;
            if (history.Count > 0)
            {
                RestoreState(history.Pop());
                return;
            }
        }

        if (resetRequested)
        {
            if (!fallingInGrid)
            {
                Snapshot before = CaptureState();
                if (!MatchesInitialState(before))
                    history.Push(before);
            }
            RestoreState(initialState);
            return;
        }

        if (levelComplete)
            return;

        if (fallingInGrid)
        {
            gridFallSpeed += Physics.gravity.y * Time.fixedDeltaTime;
            body.position += Vector3.up * (gridFallSpeed * Time.fixedDeltaTime);
            if (body.position.y <= spawnPosition.y - GridFallDistance)
                RespawnAfterFailure();
            return;
        }

        if (Mode == PlayerMode.Platform && HasPlatformFailure())
        {
            RespawnAfterFailure();
            return;
        }

        if (TryCompleteLevel())
            return;

        if (switchRequested)
        {
            history.Push(CaptureState());
            EndAction();
            PlayerMode nextMode = Mode == PlayerMode.Grid ? PlayerMode.Platform : PlayerMode.Grid;
            if (nextMode == PlayerMode.Grid)
            {
                Vector3 position = body.position;
                body.position = new Vector3(Mathf.Round(position.x), position.y, Mathf.Round(position.z));
            }
            SetMode(nextMode);
            if (Mode == PlayerMode.Grid)
                CheckGridSupport();
            jumpRequested = switchRequested = false;
            return;
        }

        if (Mode == PlayerMode.Grid)
            UpdateGridMovement();
        else
            UpdatePlatformMovement();

        jumpRequested = false;
    }

    private bool HasPlatformFailure()
    {
        if (platformBody.position.y < fallLimit ||
            (projectedSpikeBed != null && platformShape.IsTouching(projectedSpikeBed)))
            return true;

        int count = platformShape.GetContacts(platformContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = platformContacts[i];
            if (contact.normal.y > -0.5f)
                continue;

            foreach (PrototypeFallingBlock block in fallingBlocks)
                if (block.FellOnto(contact))
                    return true;
        }
        return false;
    }

    private void RespawnAfterFailure()
    {
        // Undo still targets the action that led to the failure.
        RestoreState(initialState);
    }

    private void CreatePlatformPhysics()
    {
        platformPhysics = new GameObject("Top View Physics");
        platformPhysics.SetActive(false);
        projectedSolids = new BoxCollider2D[platformSolids.Length];
        for (int i = 0; i < platformSolids.Length; i++)
        {
            BoxCollider solid = platformSolids[i];
            if (solid == null)
                continue;
            var projection = new GameObject(solid.name + " footprint");
            projection.transform.SetParent(platformPhysics.transform);
            projectedSolids[i] = projection.AddComponent<BoxCollider2D>();
            if (solid == spikeBed)
                projectedSpikeBed = projectedSolids[i];
        }


        projectedBoxes = new BoxCollider2D[boxes.Length];
        for (int i = 0; i < boxes.Length; i++)
        {
            var projection = new GameObject(boxes[i].name + " footprint");
            projection.transform.SetParent(platformPhysics.transform);
            projectedBoxes[i] = projection.AddComponent<BoxCollider2D>();
        }

        foreach (PrototypeFallingBlock block in fallingBlocks)
            block.CreateFootprint(platformPhysics.transform);

        var proxy = new GameObject("Player footprint");
        proxy.transform.SetParent(platformPhysics.transform);
        platformBody = proxy.AddComponent<Rigidbody2D>();
        platformBody.freezeRotation = true;
        platformBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        platformShape = proxy.AddComponent<CapsuleCollider2D>();
        platformShape.size = new Vector2(0.64f, 0.8f);
    }

    private Snapshot CaptureState()
    {
        var state = new Snapshot
        {
            Mode = Mode,
            Position = body.position,
            PlatformVelocity = Mode == PlayerMode.Platform ? platformBody.velocity : Vector2.zero,
            BoxPositions = new Vector3[boxes.Length],
            FallingBlocks = new PrototypeFallingBlock.State[fallingBlocks.Length],
            LevelComplete = levelComplete
        };
        for (int i = 0; i < boxes.Length; i++)
            state.BoxPositions[i] = boxes[i].Position;
        for (int i = 0; i < fallingBlocks.Length; i++)
            state.FallingBlocks[i] = fallingBlocks[i].CurrentState;
        return state;
    }

    private bool MatchesInitialState(Snapshot state)
    {
        if (state.LevelComplete || state.Mode != initialState.Mode || state.Position != initialState.Position)
            return false;
        for (int i = 0; i < boxes.Length; i++)
            if (state.BoxPositions[i] != initialState.BoxPositions[i])
                return false;
        for (int i = 0; i < fallingBlocks.Length; i++)
            if (state.FallingBlocks[i].Position != initialState.FallingBlocks[i].Position ||
                state.FallingBlocks[i].Velocity != initialState.FallingBlocks[i].Velocity)
                return false;
        return true;
    }

    private void RestoreState(Snapshot state)
    {
        EndAction();
        resetRequested = switchRequested = jumpRequested = false;
        waitForMoveRelease |= moveInput != Vector2.zero;
        for (int i = 0; i < boxes.Length; i++)
            boxes[i].Position = state.BoxPositions[i];

        for (int i = 0; i < fallingBlocks.Length; i++)
            fallingBlocks[i].RestoreWorldPosition(state.FallingBlocks[i]);

        body.position = state.Position;
        Physics.SyncTransforms();
        SetMode(state.Mode);
        levelComplete = state.LevelComplete;
        if (state.Mode == PlayerMode.Platform)
        {
            platformBody.velocity = state.PlatformVelocity;
            if (levelComplete)
                StopPlatformPhysics();
        }
    }

    private bool TryCompleteLevel()
    {
        if (levelExit == null || (exitGate != null && !exitGate.IsOpen))
            return false;

        Vector3 position = body.position;
        Vector3 offset = position - levelExit.position;
        if (Mathf.Abs(offset.x) >= 0.4f || Mathf.Abs(offset.z) >= 0.4f ||
            (Mode == PlayerMode.Grid && !HasSupport(position)))
            return false;

        EndAction();
        levelComplete = true;
        switchRequested = jumpRequested = false;
        if (Mode == PlayerMode.Platform)
            StopPlatformPhysics();
        return true;
    }

    private void StopPlatformPhysics()
    {
        platformBody.simulated = false;
        foreach (PrototypeFallingBlock block in fallingBlocks)
            block.StopPhysics();
    }

    private void EndAction()
    {
        activeAction = ActionKind.None;
        activePushBox = null;
        pendingPlatformWalk = null;
    }

    private bool IsPlatformGrounded()
    {
        if (platformBody.velocity.y > 0.05f)
            return false;
        int count = platformShape.GetContacts(platformContacts);
        for (int i = 0; i < count; i++)
            if (platformContacts[i].normal.y >= 0.5f)
                return true;
        return false;
    }

    private void FinishPlatformStep()
    {
        bool grounded = IsPlatformGrounded();
        bool moved = Mathf.Abs(platformBody.position.x - platformXBeforeStep) > 0.00001f;
        if (pendingPlatformWalk != null)
        {
            if (moved)
            {
                history.Push(pendingPlatformWalk);
                activeAction = grounded ? ActionKind.PlatformWalk : ActionKind.Air;
            }
            pendingPlatformWalk = null;
        }
        else if (activeAction == ActionKind.PlatformWalk)
        {
            if (!grounded)
                activeAction = ActionKind.Air;
            else if (!moved)
                EndAction();
        }
        else if (activeAction == ActionKind.Air && grounded)
        {
            EndAction();
        }
    }

    private void UpdatePlatformMovement()
    {
        Vector2 velocity = platformBody.velocity;
        if (jumpRequested && IsPlatformGrounded())
        {
            history.Push(CaptureState());
            activeAction = ActionKind.Air;
            velocity.y = Mathf.Sqrt(-2f * Physics2D.gravity.y * jumpHeight);
        }
        else if (moveInput.x == 0f && activeAction == ActionKind.PlatformWalk)
        {
            EndAction();
        }
        else if (moveInput.x != 0f && activeAction == ActionKind.None)
        {
            // Confirm actual displacement after the physics step so a blocked input adds no history.
            pendingPlatformWalk = CaptureState();
        }

        platformXBeforeStep = platformBody.position.x;
        velocity.x = moveInput.x * sideSpeed;
        platformBody.velocity = velocity;
        foreach (PrototypeFallingBlock block in fallingBlocks)
            block.RecordBeforePhysicsStep();
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
        if (!levelComplete)
            foreach (PrototypeFallingBlock block in fallingBlocks)
                block.SyncFromPhysics();
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
                EndAction();
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
        Vector3 cell = position + direction;
        if (!HasSupport(position) || HasObstacle(cell, out PrototypePushBox box))
        {
            EndAction();
            return;
        }

        ActionKind kind = box == null ? ActionKind.GridWalk : ActionKind.Push;
        Snapshot before = kind != activeAction || box != activePushBox ? CaptureState() : null;
        if (box != null && !box.TryPushCell(direction, pushStrength, groundLayers))
        {
            EndAction();
            return;
        }

        if (before != null)
            history.Push(before);
        activeAction = kind;
        activePushBox = box;
        body.position = cell;
        if (box != null)
        {
            RefreshGates();
            Physics.SyncTransforms();
        }
        CheckGridSupport();
    }

    private void CheckGridSupport()
    {
        if (HasSupport(body.position))
            return;

        fallingInGrid = true;
        capsule.enabled = false;
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

    private void RefreshGates()
    {
        foreach (PrototypePressureGate gate in gates)
            gate.Refresh(boxes);
    }

    private static void UpdateProjection(BoxCollider source, BoxCollider2D projection)
    {
        projection.enabled = source.enabled;
        if (!source.enabled)
            return;
        Bounds bounds = source.bounds;
        projection.transform.position = new Vector3(bounds.center.x, bounds.center.z, 0f);
        projection.size = new Vector2(bounds.size.x, bounds.size.z);
    }

    private void SetMode(PlayerMode mode)
    {
        Mode = mode;
        fallingInGrid = false;
        gridFallSpeed = 0f;
        queuedGridDirection = Vector3.zero;
        nextGridStep = 0f;

        bool platformMode = mode == PlayerMode.Platform;
        capsule.enabled = !platformMode;
        platformPhysics.SetActive(platformMode);
        RefreshGates();
        // Publish the final falling-block footprints before any grid collision query.
        Physics.SyncTransforms();
        if (!platformMode)
            return;
        for (int i = 0; i < platformSolids.Length; i++)
            if (projectedSolids[i] != null)
                UpdateProjection(platformSolids[i], projectedSolids[i]);
        for (int i = 0; i < boxes.Length; i++)
        {
            BoxCollider shape = boxes[i].GetComponent<BoxCollider>();
            if (System.Array.IndexOf(platformSolids, shape) >= 0)
                projectedBoxes[i].enabled = false;
            else
                UpdateProjection(shape, projectedBoxes[i]);
        }
        platformBody.simulated = true;
        platformBody.position = new Vector2(body.position.x, body.position.z);
        platformBody.velocity = Vector2.zero;
        foreach (PrototypeFallingBlock block in fallingBlocks)
            block.ResumePhysics();
        Physics2D.SyncTransforms();
    }

    private static readonly Color HudSurface = new Color32(24, 32, 39, 245);
    private static readonly Color HudMuted = new Color32(200, 213, 219, 255);
    private static readonly Color HudAccent = new Color32(163, 230, 192, 255);
    private GUIStyle hudText;
    private GUIStyle keyLabel;
    private GUIStyle undoButton;
    private GUIStyle completeTitle;
    private GUIStyle nextButton;

    private void InitializeHudStyles()
    {
        hudText = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
        hudText.normal.textColor = HudMuted;
        keyLabel = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        keyLabel.normal.textColor = Color.white;
        undoButton = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        completeTitle = new GUIStyle(keyLabel) { fontSize = 36 };
        completeTitle.normal.textColor = HudAccent;
        nextButton = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
    }

    private void OnGUI()
    {
        if (hudText == null)
            InitializeHudStyles();

        const float hudWidth = 556f;
        float scale = Mathf.Min(
            Mathf.Clamp(Screen.height / 720f, 0.8f, 1.75f),
            Mathf.Min(Screen.width / (hudWidth + 32f), Screen.height / 334f));
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        try
        {
            DrawControls(hudWidth);
            if (levelComplete)
                DrawCompletion(Screen.width / scale, Screen.height / scale);
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void DrawControls(float width)
    {
        string movement = Mode == PlayerMode.Platform
            ? "TOP VIEW   •   A / D or Left / Right to move   •   Space to jump"
            : "GRID   •   WASD or Arrow keys to move";
        DrawPanel(new Rect(16, 16, width, 84));
        GUI.Label(new Rect(32, 28, width - 32, 24), movement, hudText);

        const float y = 60;
        DrawKey(new Rect(32, y, 90, 28), "Right Shift");
        GUI.Label(new Rect(130, y + 3, 108, 24), "Switch view", hudText);
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && history.Count > 0;
        if (GUI.Button(new Rect(244, y, 30, 28), "Z", undoButton))
            undoRequested = true;
        GUI.enabled = wasEnabled;
        GUI.Label(new Rect(282, y + 3, 116, 24), "Undo last step", hudText);
        DrawKey(new Rect(404, y, 30, 28), "R");
        GUI.Label(new Rect(442, y + 3, 114, 24), "Restart level", hudText);
    }

    private void DrawKey(Rect rect, string key)
    {
        DrawFill(rect, new Color32(54, 70, 80, 255));
        GUI.Label(rect, key, keyLabel);
    }

    private void DrawCompletion(float screenWidth, float screenHeight)
    {
        int nextLevel = NextLevelIndex;
        float height = nextLevel >= 0 ? 168f : 100f;
        Rect card = new Rect((screenWidth - 480f) / 2f,
            Mathf.Max(150f, (screenHeight - height) / 2f), 480f, height);
        DrawPanel(card);
        GUI.Label(new Rect(card.x, card.y + 14, card.width, 62),
            nextLevel >= 0 ? "Level Complete!" : "All Levels Complete!", completeTitle);
        if (nextLevel >= 0 && GUI.Button(
            new Rect(card.x + 64, card.y + 84, card.width - 128, 60),
            "Next Level  [Enter]", nextButton))
            SceneManager.LoadScene(nextLevel);
    }

    private static void DrawPanel(Rect rect)
    {
        DrawFill(rect, HudSurface);
        DrawFill(new Rect(rect.x, rect.y, rect.width, 3), HudAccent);
    }

    private static void DrawFill(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }
}
