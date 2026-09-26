// 임시 UI 색의 "역할". 화면은 색을 직접 들지 않고 역할만 들며, 색은 'UIThemePalette' 한 곳이 정한다.
//
// ⚠️ 값이 씬·프리팹에 정수로 직렬화된다 — 순서를 바꾸거나 중간을 지우지 않는다. 새 역할은 끝에 번호를 붙여 더한다.
public enum UIThemeRole
{
    // 바탕
    PanelBg  = 0,  // 창·캔버스 바탕
    PanelSub = 1,  // 패널 안 구획·행 바탕
    Slot     = 2,  // 슬롯·칸 바탕
    Border   = 3,  // 테두리·구분선

    // 버튼
    Button         = 10, // 일반 버튼
    ButtonSelected = 11, // 고른 탭·고른 산업·찍은 노드
    ButtonPrimary  = 12, // 핵심 행동(가챠·수령·확인)
    ButtonDisabled = 13, // 잠긴 버튼

    // 강조
    Accent    = 20, // 하늘색 강조·진행 바
    Highlight = 21, // 노랑 — 재화 수치·중요 문구

    // 글씨
    TextMain     = 30,
    TextSub      = 31,
    TextDisabled = 32,
    TextDark     = 33, // 밝은 바탕 위 글씨

    // 의미색
    Positive = 40, // 증가·성공
    Negative = 41, // 부족·경고·삭제
    Overlay  = 42, // 모달 뒤 딤·칸 위 어둠 (검정 — 투명도는 붙은 쪽이 정한다)
}
