using System;
using Manager;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UI
{
    public class GameOverUI : MonoBehaviour
    {
        [field: SerializeField]
        private Button HomeButton { get; set; }
        
        public static GameOverUI Instance;

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

        public void Hide() => this.gameObject.SetActive(false);
    }
}