using UnityEngine;

namespace DesktopWindowControl.EditorTools
{
    // 가공 결과물의 규격. 바꾸면 '전부 다시 굽기'로 모든 결과물을 다시 만든다 — 'Art 규칙.md'와 함께 고친다.
    internal static class ArtSpec
    {
        public const string Root            = "Assets/Art";
        public const string SourceRoot      = Root + "/_source";
        public const string CharactersRoot  = Root + "/characters";
        public const string BackgroundsRoot = Root + "/backgrounds";
        public const string TargetsRoot     = Root + "/targets";
        public const string IconsRoot       = Root + "/icons";
        public const string ItemIconsRoot   = IconsRoot + "/items";    // 'item_<ItemTID>.png' · 'item_0' = 대체
        public const string EquipIconsRoot  = IconsRoot + "/equips";   // 'equip_<EquipTID>.png' · 'equip_0' = 대체
        public const string ItemIconPrefix  = "item_";
        public const string EquipIconPrefix = "equip_";
        public const string IconRecipePath  = IconsRoot + "/icon_recipe.asset";

        // 런타임이 'Resources.Load'로 찾는다 — 그림과 같이 git 밖이라 'Art/' 안의 Resources에 둔다
        public const string CatalogPath     = Root + "/Resources/VisualCatalog.asset";

        public const string MenuRoot = "Window/DesktopWindowControl/아트/";

        // 모든 결과물이 같은 값 — 섞여도 크기 비율이 맞는다
        public const int PixelsPerUnit = 100;

        // 캐릭터 한 칸. 발이 아래 가운데(112, 0)에 온다 — 공격 사거리·이펙트가 왼쪽으로 뻗으므로 가로를 넉넉히 둔다
        // (160 → 224, 2026-10-05: 이펙트 든 공격이 발에서 왼쪽으로 105px까지 뻗는다)
        public static readonly Vector2Int CharacterCell = new Vector2Int(224, 96);

        // 이 값보다 불투명하면 그림이 있는 픽셀로 본다 (테두리·발 위치 계산)
        public const byte OpaqueAlpha = 26;

        // 프레임이 여러 장인 시트의 파일 접미사 — 임포트할 때 'Multiple'로 잘린다
        public const string IdleSuffix   = "_idle";
        public const string RunSuffix    = "_run";
        public const string AttackSuffix = "_attack";   // 연속 공격은 '_attack_1' · '_attack_2' …
        public const string EffectSuffix = "_fx";       // 공격 이펙트 띠 — '_attack_1_fx'

        public static bool IsStrip(string fileName)
            => fileName.EndsWith(IdleSuffix) || fileName.EndsWith(RunSuffix) || fileName.EndsWith(EffectSuffix) || System.Text.RegularExpressions.Regex.IsMatch(fileName, AttackSuffix + @"_\d+$");
    }
}
