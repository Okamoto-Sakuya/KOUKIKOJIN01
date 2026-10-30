using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("復帰位置。未設定ならこのオブジェクトの位置を使う")]
    [SerializeField] private Transform respawnPoint;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        WireActionController player =
            other.GetComponentInParent<WireActionController>();

        if (player == null) return;

        Transform point = respawnPoint != null ? respawnPoint : transform;
        player.SetCheckpoint(point.position, point.rotation);
    }
}