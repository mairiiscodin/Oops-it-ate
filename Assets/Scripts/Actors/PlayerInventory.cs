using System;
using UnityEngine;

namespace OopsItAte.Actors
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private bool hasFood;

        public bool HasFood => hasFood;
        public event Action<bool> HasFoodChanged;

        public bool TryTakeFood()
        {
            if (hasFood)
            {
                return false;
            }

            hasFood = true;
            HasFoodChanged?.Invoke(hasFood);
            Debug.Log("Player picked up food.");
            return true;
        }

        public bool TryUseFood()
        {
            if (!hasFood)
            {
                return false;
            }

            hasFood = false;
            HasFoodChanged?.Invoke(hasFood);
            Debug.Log("Player used food.");
            return true;
        }
    }
}
