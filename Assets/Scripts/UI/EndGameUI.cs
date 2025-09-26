using System;
using Manager;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UI
{
    public class EndGameUI : MonoBehaviour
    {
        [field: SerializeField]
        private Button HomeButton { get; set; }
        
        [field: SerializeField]
        private TextMeshProUGUI PlayerEliminated { get; set; }
        
        [field: SerializeField]
        private TextMeshProUGUI Coins { get; set; }
        
        public static EndGameUI Instance;

        public void Awake()
        {
            HomeButton.onClick.AddListener(()=> GameManager.Instance.LeaveGame());
            Hide();
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        public void Show()
        {
            PlayerCounterUI.Instance.Disable();
            gameObject.SetActive(true);
        }
        
        public void UpdatePlayerEliminated(int eliminatedAmount) =>  PlayerEliminated.text = $"{eliminatedAmount}";
        public void UpdateCoins(int coins) => Coins.text = $"{coins}";

        public void Hide() => this.gameObject.SetActive(false);
    }
}