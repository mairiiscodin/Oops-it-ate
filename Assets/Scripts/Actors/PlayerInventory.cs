using UnityEngine;

namespace OopsItAte.Actors
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private bool hasFood;

        public bool HasFood => hasFood;

        public bool TryTakeFood()
        {
            if (hasFood)
            {
                return false;
            }

            hasFood = true;
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
            Debug.Log("Player used food.");
            return true;
        }
    }
}
