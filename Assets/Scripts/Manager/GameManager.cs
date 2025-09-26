using System;
using System.Collections.Generic;
using Controllers;
using UI;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Multiplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manager
{
    public class GameManager : MonoBehaviour
    {
       public enum SceneNames
       {
           Lobby = 0,
           Game = 1,
           Client = 2,
           Server = 3,
           Startup = 4,
       }
       
       public static GameManager Instance { get; private set; }
       private const int TargetPlayersToEscape = 2;
       private Dictionary<string, PlayerController> PlayersByAuthId { get; set; }
       
#if NOT_SERVER
        private int TotalPlayersEliminated { get; set; } = 0; 
#else
        private NetworkVariable<EliminateCountData> TotalPlayersEliminated { get; set; } = new NetworkVariable<EliminateCountData>();
#endif

       private void Awake()
       {
           if (Instance != null && Instance != this)
           {
               Destroy(gameObject);
               return;
           }

           Instance = this;
           DontDestroyOnLoad(gameObject);
           Reset();
           
           PlayersByAuthId = new Dictionary<string, PlayerController>();
       }

       public void Reset()
       {
#if NOT_SERVER
           TotalPlayersEliminated = 0; 
#else
           TotalPlayersEliminated.Value = new EliminateCountData()
               { PlayerId = AuthenticationService.Instance.PlayerId, TotalPlayersEliminated = 0 };
#endif
       }

       public void LeaveGame()
       {
           GoToStartup();
       }

       private async void GoToStartup()
       {
#if NOT_SERVER
           Reset(); 
           SceneManager.LoadScene(SceneNames.Startup.ToString()); 
#else
           if (NetworkManager.Singleton.IsClient)
           {
               NetworkManager.Singleton.Shutdown();
               SceneManager.LoadScene(SceneNames.Startup.ToString());
           }

           if (NetworkManager.Singleton.IsServer)
           {
               Application.Quit();
           }
#endif
       }

       private async void OnApplicationQuit()
       {
#if UNITY_SERVER
           if (NetworkManager.Singleton.IsServer)
           {
               NetworkManager.Singleton.Shutdown();
               await MultiplayService.Instance.UnreadyServerAsync();
           }
#endif
       }

       public void NotifyPlayerElimination(bool notUpdate = false, string playerID = "")
       {
        #if NOT_SERVER
           if (!notUpdate) 
               TotalPlayersEliminated++;
           PlayerCounterUI.Instance.UpdateCounter(TargetPlayersToEscape - TotalPlayersEliminated);
        #else
            if (!notUpdate) 
                return;
            
            if (playerID != AuthenticationService.Instance.PlayerId)
                return;

            var newEliminatedData = TotalPlayersEliminated.Value;
            newEliminatedData.TotalPlayersEliminated++;
            TotalPlayersEliminated.Value = newEliminatedData;
            
            PlayerCounterUI.Instance.UpdateCounter(TargetPlayersToEscape - TotalPlayersEliminated.Value.TotalPlayersEliminated);
        #endif
       }

       public void Register(PlayerController playerController, string playerId)
       {
           if (!PlayersByAuthId.ContainsKey(playerId))
               PlayersByAuthId.Add(playerId,playerController);
       }

       public void Unregister(string playerId)
       {
           PlayersByAuthId.Remove(playerId);
       }

       public PlayerController GetPlayerControllerByAuthId(string playerId)
       {
           if (!PlayersByAuthId.ContainsKey(playerId))
               return null;
           
           return PlayersByAuthId[playerId];
       }

       public struct EliminateCountData
       {
           public string PlayerId { get; set; }
           public int TotalPlayersEliminated { get; set; }
       }
    }
}