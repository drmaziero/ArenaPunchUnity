using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class WallController : NetworkBehaviour
    {
        private void OnCollisionStay(Collision other)
        {
            if (!IsServer)
                return;

            if (other.gameObject.TryGetComponent(out PlayerController enemyController))
            {
                enemyController.Death();
            }
        }
    }
}