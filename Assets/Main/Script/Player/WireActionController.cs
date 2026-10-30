using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class WireActionController : MonoBehaviour
{
    // =====================================================
    // カメラ・ワイヤー設定
    // =====================================================

    [Header("カメラ・ワイヤー設定")]
    [Tooltip("照準方向を取得するカメラ")]
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
    // WASD移動設定
    // =====================================================

    [Header("WASD移動設定")]
    [Tooltip("通常移動速度")]
    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("通常移動の加速度")]
    [SerializeField] private float moveAcceleration = 25f;

    [Tooltip("入力がないときの減速")]
    [SerializeField] private float moveDeceleration = 15f;

    [Tooltip("移動方向にPlayerを向ける")]
    [SerializeField] private bool faceMoveDirection = true;

    [Tooltip("向きを変更する速度")]
    [SerializeField] private float rotationSpeed = 12f;


    [Header("空中移動設定")]
    [Tooltip("空中での加速度(m/s?)。大きいほど空中で動きやすい")]
    [SerializeField] private float airMoveAcceleration = 15f;

    [Tooltip("空中で入力方向に出せる速度。これを超える速度は削られない")]
    [SerializeField] private float airMoveMaxSpeed = 7f;

    // =====================================================
    // ダッシュ&ジャンプ設定
    // =====================================================

    [Header("ダッシュ設定")]
    [Tooltip("Ctrl + Wでダッシュする速度")]
    [SerializeField] private float dashSpeed = 18f;

    [Tooltip("ダッシュ時の加速度")]
    [SerializeField] private float dashAcceleration = 60f;

    [Tooltip("ダッシュ中の最大水平速度")]
    [SerializeField] private float maxDashSpeed = 20f;

    [Header("ジャンプ設定")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayers = ~0;

    private bool isGrounded;

    // =====================================================
    // ワイヤー引き寄せ設定
    // =====================================================

    [Header("ワイヤー引き寄せ設定")]
    [Tooltip("接続地点へ引き寄せる加速度")]
    [SerializeField] private float pullAcceleration = 12f;

    [Tooltip("引き寄せ方向の速度がこの値を超えている間は、追加の引き寄せを行わない(速度を削ることはしない)。0以下なら制限なし")]
    [SerializeField] private float maxPullSpeed = 25f;

    [Tooltip("ロープの最短長さ。これより近づくと引き寄せを止め、振り子のように振れる")]
    [SerializeField] private float minRopeLength = 2f;

    [Tooltip("接続地点にどれだけ近づくと引き寄せが弱まり始めるか(オーバーシュート防止)")]
    [SerializeField] private float pullFalloffDistance = 5f;

    [Tooltip("目標速度を超えて地上を移動しているときの減速度(小さいほど勢いが残る)")]
    [SerializeField] private float groundOverspeedDeceleration = 4f;


    [Header("ワイヤー解除後の慣性")]
    [SerializeField] private float wireReleaseDeceleration = 0.5f;


    // =====================================================
    // ガス設定
    // =====================================================

    [Header("ガス設定")]
    [Tooltip("ガスの最大量")]
    [SerializeField] private float maxGas = 100f;

    [Tooltip("開始時のガス量")]
    [SerializeField] private float startingGas = 100f;

    [Tooltip("ガス噴射中の加速力")]
    [SerializeField] private float gasBoostForce = 30f;

    [Tooltip("1秒あたりのガス消費量")]
    [SerializeField] private float gasConsumptionRate = 25f;

    [Tooltip("ガスを使用していないときの回復量/秒")]
    [SerializeField] private float gasRecoveryRate = 10f;

    [Tooltip("ガス噴射中の最大水平速度。0以下なら制限なし")]
    [SerializeField] private float maxGasBoostSpeed = 35f;

    [Tooltip("ガス噴射をワイヤー接続中だけ許可する")]
    [SerializeField] private bool gasRequiresAttachedWire = true;

    // =====================================================
    // ガスエフェクト
    // =====================================================

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

    private Rigidbody rb;

    private enum WireState
    {
        Idle,
        Extending,
        Attached,
        Retracting
    }

    private WireState wireState = WireState.Idle;

    private Vector3 wireTargetPoint;
    private Vector3 wireTipPosition;
    private Vector3 moveDirection;

    private bool gasInputHeld;
    private bool isGasBoosting;
    private bool dashInputHeld;

    private bool HasAttachedWire =>
        wireState == WireState.Attached;

    private float currentGas;

    // 現在のロープの長さ(接続時の距離。縮む一方で伸びない)
    private float ropeLength;

    // 復帰地点(最初は開始位置)
    private Vector3 respawnPosition;
    private Quaternion respawnRotation;

    // =====================================================
    // 初期化
    // =====================================================

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

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

        // 最初のチェックポイントは開始位置
        respawnPosition = transform.position;
        respawnRotation = transform.rotation;

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
            return;
        }

        // WASD移動入力
        ReadMovementInput(keyboard);

        // Ctrl + Wでダッシュ
        dashInputHeld =
            (keyboard.leftCtrlKey.isPressed ||
             keyboard.rightCtrlKey.isPressed) &&
            keyboard.wKey.isPressed;

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

        // =====================================================
        // ジャンプ入力
        // =====================================================
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            if (CheckGrounded())
            {
                Jump();
            }
        }

        // ワイヤーの状態を更新
        UpdateWire();

        // ガスの消費・回復
        UpdateGas();

        // ワイヤー描画
        UpdateWireLine();
    }

    // =====================================================
    // WASD入力をカメラ基準の方向に変換
    // =====================================================


    private void ReadMovementInput(Keyboard keyboard)
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed) horizontal += 1f;
        if (keyboard.sKey.isPressed) vertical -= 1f;
        if (keyboard.wKey.isPressed) vertical += 1f;

        // カメラが向いている方向を基準に移動する
        Vector3 forward;
        Vector3 right;

        if (playerCamera != null)
        {
            forward = playerCamera.transform.forward;
            right = playerCamera.transform.right;
        }
        else
        {
            forward = transform.forward;
            right = transform.right;
        }

        // 上下方向の視点角度を移動方向に反映させない
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        // Wで前進、Sで後退、A/Dで左右移動
        moveDirection =
            forward * vertical +
            right * horizontal;

        // 斜め移動の速度を一定にする
        moveDirection = Vector3.ClampMagnitude(
            moveDirection, 1f
        );

        // Playerの回転処理は行わない
    }

    // =====================================================
    // ワイヤー射出
    // =====================================================

    private void ShootWire()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning(
                "WireActionController: Player Cameraが未設定です。"
            );
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        // 自分自身のColliderを避けて接続先を検索
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            maxWireDistance,
            wireTargetLayers,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        bool foundTarget = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform) ||
                hit.collider.transform == transform)
            {
                continue;
            }

            wireTargetPoint = hit.point;
            foundTarget = true;
            break;
        }

        if (!foundTarget)
        {
            Debug.Log("ワイヤーが届く範囲に対象がありません。");
            return;
        }

        wireTipPosition = GetWireStartPosition();
        wireState = WireState.Extending;

        if (wireLine != null)
        {
            wireLine.enabled = true;
        }
    }

    // =====================================================
    // ワイヤー始点
    // =====================================================

    private Vector3 GetWireStartPosition()
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

    // =====================================================
    // ワイヤー射出アニメーション
    // =====================================================

    private void UpdateWireExtending()
    {
        if (Mouse.current == null ||
            !Mouse.current.rightButton.isPressed)
        {
            StartRetracting();
            return;
        }

        wireTipPosition = Vector3.MoveTowards(
            wireTipPosition,
            wireTargetPoint,
            wireExtendSpeed * Time.deltaTime
        );

        if (Vector3.Distance(
            wireTipPosition, wireTargetPoint) < 0.02f)
        {
            wireTipPosition = wireTargetPoint;
            wireState = WireState.Attached;

            // 接続した瞬間の距離をロープの長さにする
            ropeLength = Mathf.Max(
                Vector3.Distance(rb.position, wireTargetPoint),
                minRopeLength
            );
        }
    }

    // =====================================================
    // ワイヤー回収
    // =====================================================

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
        wireTipPosition = Vector3.MoveTowards(
            wireTipPosition,
            GetWireStartPosition(),
            wireRetractSpeed * Time.deltaTime
        );

        if (Vector3.Distance(
            wireTipPosition, GetWireStartPosition()) < 0.05f)
        {
            wireTipPosition = GetWireStartPosition();
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

        // WASD移動とダッシュ
        ApplyMovement();

        // ワイヤーによる引き寄せ
        if (HasAttachedWire)
        {
            ApplyWirePull();
        }

        // ガス噴射
        if (isGasBoosting)
        {
            ApplyGasBoost();
        }

    }

    // =====================================================
    // 移動・ダッシュ処理
    // =====================================================


    private void ApplyMovement()
    {
        bool hasMovement =
            moveDirection.sqrMagnitude > 0.01f;

        // 地面判定
        // ワイヤー接続中は地上の減速処理を使わない(勢いを保つ)
        bool grounded = CheckGrounded() && !HasAttachedWire;

        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(
            velocity.x, 0f, velocity.z
        );

        // =====================================================
        // 入力がないとき
        // =====================================================
        if (!hasMovement)
        {
            // ワイヤー接続中やガス噴射中は慣性を維持
            if (HasAttachedWire || isGasBoosting)
            {
                return;
            }

            if (grounded)
            {
                // 地上は今までどおり減速
                Vector3 slowedVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    Vector3.zero,
                    moveDeceleration * Time.fixedDeltaTime
                );

                rb.AddForce(
                    slowedVelocity - horizontalVelocity,
                    ForceMode.VelocityChange
                );
            }
            else
            {
                // 空中では弱い減速だけを行い、慣性を残す
                // 速度を airMoveMaxSpeed で強制的に制限しない
                Vector3 slowedVelocity = Vector3.MoveTowards(
                    horizontalVelocity,
                    Vector3.zero,
                    wireReleaseDeceleration * Time.fixedDeltaTime
                );

                rb.AddForce(
                    slowedVelocity - horizontalVelocity,
                    ForceMode.VelocityChange
                );
            }

            return;
        }

        // =====================================================
        // 空中移動
        // =====================================================
        if (!grounded)
        {
            // 空中操作は「入力方向への加速を足すだけ」にする。
            // 速度を削ったり向きを無理に変えたりしないので、
            // ワイヤーで付いた勢いや振り子の動きを壊さない。
            Vector3 inputDirection = moveDirection.normalized;

            float speedAlongInput =
                Vector3.Dot(horizontalVelocity, inputDirection);

            // 入力方向の速度が airMoveMaxSpeed 未満のときだけ加速する
            if (speedAlongInput < airMoveMaxSpeed)
            {
                float addSpeed = Mathf.Min(
                    airMoveAcceleration * Time.fixedDeltaTime,
                    airMoveMaxSpeed - speedAlongInput
                );

                rb.AddForce(
                    inputDirection * addSpeed,
                    ForceMode.VelocityChange
                );
            }

            return;
        }

        // =====================================================
        // 地上移動・ダッシュ（従来どおり）
        // =====================================================
        bool isDashing =
            dashInputHeld && moveDirection != Vector3.zero;

        float targetSpeed = isDashing
            ? dashSpeed
            : moveSpeed;

        float acceleration = isDashing
            ? dashAcceleration
            : moveAcceleration;

        // 目標速度を超えているときは急ブレーキをかけず、ゆるやかに減速する
        if (horizontalVelocity.magnitude > targetSpeed)
        {
            acceleration = Mathf.Min(
                acceleration,
                groundOverspeedDeceleration
            );
        }

        Vector3 targetGroundVelocity =
            moveDirection * targetSpeed;

        Vector3 nextGroundVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetGroundVelocity,
            acceleration * Time.fixedDeltaTime
        );

        rb.AddForce(
            nextGroundVelocity - horizontalVelocity,
            ForceMode.VelocityChange
        );

        // 地上ダッシュ中の水平速度を制限
        if (isDashing && maxDashSpeed > 0f)
        {
            Vector3 current = rb.linearVelocity;

            Vector3 horizontal = new Vector3(
                current.x, 0f, current.z
            );

            if (horizontal.magnitude > maxDashSpeed)
            {
                horizontal = horizontal.normalized * maxDashSpeed;

                rb.linearVelocity = new Vector3(
                    horizontal.x,
                    current.y,
                    horizontal.z
                );
            }
        }
    }

    // =====================================================
    // ワイヤー引き寄せ
    // =====================================================

    private void ApplyWirePull()
    {
        Vector3 toAnchor = wireTargetPoint - rb.position;
        float distance = toAnchor.magnitude;

        if (distance < 0.01f)
        {
            return;
        }

        Vector3 dirToAnchor = toAnchor / distance;

        // -------------------------------------------------
        // 1. 引き寄せ(リール)
        //    速度を削らず、足りないときだけ加速する。
        //    接続地点に近づくほど弱めて、通り過ぎて戻される動きを防ぐ。
        // -------------------------------------------------
        float inwardSpeed =
            Vector3.Dot(rb.linearVelocity, dirToAnchor);

        bool underSpeedLimit =
            maxPullSpeed <= 0f || inwardSpeed < maxPullSpeed;

        if (distance > minRopeLength && underSpeedLimit)
        {
            float falloff = Mathf.Clamp01(
                (distance - minRopeLength) /
                Mathf.Max(0.01f, pullFalloffDistance)
            );

            rb.AddForce(
                dirToAnchor * pullAcceleration * falloff,
                ForceMode.Acceleration
            );
        }

        // -------------------------------------------------
        // 2. ロープの拘束
        //    ロープより外へ出ようとする速度成分だけを消す。
        //    横方向(接線)の速度は残るので、振り子のように勢いが続く。
        // -------------------------------------------------
        if (distance > ropeLength)
        {
            Vector3 outward = -dirToAnchor;
            Vector3 velocity = rb.linearVelocity;

            float outwardSpeed = Vector3.Dot(velocity, outward);

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
            currentGas -=
                gasConsumptionRate * Time.deltaTime;

            currentGas = Mathf.Max(0f, currentGas);

            if (currentGas <= 0f)
            {
                isGasBoosting = false;
            }
        }
        else
        {
            currentGas +=
                gasRecoveryRate * Time.deltaTime;

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
    // ガス噴射
    // =====================================================


    private void ApplyGasBoost()
    {
        // カメラが設定されていなければ何もしない
        if (playerCamera == null)
        {
            return;
        }

        // カメラが向いている方向を取得
        Vector3 boostDirection = playerCamera.transform.forward.normalized;

        // 噴射方向の速度が上限未満のときだけ加速する。
        // 上限を超えていても速度は削らない(急な減速を防ぐ)。
        float speedAlongBoost =
            Vector3.Dot(rb.linearVelocity, boostDirection);

        if (maxGasBoostSpeed <= 0f || speedAlongBoost < maxGasBoostSpeed)
        {
            // カメラの上下方向も含めてガス噴射する
            rb.AddForce(
                boostDirection * gasBoostForce,
                ForceMode.Acceleration
            );
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

    /// <summary>
    /// 復帰地点を更新する(Checkpointから呼ばれる)
    /// </summary>
    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        respawnPosition = position;
        respawnRotation = rotation;
    }

    /// <summary>
    /// 最後に通ったチェックポイントへ戻る(RespawnGroundから呼ばれる)
    /// </summary>
    public void Respawn()
    {
        // ワイヤーを即座に解除
        wireState = WireState.Idle;

        if (wireLine != null)
        {
            wireLine.enabled = false;
        }

        // ガス噴射を停止
        isGasBoosting = false;
        StopGasEffect();

        // 速度をリセットして移動
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = respawnPosition;
        rb.rotation = respawnRotation;
        transform.SetPositionAndRotation(respawnPosition, respawnRotation);
        Physics.SyncTransforms();

        if (refillGasOnRespawn)
        {
            currentGas = maxGas;
        }
    }

    // =====================================================
    // 外部から参照する関数
    // =====================================================

    public float GetCurrentGas()
    {
        return currentGas;
    }

    public float GetMaxGas()
    {
        return maxGas;
    }

    public bool IsWireAttached()
    {
        return HasAttachedWire;
    }

    public bool IsGasBoosting()
    {
        return isGasBoosting;
    }

    // =====================================================
    // 地面判定
    // =====================================================
    private bool CheckGrounded()
    {
        // プレイヤーのColliderを取得
        Collider playerCollider = GetComponent<Collider>();

        if (playerCollider == null)
        {
            Debug.LogWarning("PlayerにColliderがありません！");
            return false;
        }

        // Colliderの底面付近から下方向に判定
        Bounds bounds = playerCollider.bounds;

        Vector3 origin = new Vector3(
            bounds.center.x,
            bounds.min.y + 0.05f,
            bounds.center.z
        );

        float checkDistance = groundCheckDistance + 0.1f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            checkDistance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    // =====================================================
    // ジャンプ
    // =====================================================
    private void Jump()
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;

        // 落下中の速度をリセット
        if (velocity.y < 0f)
        {
            velocity.y = 0f;
        }

        rb.linearVelocity = velocity;

        // ジャンプ
        rb.AddForce(
            Vector3.up * jumpForce,
            ForceMode.VelocityChange
        );

        Debug.Log("ジャンプ！");
    }
}