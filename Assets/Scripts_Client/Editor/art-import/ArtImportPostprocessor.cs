using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 'Assets/Art/' 결과물 폴더의 텍스처 임포트 설정을 규격으로 고정한다 — 굽기가 만든 것이든 손으로 넣은 것이든.
    // 인스펙터에서 바꿔도 다음 임포트에 되돌아온다. 규격을 바꾸려면 여기와 'Art 규칙.md'를 함께 고친다.
    //
    // 원본('_source/')은 손대지 않는다 — 팩이 가져온 설정 그대로 둔다.
    internal sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');

            if (path.StartsWith(ArtSpec.BackgroundsRoot + "/"))
            {
                ApplyBackground((TextureImporter)assetImporter);
            }
            else if (path.StartsWith(ArtSpec.CharactersRoot + "/") || path.StartsWith(ArtSpec.TargetsRoot + "/") || path.StartsWith(ArtSpec.IconsRoot + "/"))
            {
                ApplySprite((TextureImporter)assetImporter, System.IO.Path.GetFileNameWithoutExtension(path), path.StartsWith(ArtSpec.IconsRoot + "/"));
            }
            else if (path.StartsWith(ArtSpec.FxRoot + "/"))
            {
                ApplyFx((TextureImporter)assetImporter, System.IO.Path.GetFileNameWithoutExtension(path));
            }
        }

        // 이펙트 — 전부 도트(픽셀 아트)라 점 필터다(2026-11-11 · 부드러운 흰 도형을 도트로 바꿨다). 한 장 · 가운데 기준.
        // ※ 9-slice 경계(spriteBorder)는 건드리지 않는다 — 테두리 띠 같은 그림은 인스펙터(Sprite Editor)에서 정하고 .meta에 남는다.
        // ※ UI 틀('ui_*' — 'fx/ui/', 코드로 그린 픽셀 9-slice)은 예외 — 점 필터 + 경계를 여기서 넣는다('UiBorders' = 'make_ui.py'의 BORDER).
        private static readonly System.Collections.Generic.Dictionary<string, int> UiBorders = new()
        {
            { "ui_panel", 12 }, { "ui_button", 5 }, { "ui_input", 4 }, { "ui_header", 6 },
            { "ui_panel_trim", 12 }, { "ui_button_trim", 5 }, { "ui_input_trim", 4 }, { "ui_header_trim", 6 },
            // 칸 그림 — 'make_slots.py'의 BORDER (2026-11-10)
            { "ui_slot_empty", 10 }, { "ui_slot_locked", 10 }, { "rarity_bg", 12 }, { "rarity_frame", 8 }, { "rarity_frame_gold", 8 },
        };

        private static void ApplyFx(TextureImporter importer, string fileName)
        {
            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ArtSpec.PixelsPerUnit;
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.npotScale           = TextureImporterNPOTScale.None;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spriteMeshType  = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            if (UiBorders.TryGetValue(fileName, out int border))
            {
                importer.spriteBorder = new Vector4(border, border, border, border);
            }
        }

        // 픽셀 아트 공통 — 점 필터 · 무압축 · 밉맵 없음
        private static void ApplyPixelArt(TextureImporter importer)
        {
            importer.filterMode          = FilterMode.Point;
            importer.textureCompression  = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled       = false;
            importer.alphaIsTransparency = true;
            importer.npotScale           = TextureImporterNPOTScale.None;
            importer.maxTextureSize      = 8192;   // 긴 띠(19칸 × 224 = 4256)가 줄어들지 않게 — 4096이면 칸이 224보다 작아진다
        }

        // 패럴랙스 층 — RawImage가 uvRect로 가로 반복한다
        private static void ApplyBackground(TextureImporter importer)
        {
            ApplyPixelArt(importer);
            importer.textureType = TextureImporterType.Default;
            importer.wrapModeU   = TextureWrapMode.Repeat;
            importer.wrapModeV   = TextureWrapMode.Clamp;
        }

        // 캐릭터·대상·아이콘 — 달리기·공격(1·2·3타…) 띠는 여러 장('ArtBaker'가 칸을 자른다), 나머지는 한 장
        private static void ApplySprite(TextureImporter importer, string fileName, bool isIcon)
        {
            ApplyPixelArt(importer);
            importer.textureType         = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ArtSpec.PixelsPerUnit;
            importer.wrapMode            = TextureWrapMode.Clamp;

            bool isStrip = ArtSpec.IsStrip(fileName);
            importer.spriteImportMode = isStrip ? SpriteImportMode.Multiple : SpriteImportMode.Single;

            if (isStrip)
            {
                return;
            }

            // 포트레이트·머리·아이콘은 가운데, 땅에 서는 것(대상)은 발 기준
            bool isCrop = isIcon || fileName.EndsWith("_portrait") || fileName.EndsWith("_head");
            var  settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)(isCrop ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
            settings.spriteMeshType  = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
