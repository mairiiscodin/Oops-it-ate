using UnityEngine;

namespace OopsItAte.Levels
{
    [AddComponentMenu("")]
    public sealed class LevelMapObject : MonoBehaviour
    {
        [SerializeField] private int markerCode;

        public char Marker => (char)markerCode;

        public void Configure(char value)
        {
            markerCode = value;
        }
    }
}
