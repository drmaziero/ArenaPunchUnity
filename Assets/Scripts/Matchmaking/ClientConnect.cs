using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
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
        private TextMeshProUGUI StatusDebug { get; set; }

        private CreateTicketResponse createTicketResponse;
        private float pollTicketTimer;
        private float pollTicketTimerMax = 1.1f;

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
            
            NetworkManager.Singleton.OnClientConnectedCallback += ClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;

            FindMatchButton.onClick.AddListener(FindMatchAsync);
        }

        private void OnDestroy()
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= ClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= ClientDisconnected;
        }

        private void ClientDisconnected(ulong clientId)
        {
            ServerBootstrap.Instance.UnregisterPlayerIdServerRpc(AuthenticationService.Instance.PlayerId);
        }

        private void ClientConnected(ulong clientId)
        {
            ServerBootstrap.Instance.RegisterPlayerIdServerRpc(AuthenticationService.Instance.PlayerId);
        }

        private async void FindMatchAsync()
        {
            FindMatchButton.gameObject.SetActive(false);
            var players = new List<Player>()
                { new(AuthenticationService.Instance.PlayerId, new Dictionary<string, object>()) };
            var attributes = new Dictionary<string, object>();
            var options = new CreateTicketOptions(QueueName, attributes);
            
            createTicketResponse = await MatchmakerService.Instance.CreateTicketAsync(players, options);
            pollTicketTimer = pollTicketTimerMax;
        }

        private void Update()
        {
            if (createTicketResponse != null)
            {
                pollTicketTimer -= Time.deltaTime;
                if (pollTicketTimer <= 0.0f)
                {
                    pollTicketTimer = pollTicketTimerMax;
                    PollMatchmakerTicket();
                }
            }
        }

        private async void PollMatchmakerTicket()
        {
            Debug.Log("[Client] PollMatchmakerTicket");
            var ticketStatusResponse = await MatchmakerService.Instance.GetTicketAsync(createTicketResponse.Id);

            if (ticketStatusResponse == null)
            {
                Debug.Log("[Client] ticket keep waiting");
                return;
            }
            
            if (ticketStatusResponse?.Value is MultiplayAssignment assignment)
            {
                Debug.Log($"[Client] Assignment: Status: {assignment.Status}, IP: {assignment.Ip}, Port={assignment.Port}");
                switch (assignment.Status)
                {
                    case MultiplayAssignment.StatusOptions.Found:
                        NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(assignment.Ip, (ushort)assignment.Port);
                        bool result = NetworkManager.Singleton.StartClient();
                        StatusDebug.SetText($"[CLIENT] Start Client: {result} on {assignment.Ip}:{assignment.Port}");
                        break;

                    case MultiplayAssignment.StatusOptions.InProgress:
                        StatusDebug.SetText("[CLIENT] In Progress.");
                        break;

                    case MultiplayAssignment.StatusOptions.Failed:
                        Debug.Log($"[CLIENT] Matchmaker failed. {assignment.Message}");
                        StatusDebug.SetText($"[CLIENT] Matchmaker failed. {assignment.Message}");
                        break;

                    case MultiplayAssignment.StatusOptions.Timeout:
                        StatusDebug.SetText("[CLIENT] Matchmaker Timeout.");
                        break;
                }
            }
        }
    }
}
