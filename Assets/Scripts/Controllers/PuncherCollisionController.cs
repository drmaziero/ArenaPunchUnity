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
#if !NOT_SERVER
             if (!IsServer)
                return;
            
             if (!PuncherController.IsExpanding.Value)
                return;
#else
            if (!PuncherController.IsExpanding)
                return;
#endif
            
            if (other.TryGetComponent<ObstacleHittableController>(out var playerBody))
            {
                PlayerController enemy = playerBody.GetPlayerController();
                Vector2 direction = (other.transform.position - transform.position).normalized;
#if NOT_SERVER
                if (enemy.IsHit)
                    return;
#else
                if (enemy.IsHit.Value)
                    return;
#endif
                enemy.ApplyPush(direction);
                enemy.GetHit();
            }
        }
    }
}