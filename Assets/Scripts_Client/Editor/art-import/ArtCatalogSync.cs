using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 그림 목록('VisualCatalog')을 폴더에서 다시 쓴다 — '전부 다시 굽기'의 마지막 단계.
    //
    // ■ 목록은 손으로 채우지 않는다
    //   캐릭터 = 레시피의 'characterTids'(CharacterTable TID). TID 0을 넣은 그림이 그림 없는 캐릭터의 대체 그림이다.
    //   아이콘 = 'icons/items/item_<TID>.png' · 'icons/equips/equip_<TID>.png' — 파일 이름의 TID가 곧 ItemTable·EquipTable TID.
    //            'item_0' · 'equip_0'이 아이콘 없는 것의 대체 아이콘이다.
    //   이펙트 = 'fx/**/<키>_<부위>.png' — 파일 이름 그대로가 찾는 이름('VisualCatalog.FxOf').
    //   배경·대상은 산업마다 하나라 목록에서 직접 고른다(여기서 건드리지 않는다).
    //
    // ■ 메뉴 '목록만 다시 쓰기' — 굽지 않고 목록만
    //   이펙트·아이콘 파일을 손으로 넣거나 바꿨을 때 '전부 다시 굽기'(캐릭터·배경까지 다시 굽는다)를 돌리지 않아도 된다.
    internal static class ArtCatalogSync
    {
        [MenuItem(ArtSpec.MenuRoot + "목록만 다시 쓰기", priority = 2)]
        private static void SyncFromMenu()
        {
            string result = Sync();

            AssetDatabase.SaveAssets();
            Debug.Log(result);
        }

        public static string Sync()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<VisualCatalog>(ArtSpec.CatalogPath);

            if (catalog == null)
            {
                return $"❌ 목록이 없다 — {ArtSpec.CatalogPath}";
            }

            var so = new SerializedObject(catalog);

            // 캐릭터 — 레시피 옆의 '<키>.asset'
            var characters = new List<(int tid, CharacterVisual visual)>();
            CharacterVisual? fallback = null;

            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(CharacterRecipe)}", new[] { ArtSpec.CharactersRoot }))
            {
                string recipePath = AssetDatabase.GUIDToAssetPath(guid);
                var    recipe     = AssetDatabase.LoadAssetAtPath<CharacterRecipe>(recipePath);
                string folder     = Path.GetDirectoryName(recipePath)!.Replace('\\', '/');
                var    visual     = AssetDatabase.LoadAssetAtPath<CharacterVisual>($"{folder}/{Path.GetFileName(folder)}.asset");

                if (recipe == null || visual == null)
                {
                    continue;
                }

                foreach (int tid in recipe.CharacterTids.Distinct())
                {
                    if (tid == 0)
                    {
                        fallback = visual;
                    }
                    else
                    {
                        characters.Add((tid, visual));
                    }
                }
            }

            SerializedProperty characterArray = so.FindProperty("characters");
            characterArray.arraySize = characters.Count;

            for (int i = 0; i < characters.Count; i++)
            {
                SerializedProperty entry = characterArray.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("characterTid").intValue        = characters[i].tid;
                entry.FindPropertyRelative("visual").objectReferenceValue = characters[i].visual;
            }

            // 대체 그림을 정한 레시피가 없으면 지금 값을 둔다
            if (fallback != null)
            {
                so.FindProperty("fallbackCharacter").objectReferenceValue = fallback;
            }

            int items  = WriteIcons(so, "itemIcons",  "fallbackItemIcon",  ArtSpec.ItemIconsRoot,  ArtSpec.ItemIconPrefix);
            int equips = WriteIcons(so, "equipIcons", "fallbackEquipIcon", ArtSpec.EquipIconsRoot, ArtSpec.EquipIconPrefix);
            int fxs    = WriteFx(so);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            string fallbackName = so.FindProperty("fallbackCharacter").objectReferenceValue is Object f ? f.name : "없음";

            return $"✅ 목록: 캐릭터 {characters.Count} (대체 {fallbackName}) · 자원 아이콘 {items} · 장비 아이콘 {equips} · 이펙트 {fxs}";
        }

        // 'fx/' 아래 PNG를 전부 훑어 파일 이름으로 목록에 쓴다. 이름이 겹치면 뒤의 것을 버리고 경고한다
        private static int WriteFx(SerializedObject so)
        {
            var fxs  = new List<(string key, Sprite sprite)>();
            var seen = new HashSet<string>();

            if (Directory.Exists(ArtSpec.FxRoot))
            {
                foreach (string file in Directory.GetFiles(ArtSpec.FxRoot, "*.png", SearchOption.AllDirectories))
                {
                    string path   = file.Replace('\\', '/');
                    string key    = Path.GetFileNameWithoutExtension(path);
                    var    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                    if (sprite == null)
                    {
                        continue;
                    }

                    if (!seen.Add(key))
                    {
                        Debug.LogWarning($"[아트] 이펙트 이름이 겹친다 — {path}는 목록에 넣지 않는다. 파일 이름에 연출 키를 붙인다('<키>_<부위>').");

                        continue;
                    }

                    fxs.Add((key, sprite));
                }
            }

            fxs.Sort((a, b) => string.CompareOrdinal(a.key, b.key));

            SerializedProperty array = so.FindProperty("fxSprites");
            array.arraySize = fxs.Count;

            for (int i = 0; i < fxs.Count; i++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue             = fxs[i].key;
                entry.FindPropertyRelative("sprite").objectReferenceValue = fxs[i].sprite;
            }

            return fxs.Count;
        }

        // '<접두>_<TID>.png'를 훑어 목록에 쓴다. TID 0은 대체 아이콘 칸으로. 돌려주는 값은 전용 아이콘 수
        private static int WriteIcons(SerializedObject so, string arrayName, string fallbackName, string root, string prefix)
        {
            var icons = new List<(int tid, Sprite sprite)>();
            Sprite? fallback = null;

            if (Directory.Exists(root))
            {
                foreach (string file in Directory.GetFiles(root, $"{prefix}*.png"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);

                    if (!int.TryParse(name.Substring(prefix.Length), out int tid))
                    {
                        continue;
                    }

                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/{name}.png");

                    if (sprite == null)
                    {
                        continue;
                    }

                    if (tid == 0)
                    {
                        fallback = sprite;
                    }
                    else
                    {
                        icons.Add((tid, sprite));
                    }
                }
            }

            icons.Sort((a, b) => a.tid.CompareTo(b.tid));

            SerializedProperty array = so.FindProperty(arrayName);
            array.arraySize = icons.Count;

            for (int i = 0; i < icons.Count; i++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("tid").intValue              = icons[i].tid;
                entry.FindPropertyRelative("icon").objectReferenceValue = icons[i].sprite;
            }

            so.FindProperty(fallbackName).objectReferenceValue = fallback;

            return icons.Count;
        }
    }
}
