using System;
using System.Collections.Generic;
using System.Linq;
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
    public class GameManager : NetworkBehaviour
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
       private Dictionary<ulong, FixedString128Bytes> AuthIdByClientId { get; set; }
       
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
           AuthIdByClientId = new Dictionary<ulong, FixedString128Bytes>();
           TotalPlayersEliminated = new NetworkList<EliminateCountData>();
           TotalPlayersEliminated.OnListChanged += OnEliminationListChanged;
       }

       private void OnEliminationListChanged(NetworkListEvent<EliminateCountData> changeEvent)
       {
           switch (changeEvent.Type)
           {
               case NetworkListEvent<EliminateCountData>.EventType.Add:
                   if (NetworkManager.Singleton.IsClient)
                   {
                       if (changeEvent.Value.PlayerId.ToString() == AuthenticationService.Instance.PlayerId)
                       {
                           Debug.Log($"[Client] Update Counter (Add): {TargetPlayersToEscape - changeEvent.Value.TotalPlayersEliminated}");
                           PlayerCounterUI.Instance.UpdateCounter(TargetPlayersToEscape - changeEvent.Value.TotalPlayersEliminated);
                           EndGameUI.Instance.UpdatePlayerEliminated(changeEvent.Value.TotalPlayersEliminated);
                       }
                   }

                   break;
               case NetworkListEvent<EliminateCountData>.EventType.Insert:
                   break;
               case NetworkListEvent<EliminateCountData>.EventType.Remove:
                   break;
               case NetworkListEvent<EliminateCountData>.EventType.RemoveAt:
                   break;
               case NetworkListEvent<EliminateCountData>.EventType.Value:
                   if (NetworkManager.Singleton.IsClient)
                   {
                       if (changeEvent.Value.PlayerId.ToString() == AuthenticationService.Instance.PlayerId)
                       {
                           Debug.Log($"[Client] Update Counter (Update): {TargetPlayersToEscape - changeEvent.Value.TotalPlayersEliminated}");
                           PlayerCounterUI.Instance.UpdateCounter(TargetPlayersToEscape - changeEvent.Value.TotalPlayersEliminated);
                           EndGameUI.Instance.UpdatePlayerEliminated(changeEvent.Value.TotalPlayersEliminated);
                       }
                   }
                   break;
               case NetworkListEvent<EliminateCountData>.EventType.Clear:
                   break;
               case NetworkListEvent<EliminateCountData>.EventType.Full:
                   break;
               default:
                   throw new ArgumentOutOfRangeException();
           }
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
       public void UpdateOrCreatePlayerEliminationServerRpc(FixedString128Bytes playerId)
       {
           for (var i = 0; i < TotalPlayersEliminated.Count; i++)
           {
               if (TotalPlayersEliminated[i].PlayerId == playerId)
               {
                   var newData = TotalPlayersEliminated[i];
                   newData.TotalPlayersEliminated++;
                   TotalPlayersEliminated[i] = newData;
                   return;
               }
           }
           
           TotalPlayersEliminated.Add(new EliminateCountData(){PlayerId = playerId, TotalPlayersEliminated = 0});
       }
       
       [ServerRpc]
       public void RemoveEliminationServerRpc(FixedString128Bytes playerId)
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

       [ServerRpc(RequireOwnership = false)]
       public void RegisterServerRpc(NetworkObjectReference playerRef, FixedString128Bytes playerId, ulong clientId)
       {
           Debug.Log($"Try Register: Length = {PlayersByAuthId.Count}");
           if (PlayersByAuthId.ContainsKey(playerId))
               return;

           if (playerRef.TryGet(out NetworkObject netObj))
           {
               Debug.Log($"Register: player Id = {playerId}, client id = {clientId}");
               PlayersByAuthId.Add(playerId, netObj.GetComponent<PlayerController>());
               AuthIdByClientId.Add(clientId, playerId);
           }
       }

       [ServerRpc(RequireOwnership = false)]
       public void UnregisterServerRpc(FixedString128Bytes playerId)
       {
           PlayersByAuthId.Remove(playerId);
           foreach (var x in AuthIdByClientId)
           {
               if (x.Value == playerId)
                   AuthIdByClientId.Remove(x.Key);
           }
       }
       
       public PlayerController GetPlayerControllerByAuthId(FixedString128Bytes playerId)
       {
           if (string.IsNullOrEmpty(playerId.ToString()))
               return null;
           
           return !PlayersByAuthId.ContainsKey(playerId) ? null : PlayersByAuthId[playerId];
       }

       public FixedString128Bytes GetAuthIdByClientId(ulong clientId)
       {
           Debug.Log("Get Auth Id by Client Id");
           foreach (var x in AuthIdByClientId)
           {
               Debug.Log($"Auth Id by Client Id: {x.Key} => {x.Value}, param: {clientId}");
           }
           
           return !AuthIdByClientId.ContainsKey(clientId) ? "" : AuthIdByClientId[clientId];
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