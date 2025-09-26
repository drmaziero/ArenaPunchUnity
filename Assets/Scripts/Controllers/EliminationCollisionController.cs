using System;
using Manager;
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
                if (this.GetComponent<PlayerController>().IsNotServerLocalPlayer)
                {
                    GameOverUI.Instance.Show();
                }
                else
                    GameManager.Instance.NotifyPlayerElimination();
#endif
            }
        }

        [ClientRpc]
        private void ShowGameOverClientRpc(ClientRpcParams rpcParams = default)
        {
            if (IsOwner)
            {
                Debug.LogWarning("Show Game Over UI");
                GameOverUI.Instance.Show();
            }
            else
                GameManager.Instance.NotifyPlayerElimination();
        }
    }
}