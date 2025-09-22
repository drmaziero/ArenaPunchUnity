using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class PuncherCollisionController : NetworkBehaviour
    {
        [field: SerializeField]
        private ObstacleController PuncherController { get; set; }
        private void OnTriggerStay2D(Collider2D other)
        {
            if (!IsServer)
                return;

            if (!PuncherController.IsExpanding.Value)
                return;
            
            if (other.TryGetComponent<ObstacleHittableController>(out var playerBody))
            {
                PlayerController enemy = playerBody.GetPlayerController();
                Vector2 direction = (other.transform.position - transform.position).normalized;
                if (enemy.IsHit.Value)
                    return;
                
                enemy.ApplyPush(direction);
                enemy.GetHit();
            }
        }
    }
}