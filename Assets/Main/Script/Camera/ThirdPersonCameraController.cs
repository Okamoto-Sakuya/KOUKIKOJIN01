using UnityEngine;

public class Camera2DController : MonoBehaviour
{
    [Header("追従対象")]
    [SerializeField] private Transform target;

    [Header("カメラ設定")]
    [Tooltip("対象からのオフセット。zは-10のままにする")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);

    [Tooltip("追従の速さ")]
    [SerializeField] private float followSpeed = 15f;

    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("Camera2DController：Targetを設定してください。");
            enabled = false;
            return;
        }

        transform.position = target.position + offset;
    }

    private void LateUpdate()
    {
        Vector3 desired = target.position + offset;

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
    }
}