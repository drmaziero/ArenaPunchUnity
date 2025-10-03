using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Controllers;
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
        [field: SerializeField] private Button FindMatchButton { get; set; }
        [field: SerializeField] private string QueueName { get; set; }
        [field: SerializeField] private TextMeshProUGUI StatusDebug { get; set; }

        private CreateTicketResponse createTicketResponse;
        private float pollTicketTimer;
        private float pollTicketTimerMax = 1.1f;

        private async void Start()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#endif
            
#if LOCAL_CLIENT
            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetConnectionData("127.0.0.1", 7777);
            bool result = NetworkManager.Singleton.StartClient();
            StatusDebug.SetText($"[ClientLocal] Start Client: {result}");
#else
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            NetworkManager.Singleton.OnClientConnectedCallback += ClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += ClientDisconnected;

            FindMatchButton.onClick.AddListener(FindMatchAsync);
#endif
        }

        private void OnDestroy()
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= ClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= ClientDisconnected;
        }

        private void ClientDisconnected(ulong clientId)
        {
            Debug.Log($"[Client] Disconnected: {NetworkManager.Singleton.DisconnectReason}");
            var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObj != null)
                playerObj.GetComponent<PlayerController>().UnregisterAuthIdServerRpc(AuthenticationService.Instance.PlayerId);
        }

        private void ClientConnected(ulong clientId)
        {
            Debug.Log("[Client] Connected!");
            var playerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;
            if (playerObj != null)
                playerObj.GetComponent<PlayerController>().RegisterAuthIdServerRpc(AuthenticationService.Instance.PlayerId);
        }

        private async void FindMatchAsync()
        {
            FindMatchButton.gameObject.SetActive(false);

            var players = new List<Player> { new Player(AuthenticationService.Instance.PlayerId) };
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
                if (pollTicketTimer <= 0f)
                {
                    pollTicketTimer = pollTicketTimerMax;
                    PollMatchmakerTicket();
                }
            }
        }

        private async void PollMatchmakerTicket()
        {
            var ticketStatusResponse = await MatchmakerService.Instance.GetTicketAsync(createTicketResponse.Id);

            if (ticketStatusResponse?.Value is MultiplayAssignment assignment)
            {
                switch (assignment.Status)
                {
                    case MultiplayAssignment.StatusOptions.Found:
                        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                        transport.SetConnectionData(assignment.Ip, (ushort)assignment.Port);
                        bool result = NetworkManager.Singleton.StartClient();
                        StatusDebug.SetText($"[Client] Connected to {assignment.Ip}:{assignment.Port} : [{result}]");
                        break;

                    case MultiplayAssignment.StatusOptions.InProgress:
                        StatusDebug.SetText("[Client] Matchmaking in progress...");
                        break;

                    case MultiplayAssignment.StatusOptions.Failed:
                        StatusDebug.SetText($"[Client] Matchmaker failed: {assignment.Message}");
                        break;

                    case MultiplayAssignment.StatusOptions.Timeout:
                        StatusDebug.SetText("[Client] Matchmaker timeout.");
                        break;
                }
            }
        }
    }
}