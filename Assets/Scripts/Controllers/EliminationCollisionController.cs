using System;
using System.Collections;
using Manager;
using UI;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

namespace Controllers
{
    public class EliminationCollisionController : NetworkBehaviour
    {
#if NOT_SERVER
        private bool IsEliminated = false;
#else
        private NetworkVariable<bool> IsEliminated = new NetworkVariable<bool>(false);
#endif
        
        private void OnCollisionEnter2D(Collision2D other)
        {
#if !NOT_SERVER
            if (!IsServer)
                return;
#endif

            if (other.collider.CompareTag("RingLimit"))
            {
#if NOT_SERVER
                if (!IsEliminated)
                    StartCoroutine(StartingElimination());
#else
                if (!IsEliminated.Value)
                    StartCoroutine(StartingElimination());
#endif
            }
        }

        [ClientRpc]
        private void ShowGameOverClientRpc(string notifyPlayerId)
        {
#if !NOT_SERVER
            if (IsOwner)
            {
                Debug.LogWarning("Show Game Over UI");
                EndGameUI.Instance.Show();
            }
            else
                GameManager.Instance.UpdateOrCreatePlayerEliminationDataRpc(notifyPlayerId);
#endif
        }

        private IEnumerator StartingElimination()
        {
#if NOT_SERVER
            IsEliminated = true;
#else
            IsEliminated.Value = true;
#endif
            this.gameObject.GetComponent<PlayerController>().Eliminate();
            yield return new WaitForSeconds(0.25f);
#if !NOT_SERVER
            string attackerPlayerID = GetComponent<PlayerController>().AttackPlayerId.Value.ToString();
            ShowGameOverClientRpc(attackerPlayerID);
            NetworkObject.Despawn(true);
            var attackPlayerController = GameManager.Instance.GetPlayerControllerByAuthId(attackerPlayerID);
            if (attackPlayerController != null)
                attackPlayerController.AddCoinsServerRpc(attackerPlayerID, GetComponent<PlayerController>().GetHalfCoins());
            GetComponent<PlayerController>().LoseCoinsServerRpc(AuthenticationService.Instance.PlayerId);
#else
            Debug.Log($"Eliminate Player by: {gameObject.GetComponent<PlayerController>().AttackPlayerId}");
            GameManager.Instance.Unregister(gameObject.GetComponent<PlayerController>().MyPlayerId);
            Destroy(this.gameObject);
            if (this.GetComponent<PlayerController>().IsNotServerLocalPlayer)
            {
                EndGameUI.Instance.Show();
            }
            else
                GameManager.Instance.NotifyPlayerElimination();
            
            var attackPlayerController = GameManager.Instance.GetPlayerControllerByAuthId(gameObject.GetComponent<PlayerController>().AttackPlayerId);
            if (attackPlayerController != null)
                attackPlayerController.AddCoinsLocal(GetComponent<PlayerController>().GetHalfCoins());
            GetComponent<PlayerController>().LoseCoinsLocal();
#endif
           
#if NOT_SERVER
            IsEliminated = false;
#else
            IsEliminated.Value = false;
#endif
            
        }
    }
}