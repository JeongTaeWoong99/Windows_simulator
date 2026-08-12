using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 창고 열의 탭 줄 — 캐릭터 · 장비 · 자원 · 특성 4탭. (기획 2.2)
///
/// ⚠️ 지금은 자원 탭 하나만 실재한다. 나머지 셋은 화면이 아직 없다.
/// 이 클래스는 버튼을 잡아 두고 "아직 없다"를 알리는 것까지만 한다 —
/// 안 잡아 두면 눌러도 아무 일이 없어 버튼이 고장 난 것과 구분되지 않는다.
///
/// 탭이 생기면 'UIManager'가 '#Main Canvas'에 하는 것과 같은 방식으로 여기서 갈아 끼운다 —
/// 탭 enum 하나를 seam으로 두고 켜는 곳을 여기 한 곳으로 모은다 ('UI 규칙.md' 4장).
/// </summary>
public class StorageTabPresenter : MonoBehaviour
{
    // ※ NonReorderable 두 가지를 동시에 얻는다 —
    //   [1] 순서가 곧 탭이라 드래그로 뒤바뀌면 조용히 엉뚱한 탭이 열린다. 아예 못 끌게 막는다.
    //   [2] reorderable list 로 그려지면 Unity 가 그 위의 [CenterHeader] 를 건너뛴다 (UI 규칙 §6)
    [CenterHeader("참조")]
    [SerializeField, NonReorderable, Tooltip("탭 버튼들. 인스펙터에 넣은 순서가 곧 탭 순서다(캐릭터·장비·자원·특성)")]
    private Button[] tabButtons = new Button[0];

    // 참조 확보 → 배선 (클라 공통 규약)
    private void Start()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null)
                continue;

            // 반복 변수를 그대로 넘기면 모든 콜백이 마지막 값을 본다. 복사본을 캡처한다.
            int index = i;
            tabButtons[i].onClick.AddListener(() => OnTabClicked(index));
        }
    }

    // 탭을 눌렀다 (탭 버튼 OnClick에 코드로 연결)
    private void OnTabClicked(int index)
    {
        ClientLogger.Warn(ClientLogger.UI,
            $"창고 {index}번 탭은 아직 화면이 없다 — 지금 보이는 자원 탭이 전부다.", this);
    }
}
