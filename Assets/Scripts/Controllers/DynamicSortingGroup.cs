using UnityEngine;
using UnityEngine.Rendering;

namespace Controllers
{
    public class DynamicSortingGroup : MonoBehaviour
    {
        [field: SerializeField]
        public Transform pivot;
        
        public float yMultiplier = 100f;
        
        public int orderOffset = 0;

        private SortingGroup sortingGroup;

        void Awake()
        {
            sortingGroup = GetComponent<SortingGroup>();
            if (pivot == null) pivot = transform;
        }

        void LateUpdate()
        {
            int baseOrder = -(int)Mathf.Round(pivot.position.y * yMultiplier);
            sortingGroup.sortingOrder = baseOrder + orderOffset;
        }
    }
}