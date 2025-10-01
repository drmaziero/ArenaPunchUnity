using System;
using System.Collections.Generic;
using Controllers;
using UI;
using Unity.Collections;
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
       private Dictionary<FixedString128Bytes, PlayerController> PlayersByAuthId { get; set; }
       
#if NOT_SERVER
        private int TotalPlayersEliminated { get; set; } = 0; 
#else
        private NetworkList<EliminateCountData> TotalPlayersEliminated { get; set; }
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
#if NOT_SERVER
           Reset();
#endif
           PlayersByAuthId = new Dictionary<FixedString128Bytes, PlayerController>();
           TotalPlayersEliminated = new NetworkList<EliminateCountData>();
       }

       public void Reset()
       {
#if NOT_SERVER
           TotalPlayersEliminated = 0; 
#else
           TotalPlayersEliminated.Clear();
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
           EndGameUI.Instance.UpdatePlayerEliminated(TotalPlayersEliminated);
        #endif
       }

       [ServerRpc]
       public void UpdateOrCreatePlayerEliminationDataRpc(FixedString128Bytes playerId)
       {
           for (var i = 0; i < TotalPlayersEliminated.Count; i++)
           {
               if (TotalPlayersEliminated[i].PlayerId == playerId)
               {
                   var newData = TotalPlayersEliminated[i];
                   newData.TotalPlayersEliminated++;
                   TotalPlayersEliminated[i] = newData;

                   UpdateEliminationUIClientRpc(playerId, newData.TotalPlayersEliminated);
                   return;
               }
           }
           
           TotalPlayersEliminated.Add(new EliminateCountData(){PlayerId = playerId, TotalPlayersEliminated = 0});
           UpdateEliminationUIClientRpc(playerId, 0);
       }

       [ClientRpc]
       private void UpdateEliminationUIClientRpc(FixedString128Bytes playerId, int totalEliminatedCount)
       {
           Debug.Log("[Client] Update Elimination UI Client RPC");
           if (playerId != AuthenticationService.Instance.PlayerId)
               return;
           
           Debug.Log($"[Client] Update Counter: {TargetPlayersToEscape - totalEliminatedCount}");
           PlayerCounterUI.Instance.UpdateCounter(TargetPlayersToEscape - totalEliminatedCount);
           EndGameUI.Instance.UpdatePlayerEliminated(totalEliminatedCount);
       }

       [ServerRpc]
       public void RemoveEliminationDataRpc(FixedString128Bytes playerId)
       {
           for (var i = 0; i < TotalPlayersEliminated.Count; i++)
           {
               if (TotalPlayersEliminated[i].PlayerId == playerId)
               {
                   TotalPlayersEliminated.RemoveAt(i);
                   return;
               }
           }
       }

       public void Register(PlayerController playerController, FixedString128Bytes playerId)
       {
           if (!PlayersByAuthId.ContainsKey(playerId))
               PlayersByAuthId.Add(playerId,playerController);
       }

       public void Unregister(FixedString128Bytes playerId)
       {
           PlayersByAuthId.Remove(playerId);
       }

       public PlayerController GetPlayerControllerByAuthId(FixedString128Bytes playerId)
       {
           if (string.IsNullOrEmpty(playerId.ToString()))
               return null;
           
           if (!PlayersByAuthId.ContainsKey(playerId))
               return null;
           
           return PlayersByAuthId[playerId];
       }

       public struct EliminateCountData : INetworkSerializable, IEquatable<EliminateCountData>
       {
           public FixedString128Bytes PlayerId;
           public int TotalPlayersEliminated;
           
           public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
           {
               serializer.SerializeValue(ref PlayerId);
               serializer.SerializeValue(ref TotalPlayersEliminated);
           }

           public bool Equals(EliminateCountData other)
           {
               return PlayerId.Equals(other.PlayerId) && TotalPlayersEliminated == other.TotalPlayersEliminated;
           }

           public override bool Equals(object obj)
           {
               return obj is EliminateCountData other && Equals(other);
           }

           public override int GetHashCode()
           {
               return HashCode.Combine(PlayerId, TotalPlayersEliminated);
           }
       }
    }
}