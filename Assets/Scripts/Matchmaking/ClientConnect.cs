using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Matchmaking
{
    public class ClientConnect : MonoBehaviour
    {
        [field: SerializeField] 
        private Button FindMatchButton { get; set; }
        [field: SerializeField] 
        private string QueueName { get; set; }
        [field: SerializeField] 
        private float PollInterval { get; set; }= 1.5f;

        private string ticketId;

        private async void Start()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#endif
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            FindMatchButton.onClick.AddListener(() => _ = FindMatchAsync());
        }

        private async Task FindMatchAsync()
        {
            var player = new Player(AuthenticationService.Instance.PlayerId);
            var create = await MatchmakerService.Instance.CreateTicketAsync(
                new List<Player> { player },
                new CreateTicketOptions(QueueName)
            );
            ticketId = create.Id;
            Debug.Log($"[CLIENT] Ticket: {ticketId}");
            
            while (!string.IsNullOrEmpty(ticketId))
            {
                var status = await MatchmakerService.Instance.GetTicketAsync(ticketId);

                if (status != null && status.Type == typeof(MultiplayAssignment))
                {
                    var assign = status.Value as MultiplayAssignment;
                    switch (assign.Status)
                    {
                        case MultiplayAssignment.StatusOptions.Found:
                            Debug.Log("[CLIENT] Servidor encontrado!");
                           ticketId = null;

                            string ip = assign.Ip;
                            ushort port = (ushort)assign.Port;

                            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
                            utp.SetConnectionData(ip, port);
                            NetworkManager.Singleton.StartClient();
                            return;

                        case MultiplayAssignment.StatusOptions.InProgress:
                            break;

                        case MultiplayAssignment.StatusOptions.Failed:
                            Debug.LogError("[CLIENT] Matchmaker falhou.");
                            ticketId = null;
                            return;

                        case MultiplayAssignment.StatusOptions.Timeout:
                            Debug.LogWarning("[CLIENT] Matchmaker timeout.");
                            ticketId = null;
                            return;
                    }
                }

                await Task.Delay((int)(PollInterval * 1000));
            }
        }
    }
}
