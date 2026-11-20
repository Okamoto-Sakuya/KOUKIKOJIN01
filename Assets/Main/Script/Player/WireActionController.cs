using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class WireActionController : MonoBehaviour
{
    // =====================================================
    // カメラ・ワイヤー設定
    // =====================================================

    [Header("カメラ・ワイヤー設定")]
    [Tooltip("マウス位置をワールド座標に変換するカメラ(Orthographic)")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("ワイヤーの始点。未設定ならPlayerの位置を使う")]
    [SerializeField] private Transform wireOrigin;

    [Tooltip("ワイヤーを描画するLineRenderer")]
    [SerializeField] private LineRenderer wireLine;

    [Tooltip("ワイヤーの最大射程")]
    [SerializeField] private float maxWireDistance = 50f;

    [Tooltip("ワイヤーの射出速度")]
    [SerializeField] private float wireExtendSpeed = 80f;

    [Tooltip("ワイヤーの回収速度")]
    [SerializeField] private float wireRetractSpeed = 100f;

    [Tooltip("ワイヤーの太さ")]
    [SerializeField] private float wireWidth = 0.04f;

    [Tooltip("ワイヤーが刺さる対象のレイヤー")]
    [SerializeField] private LayerMask wireTargetLayers = ~0;

    // =====================================================
    // 移動設定
    // =====================================================

    [Header("移動設定")]
    [Tooltip("通常移動速度")]
    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("通常移動の加速度")]
    [SerializeField] private float moveAcceleration = 25f;

    [Tooltip("入力がないときの減速")]
    [SerializeField] private float moveDeceleration = 15f;

    [Header("空中移動設定")]
    [Tooltip("空中での加速度。大きいほど空中で動きやすい")]
    [SerializeField] private float airMoveAcceleration = 15f;

    [Tooltip("空中で入力方向に出せる速度。これを超える速度は削られない")]
    [SerializeField] private float airMoveMaxSpeed = 7f;

    // =====================================================
    // ダッシュ&ジャンプ設定
    // =====================================================

    [Header("ダッシュ設定")]
    [Tooltip("Ctrl + A/Dでダッシュする速度")]
    [SerializeField] private float dashSpeed = 18f;

    [Tooltip("ダッシュ時の加速度")]
    [SerializeField] private float dashAcceleration = 60f;

    [Tooltip("ダッシュ中の最大水平速度")]
    [SerializeField] private float maxDashSpeed = 20f;

    [Header("ジャンプ設定")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayers = ~0;

    // =====================================================
    // ワイヤー引き寄せ設定
    // =====================================================

    [Header("ワイヤー引き寄せ設定")]
    [Tooltip("接続地点へ引き寄せる加速度")]
    [SerializeField] private float pullAcceleration = 12f;

    [Tooltip("引き寄せ方向の速度がこの値を超えている間は、追加の引き寄せを行わない。0以下なら制限なし")]
    [SerializeField] private float maxPullSpeed = 25f;

    [Tooltip("ロープの最短長さ。これより近づくと引き寄せを止め、振り子のように振れる")]
    [SerializeField] private float minRopeLength = 2f;

    [Tooltip("接続地点にどれだけ近づくと引き寄せが弱まり始めるか")]
    [SerializeField] private float pullFalloffDistance = 5f;

    [Tooltip("目標速度を超えて地上を移動しているときの減速度(小さいほど勢いが残る)")]
    [SerializeField] private float groundOverspeedDeceleration = 4f;

    [Header("ワイヤー解除後の慣性")]
    [SerializeField] private float wireReleaseDeceleration = 0.5f;

    // =====================================================
    // ガス設定
    // =====================================================

    [Header("ガス設定")]
    [SerializeField] private float maxGas = 100f;
    [SerializeField] private float startingGas = 100f;

    [Tooltip("ガス噴射中の加速度")]
    [SerializeField] private float gasBoostForce = 30f;

    [Tooltip("1秒あたりのガス消費量")]
    [SerializeField] private float gasConsumptionRate = 25f;

    [Tooltip("ガスを使用していないときの回復量/秒")]
    [SerializeField] private float gasRecoveryRate = 10f;

    [Tooltip("噴射方向の最大速度。0以下なら制限なし")]
    [SerializeField] private float maxGasBoostSpeed = 35f;

    [Tooltip("ガス噴射をワイヤー接続中だけ許可する")]
    [SerializeField] private bool gasRequiresAttachedWire = true;

    [Header("ガスエフェクト")]
    [SerializeField] private ParticleSystem gasEffect;

    // =====================================================
    // チェックポイント設定
    // =====================================================

    [Header("チェックポイント設定")]
    [Tooltip("復帰時にガスを全回復する")]
    [SerializeField] private bool refillGasOnRespawn = true;

    // =====================================================
    // 内部データ
    // =====================================================

    private Rigidbody2D rb;
    private Collider2D playerCollider;

    private enum WireState
    {
        Idle,
        Extending,
        Attached,
        Retracting
    }

    private WireState wireState = WireState.Idle;

    private Vector2 wireTargetPoint;
    private Vector2 wireTipPosition;

    // 左右入力(-1, 0, 1)
    private float moveInput;

    private bool gasInputHeld;
    private bool isGasBoosting;
    private bool dashInputHeld;

    private bool HasAttachedWire => wireState == WireState.Attached;

    private float currentGas;
    private float ropeLength;

    private Vector2 respawnPosition;
    private float respawnRotationZ;

    // 地面判定・ワイヤー判定用バッファ
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];
    private ContactFilter2D groundFilter;
    private ContactFilter2D wireFilter;

    // =====================================================
    // 初期化
    // =====================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (wireLine != null)
        {
            wireLine.useWorldSpace = true;
            wireLine.positionCount = 2;
            wireLine.startWidth = wireWidth;
            wireLine.endWidth = wireWidth;
            wireLine.enabled = false;
        }

        maxGas = Mathf.Max(0f, maxGas);
        startingGas = Mathf.Clamp(startingGas, 0f, maxGas);
        currentGas = startingGas;

        respawnPosition = rb.position;
        respawnRotationZ = rb.rotation;

        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayers);
        groundFilter.useTriggers = false;

        wireFilter = new ContactFilter2D();
        wireFilter.SetLayerMask(wireTargetLayers);
        wireFilter.useTriggers = false;

        if (gasEffect != null)
        {
            gasEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    // =====================================================
    // 入力処理
    // =====================================================

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null || mouse == null)
        {
            gasInputHeld = false;
            dashInputHeld = false;
            moveInput = 0f;
            return;
        }

        // A/D移動入力
        moveInput = 0f;
        if (keyboard.aKey.isPressed) moveInput -= 1f;
        if (keyboard.dKey.isPressed) moveInput += 1f;

        // Ctrl + A/Dでダッシュ
        dashInputHeld =
            (keyboard.leftCtrlKey.isPressed ||
             keyboard.rightCtrlKey.isPressed) &&
            moveInput != 0f;

        // Shiftでガス噴射
        gasInputHeld =
            keyboard.leftShiftKey.isPressed ||
            keyboard.rightShiftKey.isPressed;

        // 右クリックでワイヤー射出
        if (mouse.rightButton.wasPressedThisFrame &&
            wireState == WireState.Idle)
        {
            ShootWire();
        }

        // 右クリックを離したらワイヤー回収
        if (mouse.rightButton.wasReleasedThisFrame &&
            wireState != WireState.Idle)
        {
            StartRetracting();
        }

        // ジャンプ
        if (keyboard.spaceKey.wasPressedThisFrame && CheckGrounded())
        {
            Jump();
        }

        UpdateWire();
        UpdateGas();
        UpdateWireLine();
    }

    // =====================================================
    // 照準(マウス位置をワールド座標に変換)
    // =====================================================

    private bool TryGetMouseWorldPoint(out Vector2 worldPoint)
    {
        worldPoint = Vector2.zero;

        if (playerCamera == null || Mouse.current == null)
        {
            return false;
        }

        Vector3 screen = Mouse.current.position.ReadValue();
        screen.z = Mathf.Abs(playerCamera.transform.position.z);

        worldPoint = playerCamera.ScreenToWorldPoint(screen);
        return true;
    }

    // =====================================================
    // ワイヤー射出
    // =====================================================

    private void ShootWire()
    {
        if (!TryGetMouseWorldPoint(out Vector2 mouseWorld))
        {
            Debug.LogWarning(
                "WireActionController: Player Cameraが未設定です。"
            );
            return;
        }

        Vector2 origin = GetWireStartPosition();
        Vector2 direction = mouseWorld - origin;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction.Normalize();

        int count = Physics2D.Raycast(
            origin,
            direction,
            wireFilter,
            hitBuffer,
            maxWireDistance
        );

        bool foundTarget = false;
        float nearest = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = hitBuffer[i];

            // 自分自身のColliderは無視
            if (hit.collider.transform.IsChildOf(transform) ||
                hit.collider.transform == transform)
            {
                continue;
            }

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                wireTargetPoint = hit.point;
                foundTarget = true;
            }
        }

        if (!foundTarget)
        {
            Debug.Log("ワイヤーが届く範囲に対象がありません。");
            return;
        }

        wireTipPosition = origin;
        wireState = WireState.Extending;

        if (wireLine != null)
        {
            wireLine.enabled = true;
        }
    }

    // =====================================================
    // ワイヤー始点
    // =====================================================

    private Vector2 GetWireStartPosition()
    {
        if (wireOrigin != null)
        {
            return wireOrigin.position;
        }

        return transform.position;
    }

    // =====================================================
    // ワイヤー状態更新
    // =====================================================

    private void UpdateWire()
    {
        switch (wireState)
        {
            case WireState.Idle:
                break;

            case WireState.Extending:
                UpdateWireExtending();
                break;

            case WireState.Attached:
                wireTipPosition = wireTargetPoint;
                break;

            case WireState.Retracting:
                UpdateWireRetracting();
                break;
        }
    }

    private void UpdateWireExtending()
    {
        if (Mouse.current == null ||
            !Mouse.current.rightButton.isPressed)
        {
            StartRetracting();
            return;
        }

        wireTipPosition = Vector2.MoveTowards(
            wireTipPosition,
            wireTargetPoint,
            wireExtendSpeed * Time.deltaTime
        );

        if (Vector2.Distance(wireTipPosition, wireTargetPoint) < 0.02f)
        {
            wireTipPosition = wireTargetPoint;
            wireState = WireState.Attached;

            // 接続した瞬間の距離をロープの長さにする
            ropeLength = Mathf.Max(
                Vector2.Distance(rb.position, wireTargetPoint),
                minRopeLength
            );
        }
    }

    private void StartRetracting()
    {
        if (wireState == WireState.Idle)
        {
            return;
        }

        wireState = WireState.Retracting;
        isGasBoosting = false;

        StopGasEffect();
    }

    private void UpdateWireRetracting()
    {
        Vector2 start = GetWireStartPosition();

        wireTipPosition = Vector2.MoveTowards(
            wireTipPosition,
            start,
            wireRetractSpeed * Time.deltaTime
        );

        if (Vector2.Distance(wireTipPosition, start) < 0.05f)
        {
            wireTipPosition = start;
            wireState = WireState.Idle;

            if (wireLine != null)
            {
                wireLine.enabled = false;
            }
        }
    }

    // =====================================================
    // 物理処理
    // =====================================================

    private void FixedUpdate()
    {
        if (rb == null)
        {
            return;
        }

        ApplyMovement();

        if (HasAttachedWire)
        {
            ApplyWirePull();
        }

        if (isGasBoosting)
        {
            ApplyGasBoost();
        }
    }

    // =====================================================
    // 移動・ダッシュ処理
    // =====================================================

    private void SetHorizontalVelocity(float x)
    {
        Vector2 v = rb.linearVelocity;
        v.x = x;
        rb.linearVelocity = v;
    }

    private void ApplyMovement()
    {
        bool hasMovement = Mathf.Abs(moveInput) > 0.01f;

        // ワイヤー接続中は地上の減速処理を使わない(勢いを保つ)
        bool grounded = CheckGrounded() && !HasAttachedWire;

        float vx = rb.linearVelocity.x;

        // ---------------- 入力がないとき ----------------
        if (!hasMovement)
        {
            // ワイヤー接続中やガス噴射中は慣性を維持
            if (HasAttachedWire || isGasBoosting)
            {
                return;
            }

            float decel = grounded
                ? moveDeceleration
                : wireReleaseDeceleration;

            SetHorizontalVelocity(
                Mathf.MoveTowards(vx, 0f, decel * Time.fixedDeltaTime)
            );
            return;
        }

        // ---------------- 空中移動 ----------------
        if (!grounded)
        {
            // 入力方向への加速を足すだけ。速度は削らない
            float dir = Mathf.Sign(moveInput);
            float speedAlongInput = vx * dir;

            if (speedAlongInput < airMoveMaxSpeed)
            {
                float addSpeed = Mathf.Min(
                    airMoveAcceleration * Time.fixedDeltaTime,
                    airMoveMaxSpeed - speedAlongInput
                );

                SetHorizontalVelocity(vx + dir * addSpeed);
            }

            return;
        }

        // ---------------- 地上移動・ダッシュ ----------------
        bool isDashing = dashInputHeld;

        float targetSpeed = isDashing ? dashSpeed : moveSpeed;
        float acceleration = isDashing ? dashAcceleration : moveAcceleration;

        // 目標速度を超えているときはゆるやかに減速
        if (Mathf.Abs(vx) > targetSpeed)
        {
            acceleration = Mathf.Min(
                acceleration,
                groundOverspeedDeceleration
            );
        }

        float nextVx = Mathf.MoveTowards(
            vx,
            moveInput * targetSpeed,
            acceleration * Time.fixedDeltaTime
        );

        // 地上ダッシュ中の水平速度を制限
        if (isDashing && maxDashSpeed > 0f)
        {
            nextVx = Mathf.Clamp(nextVx, -maxDashSpeed, maxDashSpeed);
        }

        SetHorizontalVelocity(nextVx);
    }

    // =====================================================
    // ワイヤー引き寄せ
    // =====================================================

    private void ApplyWirePull()
    {
        Vector2 toAnchor = wireTargetPoint - rb.position;
        float distance = toAnchor.magnitude;

        if (distance < 0.01f)
        {
            return;
        }

        Vector2 dirToAnchor = toAnchor / distance;

        // 1. 引き寄せ(速度を削らず、足りないときだけ加速)
        float inwardSpeed = Vector2.Dot(rb.linearVelocity, dirToAnchor);

        bool underSpeedLimit =
            maxPullSpeed <= 0f || inwardSpeed < maxPullSpeed;

        if (distance > minRopeLength && underSpeedLimit)
        {
            float falloff = Mathf.Clamp01(
                (distance - minRopeLength) /
                Mathf.Max(0.01f, pullFalloffDistance)
            );

            rb.linearVelocity +=
                dirToAnchor * pullAcceleration * falloff * Time.fixedDeltaTime;
        }

        // 2. ロープの拘束(外へ出る速度成分だけを消す)
        if (distance > ropeLength)
        {
            Vector2 outward = -dirToAnchor;
            Vector2 velocity = rb.linearVelocity;

            float outwardSpeed = Vector2.Dot(velocity, outward);

            if (outwardSpeed > 0f)
            {
                rb.linearVelocity = velocity - outward * outwardSpeed;
            }

            // ロープの長さまで位置を戻す
            rb.position += dirToAnchor * (distance - ropeLength);
        }
        else
        {
            // ロープは縮む一方で伸びない
            ropeLength = Mathf.Max(distance, minRopeLength);
        }
    }

    // =====================================================
    // ガス消費・回復
    // =====================================================

    private void UpdateGas()
    {
        bool canBoost =
            gasInputHeld &&
            currentGas > 0f &&
            (!gasRequiresAttachedWire || HasAttachedWire);

        isGasBoosting = canBoost;

        if (isGasBoosting)
        {
            currentGas -= gasConsumptionRate * Time.deltaTime;
            currentGas = Mathf.Max(0f, currentGas);

            if (currentGas <= 0f)
            {
                isGasBoosting = false;
            }
        }
        else
        {
            currentGas += gasRecoveryRate * Time.deltaTime;
            currentGas = Mathf.Min(maxGas, currentGas);
        }

        if (isGasBoosting)
        {
            PlayGasEffect();
        }
        else
        {
            StopGasEffect();
        }
    }

    // =====================================================
    // ガス噴射(マウスカーソル方向)
    // =====================================================

    private void ApplyGasBoost()
    {
        if (!TryGetMouseWorldPoint(out Vector2 mouseWorld))
        {
            return;
        }

        Vector2 boostDirection = mouseWorld - rb.position;

        if (boostDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        boostDirection.Normalize();

        // 噴射方向の速度が上限未満のときだけ加速(超えていても削らない)
        float speedAlongBoost =
            Vector2.Dot(rb.linearVelocity, boostDirection);

        if (maxGasBoostSpeed <= 0f || speedAlongBoost < maxGasBoostSpeed)
        {
            rb.linearVelocity +=
                boostDirection * gasBoostForce * Time.fixedDeltaTime;
        }
    }

    // =====================================================
    // ガスエフェクト
    // =====================================================

    private void PlayGasEffect()
    {
        if (gasEffect != null && !gasEffect.isPlaying)
        {
            gasEffect.Play();
        }
    }

    private void StopGasEffect()
    {
        if (gasEffect != null && gasEffect.isPlaying)
        {
            gasEffect.Stop();
        }
    }

    // =====================================================
    // ワイヤー描画
    // =====================================================

    private void UpdateWireLine()
    {
        if (wireLine == null || !wireLine.enabled)
        {
            return;
        }

        wireLine.SetPosition(0, GetWireStartPosition());
        wireLine.SetPosition(1, wireTipPosition);
    }

    // =====================================================
    // チェックポイント・復帰
    // =====================================================

    public void SetCheckpoint(Vector2 position, Quaternion rotation)
    {
        respawnPosition = position;
        respawnRotationZ = rotation.eulerAngles.z;
    }

    public void Respawn()
    {
        wireState = WireState.Idle;

        if (wireLine != null)
        {
            wireLine.enabled = false;
        }

        isGasBoosting = false;
        StopGasEffect();

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.position = respawnPosition;
        rb.rotation = respawnRotationZ;
        transform.SetPositionAndRotation(
            new Vector3(respawnPosition.x, respawnPosition.y, transform.position.z),
            Quaternion.Euler(0f, 0f, respawnRotationZ)
        );
        Physics2D.SyncTransforms();

        if (refillGasOnRespawn)
        {
            currentGas = maxGas;
        }
    }

    // =====================================================
    // 外部から参照する関数
    // =====================================================

    public float GetCurrentGas() => currentGas;
    public float GetMaxGas() => maxGas;
    public bool IsWireAttached() => HasAttachedWire;
    public bool IsGasBoosting() => isGasBoosting;

    // =====================================================
    // 地面判定
    // =====================================================

    private bool CheckGrounded()
    {
        if (playerCollider == null)
        {
            Debug.LogWarning("PlayerにCollider2Dがありません！");
            return false;
        }

        Bounds bounds = playerCollider.bounds;

        Vector2 origin = new Vector2(
            bounds.center.x,
            bounds.min.y + 0.05f
        );

        float checkDistance = groundCheckDistance + 0.1f;

        int count = Physics2D.Raycast(
            origin,
            Vector2.down,
            groundFilter,
            hitBuffer,
            checkDistance
        );

        // 自分自身のColliderは除外(2Dは内側からのレイも当たるため)
        for (int i = 0; i < count; i++)
        {
            Collider2D col = hitBuffer[i].collider;

            if (col == playerCollider ||
                col.transform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // =====================================================
    // ジャンプ
    // =====================================================

    private void Jump()
    {
        if (rb == null) return;

        Vector2 velocity = rb.linearVelocity;

        // 落下中の速度をリセット
        if (velocity.y < 0f)
        {
            velocity.y = 0f;
        }

        velocity.y += jumpForce;
        rb.linearVelocity = velocity;
    }
}