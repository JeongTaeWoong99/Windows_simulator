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
    // ■ 그림은 다시 만들고, 조정값은 처음 한 번만 넣는다
    //   레시피 옆의 PNG와 SO의 그림 참조(텍스처·스프라이트·층 높이)는 구울 때마다 덮인다.
    //   ⚠️ **사람이 SO에서 다듬는 값은 SO가 처음 생길 때만 레시피에서 채운다** — 배경의 땅 높이·층 속도, 대상의 레벨 색.
    //   그 뒤로는 굽기가 건드리지 않는다. 레시피 값은 "첫 값"일 뿐이다.
    //   (2026-10-05 — 그림 저장소 분리 때 [전부 다시 굽기]가 다듬어 둔 땅 높이 4벌을 레시피 값으로 되돌렸다)
    //   층이 늘어난 경우처럼 SO에 자리가 없던 값만 레시피에서 채운다.
    //   스프라이트 ID는 이름으로 이어 받아, 다시 구워도 다른 곳의 참조가 끊기지 않는다.
    //
    // ■ 임포트 설정은 여기서 정하지 않는다
    //   'ArtImportPostprocessor'가 폴더·접미사로 정한다 — 손으로 넣은 그림도 같은 규격을 받게.
    internal static class ArtBaker
    {
        // 포트레이트·머리 결과 크기 (정사각형). 크롭을 정수 배로 키워 이 안에 가운데 맞춘다
        private const int PortraitCanvas = 64;
        private const int HeadCanvas     = 32;

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
            Fit(anchorCell.Crop(portraitRect), PortraitCanvas).SavePng(portraitPath);
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

            CharacterVisual visual = LoadOrCreate<CharacterVisual>($"{folder}/{key}.asset");
            var so = new SerializedObject(visual);
            SetArray(so.FindProperty("idleFrames"), idleSprites);
            SetArray(so.FindProperty("runFrames"), runSprites);

            SerializedProperty attacksProperty = so.FindProperty("attacks");
            attacksProperty.arraySize = attackSprites.Length;

            for (int i = 0; i < attackSprites.Length; i++)
            {
                SerializedProperty motion = attacksProperty.GetArrayElementAtIndex(i);
                SetArray(motion.FindPropertyRelative("frames"), attackSprites[i]);
                motion.FindPropertyRelative("hitFrame").intValue = hitFrames[i];
                SetArray(motion.FindPropertyRelative("effectFrames"), effectSprites[i]);
                SetArray(motion.FindPropertyRelative("hitEffectFrames"), hits[i]);
            }

            so.FindProperty("portrait").objectReferenceValue = portrait;
            so.FindProperty("head").objectReferenceValue     = head;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(visual);

            string warn = warnings.Count > 0 ? "\n   ⚠️ " + string.Join("\n   ⚠️ ", warnings.Distinct()) : "";

            string attackSummary = string.Join(" → ", attackSprites.Select((frames, i) => $"{frames.Length}(타격 {hitFrames[i]})"));

            return $"✅ {key}: 대기 {idleSprites.Length} · 달리기 {runSprites.Length} · 공격 {attackSummary} · " +
                   $"상반신 {portraitRect} · 머리 {headRect}{warn}";
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
        private static RectInt AutoPortrait(ArtImage cell)
        {
            RectInt body = cell.OpaqueBounds();
            int     size = Mathf.Max(8, Mathf.RoundToInt(body.height * 0.7f));

            return new RectInt(cell.Width / 2 - size / 2, body.yMax - size + 1, size, size);
        }

        private static RectInt AutoHead(ArtImage cell)
        {
            RectInt body = cell.OpaqueBounds();
            int     size = Mathf.Max(6, Mathf.RoundToInt(body.height * 0.35f));

            return new RectInt(cell.Width / 2 - size / 2, body.yMax - size + 1, size, size);
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

        // 크롭을 정수 배로 키워 정사각형 캔버스 가운데에 놓는다 — 캐릭터마다 몸집이 달라도 결과 크기가 같다
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

            so.FindProperty("stageHeight").intValue = stageHeight;

            // 땅 높이는 SO에서 다듬는다 — 처음 생길 때만 레시피 값을 넣는다(머리 주석).
            if (created)
            {
                so.FindProperty("groundHeight").intValue = recipe.GroundHeight;
            }

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
