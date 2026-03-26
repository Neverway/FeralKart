using System;
using System.Collections;
using System.Collections.Generic;
using RivenFramework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;

public class FeKaPawn_Base : FeKaPawn
{
    // Pawn Controller stuff
    private FeKaPawnActions action2 = new FeKaPawnActions();
    private InputActions.FEKAActions inputActions;
    private GI_WidgetManager widgetManager;
    [SerializeField] private GameObject DeathScreenWidget, RespawnScreenWidget, deathFX;

    private float moveInput;
    private Vector2 steerInput;
    private bool isBreaking;
    
    // Barrel Roll stuff
    private float lastTiltLeftTapTime = -1f;
    private float lastTiltRightTapTime = -1f;
    private bool isRolling = false;
    private const float doubleTapWindow = 0.3f;
    
    // CPU stuff
    private Spline racingLine;
    private float racingLineT = 0f;
    private bool racingLineBuilt = false;

    private float stuckTimer = 0f;
    private Vector3 lastStuckCheckPos;
    private const float stuckCheckInterval = 2f;
    private const float stuckMoveThreshold = 2f;
    private bool isRecovering = false;
    
    private const float pawnAvoidanceRadius = 6f;
    private const float pawnAvoidanceStrength = 0.8f;
    private int stuckRecoveryAttempts = 0;
    private const int maxRecoveryAttempts = 3;
    
    // CPU item stuff
    private ItemSpawner currentItemTarget = null;
    private const float itemPickupRadius = 25;
    private const float itemChaseExitRadius = 35f;
    private const float itemPickupChance = 0.65f;

    [Tooltip("A reference to the race manager so the CPU can get the list of all racers when steering")]
    private GI_RaceManager raceManager;
    
    public override void Awake()
    {
        base.Awake();
        
        raceManager = GameInstance.Get<GI_RaceManager>();
        
        // Subscribe to events
        OnPawnDeath += () => { OnDeath(); };
        
        if (FeKaCurrentStats.controlMode != ControlMode.LocalPlayer) return;
        // Setup inputs
        inputActions = new InputActions().FEKA;
        inputActions.Enable();
    }

    public void Update()
    {
        /*Debug.Log($" movement: {moveInput} | " +
                  $"wheel rpm: {FeKaCurrentStats.wheels[0].wheelCollider.rpm} | " +
                  $"wheel rs: {FeKaCurrentStats.wheels[0].wheelCollider.rotationSpeed} | " +
                  $"wheel bt: {FeKaCurrentStats.wheels[0].wheelCollider.brakeTorque}" +
                  $"wheel mt: {FeKaCurrentStats.wheels[0].wheelCollider.motorTorque}");*/
        
        switch (FeKaCurrentStats.controlMode)
        {
            case ControlMode.LocalPlayer:
                LocalPlayerUpdate();
                break;
            case ControlMode.CPU:
                CPUUpdate();
                break;
            case ControlMode.NetworkPlayer:
                break;
        }
    }

    public void FixedUpdate()
    {
        switch (FeKaCurrentStats.controlMode)
        {
            case ControlMode.LocalPlayer:
                LocalPlayerFixedUpdate();
                break;
            case ControlMode.CPU:
                CPUFixedUpdate();
                break;
            case ControlMode.NetworkPlayer:
                break;
        }
    }

    // PLAYER
    private void LocalPlayerUpdate()
    {
        // Pausing
        UpdatePauseMenu();

        if (isPaused || isDead)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RespawnAtLastCheckpoint();
        }
        
        // Movement
        steerInput = new Vector3(inputActions.Steer.ReadValue<Vector2>().x, 0, inputActions.Steer.ReadValue<Vector2>().y);

        isBreaking = inputActions.Handbreak.IsPressed();

        if (inputActions.Acelerate.IsPressed())
        {
            moveInput = 1;
        }
        else if (inputActions.Decelerate.IsPressed())
        {
            moveInput = -1;
        }
        else
        {
            moveInput = 0;
        }

        // Jumping
        if (inputActions.HopDedicated.IsPressed() || inputActions.TiltLeft.IsPressed() && inputActions.TiltRight.IsPressed())
        {
            action2.Jump(this);
        }
        
        // Leaning
        if (action2.IsOnGround(this))
        {
            if (inputActions.TiltLeft.WasPressedThisFrame())
            {
                if (!isRolling && Time.time - lastTiltLeftTapTime <= doubleTapWindow)
                    StartCoroutine(BarrelRoll(-1));
                else
                    lastTiltLeftTapTime = Time.time;
            }
            if (inputActions.TiltRight.WasPressedThisFrame())
            {
                if (!isRolling && Time.time - lastTiltRightTapTime <= doubleTapWindow)
                    StartCoroutine(BarrelRoll(1));
                else
                    lastTiltRightTapTime = Time.time;
            }
        }

        if (!isRolling)
        {
            if (inputActions.TiltLeft.IsPressed())
                action2.Tilt(this, FeKaCurrentStats.targetTiltAngle, FeKaCurrentStats.tiltVisualMesh);
            else if (inputActions.TiltRight.IsPressed())
                action2.Tilt(this, -FeKaCurrentStats.targetTiltAngle, FeKaCurrentStats.tiltVisualMesh);
            else
                action2.TiltReturnToNeutral(this, FeKaCurrentStats.tiltVisualMesh);
        }

        // Item usage
        
        // CarFX
        action2.WheelEffects(this, isBreaking);
    }
    
    private void LocalPlayerFixedUpdate()
    {

        if (isPaused || isDead)
        {
            return;
        }
        
        // Movement
        action2.Move(this, moveInput);

        action2.Steer(this, steerInput.x);

        action2.Brake(this, moveInput, isBreaking);

        
    }
    
    // NPC
    private void CPUUpdate()
    {
        if (isDead || isRecovering) return;

        if (!racingLineBuilt)
        {
            var container = GameObject.FindWithTag("RacingTrackSpline").GetComponent<SplineContainer>();
            if (container == null) return;
            racingLine = container.Spline;
            racingLineBuilt = true;
        }

        // --- Stuck detection ---
        stuckTimer += Time.deltaTime;
        if (stuckTimer >= stuckCheckInterval)
        {
            if (Vector3.Distance(transform.position, lastStuckCheckPos) < stuckMoveThreshold) StartCoroutine(RecoverFromStuck());
            else stuckRecoveryAttempts = 0;
            lastStuckCheckPos = transform.position;
            stuckTimer = 0f;
        }

        // --- Find where we are on the spline ---
        SplineUtility.GetNearestPoint(racingLine, transform.position, out _, out var nearestT);
        var lookaheadT = AdvanceTAlongSpline(nearestT, GetLookaheadDistance());
        var lookaheadPos = (Vector3)racingLine.EvaluatePosition(lookaheadT);

        // --- Obstacle check between us and the lookahead point ---
        var actualTarget = lookaheadPos;
        if (Physics.Linecast(transform.position + Vector3.up * 0.5f, lookaheadPos + Vector3.up * 0.5f, out var obstacleHit) && !obstacleHit.collider.isTrigger)
        {
            var deflect = Vector3.Cross(obstacleHit.normal, Vector3.up).normalized;
            actualTarget = obstacleHit.point + deflect * 3f;
        }

         // --- Steering ---
        var toTarget = actualTarget - transform.position;
        var localDir = transform.InverseTransformDirection(toTarget);
        var geometricSteer = Mathf.Clamp(localDir.x / 10f, -1f, 1f);

        var localVelocity = transform.InverseTransformDirection(physicsbody.velocity);
        var velocityCorrection = -localVelocity.x / (FeKaCurrentStats.maxSpeed * 1.5f);

        // --- Item targeting ---
        if (currentItemTarget != null)
        {
            if (!currentItemTarget.itemAvailable 
                || Vector3.Distance(transform.position, currentItemTarget.transform.position) > itemChaseExitRadius)
            {
                currentItemTarget = null;
            }
        }
        else if (FeKaCurrentStats.utility == null && UnityEngine.Random.value < itemPickupChance * Time.deltaTime)
        {
            currentItemTarget = FindNearbyItemSpawner();
        }

        var itemSteer = 0f;
        if (currentItemTarget != null)
        {
            var toItem = currentItemTarget.transform.position - transform.position;
            var localToItem = transform.InverseTransformDirection(toItem);
            var itemSteerRaw = Mathf.Clamp(localToItem.x / 10f, -1f, 1f);
            var forwardness = Mathf.Clamp01(localToItem.z / itemPickupRadius);
            itemSteer = Mathf.Lerp(geometricSteer, itemSteerRaw, forwardness);
            geometricSteer = itemSteer;
        }

        // --- Pawn avoidance ---
        var avoidanceSteer = 0f;
        if (raceManager == null)
        {
            GameInstance.Get<GI_RaceManager>();
        }
        foreach (var pawn in raceManager.racers)
        {
            if (pawn == this) continue;
            var toPawn = pawn.transform.position - transform.position;
            var localToPawn = transform.InverseTransformDirection(toPawn);
            if (localToPawn.z > 0f && toPawn.magnitude < pawnAvoidanceRadius)
            {
                var proximity = 1f - (toPawn.magnitude / pawnAvoidanceRadius);
                avoidanceSteer -= Mathf.Sign(localToPawn.x) * proximity * pawnAvoidanceStrength;
            }
        }

        steerInput = new Vector2(Mathf.Clamp(geometricSteer + velocityCorrection + avoidanceSteer, -1f, 1f), 0);

        // --- Speed control ---
        var curvature = GetSplineCurvature(nearestT, 0.05f);
        var currentSpeed = physicsbody.velocity.magnitude;
        var targetBehindUs = localDir.z < 0;
        var speedLimit = Mathf.Lerp(FeKaCurrentStats.maxSpeed, FeKaCurrentStats.maxSpeed * 0.65f, curvature * 2f);

        if (targetBehindUs || currentSpeed > speedLimit)
        {
            moveInput = targetBehindUs ? 0f : Mathf.Lerp(1f, 0f, (currentSpeed - speedLimit) / 5f);
            isBreaking = currentSpeed > speedLimit * 1.1f;
        }
        else
        {
            moveInput = 1f;
            isBreaking = false;
        }
        racingLineT = nearestT;
    }

    private void CPUFixedUpdate()
    {
        if (isDead) return;
        action2.Move(this, moveInput);
        action2.Steer(this, steerInput.x);
        action2.Brake(this, moveInput, isBreaking);
    }
    
    private void UpdatePauseMenu()
    {
        if (!widgetManager)
        {
            widgetManager = GameInstance.Get<GI_WidgetManager>();
            if (!widgetManager) return;
        }
        isPaused = widgetManager.GetExistingWidget("WB_Pause");
        
        // Pause Game
        if (inputActions.Pause.WasPressedThisFrame())
        {
            widgetManager.ToggleWidget("WB_Pause");
        }
        
        // Lock mouse when unpaused, unlock when paused
        if (isPaused)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ShowRespawnScreen()
    {
        if (!widgetManager)
        {
            widgetManager = GameInstance.Get<GI_WidgetManager>();
            if (!widgetManager) return;
        }
        widgetManager.AddWidget("WB_Respawning");
    }

    private void OnDeath()
    {
        Instantiate(deathFX, transform.position, transform.rotation, null);
        if (FeKaCurrentStats.stocks <= 0)
        {
            if (!widgetManager)
            {
                widgetManager = GameInstance.Get<GI_WidgetManager>();
                if (!widgetManager) return;
            }
            widgetManager.AddWidget("WB_DeathScreen");
        }
        else
        {
            FeKaCurrentStats.stocks -= 1;
            StartCoroutine(AwaitRespawn());
        }
    }

    private IEnumerator AwaitRespawn()
    {
        ShowRespawnScreen();
        yield return new WaitForSeconds(FeKaCurrentStats.respawnTime);
        var lastCheckpoint = FeKaCurrentStats.currentCheckpoint - 1;
        if (lastCheckpoint < 0) lastCheckpoint = FindObjectOfType<CheckpointTracker>().raceCheckpoints.Count-1;
        var respawnTransform = FindObjectOfType<CheckpointTracker>().raceCheckpoints[lastCheckpoint].transform;
        
        transform.position = respawnTransform.position;
        transform.rotation = respawnTransform.rotation;
        FeKaCurrentStats.health = FeKaDefaultStats.health;
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        isDead = false;
    }
    
    private IEnumerator BarrelRoll(int _direction)
    {
        isRolling = true;

        var duration = FeKaCurrentStats.barrelRollDuration;
        var elapsed = 0f;
        var visualMesh = FeKaCurrentStats.tiltVisualMesh;
        

        // Nudge physics body to the side
        physicsbody.AddForce(transform.right * _direction * FeKaCurrentStats.barrelRollForce, ForceMode.Impulse);
        physicsbody.AddForce(transform.up * FeKaCurrentStats.barrelRollHopForce, ForceMode.Impulse);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = elapsed / duration;

            // DO A BARREL ROLL!!!
            var zRot = -_direction * 360f * t;
            var currentRot = visualMesh.localEulerAngles;
            visualMesh.localEulerAngles = new Vector3(currentRot.x, currentRot.y, zRot);

            // Arc the roll
            visualMesh.localPosition = new Vector3(
                visualMesh.localPosition.x,
                Mathf.Sin(t * Mathf.PI) * FeKaCurrentStats.barrelRollYPeak,
                visualMesh.localPosition.z
            );

            yield return null;
        }

        // Move visual back smoothly to nuetral rotation and position
        visualMesh.localEulerAngles = new Vector3(
            visualMesh.localEulerAngles.x,
            visualMesh.localEulerAngles.y,
            0f
        );
        visualMesh.localPosition = new Vector3(
            visualMesh.localPosition.x,
            0f,
            visualMesh.localPosition.z
        );

        isRolling = false;
    }

    public void Init()
    {

        var respawnTransform = WorldSettings.GetPlayerStartPoint().transform;
        transform.position = respawnTransform.position;
        transform.rotation = respawnTransform.rotation;
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        isDead = false;
        isPaused = false;

        // Restore the default stats to the character
        currentStats = (FeKaPawnStats)FeKaDefaultStats.Clone();
    }

    public void RespawnAtLastCheckpoint()
    {
        var checkpointTracker = FindObjectOfType<CheckpointTracker>();
        
        var lastCheckpoint = FeKaCurrentStats.currentCheckpoint - 1;
        if (lastCheckpoint < 0) lastCheckpoint = checkpointTracker.raceCheckpoints.Count-1;
        
        ModifyHealth(-FeKaCurrentStats.checkpointResetHealthPenalty);
        
        var respawnTransform = checkpointTracker.raceCheckpoints[lastCheckpoint].transform;
        if (respawnTransform == null) respawnTransform = checkpointTracker.raceCheckpoints[0].transform;
        transform.position = respawnTransform.position;
        transform.rotation = respawnTransform.rotation;
        GetComponent<Rigidbody>().velocity = Vector3.zero;
    }
    
    // CPU Checks
    private float GetLookaheadDistance()
    {
        var speedRatio = physicsbody.velocity.magnitude / FeKaCurrentStats.maxSpeed;
        return Mathf.Lerp(6f, 18f, speedRatio);
    }
    
    private float AdvanceTAlongSpline(float startT, float worldDistance)
    {
        var splineLength = racingLine.GetLength();
        if (splineLength <= 0f) return startT;
        var tStep = worldDistance / splineLength;
        return (startT + tStep) % 1f;
    }

    private float GetSplineCurvature(float t, float lookAheadT)
    {
        var forwardNow  = (Vector3)racingLine.EvaluateTangent(t);
        var forwardNext = (Vector3)racingLine.EvaluateTangent((t + lookAheadT) % 1f);
        var angle = Vector3.Angle(forwardNow, forwardNext);
        return Mathf.Clamp01(angle / 90f);
    }

    private IEnumerator RecoverFromStuck()
    {
        isRecovering = true;
        stuckRecoveryAttempts++;

        if (stuckRecoveryAttempts >= maxRecoveryAttempts)
        {
            stuckRecoveryAttempts = 0;
            RespawnAtLastCheckpoint();
            yield return new WaitForSeconds(0.5f);
            isRecovering = false;
            yield break;
        }

        SplineUtility.GetNearestPoint(racingLine, transform.position, out _, out var nearestT);
        var linePos = (Vector3)racingLine.EvaluatePosition(nearestT);
        var toLine = transform.InverseTransformDirection(linePos - transform.position);

        var turnDirection = Mathf.Sign(toLine.x) == 0f ? 1f : Mathf.Sign(toLine.x);
        var splineIsAhead = toLine.z > 0f;

        var timer = 0f;
        while (timer < 2f)
        {
            if (timer < 1f)
            {
                moveInput = -1f;
                isBreaking = false;
                steerInput = new Vector2(-turnDirection, 0);
            }
            else
            {
                moveInput = 1f;
                isBreaking = false;
                steerInput = new Vector2(splineIsAhead ? turnDirection : -turnDirection, 0);
            }
            if (timer > 0.5f && physicsbody.velocity.magnitude > stuckMoveThreshold)
                break;

            timer += Time.deltaTime;
            yield return null;
        }

        isRecovering = false;
    }
    
    private ItemSpawner FindNearbyItemSpawner()
    {
        var bestSpawner = (ItemSpawner)null;
        var bestScore = float.MinValue;

        foreach (var spawner in FindObjectsOfType<ItemSpawner>())
        {
            if (!spawner.itemAvailable) continue;

            var dist = Vector3.Distance(transform.position, spawner.transform.position);
            if (dist > itemPickupRadius) continue;

            // Score = rarity weight / distance, so rare close items beat common far ones
            var rarityWeight = (int)spawner.item.rarity + 1f;
            var score = rarityWeight / dist;

            if (score > bestScore)
            {
                bestScore = score;
                bestSpawner = spawner;
            }
        }

        return bestSpawner;
    }
}
