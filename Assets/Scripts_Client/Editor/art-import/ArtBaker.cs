using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 레시피 → 결과물(PNG + 런타임 SO). 원본 팩은 읽기만 한다.
    //
    // ■ 그림은 다시 만들고, SO는 처음 한 번만 채운다
    //   레시피 옆의 PNG는 구울 때마다 다시 쓴다. 스프라이트 ID는 이름으로 이어 받아 SO·씬의 참조가 끊기지 않는다.
    //   ⚠️ **결과 SO의 값은 사람이 다듬는다 — 굽기는 SO가 처음 생길 때·칸이 비어 있을 때만 채운다.**
    //     캐릭터: 대기·달리기 프레임, 공격 칸(순서·반복·타격 프레임·이펙트), 상반신·머리
    //     배경  : 무대 높이·땅 높이·층 속도 (층 텍스처 참조만 매번 갱신)
    //     대상  : 레벨 색 (그림·섬광 참조만 매번 갱신)
    //   레시피 값은 "첫 값"일 뿐이다. 다시 채우게 하려면 SO의 그 칸을 비우고 굽는다.
    //   (2026-10-05 [전부 다시 굽기]가 다듬어 둔 땅 높이 4벌을 되돌렸고,
    //    2026-10-07 black-knight 공격 순서 1 2 1 3을 레시피 순서로 되돌렸다 — 둘 다 사람이 다듬은 값이었다)
    //
    // ■ 임포트 설정은 여기서 정하지 않는다
    //   'ArtImportPostprocessor'가 폴더·접미사로 정한다 — 손으로 넣은 그림도 같은 규격을 받게.
    internal static class ArtBaker
    {
        // 포트레이트 결과의 최대 변 — 크롭을 이 안에서 정수 배로 키우되 **캔버스로 채우지 않는다**('Upscale').
        // 머리 결과 크기 (정사각형) — 크롭을 정수 배로 키워 이 안에 가운데 맞춘다('Fit').
        private const int PortraitMaxSize = 64;
        private const int HeadCanvas      = 32;

        // 자동 상반신 크롭 — 키의 이 비율을 위에서부터(머리 + 상체) · 최대 변 32(× 2 = 64로 꼭 맞는다)
        private const float PortraitHeightRatio = 0.6f;
        private const int   PortraitAutoMax     = 32;

        // 몸통 가운데를 잴 위쪽 줄 수 — 머리·어깨
        private const int TorsoRows = 12;

        [MenuItem(ArtSpec.MenuRoot + "전부 다시 굽기", priority = 0)]
        public static void BakeAll()
        {
            var log = new StringBuilder();

            try
            {
                foreach (CharacterRecipe recipe in FindRecipes<CharacterRecipe>())
                {
                    log.AppendLine(Bake(recipe));
                }

                foreach (BackgroundRecipe recipe in FindRecipes<BackgroundRecipe>())
                {
                    log.AppendLine(Bake(recipe));
                }

                foreach (TargetRecipe recipe in FindRecipes<TargetRecipe>())
                {
                    log.AppendLine(Bake(recipe));
                }

                foreach (IconRecipe recipe in FindRecipes<IconRecipe>())
                {
                    log.AppendLine(Bake(recipe));
                }

                AssetDatabase.SaveAssets();
                log.AppendLine(ArtCatalogSync.Sync());
            }
            finally
            {
                ArtImage.ClearCache();
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[아트] 전부 다시 구웠다\n{log}");
        }

        // ── 캐릭터 ──────────────────────────────────────────

        public static string Bake(CharacterRecipe recipe)
        {
            (string folder, string key) = Locate(recipe);
            var warnings = new List<string>();

            Sprite[]   run     = Resolve(recipe.RunSprites, recipe.RunClip);
            Sprite[][] attacks = recipe.Attacks.Select(a => Trim(Resolve(a.sprites, a.clip), a.trimStart, a.trimEnd)).ToArray();
            Sprite[][] effects = recipe.Attacks.Select(a => Trim(Resolve(a.effectSprites ?? System.Array.Empty<Sprite>(), a.effectClip), a.trimStart, a.trimEnd)).ToArray();
            Sprite[][] hits    = recipe.Attacks.Select(a => Resolve(a.hitEffectSprites ?? System.Array.Empty<Sprite>(), a.hitEffectClip)).ToArray();
            Sprite[]   idle    = recipe.IdleClip != null ? SpritesOf(recipe.IdleClip) : System.Array.Empty<Sprite>();

            if (run.Length == 0 || attacks.Length == 0 || attacks.Any(a => a.Length == 0))
            {
                return $"❌ {key}: 달리기·공격 원본이 비어 있다";
            }

            Sprite anchor = idle.Length > 0 ? idle[0] : run[0];

            // 기준 프레임의 발을 칸 아래 가운데에 놓는 이동량 — 모든 프레임에 같은 값을 쓴다(원본의 움직임을 지킨다)
            ArtImage   anchorImage = Prepare(anchor, recipe);
            Vector2Int feet        = anchorImage.FeetPoint();
            Vector2Int cell        = ArtSpec.CharacterCell;
            var        offset      = new Vector2Int(cell.x / 2 - feet.x, -feet.y);

            ArtImage[]   runCells    = run.Select(s => Place(s, recipe, anchorImage, offset, warnings)).ToArray();
            ArtImage[][] attackCells = attacks.Select(a => a.Select(s => Place(s, recipe, anchorImage, offset, warnings)).ToArray()).ToArray();
            ArtImage[]   idleCells   = idle.Select(s => Place(s, recipe, anchorImage, offset, warnings)).ToArray();
            ArtImage[][] effectCells = effects.Select(a => a.Select(s => Place(s, recipe, anchorImage, offset, warnings)).ToArray()).ToArray();
            ArtImage     anchorCell  = Place(anchor, recipe, anchorImage, offset, warnings);

            int[] hitFrames = attackCells
                .Select((cells, i) => recipe.Attacks[i].hitFrameOverride > 0 ? recipe.Attacks[i].hitFrameOverride : FindHitFrame(cells))
                .ToArray();

            RectInt portraitRect = recipe.PortraitRect.width > 0 ? recipe.PortraitRect : AutoPortrait(anchorCell);
            RectInt headRect     = recipe.HeadRect.width > 0     ? recipe.HeadRect     : AutoHead(anchorCell);

            string runPath      = $"{folder}/{key}{ArtSpec.RunSuffix}.png";
            string portraitPath = $"{folder}/{key}_portrait.png";
            string headPath     = $"{folder}/{key}_head.png";

            Strip(runCells).SavePng(runPath);
            Upscale(anchorCell.Crop(portraitRect), PortraitMaxSize).SavePng(portraitPath);
            Fit(anchorCell.Crop(headRect),     HeadCanvas).SavePng(headPath);

            Sprite[] runSprites    = ImportStrip(runPath,    $"{key}{ArtSpec.RunSuffix}",    runCells.Length);
            Sprite[] idleSprites   = BakeStrip(folder, $"{key}{ArtSpec.IdleSuffix}", idleCells);
            Sprite   portrait      = ImportSingle(portraitPath);
            Sprite   head          = ImportSingle(headPath);

            // 공격은 연속 공격마다 띠 하나 — '<키>_attack_1' …. 공격 수가 줄었으면 남는 띠를 지운다
            var attackSprites = new Sprite[attackCells.Length][];
            var effectSprites = new Sprite[attackCells.Length][];

            for (int i = 0; i < attackCells.Length; i++)
            {
                string attackName = $"{key}{ArtSpec.AttackSuffix}_{i + 1}";
                string attackPath = $"{folder}/{attackName}.png";
                Strip(attackCells[i]).SavePng(attackPath);
                attackSprites[i] = ImportStrip(attackPath, attackName, attackCells[i].Length);
                effectSprites[i] = BakeStrip(folder, $"{attackName}{ArtSpec.EffectSuffix}", effectCells[i]);
            }

            DeleteStaleAttackStrips(folder, key, attackCells.Length);

            // 그림 파일은 위에서 다시 썼다 — 스프라이트 ID를 이름으로 이어 받아 SO의 참조는 그대로 산다.
            // SO의 칸(프레임 배열·공격 순서·타격 프레임·상반신·머리)은 사람이 다듬는 값이라 **비어 있을 때만** 채운다(머리 주석).
            CharacterVisual visual = LoadOrCreate<CharacterVisual>($"{folder}/{key}.asset", out bool created);
            var so   = new SerializedObject(visual);
            var kept = new List<string>();

            FillArrayIfEmpty(so.FindProperty("idleFrames"), idleSprites, "대기",   kept);
            FillArrayIfEmpty(so.FindProperty("runFrames"),  runSprites,  "달리기", kept);

            // 공격은 한 칸이라도 있으면 통째로 둔다 — 순서(1 2 1 3 같은 반복)·타격 프레임을 사람이 다듬는다
            SerializedProperty attacksProperty = so.FindProperty("attacks");

            if (attacksProperty.arraySize == 0)
            {
                attacksProperty.arraySize = attackSprites.Length;

                for (int i = 0; i < attackSprites.Length; i++)
                {
                    SerializedProperty motion = attacksProperty.GetArrayElementAtIndex(i);
                    SetArray(motion.FindPropertyRelative("frames"), attackSprites[i]);
                    motion.FindPropertyRelative("hitFrame").intValue = hitFrames[i];
                    SetArray(motion.FindPropertyRelative("effectFrames"), effectSprites[i]);
                    SetArray(motion.FindPropertyRelative("hitEffectFrames"), hits[i]);
                }
            }
            else
            {
                kept.Add("공격");
            }

            FillReferenceIfEmpty(so.FindProperty("portrait"), portrait, "상반신", kept);
            FillReferenceIfEmpty(so.FindProperty("head"),     head,     "머리",   kept);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visual);

            string warn = warnings.Count > 0 ? "\n   ⚠️ " + string.Join("\n   ⚠️ ", warnings.Distinct()) : "";

            string attackSummary = string.Join(" → ", attackSprites.Select((frames, i) => $"{frames.Length}(타격 {hitFrames[i]})"));

            return $"✅ {key}: 대기 {idleSprites.Length} · 달리기 {runSprites.Length} · 공격 {attackSummary} · " +
                   $"상반신 {portraitRect} · 머리 {headRect}" +
                   (created || kept.Count == 0 ? "" : $" (SO 값 유지: {string.Join("·", kept)})") + warn;
        }

        // 있어도 없어도 되는 띠 — 대기 '<키>_idle' · 공격 이펙트 '<키>_attack_N_fx'.
        // 원본이 없으면 띠를 지우고 빈 배열(재생 쪽이 대기는 달리기 첫 프레임으로 대신하고, 이펙트는 그리지 않는다)
        private static Sprite[] BakeStrip(string folder, string name, ArtImage[] cells)
        {
            string path = $"{folder}/{name}.png";

            if (cells.Length == 0)
            {
                AssetDatabase.DeleteAsset(path);

                return System.Array.Empty<Sprite>();
            }

            Strip(cells).SavePng(path);

            return ImportStrip(path, name, cells.Length);
        }

        // 공격 앞뒤 프레임을 잘라 낸다 — 다 잘려 나가면 빈 배열(굽기가 실패로 알린다)
        private static Sprite[] Trim(Sprite[] frames, int start, int end)
        {
            int skip  = Mathf.Max(0, start);
            int count = frames.Length - skip - Mathf.Max(0, end);

            return count > 0 ? frames.Skip(skip).Take(count).ToArray() : System.Array.Empty<Sprite>();
        }

        // 예전 단일 띠('<키>_attack')와 공격 수보다 큰 번호의 띠를 지운다
        private static void DeleteStaleAttackStrips(string folder, string key, int attackCount)
        {
            string singlePath = $"{folder}/{key}{ArtSpec.AttackSuffix}.png";

            if (AssetDatabase.LoadMainAssetAtPath(singlePath) != null)
            {
                AssetDatabase.DeleteAsset(singlePath);
            }

            for (int i = attackCount + 1; ; i++)
            {
                string stalePath = $"{folder}/{key}{ArtSpec.AttackSuffix}_{i}.png";

                AssetDatabase.DeleteAsset($"{folder}/{key}{ArtSpec.AttackSuffix}_{i}{ArtSpec.EffectSuffix}.png");

                if (!AssetDatabase.DeleteAsset(stalePath))
                {
                    break;
                }
            }
        }

        private static ArtImage Prepare(Sprite sprite, CharacterRecipe recipe)
        {
            ArtImage image = ArtImage.FromSprite(sprite);

            if (recipe.SourceFacesRight)
            {
                image = image.FlipX();
            }

            return image.DownscaleNearest(recipe.Downscale);
        }

        private static ArtImage Place(Sprite sprite, CharacterRecipe recipe, ArtImage anchor, Vector2Int offset, List<string> warnings)
        {
            ArtImage frame = Prepare(sprite, recipe);

            if (frame.Width != anchor.Width || frame.Height != anchor.Height)
            {
                warnings.Add($"{sprite.name}: 프레임 크기가 기준과 다르다 ({frame.Width}×{frame.Height})");
            }

            Vector2Int cellSize = ArtSpec.CharacterCell;
            var        cell     = new ArtImage(cellSize.x, cellSize.y);
            cell.AlphaOver(frame, offset.x, offset.y);

            // 칸 밖으로 나간 그림 — 칸을 키우거나 축소를 건다
            RectInt bounds = frame.OpaqueBounds();
            if (bounds.xMin + offset.x < 0 || bounds.yMin + offset.y < 0 ||
                bounds.xMax + offset.x > cellSize.x || bounds.yMax + offset.y > cellSize.y)
            {
                warnings.Add($"{sprite.name}: 칸({cellSize.x}×{cellSize.y}) 밖으로 잘렸다");
            }

            return cell;
        }

        // 공격하며 사거리(가장 왼쪽 픽셀)가 가장 크게 뻗는 프레임 = 타격 프레임
        private static int FindHitFrame(ArtImage[] attackCells)
        {
            int bestFrame = attackCells.Length / 2;
            int bestReach = 0;

            for (int i = 1; i < attackCells.Length; i++)
            {
                int reach = attackCells[i - 1].OpaqueBounds().xMin - attackCells[i].OpaqueBounds().xMin;

                if (reach > bestReach)
                {
                    bestReach = reach;
                    bestFrame = i;
                }
            }

            return bestFrame;
        }

        // 자동 크롭은 자리만 잡아 준다 — 결과를 보고 레시피에 사각형을 적는다
        //
        // 상반신 규칙('Art 규칙.md' "상반신 크롭"): **머리와 상체**가 중심 — 위는 머리 꼭대기에 붙이고,
        // 가로는 **몸통 가운데**(위쪽 몇 줄의 불투명 픽셀 평균). 무기·방패는 잘려도 된다.
        // 크기는 키의 'PortraitHeightRatio'(머리 + 상체) — 몸 전체를 담으면 칸에서 멀리 보인다(2026-10-07). 최대 'PortraitAutoMax'.
        // ⚠️ 평균이 무기에 끌릴 수 있다 — 결과를 보고 어긋나면 레시피에 직접 적는다.
        private static RectInt AutoPortrait(ArtImage cell)
        {
            RectInt body    = cell.OpaqueBounds();
            int     size    = Mathf.Clamp(Mathf.RoundToInt(body.height * PortraitHeightRatio), 8, PortraitAutoMax);
            int     centerX = TorsoCenterX(cell, body);

            return new RectInt(centerX - size / 2, body.yMax - size, size, size);
        }

        // 머리 규칙: 크기를 **캔버스를 정수 배로 꼭 채우는 값**(32 → 16 · 8)으로 올린다 — 14px을 자르면 2배 28px이라
        // 32 캔버스에 4px 여백이 남는다(2026-10-07 hero-knight·warrior). 위는 머리 꼭대기 + 1px, 가로는 발 x.
        private static RectInt AutoHead(ArtImage cell)
        {
            RectInt body = cell.OpaqueBounds();
            int     raw  = Mathf.Max(6, Mathf.RoundToInt(body.height * 0.35f));
            int     size = HeadCanvas;

            // 캔버스의 약수 중 raw 이상인 가장 작은 값 — 32면 8 · 16 · 32
            while (size / 2 >= raw && size % 2 == 0)
            {
                size /= 2;
            }

            return new RectInt(cell.Width / 2 - size / 2, body.yMax - size + 1, size, size);
        }

        // 몸통 가운데 x — 위쪽 'TorsoRows'줄(머리·어깨)의 불투명 픽셀 평균 (AutoPortrait에서 호출).
        // 몸 전체의 가운데를 쓰면 한쪽으로 뻗은 칼·방패에 끌려 몸통이 한쪽으로 쏠린다(2026-10-07 hero-knight).
        private static int TorsoCenterX(ArtImage cell, RectInt body)
        {
            float sum   = 0f;
            int   count = 0;

            for (int y = body.yMax - 1; y >= body.yMax - TorsoRows && y >= body.yMin; y--)
            {
                for (int x = body.xMin; x < body.xMax; x++)
                {
                    if (cell.IsOpaque(x, y))
                    {
                        sum += x;
                        count++;
                    }
                }
            }

            return count > 0 ? Mathf.RoundToInt(sum / count) : body.xMin + body.width / 2;
        }

        private static ArtImage Strip(ArtImage[] cells)
        {
            var strip = new ArtImage(cells[0].Width * cells.Length, cells[0].Height);

            for (int i = 0; i < cells.Length; i++)
            {
                strip.AlphaOver(cells[i], cells[0].Width * i);
            }

            return strip;
        }

        // 크롭을 정수 배로만 키운다 — 캔버스를 덧대지 않아 결과 크기 = 크롭 × 배수 (상반신용).
        //
        // ■ 왜 'Fit'을 쓰지 않나 (2026-10-07)
        //   64 캔버스에 맞추면 크롭이 33~63px일 때 배수가 1이라 나머지가 전부 여백이 된다 — warrior(33)·hero-knight(44)가
        //   칸 안에서 작게 떠 보였다. 화면은 UI Image가 비율을 지켜 늘리므로 결과 크기가 캐릭터마다 달라도 된다.
        private static ArtImage Upscale(ArtImage crop, int maxSize)
        {
            int scale  = Mathf.Max(1, maxSize / Mathf.Max(crop.Width, crop.Height));
            var result = new ArtImage(crop.Width * scale, crop.Height * scale);

            for (int y = 0; y < result.Height; y++)
            {
                for (int x = 0; x < result.Width; x++)
                {
                    result[x, y] = crop[x / scale, y / scale];
                }
            }

            return result;
        }

        // 크롭을 정수 배로 키워 정사각형 캔버스 가운데에 놓는다 — 캐릭터마다 몸집이 달라도 결과 크기가 같다 (머리용)
        private static ArtImage Fit(ArtImage crop, int canvasSize)
        {
            int scale  = Mathf.Max(1, canvasSize / Mathf.Max(crop.Width, crop.Height));
            var canvas = new ArtImage(canvasSize, canvasSize);
            int ox     = (canvasSize - crop.Width * scale) / 2;
            int oy     = (canvasSize - crop.Height * scale) / 2;

            for (int y = 0; y < crop.Height * scale; y++)
            {
                for (int x = 0; x < crop.Width * scale; x++)
                {
                    if (canvas.InBounds(ox + x, oy + y))
                    {
                        canvas[ox + x, oy + y] = crop[x / scale, y / scale];
                    }
                }
            }

            return canvas;
        }

        // ── 배경 ────────────────────────────────────────────

        public static string Bake(BackgroundRecipe recipe)
        {
            (string folder, string key) = Locate(recipe);
            var layers   = new List<BackgroundVisual.Layer>();
            var warnings = new List<string>();
            int stageHeight = 0;

            for (int i = 0; i < recipe.Groups.Length; i++)
            {
                BackgroundRecipe.LayerGroup group = recipe.Groups[i];

                // 아틀라스 스프라이트가 있으면 그것, 없으면 텍스처 통째로
                ArtImage[] sources = group.spriteSources != null && group.spriteSources.Length > 0
                    ? group.spriteSources.Where(s => s != null).Select(ArtImage.FromSprite).ToArray()
                    : (group.sources ?? System.Array.Empty<Texture2D>()).Where(t => t != null).Select(t => ArtImage.FromTexture(t)).ToArray();

                if (sources.Length == 0)
                {
                    return $"❌ {key}: {i}번 층의 원본이 비어 있다";
                }

                var merged = new ArtImage(sources[0].Width, sources[0].Height);

                foreach (ArtImage source in sources)
                {
                    merged.AlphaOver(source);
                }

                int skip = Mathf.Clamp(recipe.SkipBottom, 0, merged.Height - 1);

                ArtImage layer = merged
                    .Crop(new RectInt(0, skip, merged.Width, Mathf.Min(recipe.CropBottom, merged.Height - skip)))
                    .DownscaleBox(recipe.Downscale);

                stageHeight = layer.Height;

                string? seam = ArtValidator.CheckSeam(layer);
                if (seam != null)
                {
                    warnings.Add($"L{i}: {seam}");
                }

                string path = $"{folder}/{key}_L{i}.png";
                layer.SavePng(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                layers.Add(new BackgroundVisual.Layer
                {
                    texture    = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                    speedRatio = group.speedRatio,
                });
            }

            // 땅 띠 — 배경 원본에 땅이 없을 때 타일을 이어 붙여 맨 앞 층으로
            if (recipe.GroundTiles.Any(t => t != null))
            {
                ArtImage[] tiles = recipe.GroundTiles.Where(t => t != null).Select(ArtImage.FromSprite).ToArray();
                var        strip = new ArtImage(tiles.Sum(t => t.Width), tiles.Max(t => t.Height));
                int        x     = 0;

                foreach (ArtImage tile in tiles)
                {
                    strip.AlphaOver(tile, x, 0);
                    x += tile.Width;
                }

                strip = strip.DownscaleNearest(recipe.GroundDownscale);

                // 층은 모두 무대 높이로 늘려 그리므로, 띠를 바닥에 붙인 무대 높이 그림으로 만든다
                var ground = new ArtImage(strip.Width, Mathf.Max(stageHeight, strip.Height));
                ground.AlphaOver(strip, 0, 0);

                string path = $"{folder}/{key}_L{layers.Count}.png";
                ground.SavePng(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                layers.Add(new BackgroundVisual.Layer
                {
                    texture    = AssetDatabase.LoadAssetAtPath<Texture2D>(path),
                    speedRatio = 1f,
                });
            }

            BackgroundVisual visual = LoadOrCreate<BackgroundVisual>($"{folder}/{key}.asset", out bool created);
            var so = new SerializedObject(visual);
            SerializedProperty layersProp = so.FindProperty("layers");
            int keptLayers = created ? 0 : layersProp.arraySize; // 이 칸까지는 다듬은 속도를 남긴다
            layersProp.arraySize = layers.Count;

            for (int i = 0; i < layers.Count; i++)
            {
                SerializedProperty element = layersProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("texture").objectReferenceValue = layers[i].texture;

                if (i >= keptLayers)
                {
                    element.FindPropertyRelative("speedRatio").floatValue = layers[i].speedRatio;
                }
            }

            // 무대·땅 높이는 SO에서 다듬는다 — 처음 생길 때만 굽기·레시피 값을 넣는다(머리 주석).
            if (created)
            {
                so.FindProperty("stageHeight").intValue  = stageHeight;
                so.FindProperty("groundHeight").intValue = recipe.GroundHeight;
            }

            stageHeight = so.FindProperty("stageHeight").intValue;
            int groundHeight = so.FindProperty("groundHeight").intValue;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visual);

            string warn = warnings.Count > 0 ? "\n   ⚠️ " + string.Join("\n   ⚠️ ", warnings) : "";

            return $"✅ {key}: 층 {layers.Count} · 높이 {stageHeight} · 땅 {groundHeight}{(created ? "" : " (SO 값 유지)")}{warn}";
        }

        // ── 대상 ────────────────────────────────────────────

        public static string Bake(TargetRecipe recipe)
        {
            (string folder, string key) = Locate(recipe);

            if (recipe.Source == null)
            {
                return $"❌ {key}: 원본 스프라이트가 비어 있다";
            }

            ArtImage raw   = ArtImage.FromSprite(recipe.Source);
            ArtImage image = raw.Crop(raw.OpaqueBounds()).DownscaleNearest(recipe.Downscale);

            string spritePath = $"{folder}/{key}.png";
            string flashPath  = $"{folder}/{key}_flash.png";

            image.SavePng(spritePath);
            image.WhiteSilhouette().SavePng(flashPath);

            TargetVisual visual = LoadOrCreate<TargetVisual>($"{folder}/{key}.asset", out bool created);
            var so = new SerializedObject(visual);
            so.FindProperty("sprite").objectReferenceValue = ImportSingle(spritePath);
            so.FindProperty("flash").objectReferenceValue  = ImportSingle(flashPath);

            // 레벨 색은 SO에서 다듬는다 — 없던 칸만 레시피에서 채운다(머리 주석). 칸 수는 레시피를 따른다.
            SerializedProperty tints = so.FindProperty("levelTints");
            int keptTints = created ? 0 : tints.arraySize;
            tints.arraySize = recipe.LevelTints.Length;
            for (int i = keptTints; i < recipe.LevelTints.Length; i++)
            {
                tints.GetArrayElementAtIndex(i).colorValue = recipe.LevelTints[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visual);

            return $"✅ {key}: {image.Width}×{image.Height} · 레벨 색 {recipe.LevelTints.Length}";
        }

        // ── 아이콘 ──────────────────────────────────────────

        // 레시피의 TID마다 스프라이트의 그림 부분만 잘라 'item_<TID>' · 'equip_<TID>'로 쓴다
        public static string Bake(IconRecipe recipe)
        {
            int items  = BakeIcons(recipe.Items,  ArtSpec.ItemIconsRoot,  ArtSpec.ItemIconPrefix);
            int equips = BakeIcons(recipe.Equips, ArtSpec.EquipIconsRoot, ArtSpec.EquipIconPrefix);

            return $"✅ 아이콘: 자원 {items} · 장비 {equips}";
        }

        private static int BakeIcons(IconRecipe.Entry[] entries, string root, string prefix)
        {
            Directory.CreateDirectory(root);

            int count = 0;

            foreach (IconRecipe.Entry entry in entries)
            {
                if (entry.sprite == null || entry.tid <= 0)
                {
                    continue;
                }

                ArtImage raw  = ArtImage.FromSprite(entry.sprite);
                string   path = $"{root}/{prefix}{entry.tid}.png";

                raw.Crop(raw.OpaqueBounds()).SavePng(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                count++;
            }

            return count;
        }

        // ── 공통 ────────────────────────────────────────────

        private static IEnumerable<T> FindRecipes<T>() where T : ScriptableObject
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { ArtSpec.Root })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(recipe => recipe != null);

        // 레시피가 있는 폴더와 키(= 폴더 이름)
        private static (string folder, string key) Locate(ScriptableObject recipe)
        {
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(recipe))!.Replace('\\', '/');

            return (folder, Path.GetFileName(folder));
        }

        private static Sprite[] Resolve(Sprite[] sprites, AnimationClip? clip)
        {
            if (sprites.Length > 0)
            {
                return sprites;
            }

            return clip != null ? SpritesOf(clip) : System.Array.Empty<Sprite>();
        }

        // 클립의 스프라이트 키를 순서대로 — 같은 그림이 연달아 이어지면 하나로 본다
        private static Sprite[] SpritesOf(AnimationClip clip)
        {
            EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                .FirstOrDefault(b => b.propertyName == "m_Sprite");

            if (binding.propertyName == null)
            {
                return System.Array.Empty<Sprite>();
            }

            var result = new List<Sprite>();

            foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
            {
                if (key.value is Sprite sprite && (result.Count == 0 || result[^1] != sprite))
                {
                    result.Add(sprite);
                }
            }

            return result.ToArray();
        }

        // 가로 띠를 같은 크기 칸으로 자른다. 스프라이트 ID는 이름으로 이어 받는다
        private static Sprite[] ImportStrip(string path, string baseName, int count)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var factory  = new SpriteDataProviderFactories();
            factory.Init();

            ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            Dictionary<string, GUID> oldIds = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            Vector2Int cell = ArtSpec.CharacterCell;
            var rects = new SpriteRect[count];

            for (int i = 0; i < count; i++)
            {
                string name = $"{baseName}_{i}";

                rects[i] = new SpriteRect
                {
                    name      = name,
                    rect      = new Rect(cell.x * i, 0, cell.x, cell.y),
                    alignment = SpriteAlignment.BottomCenter,
                    pivot     = new Vector2(0.5f, 0f),
                    spriteID  = oldIds.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                };
            }

            provider.SetSpriteRects(rects);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
                ?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();

            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
                .ToArray();
        }

        private static Sprite ImportSingle(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject => LoadOrCreate<T>(path, out _);

        // created : 이번에 새로 만들었나 — 참이면 조정값을 레시피에서 채운다(머리 주석)
        private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            created   = asset == null;

            if (!created)
            {
                return asset!;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);

            return asset;
        }

        // 비어 있는 배열만 채운다 — 한 칸이라도 있으면 사람이 다듬은 값으로 보고 둔다
        private static void FillArrayIfEmpty(SerializedProperty property, Object[] values, string label, List<string> kept)
        {
            if (property.arraySize > 0)
            {
                kept.Add(label);

                return;
            }

            SetArray(property, values);
        }

        // 비어 있는 참조만 채운다
        private static void FillReferenceIfEmpty(SerializedProperty property, Object value, string label, List<string> kept)
        {
            if (property.objectReferenceValue != null)
            {
                kept.Add(label);

                return;
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedProperty property, Object[] values)
        {
            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
