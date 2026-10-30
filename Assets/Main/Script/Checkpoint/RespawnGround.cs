using UnityEngine;

public class RespawnGround : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        TryRespawn(collision.collider);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryRespawn(other);
    }

    private void TryRespawn(Collider other)
    {
        WireActionController player =
            other.GetComponentInParent<WireActionController>();

        if (player != null)
        {
            player.Respawn();
        }
    }
}