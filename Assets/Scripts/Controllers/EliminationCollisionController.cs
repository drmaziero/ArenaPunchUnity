using System;
using UI;
using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class EliminationCollisionController : NetworkBehaviour
    {
        private void OnCollisionEnter2D(Collision2D other)
        {
#if !NOT_SERVER
            if (!IsServer)
                return;
#endif

            if (other.collider.CompareTag("RingLimit"))
            {
#if !NOT_SERVER
                ulong ownerClientId = OwnerClientId;
                ShowGameOverClientRpc(new ClientRpcParams()
                {
                    Send = new ClientRpcSendParams() { TargetClientIds = new[] { ownerClientId } }
                });
                NetworkObject.Despawn(true);
#else
                Destroy(this.gameObject);
                GameOverUI.Instance.Show();
#endif
            }
        }

        [ClientRpc]
        private void ShowGameOverClientRpc(ClientRpcParams rpcParams = default)
        {
            Debug.LogWarning("Show Game Over UI");
            GameOverUI.Instance.Show();
        }
    }
}