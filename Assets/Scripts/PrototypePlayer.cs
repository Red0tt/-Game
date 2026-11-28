using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum PlayerMode
{
    Platform,
    Grid
}

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed partial class PrototypePlayer : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float sideSpeed = 4f;
    [SerializeField, Min(0.05f)] private float gridStepInterval = 0.18f;
    [SerializeField, Min(0.1f)] private float jumpHeight = 1.2f;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private Transform levelExit;
    [SerializeField] private PrototypePressureGate exitGate;
    [SerializeField] private bool requireAllGatesAtExit;
    private bool levelComplete;
    private PrototypeWaterLevel waterLevel;

    [SerializeField, Min(0f)] private float pushStrength = 2f;

    [SerializeField] private bool enableFanhuaTest;
    [SerializeField] private bool fanhuaLab;
    [SerializeField,Range(0,3)] private int initialFanhuaPalette;
    [SerializeField] private Vector2Int fanhuaAnchor = new Vector2Int(-2, -1);
    [SerializeField] private Vector2Int fanhuaGridOrigin = new Vector2Int(-5, -3);
    [SerializeField] private Vector2Int fanhuaGridSize = new Vector2Int(11, 7);
    [SerializeField] private Vector2Int[] additionalFanhuaAnchors = System.Array.Empty<Vector2Int>();
    private PrototypeFanhua[] flowers = System.Array.Empty<PrototypeFanhua>();
    private PrototypeFanhua fanhua => flowers.Length == 0 ? null : flowers[0];
    private int pullFlower = -1;

    private bool ExitReady => (exitGate == null || exitGate.IsOpen) &&
        (!requireAllGatesAtExit || System.Array.TrueForAll(gates, gate => gate.IsOpen));
    private Vector2Int pullDirection;
    private bool fanhuaInteractionRequested;
    private PrototypeFallingBlock activeFallingPush;
    private Vector3 activeGridDirection;

    private PrototypePushBox[] boxes;
    private float nextGridStep;
    private Rigidbody body;
    private CapsuleCollider capsule;
    private Vector3 spawnPosition;
    private RectInt gridBounds;
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

    private enum ActionKind { None, GridWalk, Push, Pull, PlatformWalk, Air }

    private sealed class Snapshot
    {
        public PlayerMode Mode;
        public Vector3 Position;
        public Vector2 PlatformVelocity;
        public Vector3[] BoxPositions;
        public PrototypeFallingBlock.State[] FallingBlocks;
        public PrototypeFanhua.State[] Fanhua;
        public int PullFlower;
        public Vector2Int PullDirection;
        public bool LevelComplete;
        public PrototypeWaterLevel.State Water;
        public bool FallingInGrid;
        public float GridFallSpeed;
        public int PracticedFanhuaDirections;
    }

    private readonly Stack<Snapshot> history = new Stack<Snapshot>();
    private Snapshot initialState;
    private Snapshot pendingPlatformWalk;
    private float platformXBeforeStep;
    private float previousPlatformInput;
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
    private PhysicsMaterial2D platformPlayerMaterial;
    private BoxCollider2D[] projectedSolids;
    [SerializeField] private BoxCollider spikeBed;
    private BoxCollider2D projectedSpikeBed;
    private readonly ContactPoint2D[] platformContacts = new ContactPoint2D[8];

    internal PrototypeFallingBlock[] WaterSceneBlocks => fallingBlocks;
    internal BoxCollider[] WaterStaticSolids => platformSolids;
    internal RectInt WaterGrid => gridBounds;
    internal void RefreshWaterProjections()
    {
        if(projectedSolids==null) return;
        for(int i=0;i<platformSolids.Length;i++)
            if(projectedSolids[i]!=null) UpdateProjection(platformSolids[i],projectedSolids[i]);
    }
    public PlayerMode Mode { get; private set; }
    internal void RequestLabModeSwitch(){switchRequested=true;}
    internal void ConfigureLabPalette(int value){initialFanhuaPalette=Mathf.Clamp(value,0,3);}

    private void Start()
    {
        waterLevel = GetComponent<PrototypeWaterLevel>();
        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        spawnPosition = body.position;
        boxes = FindObjectsOfType<PrototypePushBox>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
        CreatePlatformPhysics();
        if (waterLevel != null) waterLevel.Initialize(platformPhysics.transform);
        if (enableFanhuaTest)
        {
            flowers = new PrototypeFanhua[1 + additionalFanhuaAnchors.Length];
            var grid = new RectInt(fanhuaGridOrigin, fanhuaGridSize);
            var material = GetComponentInChildren<Renderer>()?.sharedMaterial;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_Mark"))
                {
                    material = renderer.sharedMaterial;
                    break;
                }
            }
            for (int i = 0; i < flowers.Length; i++)
                flowers[i] = new PrototypeFanhua(transform.parent, platformPhysics.transform,
                    material, i == 0 ? fanhuaAnchor : additionalFanhuaAnchors[i - 1], grid);
        }
        foreach(var flower in flowers)for(int palette=0;palette<initialFanhuaPalette;palette++)flower.TogglePalette();
        gridBounds = fanhua != null ? fanhua.Grid : FindSceneGridBounds();
        SetMode(PlayerMode.Grid);
        initialState = CaptureState();
        levelEnteredAt = Time.unscaledTime;
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
        if (fanhua != null && Mode == PlayerMode.Grid)
            fanhuaInteractionRequested |= Input.GetKeyDown(KeyCode.Q);
        jumpRequested |= Input.GetKeyDown(KeyCode.Space);
        if (!teachFanhuaDirections)
            switchRequested |= Input.GetKeyDown(KeyCode.RightShift);
        resetRequested |= Input.GetKeyDown(KeyCode.R);
        undoRequested |= Input.GetKeyDown(KeyCode.Z);
    }

    private void FixedUpdate()
    {
        if (Mode == PlayerMode.Platform)
        {
            if (waterLevel != null && !levelComplete && !undoRequested && !resetRequested){waterLevel.Advance(Time.fixedDeltaTime, flowers);RefreshGates();}
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
            Snapshot before = CaptureState();
            if (!MatchesInitialState(before))
                history.Push(before);
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

        if (fanhua != null && Mode == PlayerMode.Grid && fanhuaInteractionRequested)
        {
            fanhuaInteractionRequested = false;
            var cell = Vector2Int.RoundToInt(new Vector2(body.position.x, body.position.z));
            int index = FindFanhuaInteractionTarget(cell);
            if (index >= 0 && flowers[index].Axis == 0 && (waterLevel == null || waterLevel.CanEditFlower(index)))
            {
                PrototypeFanhua flower = flowers[index];
                history.Push(CaptureState());
                EndAction();
                flower.TogglePalette();

                RefreshGates();
                Physics.SyncTransforms();
                return;
            }
        }

        if (switchRequested)
        {
            // Consume the edge even when entry is rejected. Otherwise every fixed frame
            // retries the same invalid transition instead of processing swimming input.
            switchRequested = false;
            Snapshot before = CaptureState();
            PlayerMode nextMode = Mode == PlayerMode.Grid ? PlayerMode.Platform : PlayerMode.Grid;
            if (nextMode == PlayerMode.Grid)
            {
                Vector3 position = body.position;
                body.position = new Vector3(Mathf.Round(position.x), spawnPosition.y, Mathf.Round(position.z));
                if (waterLevel != null && !HasSupport(body.position))
                {
                    body.position = position;
                    waterLevel.RejectReturn(false);
                    jumpRequested = false;
                    return;
                }
            }
            EndAction();
            SetMode(nextMode);
            bool entryValid = true;
            if (nextMode == PlayerMode.Grid)
            {
                var cell = new Vector2Int((int)body.position.x, (int)body.position.z);
                if (waterLevel != null)
                {
                    // No nearest-cell relocation for water or for the player.
                    capsule.enabled = false;
                    entryValid = waterLevel.TryMapBlocksToGrid(fallingBlocks, flowers, groundLayers, gridBounds, cell)
                        && HasSupport(body.position) && !HasGridOverlap(body.position);
                    capsule.enabled = true;
                }
                else
                    entryValid = PrototypeFanhua.TryPlaceFallingBlocks(fallingBlocks, flowers, groundLayers, gridBounds, cell)
                        && TryResolveGridEntry();
            }
            if (!entryValid)
            {
                RestoreState(before);
                // A rejected request must not require releasing held swim keys.
                waitForMoveRelease = false;
                if (waterLevel != null) waterLevel.RejectReturn(true);
                return;
            }

            history.Push(before);
            if (Mode == PlayerMode.Grid)
                CheckGridSupport();
            jumpRequested = false;
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
        if ((waterLevel != null && waterLevel.IsDangerous(platformShape.bounds)) ||
            System.Array.Exists(flowers, flower => flower.IsDangerous(platformShape, platformContacts)) ||
            platformBody.position.y < fallLimit ||
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
        // Holding movement into a wall must not create friction that cancels gravity.
        platformPlayerMaterial = new PhysicsMaterial2D("Player Slide")
        {
            friction = 0f,
            bounciness = 0f
        };
        platformShape.sharedMaterial = platformPlayerMaterial;
    }

    private Snapshot CaptureState()
    {
        var state = new Snapshot
        {
            Mode = Mode,
            Position = body.position,
            PlatformVelocity = Mode == PlayerMode.Platform ? platformBody.velocity : Vector2.zero,
            BoxPositions = new Vector3[boxes.Length],
            Fanhua = System.Array.ConvertAll(flowers, flower => flower.Capture()),
            PullFlower = pullFlower,
            PullDirection = pullDirection,
            FallingBlocks = new PrototypeFallingBlock.State[fallingBlocks.Length],
            FallingInGrid = fallingInGrid,
            GridFallSpeed = gridFallSpeed,
            PracticedFanhuaDirections = practicedFanhuaDirections,
            Water = waterLevel != null ? waterLevel.Capture() : null,
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
        if (waterLevel != null && state.Water != null && state.Water.Fluid != null) return false;
        if (state.PracticedFanhuaDirections != 0 || System.Array.Exists(flowers, flower => !flower.IsInitial))
            return false;
        if (state.FallingInGrid || state.LevelComplete || state.Mode != initialState.Mode || state.Position != initialState.Position)
            return false;
        for (int i = 0; i < boxes.Length; i++)
            if (state.BoxPositions[i] != initialState.BoxPositions[i])
                return false;
        for (int i = 0; i < fallingBlocks.Length; i++)
            if (state.FallingBlocks[i].Position != initialState.FallingBlocks[i].Position ||
                state.FallingBlocks[i].Velocity != initialState.FallingBlocks[i].Velocity ||
                state.FallingBlocks[i].Lost != initialState.FallingBlocks[i].Lost ||
                state.FallingBlocks[i].Plugged != initialState.FallingBlocks[i].Plugged ||
                state.FallingBlocks[i].Water != initialState.FallingBlocks[i].Water ||
                state.FallingBlocks[i].Gray != initialState.FallingBlocks[i].Gray)
                return false;
        return true;
    }

    private void RestoreState(Snapshot state)
    {
        EndAction();
        resetRequested = switchRequested = jumpRequested = fanhuaInteractionRequested = false;
        practicedFanhuaDirections = state.PracticedFanhuaDirections;
        pullDirection = state.PullDirection;
        pullFlower = state.PullFlower;
        for (int i = 0; i < flowers.Length; i++)
            flowers[i].Restore(state.Fanhua[i]);
        waitForMoveRelease |= moveInput != Vector2.zero;
        for (int i = 0; i < boxes.Length; i++)
            boxes[i].Position = state.BoxPositions[i];

        for (int i = 0; i < fallingBlocks.Length; i++)
            fallingBlocks[i].RestoreWorldPosition(state.FallingBlocks[i]);

        if (waterLevel != null) waterLevel.Restore(state.Water);
        body.position = state.Position;
        Physics.SyncTransforms();
        SetMode(state.Mode);
        levelComplete = state.LevelComplete;
        fallingInGrid = state.FallingInGrid;
        gridFallSpeed = state.GridFallSpeed;
        if (fallingInGrid)
            capsule.enabled = false;
        if (state.Mode == PlayerMode.Platform)
        {
            platformBody.velocity = state.PlatformVelocity;
            if (levelComplete)
                StopPlatformPhysics();
        }
    }

    private bool TryCompleteLevel()
    {
        if (levelExit == null || !ExitReady)
            return false;

        Vector3 position = body.position;
        Vector3 offset = position - levelExit.position;
        if (Mathf.Abs(offset.x) >= 0.4f || Mathf.Abs(offset.z) >= 0.4f ||
            (Mode == PlayerMode.Grid && !HasSupport(position)))
            return false;

        EndAction();
        history.Push(CaptureState());
        levelComplete = true;
        if(gameObject.scene.name.StartsWith("Route")){PlayerPrefs.SetInt("Fanhua.Completed."+gameObject.scene.name,1);PlayerPrefs.Save();}
        switchRequested = jumpRequested = false;
        if (Mode == PlayerMode.Platform)
            StopPlatformPhysics();
        return true;
    }

    private void StopPlatformPhysics()
    {
        platformBody.simulated = false;
        foreach (PrototypeFanhua flower in flowers)
            flower.StopPhysics();
        foreach (PrototypeFallingBlock block in fallingBlocks)
            block.StopPhysics();
    }

    private void EndAction()
    {
        activeAction = ActionKind.None;
        activePushBox = null;
        activeFallingPush = null;
        activeGridDirection = Vector3.zero;
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
        if (waterLevel != null && waterLevel.Swim(platformBody, moveInput, jumpRequested))
        {
            if (activeAction == ActionKind.None) history.Push(CaptureState());
            activeAction = ActionKind.Air;
            platformXBeforeStep = platformBody.position.x;
            return;
        }
        platformBody.gravityScale = 1f;
        Vector2 velocity = platformBody.velocity;
        if (jumpRequested && IsPlatformGrounded())
        {
            history.Push(CaptureState());
            activeAction = ActionKind.Air;
            velocity.y = Mathf.Sqrt(-2f * Physics2D.gravity.y * jumpHeight);
        }
        else if (activeAction == ActionKind.Air && !waitForMoveRelease &&
            Mathf.Abs(moveInput.x - previousPlatformInput) > 0.01f)
        {
            history.Push(CaptureState());
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
        previousPlatformInput = moveInput.x;
        velocity.x = moveInput.x * sideSpeed;
        platformBody.velocity = velocity;
        foreach (PrototypeFanhua flower in flowers)
            flower.RecordBeforePhysicsStep();
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
        {
            if (waterLevel != null) { waterLevel.Step(platformBody, flowers); return; }
            float bottomEdge = fanhua != null ? fanhua.Grid.yMin - 0.5f : fallLimit;
            foreach (PrototypeFanhua flower in flowers)
            foreach (PrototypeFallingBlock block in flower.BlueBlocks)
                PrototypeFanhua.SyncFallingBlock(block, flowers, bottomEdge);
            foreach (PrototypeFallingBlock block in fallingBlocks)
                PrototypeFanhua.SyncFallingBlock(block, flowers, bottomEdge);
        }
    }

    private void OnDestroy()
    {
        foreach(var flower in flowers)flower.Dispose();
        if (platformPhysics != null)
            Destroy(platformPhysics);
        if (platformPlayerMaterial != null)
            Destroy(platformPlayerMaterial);
        if (tutorialFont != null)
            Destroy(tutorialFont);
        if (completionFont != null)
            Destroy(completionFont);
        if (controlsFont != null)
            Destroy(controlsFont);
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
        if(Input.GetKey(KeyCode.E))
        {
            var playerCell=Vector2Int.RoundToInt(new Vector2(position.x,position.z));
            var editStep=new Vector2Int((int)direction.x,(int)direction.z);
            if(!HasSupport(position+direction)){EndAction();return;}
            for(int i=0;i<flowers.Length;i++)
            {
                if(flowers[i].DistanceTo(playerCell)>1)continue;
                if(waterLevel!=null&&!waterLevel.CanEditFlower(i))break;
                if(!flowers[i].TryShiftStrip(playerCell,editStep,groundLayers,capsule,false))continue;
                var beforeEdit=CaptureState();
                if(flowers[i].TryShiftStrip(playerCell,editStep,groundLayers,capsule,true)){history.Push(beforeEdit);body.position=position+direction;}
                EndAction();RefreshGates();break;
            }
            return;
        }
        Vector3 cell = position + direction;
        var pushCell=Vector2Int.RoundToInt(new Vector2(position.x,position.z));
        var pushStep=new Vector2Int((int)direction.x,(int)direction.z);
        if(HasSupport(position)&&HasSupport(cell)&&!HasObstacle(cell,out _,out _))
            for(int i=0;i<flowers.Length;i++)
            {
                if(!flowers[i].TryPullBase(pushCell,pushStep,groundLayers,capsule,false))continue;
                var beforeBasePull=CaptureState();
                if(flowers[i].TryPullBase(pushCell,pushStep,groundLayers,capsule,true))
                {history.Push(beforeBasePull);body.position=cell;pullFlower=-1;pullDirection=Vector2Int.zero;EndAction();RefreshGates();Physics.SyncTransforms();return;}
            }
        if(HasSupport(position))
            for(int i=0;i<flowers.Length;i++)
            {
                if(!flowers[i].TryPush(pushCell,pushStep,groundLayers,capsule,false))continue;
                if(waterLevel!=null&&!waterLevel.CanEditFlower(i)){EndAction();return;}
                var beforePush=CaptureState();
                if(flowers[i].TryPush(pushCell,pushStep,groundLayers,capsule,true))
                {
                    history.Push(beforePush);body.position=cell;pullFlower=-1;pullDirection=Vector2Int.zero;
                    EndAction();RefreshGates();Physics.SyncTransforms();CheckGridSupport();return;
                }
            }
        if (!HasSupport(position) || HasObstacle(cell, out PrototypePushBox box, out PrototypeFallingBlock falling))
        {
            EndAction();
            return;
        }

        var step = new Vector2Int((int)direction.x, (int)direction.z);
        var currentCell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
        bool besideTip = pullFlower >= 0 && flowers[pullFlower].IsAtPullEdge(currentCell, pullDirection);
        bool detach = pullFlower >= 0 && (!besideTip || step != pullDirection || box != null || falling != null);
        int selected = pullFlower;
        if (selected < 0 && box == null && falling == null)
            selected = System.Array.FindIndex(flowers, flower => flower.IsAtPullEdge(currentCell, step));
        bool pulls = selected >= 0 && !detach;
        PrototypeFanhua pulled = pulls ? flowers[selected] : null;
        Vector2Int nextEnd = pulls ? pulled.EndFor(currentCell, step) : currentCell;
        if (pulls && waterLevel != null && !waterLevel.CanEditFlower(selected))
        { EndAction(); return; }
        if (pulls && (!HasSupport(cell) ||
            !pulled.CanSetEnd(nextEnd, groundLayers, capsule)))
        {
            EndAction();
            return;
        }

        ActionKind kind = pulls ? ActionKind.Pull :
            box == null && falling == null ? ActionKind.GridWalk : ActionKind.Push;
        bool startsAction = kind != activeAction || box != activePushBox ||
            falling != activeFallingPush || direction != activeGridDirection || detach;
        Snapshot before = startsAction ? CaptureState() : null;
        if ((box != null && !box.TryPushCell(direction, pushStrength, groundLayers)) ||
            (falling != null && !falling.TryPushCell(direction, pushStrength, groundLayers, fanhua?.Grid)))
        {
            EndAction();
            return;
        }

        if (before != null)
            history.Push(before);
        activeAction = kind;
        activePushBox = box;
        activeFallingPush = falling;
        activeGridDirection = direction;
        body.position = cell;
        if (detach)
        {
            pullDirection = Vector2Int.zero;
            pullFlower = -1;
        }
        if (pulls)
        {
            pullDirection = step;
            pullFlower = selected;
            pulled.SetEnd(nextEnd);
            if (teachFanhuaDirections)
                RecordFanhuaDirection(step);
            Physics.SyncTransforms();
        }
        if (box != null || pulls)
        {
            RefreshGates();
            Physics.SyncTransforms();
        }
        if (falling != null)
            Physics.SyncTransforms();
        CheckGridSupport();
    }

    private RectInt FindSceneGridBounds()
    {
        // Base levels do not define a Fanhua grid. Use the authored board colliders.
        var bounds = new Bounds(spawnPosition, Vector3.zero);
        foreach (BoxCollider shape in FindObjectsOfType<BoxCollider>())
        {
            if (shape.gameObject.scene == gameObject.scene && shape.enabled && !shape.isTrigger &&
                (groundLayers.value & (1 << shape.gameObject.layer)) != 0)
                bounds.Encapsulate(shape.bounds);
        }

        var min = Vector2Int.CeilToInt(new Vector2(bounds.min.x, bounds.min.z));
        var max = Vector2Int.FloorToInt(new Vector2(bounds.max.x, bounds.max.z));
        return new RectInt(min, max - min + Vector2Int.one);
    }

    private bool TryResolveGridEntry()
    {
        // The 2D projection omits the folded device and other grid-only walls.
        // Exclude the player's own collider from the destination queries.
        capsule.enabled = false;
        Vector3 position = body.position;
        bool found = !HasGridOverlap(position) && !IsBetweenFallingBlocks(position);
        if (!found)
        {
            float bestDistance = float.PositiveInfinity;
            foreach (Vector2Int cell in gridBounds.allPositionsWithin)
            {
                var candidate = new Vector3(cell.x, position.y, cell.y);
                float distance = (candidate - position).sqrMagnitude;
                if (distance >= bestDistance || HasGridOverlap(candidate) ||
                    !HasSupport(candidate) || IsBetweenFallingBlocks(candidate))
                    continue;

                bestDistance = distance;
                body.position = candidate;
                found = true;
            }
        }

        capsule.enabled = true;
        Physics.SyncTransforms();
        return found;
    }

    private bool IsBetweenFallingBlocks(Vector3 position)
    {
        // Reserving the player's cell can still leave it squeezed between two blue blocks.
        return HasFallingBlockAt(position + Vector3.forward) &&
            HasFallingBlockAt(position + Vector3.back);
    }

    private bool HasFallingBlockAt(Vector3 position)
    {
        foreach (Collider obstacle in GridOverlaps(position))
            if (obstacle.TryGetComponent<PrototypeFallingBlock>(out _))
                return true;
        return false;
    }

    private bool HasGridOverlap(Vector3 position)
    {
        Vector3 center = position + capsule.center;
        Vector3 offset = Vector3.up * (capsule.height * 0.5f - capsule.radius);
        return Physics.CheckCapsule(center - offset, center + offset,
            capsule.radius - 0.025f, groundLayers, QueryTriggerInteraction.Ignore);
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
    private bool HasObstacle(Vector3 position, out PrototypePushBox box, out PrototypeFallingBlock falling)
    {
        box = null;
        falling = null;
        foreach (Collider obstacle in GridOverlaps(position))
        {
            if (box != null || falling != null)
                return true;
            if (!obstacle.TryGetComponent(out box) && !obstacle.TryGetComponent(out falling))
                return true;
        }
        return false;
    }

    private Collider[] GridOverlaps(Vector3 position)
    {
        Vector3 center = position + capsule.center;
        Vector3 offset = Vector3.up * (capsule.height * 0.5f - capsule.radius);
        return Physics.OverlapCapsule(center - offset, center + offset,
            capsule.radius - 0.025f, groundLayers, QueryTriggerInteraction.Ignore);
    }

    private void RefreshGates()
    {
        if(Mode==PlayerMode.Grid&&waterLevel!=null)waterLevel.SynchronizePaper(flowers);
        foreach (PrototypePressureGate gate in gates)
            gate.Refresh(boxes, flowers);
        RefreshWaterProjections();
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
        previousPlatformInput = 0f;
        fallingInGrid = false;
        gridFallSpeed = 0f;
        queuedGridDirection = Vector3.zero;
        nextGridStep = 0f;

        bool platformMode = mode == PlayerMode.Platform;
        capsule.enabled = !platformMode;
        platformPhysics.SetActive(platformMode);
        foreach (PrototypeFanhua flower in flowers)
            flower.SetMode(platformMode);
        if (waterLevel != null)
        {
            waterLevel.SetMode(platformMode);
            foreach (var flower in flowers)
                foreach (var block in flower.BlueBlocks) block.SetWaterView(platformMode);
            foreach (var block in fallingBlocks) block.SetWaterView(platformMode);
        }
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
        // Re-apply fluid collision pairs before the first simulation after a mode
        // change; disabling the physics root clears IgnoreCollision pairs.
        if (waterLevel != null) waterLevel.Step(platformBody, flowers);
        Physics2D.SyncTransforms();
    }

    internal static readonly Color HudSurface = new Color32(249, 239, 223, 255);
    internal static readonly Color HudInk = new Color32(53, 41, 64, 255);
    internal static readonly Color HudAccent = new Color32(109, 81, 119, 255);
    internal static readonly Color HudRim = new Color32(191, 156, 111, 255);
    private static readonly Color HudKey = new Color32(226, 210, 231, 255);
    internal static readonly Color HudMint = new Color32(193, 222, 207, 255);
    private static readonly Color HudShadow = new Color32(69, 50, 83, 80);
    private static readonly (string Key, string Action)[] ControlHints =
    {
        ("WASD", "移动"),
        ("Space", "跳跃"),
        ("右 Shift", "切换形态"),
        ("Z", "撤回操作"),
        ("R", "重置关卡"),
        ("Q", "展开前翻转配色"),
        ("推拉", "面朝推／背身拉")
    };

    private Font completionFont;
    private Font controlsFont;
    private GUIStyle controlKey;
    private GUIStyle controlAction;
    private GUIStyle completeTitle;
    private GUIStyle nextButton;

    private void InitializeHudStyles()
    {
        completionFont = Font.CreateDynamicFontFromOSFont(
            new[] { "Baskerville", "Georgia", "Cambria", "Liberation Serif" }, 38);
        completeTitle = new GUIStyle(GUI.skin.label)
        {
            font = completionFont,
            fontSize = 38,
            alignment = TextAnchor.MiddleCenter
        };
        completeTitle.normal.textColor = HudInk;
        completeTitle.hover.textColor = HudInk;
        completeTitle.active.textColor = HudInk;
        completeTitle.focused.textColor = HudInk;
        nextButton = CreateHudButtonStyle(completeTitle, 24);

        controlsFont = CreateChineseHudFont(16);
        controlAction = new GUIStyle(GUI.skin.label)
        {
            font = controlsFont,
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        };
        controlAction.normal.textColor = HudInk;
        controlKey = new GUIStyle(controlAction) { alignment = TextAnchor.MiddleCenter };
    }

    private void OnDrawGizmos()
    {
        if (!fanhuaLab || Application.isPlaying)
            return;

        var center = new Vector3(fanhuaAnchor.x + 0.5f, 0.5f, fanhuaAnchor.y + 0.5f);
        Gizmos.color = new Color(0.85f, 0.85f, 0.85f);
        Gizmos.DrawWireCube(center, new Vector3(2f, 1f, 2f));
    }

    private int FindFanhuaInteractionTarget(Vector2Int cell)
    {
        if (pullFlower >= 0 && flowers[pullFlower].Axis != 0)
            return pullFlower;

        int foldedBeside = -1;
        int nearestExtended = -1;
        int nearestDistance = int.MaxValue;
        for (int i = 0; i < flowers.Length; i++)
        {
            PrototypeFanhua flower = flowers[i];
            int distance = flower.DistanceTo(cell);
            if (flower.Axis == 0)
            {
                if (distance == 1 && foldedBeside < 0)
                    foldedBeside = i;
                continue;
            }

            if (distance == 1)
                return i;

            if (distance < nearestDistance)
            {
                nearestExtended = i;
                nearestDistance = distance;
            }
        }

        return foldedBeside >= 0 ? foldedBeside : nearestExtended;
    }

    private void OnGUI()
    {
        if (completeTitle == null)
            InitializeHudStyles();

        const float hudWidth = 556f;
        float scale = Mathf.Min(
            Mathf.Clamp(Screen.height / 720f, 0.8f, 1.75f),
            Mathf.Min(Screen.width / (hudWidth + 32f), Screen.height / 334f));
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        try
        {
            if (levelComplete)
                DrawCompletion(Screen.width / scale, Screen.height / scale);
            else
            {
                DrawControlHints();
                if(fanhua!=null && Mode==PlayerMode.Grid)
                    GUI.Label(new Rect(225,12,720,30),$"翻花 {fanhua.Extension}/10 · {fanhua.PaletteHint} · 贴边方向键推拉",controlAction);
                if (body != null)
                    DrawTutorial(Screen.width / scale, Screen.height / scale);
            }
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void DrawControlHints()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        const float rowHeight = 24f;
        int rowCount = ControlHints.Length - (teachFanhuaDirections ? 2 : 0);
        var panel = new Rect(12f, 12f, 198f, rowCount * rowHeight + 12f);
        DrawFill(panel, new Color(HudSurface.r, HudSurface.g, HudSurface.b, 0.48f));
        int row = 0;
        foreach (var hint in ControlHints)
        {
            if (teachFanhuaDirections && (hint.Key == "Space" || hint.Key == "右 Shift"))
                continue;

            float y = panel.y + 6f + row++ * rowHeight;
            var key = new Rect(panel.x + 7f, y + 2f, 62f, 20f);
            DrawFill(key, new Color(HudKey.r, HudKey.g, HudKey.b, 0.58f));
            GUI.Label(key, hint.Key, controlKey);
            GUI.Label(new Rect(panel.x + 79f, y, 115f, rowHeight), hint.Action, controlAction);
        }
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
        if (nextLevel >= 0 && DrawHudButton(
            new Rect(card.x + 64, card.y + 84, card.width - 128, 60),
            "Next Level  [Enter]", nextButton, HudMint))
            SceneManager.LoadScene(nextLevel);
    }

    internal static void DrawPanel(Rect rect)
    {
        DrawFill(new Rect(rect.x, rect.y + 4f, rect.width, rect.height), HudShadow);
        DrawFill(rect, HudRim);
        DrawFill(InsetHudRect(rect, 1f), HudSurface);
        DrawFill(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 3f), HudKey);
    }

    private static void DrawKeySurface(Rect rect, Color surface)
    {
        DrawFill(rect, HudAccent);
        DrawFill(InsetHudRect(rect, 1f), surface);
    }

    internal static GUIStyle CreateHudButtonStyle(GUIStyle source, int fontSize)
    {
        var style = new GUIStyle(source) { fontSize = fontSize };
        Color interactionInk = Color.Lerp(HudInk, Color.black, 0.5f);
        style.normal.textColor = HudInk;
        style.hover.textColor = interactionInk;
        style.active.textColor = interactionInk;
        style.focused.textColor = interactionInk;
        return style;
    }

    internal static bool DrawHudButton(Rect rect, string label, GUIStyle style, Color surface)
    {
        if (!GUI.enabled)
            surface = Color.Lerp(surface, HudSurface, 0.65f);
        else if (rect.Contains(Event.current.mousePosition))
            surface = Color.Lerp(surface, HudInk, 0.12f);

        DrawKeySurface(rect, surface);
        return GUI.Button(rect, label, style);
    }

    internal static void DrawFill(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previousColor;
    }
}
