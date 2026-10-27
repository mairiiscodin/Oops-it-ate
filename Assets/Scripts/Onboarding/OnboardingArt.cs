using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Onboarding
{
    /// <summary>
    /// Sprites for onboarding hints. Drop real art into Resources/Onboarding
    /// (KeyWASD, KeyJ, ThoughtBubble, Smoke) to replace the generated placeholders.
    /// </summary>
    public static class OnboardingArt
    {
        private const float PixelsPerUnit = 24f;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Outline = new Color32(34, 32, 52, 255);
        private static readonly Color32 KeyFace = new Color32(244, 244, 236, 255);
        private static readonly Color32 KeyShade = new Color32(168, 168, 184, 255);
        private static readonly Color32 BubbleFill = new Color32(255, 255, 255, 255);
        private static readonly Color32 Meat = new Color32(214, 118, 52, 255);
        private static readonly Color32 MeatDark = new Color32(150, 72, 36, 255);
        private static readonly Color32 Bone = new Color32(250, 240, 214, 255);
        private static readonly Color32 SmokeFill = new Color32(226, 226, 232, 255);
        private static readonly Color32 SmokeEdge = new Color32(176, 176, 190, 255);

        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['W'] = new[] { "X...X", "X...X", "X.X.X", "XX.XX", "X...X" },
            ['A'] = new[] { ".XXX.", "X...X", "XXXXX", "X...X", "X...X" },
            ['S'] = new[] { ".XXXX", "X....", ".XXX.", "....X", "XXXX." },
            ['D'] = new[] { "XXXX.", "X...X", "X...X", "X...X", "XXXX." },
            ['J'] = new[] { "..XXX", "...X.", "...X.", "X..X.", ".XX.." },
        };

        private const int KeyWidth = 11;
        private const int KeyHeight = 12;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite KeyWasd => Get("KeyWASD", BuildWasd);
        public static Sprite KeyJ => Get("KeyJ", () => BuildSingleKey('J'));
        public static Sprite ThoughtBubble => Get("ThoughtBubble", BuildThoughtBubble);
        public static Sprite Smoke => Get("Smoke", BuildSmoke);

        private static Sprite Get(string name, System.Func<Sprite> build)
        {
            if (cache.TryGetValue(name, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            sprite = Resources.Load<Sprite>($"Onboarding/{name}");
            if (sprite == null)
            {
                sprite = build();
                sprite.name = $"{name} (generated)";
            }

            cache[name] = sprite;
            return sprite;
        }

        private static Sprite BuildSingleKey(char letter)
        {
            var canvas = new PixelCanvas(KeyWidth, KeyHeight);
            DrawKey(canvas, 0, 0, letter);
            return canvas.ToSprite();
        }

        private static Sprite BuildWasd()
        {
            const int gap = 1;
            var canvas = new PixelCanvas(KeyWidth * 3 + gap * 2, KeyHeight * 2 + gap);
            DrawKey(canvas, KeyWidth + gap, 0, 'W');
            DrawKey(canvas, 0, KeyHeight + gap, 'A');
            DrawKey(canvas, KeyWidth + gap, KeyHeight + gap, 'S');
            DrawKey(canvas, (KeyWidth + gap) * 2, KeyHeight + gap, 'D');
            return canvas.ToSprite();
        }

        /// <summary>Draws one keycap with its top-left corner at (left, top).</summary>
        private static void DrawKey(PixelCanvas canvas, int left, int top, char letter)
        {
            for (int y = 0; y < KeyHeight; y++)
            {
                for (int x = 0; x < KeyWidth; x++)
                {
                    bool corner = (x == 0 || x == KeyWidth - 1) && (y == 0 || y == KeyHeight - 1);
                    if (corner)
                    {
                        continue;
                    }

                    bool edge = x == 0 || x == KeyWidth - 1 || y == 0 || y == KeyHeight - 1;
                    Color32 color = edge ? Outline : y >= KeyHeight - 3 ? KeyShade : KeyFace;
                    canvas.Set(left + x, top + y, color);
                }
            }

            string[] glyph = Glyphs[letter];
            for (int y = 0; y < glyph.Length; y++)
            {
                for (int x = 0; x < glyph[y].Length; x++)
                {
                    if (glyph[y][x] == 'X')
                    {
                        canvas.Set(left + 3 + x, top + 2 + y, Outline);
                    }
                }
            }
        }

        private static Sprite BuildThoughtBubble()
        {
            const int width = 20;
            const int height = 18;
            var canvas = new PixelCanvas(width, height);

            // Main cloud plus two trailing dots towards the bottom-left (the Pet).
            bool Inside(int x, int y)
            {
                return InEllipse(x, y, 11.5f, 6.5f, 7.6f, 5.8f)
                    || InEllipse(x, y, 3.5f, 14f, 1.9f, 1.9f)
                    || InEllipse(x, y, 1f, 16.5f, 1.1f, 1.1f);
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!Inside(x, y))
                    {
                        continue;
                    }

                    bool edge = !Inside(x - 1, y) || !Inside(x + 1, y)
                        || !Inside(x, y - 1) || !Inside(x, y + 1);
                    canvas.Set(x, y, edge ? Outline : BubbleFill);
                }
            }

            string[] drumstick =
            {
                "...mMMm.",
                "..MMMMMM",
                "..MMMMMm",
                "..bMMMm.",
                ".b..mm..",
                "bb......",
                ".b......",
            };
            for (int y = 0; y < drumstick.Length; y++)
            {
                for (int x = 0; x < drumstick[y].Length; x++)
                {
                    char pixel = drumstick[y][x];
                    if (pixel == '.')
                    {
                        continue;
                    }

                    Color32 color = pixel == 'M' ? Meat : pixel == 'm' ? MeatDark : Bone;
                    canvas.Set(8 + x, 3 + y, color);
                }
            }

            return canvas.ToSprite();
        }

        private static Sprite BuildSmoke()
        {
            const int size = 7;
            var canvas = new PixelCanvas(size, size);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!InEllipse(x, y, 3f, 3f, 3.4f, 3.4f))
                    {
                        continue;
                    }

                    bool edge = !InEllipse(x, y, 3f, 3f, 2.4f, 2.4f);
                    canvas.Set(x, y, edge ? SmokeEdge : SmokeFill);
                }
            }

            return canvas.ToSprite();
        }

        private static bool InEllipse(int x, int y, float centerX, float centerY, float radiusX, float radiusY)
        {
            float dx = (x - centerX) / radiusX;
            float dy = (y - centerY) / radiusY;
            return dx * dx + dy * dy <= 1f;
        }

        /// <summary>Pixel buffer addressed from the top-left, like an image editor.</summary>
        private sealed class PixelCanvas
        {
            private readonly int width;
            private readonly int height;
            private readonly Color32[] pixels;

            public PixelCanvas(int width, int height)
            {
                this.width = width;
                this.height = height;
                pixels = new Color32[width * height];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = Clear;
            }

            public void Set(int x, int top, Color32 color)
            {
                if (x < 0 || x >= width || top < 0 || top >= height) return;
                pixels[(height - 1 - top) * width + x] = color;
            }

            public Sprite ToSprite()
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels32(pixels);
                texture.Apply();
                return Sprite.Create(
                    texture,
                    new Rect(0f, 0f, width, height),
                    new Vector2(0.5f, 0.5f),
                    PixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect);
            }
        }
    }
}
