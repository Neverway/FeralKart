using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using DG.Tweening;
using RivenFramework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;

public class FeKaPawn_Base : FeKaPawn
{
    // Network Variables
    private NetVariableOwner netVarOwner;
 
    private NetVariable<float> netHealth;
    private NetVariable<int> netCurrentLap;
    private NetVariable<int> netCurrentCheckpoint;
    private NetVariable<int> netRacerState;
    // Damage Context
    private NetVariable<string> netLastInstigatorId;
    private NetVariable<string> netLastSourceName;
    private NetVariable<int> netLastDamageType;
 
    private const float NetSyncInterval = 0.1f;
    private float netSyncTimer = 0f;
    
    // Pawn Controller stuff
    private FeKaPawnActions action2 = new FeKaPawnActions();
    private InputActions.FEKAActions inputActions;
    private GI_WidgetManager widgetManager;
    private GI_PawnManager pawnManager;
    private bool abilityReady, finalStrikeReady;
    [SerializeField] private GameObject DeathScreenWidget, RespawnScreenWidget, deathFX, HUDWidget, Nameplate;
    
    // CONTROL STUFF VERY IMPORTANT YUH HUH!
    private Camera _camera;
    public ControlMode _controlMode;
    public ControlMode controlMode
    {
        get => _controlMode;
        set
        {
            _controlMode = value;
            if (_camera != null) _camera.gameObject.SetActive(value == ControlMode.LocalPlayer);
        }
    }

    private float moveInput;
    private Vector2 steerInput;
    private bool isBreaking;
    public GameObject shadow;
    public Transform shadowRaycastCheckPos;
    
    // Barrel Roll stuff
    private float lastTiltRollLeftTapTime = -1f;
    private float lastTiltRollRightTapTime = -1f;
    private bool isRolling = false;
    private const float doubleTapWindow = 0.3f;
    
    // CPU stuff
    private Spline racingLine;
    private float racingLineT = 0f;
    private bool racingLineBuilt = false;

    private enum RecoveryState { Idle, Reversing, Realigning, ForceRespawn }
    private RecoveryState recoveryState = RecoveryState.Idle;
    private float recoveryTimer = 0f;
    private const float reversePhaseTime = 1.2f;
    private const float realignPhaseTime = 1.8f;

    private float smoothedSteer = 0f;
    private const float steerSmoothSpeed = 6f;

    private float stuckTimer = 0f;
    private Vector3 lastStuckCheckPos;
    private const float stuckCheckInterval = 1.5f;
    private const float stuckMoveThreshold = 1.0f;
    private int stuckStrikes = 0;
    private const int maxStuckStrikes = 3;
    
    private const float pawnAvoidanceRadius = 6f;
    private const float pawnAvoidanceStrength = 0.8f;
    
    // CPU item stuff
    private ItemSpawner currentItemTarget = null;
    private ItemSpawner[] allItemSpawners;
    private const float itemPickupRadius = 25f;
    private const float itemChaseExitRadius = 40f;
    private const float itemPickupChance = 0.65f;
    private const float itemDistanceWander = 5f;
    private const float racingLineTrackingPercentage = 0.25f;
    private bool isCPUFiringRocket = false;
    [SerializeField] private CinemachineTransposer cinemachineTransposer;

    [Tooltip("A reference to the race manager so the CPU can get the list of all racers when steering")]
    private GI_RaceManager raceManager;
    
    public void Start()
    {
        base.Awake();
        
        
        // Subscribe to events
        OnPawnDeath += OnDeath;
        OnPawnHurt += OnHurt;
        OnPawnHeal += OnHeal;
        OnPawnHurt  += info => PushDamageContext(info);
        OnPawnDeath += info => PushDamageContext(info);
        
        _camera = viewPoint.GetComponentInChildren<Camera>();
        _camera.gameObject.SetActive(controlMode == ControlMode.LocalPlayer);

        // Setup net variables
        netVarOwner = GetComponent<NetVariableOwner>();
        if (netVarOwner != null)
        {
            netHealth = netVarOwner.Register<float>("feka_health", 0f, OnNetHealthChanged);
            netCurrentLap = netVarOwner.Register<int>("feka_lap", 0, OnNetCurrentLapChanged);
            netCurrentCheckpoint = netVarOwner.Register<int>("feka_checkpoint", 0, OnNetCurrentCheckpointChanged);
            netRacerState = netVarOwner.Register<int>("feka_racerState", 0, OnNetRacerStateChanged);
            netLastInstigatorId = netVarOwner.Register<string>("feka_lastInstigatorId", "", null);
            netLastSourceName = netVarOwner.Register<string>("feka_lastSourceName", "", null);
            netLastDamageType = netVarOwner.Register<int>("feka_lastDamageType", 0, null);
        }
        
        // Setup ability assignments (items need to be cloned like this so the SOs don't act like a little bitch baby)
        if (FeKaDefaultStats.fighterData != null)
        {
            FeKaCurrentStats.fighterData = Instantiate(FeKaCurrentStats.fighterData);
            if (FeKaDefaultStats.fighterData.ability != null)
            {
                FeKaCurrentStats.fighterData.ability = FeKaDefaultStats.fighterData.ability.Clone();
                FeKaCurrentStats.fighterData.ability.itemBehaviour.OnPickup(this);
            }
            if (FeKaDefaultStats.fighterData.finalStrike != null)
            {
                FeKaCurrentStats.fighterData.finalStrike = FeKaDefaultStats.fighterData.finalStrike.Clone();
                FeKaCurrentStats.fighterData.finalStrike.itemBehaviour.OnPickup(this);
            }

            StartCoroutine(RegenAbilityCharge());
            StartCoroutine(RegenFinalStrikeCharge());
        }
        
        if (controlMode == ControlMode.NetworkPlayer)
        {
            // Remove components that eff up the network transform sync
            foreach (var wheelCollider in GetComponentsInChildren<WheelCollider>())
            {
                wheelCollider.enabled = false;
            }
            Destroy(physicsbody);
        }
        if (controlMode != ControlMode.LocalPlayer)
        {
            Nameplate.SetActive(true);
            Nameplate.GetComponentInChildren<TMP_Text>().text = networkPlayerName;
            return;
        }
        // Setup inputs
        inputActions = new InputActions().FEKA;
        inputActions.Enable();
    }
    

    public void Update()
    {
        raceManager ??= GameInstance.Get<GI_RaceManager>();

        if (physicsbody)
        {
            if (FeKaCurrentStats.racerState == FeKaPawnStats.RacerState.preparing) physicsbody.isKinematic = true;
            if (FeKaCurrentStats.racerState == FeKaPawnStats.RacerState.racing) physicsbody.isKinematic = false;
        }

        if (Physics.SphereCast(shadowRaycastCheckPos.position, 0.25f, Vector3.down, out RaycastHit hit, 99f, FeKaCurrentStats.groundMask))
        {
            shadow.gameObject.SetActive(true);
            shadow.transform.position = hit.point;
            Vector3 pawnsForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            shadow.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(pawnsForward, hit.normal).normalized, hit.normal);
        }
        else
        {
            shadow.gameObject.SetActive(false);
        }
        
        switch (controlMode)
        {
            case ControlMode.LocalPlayer:
                LocalPlayerUpdate();
                PushNetVars();
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
        switch (controlMode)
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
        pawnManager ??= GameInstance.Get<GI_PawnManager>();
        pawnManager.localPlayerCharacter = gameObject;
        
        // Pausing
        UpdatePauseMenu();


        widgetManager ??= GameInstance.Get<GI_WidgetManager>();
        if (widgetManager.GetExistingWidget(HUDWidget.name) == null) widgetManager.AddWidget(HUDWidget);

        if (isPaused || isDead)
        {
            return;
        }

        if (inputActions.RespawnAtCheckpoint.WasPressedThisFrame())
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
        if (inputActions.Hop.IsPressed())
        {
            action2.Jump(this);
        }
        
        // Leaning
        if (action2.IsOnGround(this))
        {
            if (inputActions.TiltRollLeft.WasPressedThisFrame() && !inputActions.TiltRollRight.IsPressed())
            {
                if (!isRolling && Time.time - lastTiltRollLeftTapTime <= doubleTapWindow)
                    StartCoroutine(BarrelRoll(-1));
                else
                    lastTiltRollLeftTapTime = Time.time;
            }
            if (inputActions.TiltRollRight.WasPressedThisFrame() && !inputActions.TiltRollLeft.IsPressed())
            {
                if (!isRolling && Time.time - lastTiltRollRightTapTime <= doubleTapWindow)
                    StartCoroutine(BarrelRoll(1));
                else
                    lastTiltRollRightTapTime = Time.time;
            }
        }

        if (!isRolling)
        {
            if (inputActions.TiltRollLeft.IsPressed() && !inputActions.TiltRollRight.IsPressed())
                action2.Tilt(this, FeKaCurrentStats.targetTiltAngle, FeKaCurrentStats.tiltVisualMesh);
            else if (inputActions.TiltRollRight.IsPressed() && !inputActions.TiltRollLeft.IsPressed())
                action2.Tilt(this, -FeKaCurrentStats.targetTiltAngle, FeKaCurrentStats.tiltVisualMesh);
            else
                action2.TiltReturnToNeutral(this, FeKaCurrentStats.tiltVisualMesh);
        }

        // Item usage
        UpdateItems();
        
        // RearView
        if (inputActions.LookBehind.IsPressed())
        {
            cinemachineTransposer ??= viewPoint.GetComponent<CinemachineBrain>().ActiveVirtualCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachineTransposer>();
            cinemachineTransposer.m_FollowOffset.z = 6;
            FeKaCurrentStats.projectileSpawnPoint.localRotation = Quaternion.Euler(new Vector3(0, 180, 0));
        }
        else if (inputActions.LookBehind.WasReleasedThisFrame())
        {
            cinemachineTransposer ??= viewPoint.GetComponent<CinemachineBrain>().ActiveVirtualCamera.VirtualCameraGameObject.GetComponent<CinemachineVirtualCamera>().GetCinemachineComponent<CinemachineTransposer>();
            cinemachineTransposer.m_FollowOffset.z = -6;
            FeKaCurrentStats.projectileSpawnPoint.localRotation = Quaternion.Euler(new Vector3(0, 0, 0));
        }
        
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
    
    private void UpdateItems()
    {
        // Utility
        var item = FeKaCurrentStats.utility;
        if (item?.itemBehaviour != null)
        {
            item.itemBehaviour.OnUpdate(this);

            if (inputActions.UseItem.IsPressed())
                item.itemBehaviour.OnUseHeld(this);

            if (inputActions.UseItem.WasReleasedThisFrame())
                item.itemBehaviour.OnUseReleased(this);

            // Auto-clear exhausted items
            if (item.itemBehaviour.IsExhausted())
                FeKaCurrentStats.utility = null;
        }

        // Ability
        item = FeKaCurrentStats.fighterData.ability;
        if (item?.itemBehaviour != null)
        {
            if (FeKaCurrentStats.abilityCharge >= item.details.chargeAmount)
            {
                item.itemBehaviour.OnUpdate(this);

                if (inputActions.UseAbility.IsPressed())
                {
                    item.itemBehaviour.OnUseHeld(this);
                }

                if (inputActions.UseAbility.WasReleasedThisFrame())
                    item.itemBehaviour.OnUseReleased(this);

                if (item.itemBehaviour.IsExhausted() && abilityReady)
                {
                    print($"item was exhausted {item.details.chargeAmount}");
                    FeKaCurrentStats.abilityCharge = 0;
                    abilityReady = false;
                }
            }
        }

        // Final Strike
        item = FeKaCurrentStats.fighterData.finalStrike;
        if (item?.itemBehaviour != null)
        {
            if (FeKaCurrentStats.finalStrikeCharge >= item.details.chargeAmount)
            {
                item.itemBehaviour.OnUpdate(this);

                if (inputActions.TiltRollLeft.IsPressed() && inputActions.TiltRollRight.IsPressed())
                {
                    item.itemBehaviour.OnUseHeld(this);
                }

                if (inputActions.TiltRollLeft.WasReleasedThisFrame() && !inputActions.TiltRollRight.IsPressed() ||
                    !inputActions.TiltRollLeft.IsPressed() && inputActions.TiltRollRight.WasReleasedThisFrame())
                    item.itemBehaviour.OnUseReleased(this);

                if (item.itemBehaviour.IsExhausted() && finalStrikeReady)
                {
                    FeKaCurrentStats.finalStrikeCharge = 0;
                    finalStrikeReady = false;
                }
            }
        }
    }

    private IEnumerator RegenAbilityCharge()
    {
        yield return new WaitForSeconds(1);
        var item = FeKaCurrentStats.fighterData.ability;
        
        if (FeKaCurrentStats.abilityCharge < item.details.chargeAmount)
        {
            abilityReady = false;
            FeKaCurrentStats.abilityCharge += item.details.passiveRecharge;
        }
        else
        {
            FeKaCurrentStats.abilityCharge = item.details.chargeAmount;
            if (!abilityReady)
            {
                item.itemBehaviour.Reset();
                abilityReady = true;
            }
        }

        StartCoroutine(RegenAbilityCharge());
    }

    private IEnumerator RegenFinalStrikeCharge()
    {
        yield return new WaitForSeconds(1);
        var item = FeKaCurrentStats.fighterData.finalStrike;
        
        if (FeKaCurrentStats.finalStrikeCharge < item.details.chargeAmount)
        {
            finalStrikeReady = false;
            FeKaCurrentStats.finalStrikeCharge += item.details.passiveRecharge;
        }
        else
        {
            FeKaCurrentStats.finalStrikeCharge = item.details.chargeAmount;
            if (!finalStrikeReady)
            {
                item.itemBehaviour.Reset();
                finalStrikeReady = true;
            }
        }
        
        StartCoroutine(RegenFinalStrikeCharge());
    }
    
    // NPC
    private void CPUUpdate()
    {
        if (isDead) return;

        if (!racingLineBuilt)
        {
            var container = GameObject.FindWithTag("RacingTrackSpline")?.GetComponent<SplineContainer>();
            if (container == null) return;
            racingLine = container.Spline;
            racingLineBuilt = true;
            lastStuckCheckPos = transform.position;
            allItemSpawners = FindObjectsOfType<ItemSpawner>();
        }

        if (recoveryState != RecoveryState.Idle)
        {
            UpdateRecoveryStateMachine();
            return;
        }

        // --- Stuck detection ---
        stuckTimer += Time.deltaTime;
        if (stuckTimer >= stuckCheckInterval)
        {
            stuckTimer = 0f;
            float moved = Vector3.Distance(transform.position, lastStuckCheckPos);
            lastStuckCheckPos = transform.position;

            if (moved < stuckMoveThreshold)
            {
                stuckStrikes++;
                if (stuckStrikes >= maxStuckStrikes)
                {
                    stuckStrikes = 0;
                    BeginRecovery();
                    return;
                }
            }
            else
            {
                stuckStrikes = Mathf.Max(0, stuckStrikes - 1);
            }
        }

        // --- Find where we are on the spline ---
        SplineUtility.GetNearestPoint(racingLine, transform.position, out _, out var nearestT);

        float speedRatio = Mathf.Clamp01(physicsbody.velocity.magnitude / FeKaCurrentStats.maxSpeed);
        float farDist    = Mathf.Lerp(8f, 22f, speedRatio);
        float midDist    = farDist * 0.45f;

        float midT = AdvanceTAlongSpline(nearestT, midDist);
        float farT = AdvanceTAlongSpline(nearestT, farDist);

        Vector3 midPos = (Vector3)racingLine.EvaluatePosition(midT);
        Vector3 farPos = (Vector3)racingLine.EvaluatePosition(farT);

        var curvature = GetSplineCurvature(nearestT, 0.04f);

        // On tight corners weight toward the nearer target for precision
        var lookaheadPos = Vector3.Lerp(farPos, midPos, curvature * 1.5f);

        // --- Obstacle check between us and the lookahead point ---
        var actualTarget = lookaheadPos;
        if (Physics.Linecast(transform.position + Vector3.up * 0.5f, lookaheadPos + Vector3.up * 0.5f, out var obstacleHit) && !obstacleHit.collider.isTrigger)
        {
            var deflect = Vector3.Cross(obstacleHit.normal, Vector3.up).normalized;
            actualTarget = obstacleHit.point + deflect * 3.5f;
        }

        // --- Steering ---
        var toTarget = actualTarget - transform.position;
        var localDir = transform.InverseTransformDirection(toTarget);

        float angleRad = Mathf.Atan2(localDir.x, localDir.z);
        var geometricSteer = Mathf.Clamp(angleRad / (Mathf.PI * 0.25f), -1f, 1f);

        var localVelocity = transform.InverseTransformDirection(physicsbody.velocity);
        var velocityCorrection = Mathf.Clamp(-localVelocity.x / (FeKaCurrentStats.maxSpeed * 1.2f), -0.4f, 0.4f);

        // --- Item targeting ---
        UpdateItemTarget();
        if (currentItemTarget != null)
        {
            var toItem = currentItemTarget.transform.position - transform.position;
            var localToItem = transform.InverseTransformDirection(toItem);
            float itemAngle = Mathf.Atan2(localToItem.x, localToItem.z);
            float itemSteerRaw = Mathf.Clamp(itemAngle / (Mathf.PI * 0.25f), -1f, 1f);

            float itemDist = toItem.magnitude;
            float proximityWeight = 1f - Mathf.Clamp01(itemDist / itemPickupRadius);

            float lateralWeight = Mathf.Clamp01(Mathf.Abs(localToItem.x) / itemDistanceWander);

            float blendWeight = Mathf.Clamp01((proximityWeight + lateralWeight) * racingLineTrackingPercentage);
            geometricSteer = Mathf.Lerp(geometricSteer, itemSteerRaw, blendWeight);
        }

        // --- Pawn avoidance ---
        var avoidanceSteer = 0f;
        if (raceManager != null)
        {
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
        }

        float rawSteer = Mathf.Clamp(geometricSteer + velocityCorrection + avoidanceSteer, -1f, 1f);
        smoothedSteer = Mathf.Lerp(smoothedSteer, rawSteer, Time.deltaTime * steerSmoothSpeed);
        steerInput = new Vector2(smoothedSteer, 0f);

        // --- Speed control ---
        var currentSpeed = physicsbody.velocity.magnitude;
        var targetBehindUs = localDir.z < 0;
        var speedLimit = Mathf.Lerp(FeKaCurrentStats.maxSpeed, FeKaCurrentStats.maxSpeed * 0.55f, curvature * 2f);

        if (targetBehindUs)
        {
            moveInput = 0f;
            isBreaking = true;
        }
        else if (currentSpeed > speedLimit)
        {
            moveInput = Mathf.Lerp(1f, 0f, (currentSpeed - speedLimit) / 6f);
            isBreaking = currentSpeed > speedLimit * 1.15f;
        }
        else
        {
            moveInput = 1f;
            isBreaking = false;
        }
        
        UpdateCPUItemUsage();
        
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
        widgetManager ??= GameInstance.Get<GI_WidgetManager>();


        if (widgetManager.GetExistingWidget("WB_NetPlayerlist"))
        {
            isPaused = widgetManager.GetExistingWidget("WB_Pause") || widgetManager.GetExistingWidget("WB_NetPlayerlist").gameObject.activeInHierarchy || FindObjectOfType<WB_NetChat>().isTyping;;
        }
        else
        {
            isPaused = widgetManager.GetExistingWidget("WB_Pause") || FindObjectOfType<WB_NetChat>().isTyping;;
        }
        
        // Pause Game
        if (inputActions.Pause.WasPressedThisFrame())
        {
            widgetManager.ToggleWidget("WB_Pause");
        }

        if (inputActions.Playerlist.IsPressed())
        {
            var widgetPlayerList = widgetManager.GetExistingWidget("WB_NetPlayerlist");
            if (widgetPlayerList == null)
                widgetManager.AddWidget("WB_NetPlayerlist");
            else
                widgetPlayerList.SetActive(true);
        }
        else
        {
            var widgetPlayerList = widgetManager.GetExistingWidget("WB_NetPlayerlist");
            if (widgetPlayerList != null)
                widgetPlayerList.SetActive(false);
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
        if (controlMode != ControlMode.LocalPlayer ) return;
        if (!widgetManager)
        {
            widgetManager = GameInstance.Get<GI_WidgetManager>();
            if (!widgetManager) return;
        }
        widgetManager.AddWidget("WB_Respawning");
    }

    private void OnDeath(DamageInfo damageInfo)
    {
        print($"{damageInfo.instigator} used {damageInfo.source.name} to deal {damageInfo.amount} {damageInfo.type} damage to {gameObject.name} killing them");
        
        // Only send the death packet from the owner
        if (controlMode == ControlMode.LocalPlayer)
        {
            var nm = GameInstance.Get<GI_NetworkManager>();
            if (nm != null)
            {
                string instigatorName = null;
                if (damageInfo.instigator is FeKaPawn_Base instigatorPawn)
                    instigatorName = !string.IsNullOrEmpty(instigatorPawn.networkPlayerName) ? instigatorPawn.networkPlayerName : instigatorPawn.displayName;
                else
                    instigatorName = damageInfo.instigator?.displayName;

                string recipientName = !string.IsNullOrEmpty(networkPlayerName) ? networkPlayerName : nm.localProfile.playerName;
            
                var killPacket = new KillFeedPacket
                {
                    instigatorName = instigatorName,
                    sourceName = damageInfo.source.name,
                    recipientName = recipientName,
                    eliminated = FeKaCurrentStats.stocks <= 0
                };
                nm.SendPacket(nm.protocolMagic + ":KILLFEED:" + JsonUtility.ToJson(killPacket));
            }
        }

        Instantiate(deathFX, transform.position, transform.rotation, null);
        if (FeKaCurrentStats.stocks > 1)
        {
            FeKaCurrentStats.stocks -= 1;
            StartCoroutine(AwaitRespawn());
        }
        else
        {
            FeKaCurrentStats.characterSpriteRenderer.material.color = Color.black;
            if (controlMode != ControlMode.LocalPlayer ) return;
            if (!widgetManager)
            {
                widgetManager = GameInstance.Get<GI_WidgetManager>();
                if (!widgetManager) return;
            }
            widgetManager.AddWidget("WB_DeathScreen");
        }
    }

    private void OnHurt(DamageInfo damageInfo)
    {
        if (FeKaCurrentStats.health <= 0) return;
     
        print($"{damageInfo.instigator} used {damageInfo.source} to deal {damageInfo.amount} {damageInfo.type} damage to {gameObject.name}");
        
        FeKaCurrentStats.damageTaken += damageInfo.amount;
        FeKaCurrentStats.characterSpriteRenderer.material.color = Color.red;
        FeKaCurrentStats.characterSpriteRenderer.material.DOColor(new Color(1, 1, 1, 1), 1);
    }
    private void OnHeal(DamageInfo damageInfo)
    {
        print($"{damageInfo.instigator} used {damageInfo.source} to heal {damageInfo.amount} {damageInfo.type} damage on {gameObject.name}");
        FeKaCurrentStats.damageHealed += damageInfo.amount;
        FeKaCurrentStats.characterSpriteRenderer.material.color = Color.green;
        FeKaCurrentStats.characterSpriteRenderer.material.DOColor(new Color(1, 1, 1, 1), 1);
    }

    private IEnumerator AwaitRespawn()
    {
        FeKaCurrentStats.characterSpriteRenderer.material.color = Color.black;
        ShowRespawnScreen();
        FeKaCurrentStats.characterSpriteRenderer.material.color = Color.black;
        yield return new WaitForSeconds(FeKaCurrentStats.respawnTime);
        FeKaCurrentStats.characterSpriteRenderer.material.DOColor(new Color(1, 1, 1, 1), 1);
        var lastCheckpoint = FeKaCurrentStats.currentCheckpoint - 1;
        if (lastCheckpoint < 0) lastCheckpoint = FindObjectOfType<CheckpointTracker>().raceCheckpoints.Count-1;
        var respawnTransform = FindObjectOfType<CheckpointTracker>().raceCheckpoints[lastCheckpoint].transform;
        
        transform.position = respawnTransform.position;
        transform.rotation = respawnTransform.rotation;
        FeKaCurrentStats.health = FeKaDefaultStats.health;
        if (physicsbody) physicsbody.velocity = Vector3.zero;
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
        if (physicsbody) physicsbody.velocity = Vector3.zero;
        isDead = false;
        isPaused = false;

        // Restore the default stats to the character
        currentStats = (FeKaPawnStats)FeKaDefaultStats.Clone();
        FeKaCurrentStats.racerState = FeKaPawnStats.RacerState.preparing;
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

    private void BeginRecovery()
    {
        recoveryState = RecoveryState.Reversing;
        recoveryTimer = 0f;
        isBreaking    = false;

        SplineUtility.GetNearestPoint(racingLine, transform.position, out _, out float nearestT);
        Vector3 linePos  = (Vector3)racingLine.EvaluatePosition(nearestT);
        Vector3 toLine   = transform.InverseTransformDirection(linePos - transform.position);
        float turnDir    = toLine.x >= 0f ? 1f : -1f;
        steerInput       = new Vector2(-turnDir, 0f);
    }

    private void UpdateRecoveryStateMachine()
    {
        recoveryTimer += Time.deltaTime;

        switch (recoveryState)
        {
            case RecoveryState.Reversing:
                moveInput  = -1f;
                isBreaking = false;

                bool movedBack = Vector3.Distance(transform.position, lastStuckCheckPos) > stuckMoveThreshold * 2f;
                if (movedBack || recoveryTimer >= reversePhaseTime)
                {
                    recoveryTimer = 0f;
                    recoveryState = RecoveryState.Realigning;
                    SplineUtility.GetNearestPoint(racingLine, transform.position, out _, out float t);
                    Vector3 lp     = (Vector3)racingLine.EvaluatePosition(t);
                    Vector3 toLine = transform.InverseTransformDirection(lp - transform.position);
                    steerInput     = new Vector2(Mathf.Sign(toLine.x), 0f);
                }
                break;

            case RecoveryState.Realigning:
                moveInput  = 1f;
                isBreaking = false;

                float speed = physicsbody.velocity.magnitude;
                SplineUtility.GetNearestPoint(racingLine, transform.position, out _, out float nearT);
                Vector3 fwd = (Vector3)racingLine.EvaluateTangent(nearT);
                float alignment = Vector3.Dot(transform.forward, fwd.normalized);

                bool recovered = speed > stuckMoveThreshold * 2f && alignment > 0.6f;
                if (recovered)
                {
                    lastStuckCheckPos = transform.position;
                    recoveryState     = RecoveryState.Idle;
                    smoothedSteer     = 0f;
                }
                else if (recoveryTimer >= realignPhaseTime)
                {
                    recoveryState = RecoveryState.ForceRespawn;
                }
                break;

            case RecoveryState.ForceRespawn:
                RespawnAtLastCheckpoint();
                recoveryState = RecoveryState.Idle;
                smoothedSteer = 0f;
                break;
        }
    }

    private void UpdateItemTarget()
    {
        if (currentItemTarget != null)
        {
            if (!currentItemTarget.itemAvailable ||
                Vector3.Distance(transform.position, currentItemTarget.transform.position) > itemChaseExitRadius)
                currentItemTarget = null;
        }
        else if (FeKaCurrentStats.utility == null &&
                 UnityEngine.Random.value < itemPickupChance * Time.deltaTime)
        {
            currentItemTarget = FindNearbyItemSpawner();
        }
    }
    
    private ItemSpawner FindNearbyItemSpawner()
    {
        var bestSpawner = (ItemSpawner)null;
        var bestScore = float.MinValue;

        foreach (var spawner in allItemSpawners)
        {
            if (spawner == null || !spawner.itemAvailable) continue;

            var dist = Vector3.Distance(transform.position, spawner.transform.position);
            if (dist > itemPickupRadius) continue;

            var rarityWeight = (int)spawner.item.details.rarity + 1f;
            var score = rarityWeight / dist;

            if (score > bestScore)
            {
                bestScore = score;
                bestSpawner = spawner;
            }
        }

        return bestSpawner;
    }
    
    private void UpdateCPUItemUsage()
    {
        var held = FeKaCurrentStats.utility;
        if (held?.itemBehaviour == null)
        {
            isCPUFiringRocket = false;
            return;
        }

        held.itemBehaviour.OnUpdate(this);

        if (held.itemBehaviour.IsExhausted())
        {
            FeKaCurrentStats.utility = null;
            isCPUFiringRocket = false;
            return;
        }

        if (held.itemBehaviour is FeKaItem_RocketLauncher rocketLauncher)
        {
            HandleCPURocketLauncher(rocketLauncher);
        }
    }
    
    private void HandleCPURocketLauncher(FeKaItem_RocketLauncher rocketLauncher)
    {
        if (!isCPUFiringRocket)
        {
            isCPUFiringRocket = true;
            StartCoroutine(CPUFireRocket(rocketLauncher));
        }
    }

    private IEnumerator CPUFireRocket(FeKaItem_RocketLauncher rocketLauncher)
    {
        var holdTimer = 0f;
        var maxHoldTime = 16f;

        rocketLauncher.OnUseHeld(this);

        while (holdTimer < maxHoldTime)
        {
            holdTimer += Time.deltaTime;

            rocketLauncher.OnUseHeld(this);

            if (rocketLauncher.HasLock())
            {
                yield return new WaitForSeconds(0.15f);
                break;
            }

            yield return null;
        }

        rocketLauncher.OnUseReleased(this);
        isCPUFiringRocket = false;
    }
    
    
    
    // Net variable sync
    // Client side
    private void PushNetVars()
    {
        if (netVarOwner == null || FeKaCurrentStats == null) return;
 
        netSyncTimer += Time.deltaTime;
        if (netSyncTimer < NetSyncInterval) return;
        netSyncTimer = 0f;
 
        netCurrentLap.Value = FeKaCurrentStats.currentLap;
        netCurrentCheckpoint.Value = FeKaCurrentStats.currentCheckpoint;
        netRacerState.Value = (int)FeKaCurrentStats.racerState;
    }
    // Network side
    private void OnNetHealthChanged(float value)
    {
        if (controlMode != ControlMode.NetworkPlayer || FeKaCurrentStats == null) return;

        var healthDifference = FeKaCurrentStats.health - value;
        if (Mathf.Approximately(healthDifference, 0f)) return;

        var info = new DamageInfo(-healthDifference);
        info.type = (DamageType)(netLastDamageType?.Value ?? 0);
        info.source = new DamageSource { name = netLastSourceName?.Value ?? "" };

        var instigatorId = netLastInstigatorId?.Value ?? "";
        if (!string.IsNullOrEmpty(instigatorId))
        {
            raceManager ??= GameInstance.Get<GI_RaceManager>();
            if (raceManager != null)
            {
                foreach (var racer in raceManager.racers)
                {
                    var owner = racer.GetComponent<NetVariableOwner>();
                    if (owner != null && owner.NetworkObjectId == instigatorId)
                    {
                        info.instigator = racer;
                        break;
                    }
                }
            }
        }
        ModifyHealth(info);
        FeKaCurrentStats.health = value;
    }
 
    private void OnNetCurrentLapChanged(int value)
    {
        if (controlMode == ControlMode.NetworkPlayer && FeKaCurrentStats != null)
            FeKaCurrentStats.currentLap = value;
    }
 
    private void OnNetCurrentCheckpointChanged(int value)
    {
        if (controlMode == ControlMode.NetworkPlayer && FeKaCurrentStats != null)
            FeKaCurrentStats.currentCheckpoint = value;
    }
 
    private void OnNetRacerStateChanged(int value)
    {
        if (controlMode == ControlMode.NetworkPlayer && FeKaCurrentStats != null)
            FeKaCurrentStats.racerState = (FeKaPawnStats.RacerState)value;
    }
    
    private void PushDamageContext(DamageInfo info)
    {
        if (netVarOwner == null || controlMode != ControlMode.LocalPlayer) return;
        netLastInstigatorId.Value = info.instigator?.GetComponent<NetVariableOwner>()?.NetworkObjectId ?? "";
        netLastSourceName.Value   = info.source.name ?? "";
        netLastDamageType.Value   = (int)info.type;
        netHealth.Value           = FeKaCurrentStats.health;
    }
}