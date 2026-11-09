using System.Collections.Generic;
using UnityEngine;

namespace OopsItAte.Onboarding
{
    /// <summary>
    /// Sprites for onboarding hints. Drop real art into Resources/Onboarding
    /// (KeyW, KeyWDown, ..., KeyJTakeFood, KeyJFeed, ThoughtBubble, Smoke) to replace
    /// the generated placeholders.
    /// </summary>
    public static class OnboardingArt
    {
        private const float PixelsPerUnit = 24f;

        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        private static readonly Color32 Outline = new Color32(34, 32, 52, 255);
        private static readonly Color32 KeyFace = new Color32(244, 244, 236, 255);
        private static readonly Color32 KeyShade = new Color32(168, 168, 184, 255);
        private static readonly Color32 KeyPressedFace = new Color32(255, 222, 116, 255);
        private static readonly Color32 KeyPressedShade = new Color32(214, 164, 72, 255);
        private static readonly Color32 BubbleFill = new Color32(255, 255, 255, 255);
        private static readonly Color32 Crust = new Color32(214, 140, 62, 255);
        private static readonly Color32 CrustDark = new Color32(146, 86, 38, 255);
        private static readonly Color32 Crumb = new Color32(250, 220, 150, 255);
        private static readonly Color32 SmokeFill = new Color32(226, 226, 232, 255);
        private static readonly Color32 SmokeEdge = new Color32(176, 176, 190, 255);

        private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['W'] = new[] { "X...X", "X...X", "X.X.X", "XX.XX", "X...X" },
            ['A'] = new[] { ".XXX.", "X...X", "XXXXX", "X...X", "X...X" },
            ['S'] = new[] { ".XXXX", "X....", ".XXX.", "....X", "XXXX." },
            ['D'] = new[] { "XXXX.", "X...X", "X...X", "X...X", "XXXX." },
            ['J'] = new[] { "..XXX", "...X.", "...X.", "X..X.", ".XX.." },
            ['T'] = new[] { "XXXXX", "..X..", "..X..", "..X..", "..X.." },
            ['K'] = new[] { "X..X.", "X.X..", "XX...", "X.X..", "X..X." },
            ['E'] = new[] { "XXXXX", "X....", "XXXX.", "X....", "XXXXX" },
            ['F'] = new[] { "XXXXX", "X....", "XXXX.", "X....", "X...." },
            ['O'] = new[] { ".XXX.", "X...X", "X...X", "X...X", ".XXX." },
        };

        private const int KeyWidth = 11;
        private const int KeyHeight = 12;
        private const int GlyphWidth = 5;
        private const int SpaceWidth = 3;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite KeyJTakeFood => Get("KeyJTakeFood", () => BuildKeyWithLabel('J', "TAKE FOOD"));
        public static Sprite KeyJFeed => Get("KeyJFeed", () => BuildKeyWithLabel('J', "FEED"));
        public static Sprite ThoughtBubble => Get("ThoughtBubble", BuildThoughtBubble);
        public static Sprite Smoke => Get("Smoke", BuildSmoke);

        /// <summary>A single keycap; pressed keys sit one pixel lower and light up.</summary>
        public static Sprite Key(char letter, bool pressed)
        {
            return Get(pressed ? $"Key{letter}Down" : $"Key{letter}", () =>
            {
                var canvas = new PixelCanvas(KeyWidth, KeyHeight);
                DrawKey(canvas, 0, 0, letter, pressed);
                return canvas.ToSprite();
            });
        }

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

        /// <summary>Keycap followed by a dark label plate, e.g. [J] TAKE FOOD.</summary>
        private static Sprite BuildKeyWithLabel(char letter, string label)
        {
            const int gap = 2;
            const int padding = 2;
            const int labelTop = 1;
            const int labelHeight = 9;
            int labelLeft = KeyWidth + gap;
            int labelWidth = MeasureText(label) + padding * 2;

            var canvas = new PixelCanvas(labelLeft + labelWidth, KeyHeight);
            DrawKey(canvas, 0, 0, letter, false);
            for (int y = 0; y < labelHeight; y++)
            {
                for (int x = 0; x < labelWidth; x++)
                {
                    bool corner = (x == 0 || x == labelWidth - 1) && (y == 0 || y == labelHeight - 1);
                    if (!corner)
                    {
                        canvas.Set(labelLeft + x, labelTop + y, Outline);
                    }
                }
            }

            DrawText(canvas, labelLeft + padding, labelTop + 2, label, KeyFace);
            return canvas.ToSprite();
        }

        /// <summary>Draws one keycap with its top-left corner at (left, top).</summary>
        private static void DrawKey(PixelCanvas canvas, int left, int top, char letter, bool pressed)
        {
            int capTop = pressed ? 1 : 0;
            int shadeRows = pressed ? 2 : 3;
            Color32 face = pressed ? KeyPressedFace : KeyFace;
            Color32 shade = pressed ? KeyPressedShade : KeyShade;
            for (int y = capTop; y < KeyHeight; y++)
            {
                for (int x = 0; x < KeyWidth; x++)
                {
                    bool corner = (x == 0 || x == KeyWidth - 1) && (y == capTop || y == KeyHeight - 1);
                    if (corner)
                    {
                        continue;
                    }

                    bool edge = x == 0 || x == KeyWidth - 1 || y == capTop || y == KeyHeight - 1;
                    Color32 color = edge ? Outline : y >= KeyHeight - shadeRows ? shade : face;
                    canvas.Set(left + x, top + y, color);
                }
            }

            DrawGlyph(canvas, left + 3, top + 2 + capTop, letter, Outline);
        }

        private static int MeasureText(string text)
        {
            int width = 0;
            for (int i = 0; i < text.Length; i++)
            {
                width += (text[i] == ' ' ? SpaceWidth : GlyphWidth) + 1;
            }

            return Mathf.Max(0, width - 1);
        }

        private static void DrawText(PixelCanvas canvas, int left, int top, string text, Color32 color)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ')
                {
                    left += SpaceWidth + 1;
                    continue;
                }

                DrawGlyph(canvas, left, top, text[i], color);
                left += GlyphWidth + 1;
            }
        }

        private static void DrawGlyph(PixelCanvas canvas, int left, int top, char letter, Color32 color)
        {
            string[] glyph = Glyphs[letter];
            for (int y = 0; y < glyph.Length; y++)
            {
                for (int x = 0; x < glyph[y].Length; x++)
                {
                    if (glyph[y][x] == 'X')
                    {
                        canvas.Set(left + x, top + y, color);
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

            // A loaf of bread with two scored cuts, centred in the cloud.
            string[] bread =
            {
                "..dddddd..",
                ".dCCCCCCd.",
                "dCCcCCcCCd",
                "dCcCCcCCCd",
                "dCCCCCCCCd",
                ".dddddddd.",
            };
            for (int y = 0; y < bread.Length; y++)
            {
                for (int x = 0; x < bread[y].Length; x++)
                {
                    char pixel = bread[y][x];
                    if (pixel == '.')
                    {
                        continue;
                    }

                    Color32 color = pixel == 'C' ? Crust : pixel == 'd' ? CrustDark : Crumb;
                    canvas.Set(7 + x, 4 + y, color);
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
