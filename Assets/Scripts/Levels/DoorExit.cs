using OopsItAte.Grid;
using UnityEngine;

namespace OopsItAte.Levels
{
    public sealed class DoorExit : MonoBehaviour
    {
        [SerializeField] private string targetSceneName;
        [SerializeField] private GridPosition position;
        [SerializeField] private Color color = new Color(0.9f, 0.15f, 0.15f);

        public string TargetSceneName => targetSceneName;
        public GridPosition Position => position;

        public void Initialize(GridSettings grid)
        {
            position = grid.WorldToGrid(transform.position);
            transform.position = grid.GridToWorld(position) + Vector3.back * 0.2f;
            transform.localScale = Vector3.one * grid.cellSize * 0.72f;
            EnsureVisual();
        }

        private void EnsureVisual()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (GetComponent<MeshFilter>() == null)
            {
                gameObject.AddComponent<MeshFilter>().mesh = CreateQuadMesh();
            }

            renderer.material = new Material(FindUnlitShader());
            renderer.material.color = color;
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
