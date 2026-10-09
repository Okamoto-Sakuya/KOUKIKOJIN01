
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform cameraTarget;

    [Header("視点切り替え")]
    [SerializeField] private bool startInFirstPerson = true;
    [SerializeField] private float thirdPersonDistance = 5f;
    [SerializeField] private float thirdPersonHeight = 0.5f;

    [Header("マウス感度")]
    [SerializeField] private float mouseSensitivity = 0.15f;

    [Header("上下視点制限")]
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 70f;

    [Header("カメラ追従")]
    [SerializeField] private float followSpeed = 15f;

    private float yaw;
    private float pitch;

    private bool isFirstPerson;

    public bool IsFirstPerson => isFirstPerson;

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("ThirdPersonCameraController：Playerを設定してください。");
            enabled = false;
            return;
        }

        isFirstPerson = startInFirstPerson;

        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;

        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        UpdateCameraPosition(true);
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        // マウスホイールクリックで視点を切り替える
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            isFirstPerson = !isFirstPerson;
            UpdateCameraPosition(true);
        }

        // マウスで視点を操作
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * mouseSensitivity;
        pitch -= mouseDelta.y * mouseSensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        UpdateCameraPosition(false);
    }

    private void UpdateCameraPosition(bool instant)
    {
        if (player == null)
        {
            return;
        }

        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 targetPosition;

        if (cameraTarget != null)
        {
            targetPosition = cameraTarget.position;
        }
        else
        {
            targetPosition = player.position + Vector3.up * 1.5f;
        }

        Vector3 desiredPosition;

        if (isFirstPerson)
        {
            // 一人称：キャラクターの目線位置
            desiredPosition = targetPosition;
        }
        else
        {
            // 三人称：キャラクターの後ろ
            desiredPosition =
                targetPosition
                - cameraRotation * Vector3.forward * thirdPersonDistance
                + Vector3.up * thirdPersonHeight;
        }

        if (instant)
        {
            transform.position = desiredPosition;
        }
        else
        {
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);

            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                t
            );
        }

        transform.rotation = cameraRotation;
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}