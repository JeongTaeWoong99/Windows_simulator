using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 굽기용 픽셀 버퍼. 좌표는 유니티 텍스처와 같다 — (0, 0)이 왼쪽 아래.
    //
    // ■ 원본은 파일에서 읽는다
    //   원본 텍스처의 임포트 설정(Read/Write · 최대 크기)을 건드리지 않으려고, PNG 바이트를 직접 디코드한다.
    //   그래서 스프라이트 사각형은 '파일 크기 ÷ 임포트된 크기' 배율로 되돌려 쓴다.
    internal sealed class ArtImage
    {
        private static readonly Dictionary<string, ArtImage> FileCache = new Dictionary<string, ArtImage>();

        public readonly int       Width;
        public readonly int       Height;
        public readonly Color32[] Pixels;

        public ArtImage(int width, int height)
        {
            Width  = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        public Color32 this[int x, int y]
        {
            get => Pixels[y * Width + x];
            set => Pixels[y * Width + x] = value;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool IsOpaque(int x, int y) => this[x, y].a >= ArtSpec.OpaqueAlpha;

        // 굽기 한 번이 끝나면 비운다 — 원본 시트가 수 MB라 들고 있을 이유가 없다
        public static void ClearCache() => FileCache.Clear();

        // 텍스처 에셋의 원본 파일 전체
        public static ArtImage FromTexture(Texture texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);

            if (FileCache.TryGetValue(path, out ArtImage cached))
            {
                return cached;
            }

            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            decoded.LoadImage(File.ReadAllBytes(path));

            var image = new ArtImage(decoded.width, decoded.height);
            decoded.GetPixels32().CopyTo(image.Pixels, 0);
            Object.DestroyImmediate(decoded);

            FileCache[path] = image;

            return image;
        }

        // 스프라이트 한 장을 원본 해상도로 잘라 낸다
        public static ArtImage FromSprite(Sprite sprite)
        {
            ArtImage sheet = FromTexture(sprite.texture);
            float    scale = sheet.Width / (float)sprite.texture.width;
            Rect     rect  = sprite.rect;

            return sheet.Crop(new RectInt(
                Mathf.RoundToInt(rect.x * scale),     Mathf.RoundToInt(rect.y * scale),
                Mathf.RoundToInt(rect.width * scale), Mathf.RoundToInt(rect.height * scale)));
        }

        public ArtImage Crop(RectInt rect)
        {
            var result = new ArtImage(rect.width, rect.height);

            for (int y = 0; y < rect.height; y++)
            {
                for (int x = 0; x < rect.width; x++)
                {
                    int sx = rect.x + x;
                    int sy = rect.y + y;

                    if (InBounds(sx, sy))
                    {
                        result[x, y] = this[sx, sy];
                    }
                }
            }

            return result;
        }

        public ArtImage FlipX()
        {
            var result = new ArtImage(Width, Height);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    result[Width - 1 - x, y] = this[x, y];
                }
            }

            return result;
        }

        // 정수 배 축소. 픽셀 아트는 가장 가까운 픽셀(nearest)로 줄여야 선이 번지지 않는다
        public ArtImage DownscaleNearest(int factor)
        {
            if (factor <= 1)
            {
                return this;
            }

            var result = new ArtImage(Width / factor, Height / factor);

            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    result[x, y] = this[x * factor, y * factor];
                }
            }

            return result;
        }

        // 정수 배 축소. 칠해 그린 배경은 칸 평균(box)이 계단이 덜 진다 — 알파를 곱해 평균 내야 가장자리가 검어지지 않는다
        public ArtImage DownscaleBox(int factor)
        {
            if (factor <= 1)
            {
                return this;
            }

            var result = new ArtImage(Width / factor, Height / factor);

            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;

                    for (int dy = 0; dy < factor; dy++)
                    {
                        for (int dx = 0; dx < factor; dx++)
                        {
                            Color32 c  = this[x * factor + dx, y * factor + dy];
                            float   ca = c.a / 255f;
                            r += c.r * ca;
                            g += c.g * ca;
                            b += c.b * ca;
                            a += ca;
                        }
                    }

                    int count = factor * factor;
                    result[x, y] = a <= 0f
                        ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / count * 255f));
                }
            }

            return result;
        }

        // 위에 겹쳐 그린다 (보통 알파 합성)
        public void AlphaOver(ArtImage top, int offsetX = 0, int offsetY = 0)
        {
            for (int y = 0; y < top.Height; y++)
            {
                for (int x = 0; x < top.Width; x++)
                {
                    int dx = x + offsetX;
                    int dy = y + offsetY;

                    if (!InBounds(dx, dy))
                    {
                        continue;
                    }

                    Color32 src = top[x, y];

                    if (src.a == 0)
                    {
                        continue;
                    }

                    Color32 dst  = this[dx, dy];
                    float   sa   = src.a / 255f;
                    float   da   = dst.a / 255f;
                    float   outA = sa + da * (1f - sa);

                    this[dx, dy] = new Color32(
                        (byte)((src.r * sa + dst.r * da * (1f - sa)) / outA),
                        (byte)((src.g * sa + dst.g * da * (1f - sa)) / outA),
                        (byte)((src.b * sa + dst.b * da * (1f - sa)) / outA),
                        (byte)(outA * 255f));
                }
            }
        }

        // 그림이 있는 픽셀의 테두리. 비어 있으면 너비 0
        public RectInt OpaqueBounds()
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (!IsOpaque(x, y))
                    {
                        continue;
                    }

                    if (x < x0) x0 = x;
                    if (y < y0) y0 = y;
                    if (x > x1) x1 = x;
                    if (y > y1) y1 = y;
                }
            }

            return x1 < 0 ? new RectInt(0, 0, 0, 0) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        // 발 위치 — 그림 맨 아래 몇 줄에 있는 불투명 픽셀의 가로 평균
        public Vector2Int FeetPoint(int rows = 3)
        {
            RectInt bounds = OpaqueBounds();
            float   sum    = 0f;
            int     count  = 0;

            for (int y = bounds.yMin; y < bounds.yMin + rows && y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    if (IsOpaque(x, y))
                    {
                        sum += x;
                        count++;
                    }
                }
            }

            int feetX = count > 0 ? Mathf.RoundToInt(sum / count) : Mathf.RoundToInt(bounds.center.x);

            return new Vector2Int(feetX, bounds.yMin);
        }

        // 불투명한 곳은 흰색, 알파는 그대로 — 타격 섬광용 실루엣
        public ArtImage WhiteSilhouette()
        {
            var result = new ArtImage(Width, Height);

            for (int i = 0; i < Pixels.Length; i++)
            {
                result.Pixels[i] = new Color32(255, 255, 255, Pixels[i].a);
            }

            return result;
        }

        // 프로젝트 경로('Assets/...')에 PNG로 쓴다. 임포트는 부르는 쪽이 한다
        public void SavePng(string assetPath)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(Pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath)!);
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
