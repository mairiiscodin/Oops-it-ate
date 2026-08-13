using OopsItAte.Actors;
using OopsItAte.Grid;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OopsItAte.Interaction
{
    public sealed class KitchenStation : MonoBehaviour
    {
        [SerializeField] private GridPosition position;
        [SerializeField] private Color color = new Color(1f, 0.65f, 0.1f);
        [SerializeField] private PetBody growableBody;
        [Header("Expanded Oven Tiles")]
        [SerializeField] private Sprite ovenBackground;
        [SerializeField] private Sprite ovenAbove;
        [SerializeField] private Sprite ovenFront;

        public GridPosition Position => position;
        public PetBody GrowableBody => growableBody;

        public void Initialize(GridWorld world, GridPosition gridPosition)
        {
            position = gridPosition;
            transform.localScale = Vector3.one * world.Settings.cellSize;

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

            renderer.enabled = false;
            growableBody = GetComponent<PetBody>();
            if (growableBody == null)
            {
                growableBody = gameObject.AddComponent<PetBody>();
            }
            growableBody.ConfigureOvenSprites(ovenBackground, ovenAbove, ovenFront);
            growableBody.Initialize(world, position, color, "Kitchen");
            growableBody.SetCanBePushedByBodyGrowth(true);
        }

        internal void SyncBodyPosition(GridWorld world, GridPosition gridPosition)
        {
            position = gridPosition;
            transform.position = world.Settings.GridToWorld(position) + Vector3.back * 0.25f;
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
            Gizmos.DrawCube(transform.position, Vector3.one);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (ovenBackground == null)
            {
                ovenBackground = LoadFirstSprite(
                    "Assets/Assets/OvenBackground.aseprite");
            }
            if (ovenAbove == null)
            {
                ovenAbove = LoadFirstSprite(
                    "Assets/Assets/OvenAbove.aseprite");
            }
            if (ovenFront == null)
            {
                ovenFront = LoadFirstSprite(
                    "Assets/Assets/OvenFront.aseprite");
            }
        }

        private static Sprite LoadFirstSprite(string assetPath)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Sprite sprite)
                {
                    return sprite;
                }
            }

            return null;
        }
#endif
    }
}
