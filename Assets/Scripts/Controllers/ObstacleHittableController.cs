using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class ObstacleHittableController : NetworkBehaviour
    {
        [field: SerializeField]
        private PlayerController PlayerController { get; set; }

        public PlayerController GetPlayerController() => PlayerController;
    }
}