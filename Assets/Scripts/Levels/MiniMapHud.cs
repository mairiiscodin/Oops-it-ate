using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace OopsItAte.Levels
{
    /// <summary>
    /// Soul Knight style room map in the top-left corner. Visited rooms are dim,
    /// rooms whose Pets have all been fed light up, and the current room is framed.
    /// </summary>
    public sealed class MiniMapHud : MonoBehaviour
    {
        private const int CanvasSortingOrder = 100;
        private const float PulseDuration = 0.55f;
        private const float PulseScale = 1.4f;

        private MiniMapLayout layout;
        private RectTransform panel;
        private readonly Dictionary<string, Image> roomImages =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> completedRooms =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Initialize()
        {
            layout = Resources.Load<MiniMapLayout>(MiniMapLayout.ResourceName);
            if (layout == null)
            {
                Debug.LogWarning(
                    "Mini map layout is missing. Run Tools/Oops It Ate/Generate Mini Map Layout.",
                    this);
                enabled = false;
                return;
            }

            BuildCanvas();
            RememberCompletedRooms();
            Redraw();
            GameSession.RoomProgressChanged += HandleRoomProgressChanged;
        }

        private void OnDestroy()
        {
            GameSession.RoomProgressChanged -= HandleRoomProgressChanged;
        }

        private void HandleRoomProgressChanged(string roomId)
        {
            bool newlyCompleted = GameSession.IsRoomCompleted(roomId) && !completedRooms.Contains(roomId);
            RememberCompletedRooms();
            Redraw();

            if (newlyCompleted && roomImages.TryGetValue(roomId, out Image room))
            {
                StartCoroutine(PulseRoom(room));
            }
        }

        private void RememberCompletedRooms()
        {
            completedRooms.Clear();
            for (int i = 0; i < layout.rooms.Length; i++)
            {
                MiniMapLayout.Room room = layout.rooms[i];
                if (room != null && GameSession.IsRoomCompleted(room.roomId))
                {
                    completedRooms.Add(room.roomId);
                }
            }
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("Mini Map");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Stays below RoomTransitionOverlay so the iris still covers the map.
            canvas.sortingOrder = CanvasSortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreateImage("Panel", canvasObject.transform, layout.panelColor).rectTransform;
            panel.anchorMin = Vector2.up;
            panel.anchorMax = Vector2.up;
            panel.pivot = Vector2.up;
            panel.anchoredPosition = new Vector2(layout.screenMargin.x, -layout.screenMargin.y);
        }

        private void Redraw()
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Destroy(panel.GetChild(i).gameObject);
            }

            roomImages.Clear();

            HashSet<string> visibleRooms = GetVisibleRooms();
            if (visibleRooms.Count == 0)
            {
                panel.gameObject.SetActive(false);
                return;
            }

            panel.gameObject.SetActive(true);
            RectInt bounds = GetCellBounds(visibleRooms);
            float step = layout.roomSize + layout.roomSpacing;
            panel.sizeDelta = new Vector2(
                bounds.width * layout.roomSize + (bounds.width - 1) * layout.roomSpacing,
                bounds.height * layout.roomSize + (bounds.height - 1) * layout.roomSpacing)
                + Vector2.one * (layout.panelPadding * 2f);

            Vector2 CellCenter(Vector2Int cell)
            {
                return new Vector2(
                    layout.panelPadding + (cell.x - bounds.xMin) * step + layout.roomSize * 0.5f,
                    layout.panelPadding + (cell.y - bounds.yMin) * step + layout.roomSize * 0.5f);
            }

            // Links first so the rooms are drawn on top of them.
            for (int i = 0; i < layout.links.Length; i++)
            {
                MiniMapLayout.Link link = layout.links[i];
                MiniMapLayout.Room a = link == null ? null : layout.FindRoom(link.roomA);
                MiniMapLayout.Room b = link == null ? null : layout.FindRoom(link.roomB);
                if (a == null || b == null
                    || !visibleRooms.Contains(a.roomId) || !visibleRooms.Contains(b.roomId))
                {
                    continue;
                }

                DrawLink(CellCenter(a.cell), CellCenter(b.cell));
            }

            string currentRoomId = GameSession.CurrentRoomId;
            for (int i = 0; i < layout.rooms.Length; i++)
            {
                MiniMapLayout.Room room = layout.rooms[i];
                if (room == null || !visibleRooms.Contains(room.roomId))
                {
                    continue;
                }

                Vector2 center = CellCenter(room.cell);
                if (string.Equals(room.roomId, currentRoomId, StringComparison.OrdinalIgnoreCase))
                {
                    float frameSize = layout.roomSize + layout.currentFrameThickness * 2f;
                    PlaceAt(CreateImage("Current Frame", panel, layout.currentFrameColor),
                        center, new Vector2(frameSize, frameSize));
                }

                Image roomImage = CreateImage($"Room {room.roomId}", panel, GetRoomColor(room.roomId));
                PlaceAt(roomImage, center, Vector2.one * layout.roomSize);
                roomImages[room.roomId] = roomImage;
            }
        }

        private HashSet<string> GetVisibleRooms()
        {
            var visible = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < layout.rooms.Length; i++)
            {
                MiniMapLayout.Room room = layout.rooms[i];
                if (room != null && (layout.revealAllRooms || GameSession.HasVisitedRoom(room.roomId)))
                {
                    visible.Add(room.roomId);
                }
            }

            if (layout.revealAllRooms)
            {
                return visible;
            }

            // Like Soul Knight, rooms next to a visited room show up as unexplored.
            for (int i = 0; i < layout.links.Length; i++)
            {
                MiniMapLayout.Link link = layout.links[i];
                if (link == null)
                {
                    continue;
                }

                if (GameSession.HasVisitedRoom(link.roomA) && layout.FindRoom(link.roomB) != null)
                {
                    visible.Add(link.roomB);
                }

                if (GameSession.HasVisitedRoom(link.roomB) && layout.FindRoom(link.roomA) != null)
                {
                    visible.Add(link.roomA);
                }
            }

            return visible;
        }

        private RectInt GetCellBounds(HashSet<string> visibleRooms)
        {
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);
            for (int i = 0; i < layout.rooms.Length; i++)
            {
                MiniMapLayout.Room room = layout.rooms[i];
                if (room == null || !visibleRooms.Contains(room.roomId))
                {
                    continue;
                }

                min = Vector2Int.Min(min, room.cell);
                max = Vector2Int.Max(max, room.cell);
            }

            return new RectInt(min, max - min + Vector2Int.one);
        }

        private Color GetRoomColor(string roomId)
        {
            if (GameSession.IsRoomCompleted(roomId)) return layout.completedRoomColor;
            if (GameSession.HasVisitedRoom(roomId)) return layout.visitedRoomColor;
            return layout.unknownRoomColor;
        }

        private void DrawLink(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            Image link = CreateImage("Link", panel, layout.linkColor);
            PlaceAt(link, (from + to) * 0.5f, new Vector2(delta.magnitude, layout.linkThickness));
            link.rectTransform.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private IEnumerator PulseRoom(Image room)
        {
            RectTransform rect = room.rectTransform;
            Color litColor = room.color;
            float elapsed = 0f;

            // Unscaled time: the completion animation pauses gameplay with timeScale = 0.
            while (elapsed < PulseDuration && room != null)
            {
                float t = elapsed / PulseDuration;
                float bump = Mathf.Sin(t * Mathf.PI);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, PulseScale, bump);
                room.color = Color.Lerp(litColor, Color.white, bump);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (room != null)
            {
                rect.localScale = Vector3.one;
                room.color = litColor;
            }
        }

        private Image CreateImage(string objectName, Transform parent, Color color)
        {
            var imageObject = new GameObject(objectName, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);

            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (layout.roomSprite != null)
            {
                image.sprite = layout.roomSprite;
                image.type = layout.roomSprite.border != Vector4.zero
                    ? Image.Type.Sliced
                    : Image.Type.Simple;
            }

            return image;
        }

        private static void PlaceAt(Image image, Vector2 center, Vector2 size)
        {
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }
    }
}
