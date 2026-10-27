using System;
using OopsItAte.Audio;
using UnityEngine;

namespace OopsItAte.Actors
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private bool hasFood;

        public bool HasFood => hasFood;
        public event Action<bool> HasFoodChanged;

        public void SetHasFood(bool value)
        {
            if (hasFood == value)
            {
                return;
            }

            hasFood = value;
            HasFoodChanged?.Invoke(hasFood);
        }

        public bool TryTakeFood()
        {
            if (hasFood)
            {
                return false;
            }

            SetHasFood(true);
            SoundManager.Play(Sfx.Pickup);
            Debug.Log("Player picked up food.");
            return true;
        }

        public bool TryUseFood()
        {
            if (!hasFood)
            {
                return false;
            }

            SetHasFood(false);
            Debug.Log("Player used food.");
            return true;
        }
    }
}
