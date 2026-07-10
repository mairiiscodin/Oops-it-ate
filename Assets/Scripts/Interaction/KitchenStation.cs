using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Interaction
{
    public sealed class KitchenStation : MonoBehaviour
    {
        [SerializeField] private GridPosition position;
        [SerializeField] private Color color = new Color(1f, 0.65f, 0.1f);

        public GridPosition Position => position;

        public void Initialize(GridPosition gridPosition)
        {
            position = gridPosition;
            transform.localScale = Vector3.one * 0.72f;

            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }

            renderer.material = new Material(FindUnlitShader());
            renderer.material.color = color;

            if (GetComponent<MeshFilter>() == null)
            {
                gameObject.AddComponent<MeshFilter>().mesh = CreateQuadMesh();
            }
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = color;
            Gizmos.DrawCube(transform.position, Vector3.one * 0.72f);
        }
    }
}
