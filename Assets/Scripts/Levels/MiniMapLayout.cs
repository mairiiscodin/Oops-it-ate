using System;
using UnityEngine;

namespace OopsItAte.Levels
{
    /// <summary>
    /// Room positions and door connections drawn by <see cref="MiniMapHud"/>.
    /// Generate it with Tools/Oops It Ate/Generate Mini Map Layout, then tweak cells by hand if needed.
    /// </summary>
    [CreateAssetMenu(menuName = "Oops It Ate/Mini Map Layout", fileName = ResourceName)]
    public sealed class MiniMapLayout : ScriptableObject
    {
        public const string ResourceName = "MiniMapLayout";
        public const string AssetPath = "Assets/Resources/" + ResourceName + ".asset";

        [Serializable]
        public sealed class Room
        {
            public string roomId = string.Empty;
            [Tooltip("Minimap cell. +X is right, +Y is up.")]
            public Vector2Int cell;
        }

        [Serializable]
        public sealed class Link
        {
            public string roomA = string.Empty;
            public string roomB = string.Empty;
        }

        public Room[] rooms = Array.Empty<Room>();
        public Link[] links = Array.Empty<Link>();

        [Header("Visibility")]
        [Tooltip("Show every room from the start instead of only visited rooms and their neighbours.")]
        public bool revealAllRooms;

        [Header("Style")]
        [Min(4f)] public float roomSize = 28f;
        [Min(0f)] public float roomSpacing = 14f;
        [Min(1f)] public float linkThickness = 6f;
        [Min(0f)] public float currentFrameThickness = 3f;
        [Min(0f)] public float panelPadding = 10f;
        public Vector2 screenMargin = new Vector2(24f, 24f);
        [Tooltip("Optional sprite for rooms and links (e.g. a 9-sliced pixel frame). Empty draws solid squares.")]
        public Sprite roomSprite;

        public Color panelColor = new Color(0f, 0f, 0f, 0.45f);
        public Color unknownRoomColor = new Color(0.22f, 0.24f, 0.3f, 1f);
        public Color visitedRoomColor = new Color(0.45f, 0.48f, 0.55f, 1f);
        public Color completedRoomColor = new Color(1f, 0.85f, 0.35f, 1f);
        public Color currentFrameColor = Color.white;
        public Color linkColor = new Color(0.35f, 0.38f, 0.45f, 1f);

        public Room FindRoom(string roomId)
        {
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] != null
                    && string.Equals(rooms[i].roomId, roomId, StringComparison.OrdinalIgnoreCase))
                {
                    return rooms[i];
                }
            }

            return null;
        }
    }
}
