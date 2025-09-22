using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class GloveController : NetworkBehaviour
    {
        [field: SerializeField]
        private PlayerController PlayerController { get; set; }

        public PlayerController GetPlayerController() => PlayerController;
    }
}