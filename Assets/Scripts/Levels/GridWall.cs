using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class GridWall : MonoBehaviour
    {
        private void OnValidate()
        {
            MeshRenderer placeholder = GetComponent<MeshRenderer>();
            if (placeholder != null) placeholder.enabled = false;
        }
    }
}
