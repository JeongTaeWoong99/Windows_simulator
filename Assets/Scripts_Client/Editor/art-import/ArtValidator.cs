using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameData;
using MikaProtocol;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 결과물과 목록('VisualCatalog')이 규격을 지키는지 본다. 고치지 않고 알리기만 한다.
    //   ❌ 오류 — 화면이 깨진다 (빈 프레임 · 칸 크기 다름 · 대체 그림 없음)
    //   ⚠️ 경고 — 보기에 어긋난다 (배경 이음매)
    //   ℹ️ 알림 — 대체 그림으로 버티는 중 (그림 없는 캐릭터·산업)
    internal static class ArtValidator
    {
        // 배경 양 끝 열의 차이가 안쪽 이웃 열 차이 평균의 몇 배를 넘으면 이음매로 보나
        private const float SeamRatio = 3f;

        // 이음매 판정의 바닥값 — 거의 단색인 층에서 비율만 커지는 것을 거른다 (채널 평균 차)
        private const float SeamFloor = 6f;

        private static readonly EIndustryType[] Industries =
        {
            EIndustryType.Farming, EIndustryType.Fishing, EIndustryType.Mining, EIndustryType.Logging, EIndustryType.Hunting,
        };

        [MenuItem(ArtSpec.MenuRoot + "검사", priority = 1)]
        public static void ValidateFromMenu()
        {
            string report = Validate();
            Debug.Log($"[아트] 검사\n{report}");
        }

        public static string Validate()
        {
            var report = new StringBuilder();

            foreach (CharacterVisual visual in FindAll<CharacterVisual>())
            {
                CheckCharacter(visual, report);
            }

            foreach (BackgroundVisual visual in FindAll<BackgroundVisual>())
            {
                CheckBackground(visual, report);
            }

            foreach (TargetVisual visual in FindAll<TargetVisual>())
            {
                if (visual.Sprite == null || visual.Flash == null)
                {
                    report.AppendLine($"❌ 대상 {visual.name}: 그림 또는 섬광이 비어 있다");
                }
            }

            CheckCatalog(report);

            return report.Length > 0 ? report.ToString() : "✅ 문제 없음";
        }

        // 배경 층의 왼쪽 끝과 오른쪽 끝이 이어지는가. 이어지면 null
        public static string? CheckSeam(ArtImage layer)
        {
            float edge     = ColumnDiff(layer, layer.Width - 1, 0);
            float interior = 0f;

            for (int x = 0; x < layer.Width - 1; x++)
            {
                interior += ColumnDiff(layer, x, x + 1);
            }

            interior /= layer.Width - 1;

            return edge > SeamFloor && edge > interior * SeamRatio
                ? $"양 끝이 이어지지 않는다 (끝 차이 {edge:0.0} · 안쪽 평균 {interior:0.0})"
                : null;
        }

        private static float ColumnDiff(ArtImage image, int a, int b)
        {
            float sum = 0f;

            for (int y = 0; y < image.Height; y++)
            {
                Color32 ca = image[a, y];
                Color32 cb = image[b, y];
                sum += (Math.Abs(ca.r - cb.r) + Math.Abs(ca.g - cb.g) + Math.Abs(ca.b - cb.b) + Math.Abs(ca.a - cb.a)) / 4f;
            }

            return sum / image.Height;
        }

        private static void CheckCharacter(CharacterVisual visual, StringBuilder report)
        {
            if (visual.RunFrames.Length == 0 || visual.AttackFrames.Length == 0)
            {
                report.AppendLine($"❌ 캐릭터 {visual.name}: 달리기·공격 프레임이 비어 있다");
                return;
            }

            Vector2Int cell = ArtSpec.CharacterCell;

            foreach (Sprite? frame in visual.RunFrames.Concat(visual.AttackFrames))
            {
                if (frame == null || (int)frame.rect.width != cell.x || (int)frame.rect.height != cell.y)
                {
                    report.AppendLine($"❌ 캐릭터 {visual.name}: 프레임이 비었거나 칸({cell.x}×{cell.y})과 크기가 다르다 — {frame?.name}");
                    return;
                }
            }

            if (visual.HitFrame < 0 || visual.HitFrame >= visual.AttackFrames.Length)
            {
                report.AppendLine($"❌ 캐릭터 {visual.name}: 타격 프레임 {visual.HitFrame}이 공격 범위 밖이다");
            }

            if (visual.Portrait == null || visual.Head == null)
            {
                report.AppendLine($"⚠️ 캐릭터 {visual.name}: 상반신·머리 크롭이 비어 있다");
            }
        }

        private static void CheckBackground(BackgroundVisual visual, StringBuilder report)
        {
            if (visual.Layers.Length == 0)
            {
                report.AppendLine($"❌ 배경 {visual.name}: 층이 없다");
                return;
            }

            for (int i = 0; i < visual.Layers.Length; i++)
            {
                Texture2D texture = visual.Layers[i].texture;

                if (texture == null || texture.height != visual.StageHeight)
                {
                    report.AppendLine($"❌ 배경 {visual.name}: L{i}이 비었거나 높이가 {visual.StageHeight}이 아니다");
                    continue;
                }

                if (texture.wrapModeU != TextureWrapMode.Repeat)
                {
                    report.AppendLine($"❌ 배경 {visual.name}: L{i}이 가로 반복(Repeat)이 아니다");
                }

                string? seam = CheckSeam(ArtImage.FromTexture(texture));
                if (seam != null)
                {
                    report.AppendLine($"⚠️ 배경 {visual.name}: L{i} {seam}");
                }
            }

            if (!visual.Layers.Any(layer => Mathf.Approximately(layer.speedRatio, 1f)))
            {
                report.AppendLine($"⚠️ 배경 {visual.name}: 속도 1.0인 땅 층이 없다 — 대상이 땅 위에서 미끄러져 보인다");
            }

            if (visual.GroundHeight <= 0 || visual.GroundHeight >= visual.StageHeight)
            {
                report.AppendLine($"⚠️ 배경 {visual.name}: 땅 높이 {visual.GroundHeight}이 무대 안에 있지 않다");
            }

            ArtImage.ClearCache();
        }

        private static void CheckCatalog(StringBuilder report)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(ArtSpec.CatalogPath);

            if (catalog == null)
            {
                report.AppendLine($"❌ 목록이 없다 — {ArtSpec.CatalogPath}");
                return;
            }

            // 없는 TID로 찾으면 대체 그림이 나온다
            if (catalog.GetCharacter(int.MinValue) == null)
            {
                report.AppendLine("❌ 목록: 대체 캐릭터(1번)가 비어 있다");
            }

            if (catalog.GetBackground(EIndustryType.None) == null)
            {
                report.AppendLine("❌ 목록: 기본 배경이 비어 있다");
            }

            foreach (EIndustryType industry in Industries)
            {
                if (catalog.GetTarget(industry) == null)
                {
                    report.AppendLine($"ℹ️ 목록: {industry} 대상이 없다 — 대상 없이 달리기만 한다");
                }
            }

            GameDataLoader.Load();

            var missing = new List<string>();
            foreach (CharacterTableRow row in GameTable.CharacterTable.All)
            {
                if (!catalog.HasCharacter(row.CharacterTID))
                {
                    missing.Add($"{row.CharacterTID} {row.Name}");
                }
            }

            if (missing.Count > 0)
            {
                report.AppendLine($"ℹ️ 목록: 그림 없는 캐릭터 {missing.Count}종 — 대체 그림으로 그린다\n   {string.Join(" · ", missing)}");
            }
        }

        private static IEnumerable<T> FindAll<T>() where T : ScriptableObject
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { ArtSpec.Root })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(asset => asset != null);
    }
}
