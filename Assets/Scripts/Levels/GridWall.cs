using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class GridWall : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.45f, 0.45f, 0.45f, 0.85f);
            Gizmos.DrawCube(transform.position, Vector3.one);
        }
    }
}
