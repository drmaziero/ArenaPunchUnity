using System.Collections;
using TMPro;
using UnityEngine;

namespace UI
{
    public class CoinsCounterUI : MonoBehaviour
    {
        [field: SerializeField]
        private TextMeshProUGUI TotalCoins { get; set; }
        
        [field: SerializeField]
        private TextMeshProUGUI AddCoinsAmount { get; set; }
        
        public static CoinsCounterUI Instance;
        private Coroutine ShowCoinsAmountCoroutine { get; set; }

        public void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);

            AddCoinsAmount.text = "";
            ShowCoinsAmountCoroutine = null;
        }

        public void UpdateTotalCoins(int coins)
        {
            TotalCoins.text = $"{coins},00";
        }

        public void UpdateAddCoinsAmount(int coinsAmount)
        {
            StopCoroutine(ShowCoinsAmountCoroutine); 
            ShowCoinsAmountCoroutine = StartCoroutine(ShowAddCoins(coinsAmount));
        }

        private IEnumerator ShowAddCoins(int coinsAmount)
        {
            AddCoinsAmount.text = $"{coinsAmount},00";
            yield return new WaitForSeconds(0.5f);
            AddCoinsAmount.text = "";
        }

    }
}