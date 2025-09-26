using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class PuncherCollisionController : NetworkBehaviour
    {
        [field: SerializeField]
        private ObstacleController PuncherController { get; set; }
        [field: SerializeField] 
        private float AttackVFXDuration { get; set; } = 0.3f;
        [field: SerializeField]
        private SpriteRenderer HitVFXSpriteRender { get; set; }
        
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
                ApplyHitVFX();
            }
        }
        
        private void ApplyHitVFX()
        {
            StartCoroutine(PerformHitVFX());
        }

        private IEnumerator PerformHitVFX()
        {
            HitVFXSpriteRender.gameObject.SetActive(true);
            yield return new WaitForSeconds(AttackVFXDuration);
            HitVFXSpriteRender.gameObject.SetActive(false);
        }
    }
}