using System;
using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class HitController : NetworkBehaviour
    {
        [field: SerializeField]
        private PlayerController Owner { get; set; }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;
            
            if (other.gameObject == Owner.gameObject)
                return;
            
            if (other.TryGetComponent<GloveController>(out var enemyGlover))
            {
                PlayerController enemy = enemyGlover.GetPlayerController();
                if (enemy.IsAttacking.Value)
                {
                    Vector2 pushDirection = (transform.position - enemy.transform.position).normalized;
                    Owner.ApplyPush(pushDirection);
                    Owner.GetHit();
                }
            }
        }
    }
}