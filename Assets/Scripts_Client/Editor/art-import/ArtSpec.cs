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
        public const string IndustryIconsRoot = IconsRoot + "/industries"; // 'industry_<EIndustryType 값>.png' · 대체는 'item_0'
        public const string ArtifactIconsRoot = IconsRoot + "/artifacts";  // 'artifact_<ArtifactTID>.png' · 대체 없음(이름 첫 글자)
        public const string ItemIconPrefix  = "item_";
        public const string EquipIconPrefix = "equip_";
        public const string IndustryIconPrefix = "industry_";
        public const string ArtifactIconPrefix = "artifact_";
        public const string IconRecipePath  = IconsRoot + "/icon_recipe.asset";

        // 아이콘 화풍 통일 (2026-10-29) — 기준 32px. 긴 변이 32를 넘을 때만 줄이고, 어두운 1px 외곽선을 두른다.
        // 캔버스는 그림에 딱 맞춘다 — 칸이 비율을 지켜 꽉 채운다. (16px 고정 캔버스는 큰 그림이 흐려지고 작은 그림이 칸 속에 작게 떠서 되돌렸다)
        // 원본 긴 변이 'IconMinSource' 미만이면 칸이 크게 늘려 픽셀이 굵어진다 — 검사가 알린다(다시 그리거나 바꿀 후보)
        public const int IconMaxSize   = 32;
        public const int IconMinSource = 24;
        public static readonly Color32 IconOutline = new Color32(25, 20, 30, 255);

        // 가장자리 픽셀의 이 비율 이상이 밝은 무채색이면 원본에 흰 외곽선이 그려진 것으로 보고 벗긴다 ('ArtImage.StripLightRim').
        // 실측 — cainos 팩 0.73~1.0 · 밝은 파랑 통·상자(pixel-item-pack) 0.37~0.5 · 보석(karsiori) 0.41
        public const float IconLightRimRatio = 0.7f;

        // 캐릭터 해상도 기준 — 결과 SO의 'characterScale'(원본을 정한 크기까지 몇 배로 키우나)이 이보다 크면 뭉툭해 보인다. 검사가 알린다
        public const float CharacterMaxScale = 1.5f;

        // 이펙트 — 'fx/<연출 키>/<키>_<부위>.png'. 굽지 않고 그린(또는 만든) 그대로 쓴다. 파일 이름이 목록의 찾는 이름이다
        public const string FxRoot          = Root + "/fx";

        // 런타임이 'Resources.Load'로 찾는다 — 그림과 같이 git 밖이라 'Art/' 안의 Resources에 둔다
        public const string CatalogPath     = Root + "/Resources/VisualCatalog.asset";

        // 소리 — 'audio/<채널>/<키>.wav' (bgm · ui · sfx). 파일 이름이 곧 찾는 이름('Sounds' · 'SoundCatalog.Of')
        public const string AudioRoot        = Root + "/audio";
        public const string SoundCatalogPath = Root + "/Resources/" + SoundCatalog.ResourcePath + ".asset";

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
