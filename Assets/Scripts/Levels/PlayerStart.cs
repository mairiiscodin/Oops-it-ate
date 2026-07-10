using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class PlayerStart : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.85f);
            Gizmos.DrawCube(transform.position, Vector3.one * 0.72f);
        }
    }
}
