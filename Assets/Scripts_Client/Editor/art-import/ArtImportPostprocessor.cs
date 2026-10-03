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
            else if (path.StartsWith(ArtSpec.CharactersRoot + "/") || path.StartsWith(ArtSpec.TargetsRoot + "/"))
            {
                ApplySprite((TextureImporter)assetImporter, System.IO.Path.GetFileNameWithoutExtension(path));
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
            importer.maxTextureSize      = 4096;
        }

        // 패럴랙스 층 — RawImage가 uvRect로 가로 반복한다
        private static void ApplyBackground(TextureImporter importer)
        {
            ApplyPixelArt(importer);
            importer.textureType = TextureImporterType.Default;
            importer.wrapModeU   = TextureWrapMode.Repeat;
            importer.wrapModeV   = TextureWrapMode.Clamp;
        }

        // 캐릭터·대상 — 달리기·공격 띠는 여러 장('ArtBaker'가 칸을 자른다), 나머지는 한 장
        private static void ApplySprite(TextureImporter importer, string fileName)
        {
            ApplyPixelArt(importer);
            importer.textureType         = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = ArtSpec.PixelsPerUnit;
            importer.wrapMode            = TextureWrapMode.Clamp;

            bool isStrip = fileName.EndsWith(ArtSpec.RunSuffix) || fileName.EndsWith(ArtSpec.AttackSuffix);
            importer.spriteImportMode = isStrip ? SpriteImportMode.Multiple : SpriteImportMode.Single;

            if (isStrip)
            {
                return;
            }

            // 포트레이트·머리는 가운데, 땅에 서는 것(대상)은 발 기준
            bool isCrop = fileName.EndsWith("_portrait") || fileName.EndsWith("_head");
            var  settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)(isCrop ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
            settings.spriteMeshType  = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
