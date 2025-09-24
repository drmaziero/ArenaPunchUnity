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
            FindMatchButton.gameObject.SetActive(false);
            var players = new List<Player>()
                { new(AuthenticationService.Instance.PlayerId, new Dictionary<string, object>()) };
            var attributes = new Dictionary<string, object>();
            var options = new CreateTicketOptions(QueueName, attributes);

            while (!await FindMatch(players, options))
                await Task.Delay(TimeSpan.FromSeconds(1));
        }
        
        private async Task<bool> FindMatch(List<Player> players, CreateTicketOptions options)
        {
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            var ticketResponse = await MatchmakerService.Instance.CreateTicketAsync(players, options);
            
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                var ticketStatusResponse = await MatchmakerService.Instance.GetTicketAsync(ticketResponse.Id);

                if (ticketStatusResponse?.Value is MultiplayAssignment assignment)
                {
                    switch (assignment.Status)
                    {
                        case MultiplayAssignment.StatusOptions.Found:
                            transport.SetConnectionData(assignment.Ip, (ushort)assignment.Port);
                            bool result = NetworkManager.Singleton.StartClient();
                            StatusDebug.SetText($"[CLIENT] Start Client: {result} on {assignment.Ip}:{assignment.Port}");
                            return result;

                        case MultiplayAssignment.StatusOptions.InProgress:
                            StatusDebug.SetText("[CLIENT] In Progress.");
                            return false;

                        case MultiplayAssignment.StatusOptions.Failed:
                            Debug.Log($"[CLIENT] Matchmaker failed. {assignment.Message}");
                            StatusDebug.SetText($"[CLIENT] Matchmaker failed. {assignment.Message}");
                            return false;

                        case MultiplayAssignment.StatusOptions.Timeout:
                            StatusDebug.SetText("[CLIENT] Matchmaker Timeout.");
                            return false;
                    }
                }
            }
        }
    }
}
