using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Manager;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Matchmaking
{
    public class LobbyManager : MonoBehaviour
    {
        [field: SerializeField]
        private Button CreateLobbyButton { get; set; }
        
        [field: SerializeField]
        private Button JoinLobbyButton { get; set; }
        
        [field: SerializeField]
        private GameObject WaitingPlayerText { get; set; }
        [field: SerializeField]
        private GameObject ButtonsParent { get; set; }
        [field: SerializeField]
        private Button ConnectLocalButton { get; set; }
        

        private const int MaxPlayers = 4;
        private const int MinPlayersToStart = 1;
        private const string JoinCodeKey = "JoinCode";
        private const string LobbyName = "GameLobby";
        private Lobby CurrentLobby { get; set; }
        private float HeartBeatTimer { get; set; }

        private void Start()
        {
            #if UNITY_SERVER
                enabled = false;
            #endif
            //InitializeUnityAuthentication();
            
           //CreateLobbyButton.onClick.AddListener(CreateLobby);
            //JoinLobbyButton.onClick.AddListener(JoinLobby);
            //DontDestroyOnLoad(gameObject);
            //HideWaitingPlayersLabel();
            ConnectLocalButton.onClick.AddListener(ConnectLocal);
        }

        private async void InitializeUnityAuthentication()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        private async void CreateLobby()
        {
            ShowWaitingPlayersLabel();
            
            CurrentLobby = await Lobbies.Instance.CreateLobbyAsync(LobbyName, MaxPlayers);
            Debug.Log($"Lobby created: {CurrentLobby.Id}");


            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
            string joinCode = await GetRelyJoinCode(allocation);

            var lobbyData = new UpdateLobbyOptions()
            {
                Data = new Dictionary<string, DataObject>()
                {
                    { JoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) }
                }
            };

            await Lobbies.Instance.UpdateLobbyAsync(CurrentLobby.Id, lobbyData);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartHost();
            //InitAutoStartGame();
            //TryStartGame(); //Provisorio
        }

        private async void JoinLobby()
        {
            ShowWaitingPlayersLabel();
            CurrentLobby = await Lobbies.Instance.QuickJoinLobbyAsync();

            string joinCode = CurrentLobby.Data[JoinCodeKey].Value;
            JoinAllocation joinAllocation = await JoinRelay(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
            NetworkManager.Singleton.StartClient();
        }

        private async Task<string> GetRelyJoinCode(Allocation allocation)
        {
            try
            {
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                return relayJoinCode;
            }
            catch (RelayServiceException e)
            {
                Debug.Log(e);
                return string.Empty;
            }
        }

        private async Task<JoinAllocation> JoinRelay(string joinCode)
        {
            try
            {
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
                return joinAllocation;
            }
            catch (RelayServiceException e)
            {
                Debug.Log(e);
                return null;
            }
        }

        private async void Update()
        {
            if (CurrentLobby != null && AuthenticationService.Instance.PlayerId == CurrentLobby.HostId)
            {
                HeartBeatTimer -= Time.deltaTime;
                if (HeartBeatTimer <= 0.0f)
                {
                    HeartBeatTimer = 15.0f;
                    await Lobbies.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                }
            }
        }

       
        
       

        private void ShowWaitingPlayersLabel()
        {
            ButtonsParent.gameObject.SetActive(false);
            WaitingPlayerText.SetActive(true);
        }
        
        private void HideWaitingPlayersLabel()
        {
            ButtonsParent.gameObject.SetActive(true);
            WaitingPlayerText.SetActive(false);
        }
        
        private void ConnectLocal()
        {
            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetConnectionData("127.0.0.1", 7777);
            NetworkManager.Singleton.StartClient();
        }
    }
}