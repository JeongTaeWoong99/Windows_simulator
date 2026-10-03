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
        public const string CatalogPath     = Root + "/VisualCatalog.asset";

        public const string MenuRoot = "Window/DesktopWindowControl/아트/";

        // 모든 결과물이 같은 값 — 섞여도 크기 비율이 맞는다
        public const int PixelsPerUnit = 100;

        // 캐릭터 한 칸. 발이 아래 가운데(80, 0)에 온다 — 공격 사거리가 왼쪽으로 뻗으므로 가로를 넉넉히 둔다
        public static readonly Vector2Int CharacterCell = new Vector2Int(160, 96);

        // 이 값보다 불투명하면 그림이 있는 픽셀로 본다 (테두리·발 위치 계산)
        public const byte OpaqueAlpha = 26;

        // 프레임이 여러 장인 시트의 파일 접미사 — 임포트할 때 'Multiple'로 잘린다
        public const string RunSuffix    = "_run";
        public const string AttackSuffix = "_attack";
    }
}
