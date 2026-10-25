using System;

// 아티팩트 화면(도감)이 함께 보는 상태 — 고른 번호 하나와, 방금 장착해 반짝일 번호 (T-151).
//
// ■ 왜 매니저인가 — 'SellCartModel'과 같은 이유다
//   도감 격자 · 상세 칸 · 인벤토리의 아티팩트 창이 같은 "고른 번호"를 본다. 서로를 직접 쥐면 화면 사이 참조가 그물이 된다.
//
// ■ 송신하지 않는다 — 장착 · 켜고 끄기 요청은 버튼을 누른 Presenter가 직접 보낸다('SellCartModel'과 같은 경계).
//
// ※ 판매 목록은 여기 없다 — 아티팩트는 인벤토리 장비 탭에서 장비와 같은 판매 목록('SellCartModel')에 담는다(2026-10-25 사용자 결정).
public class ArtifactModel : MonoService<ArtifactModel>
{
    // 지금 고른 번호 — 상세 칸이 그린다. 0이면 고른 것이 없다.
    public int SelectedTid { get; private set; }

    // 방금 장착해 도감에서 반짝일 번호 — 도감이 꺼져 있다가 켜지는 중이면 칸이 아직 없어서, 도감이 그릴 준비가 되면 꺼내 간다('TakeHighlight').
    private int _pendingHighlight;

    // 고른 번호가 바뀌었다 — 상세 칸과 도감(고름 표시)이 다시 그린다.
    public event Action? SelectionChanged;

    // 반짝일 번호가 생겼다 — 켜져 있는 도감이 바로 꺼내 간다.
    public event Action? HighlightRequested;

    // 이 번호를 고른다 — 같은 번호면 아무 일도 없다.
    public void Select(int artifactTid)
    {
        if (SelectedTid == artifactTid)
        {
            return;
        }

        SelectedTid = artifactTid;
        SelectionChanged?.Invoke();
    }

    // 방금 장착한 번호 — 고르고, 도감에 반짝임을 맡긴다 (인벤토리 아티팩트 창이 장착 성공에서 호출).
    public void Highlight(int artifactTid)
    {
        Select(artifactTid);

        _pendingHighlight = artifactTid;
        HighlightRequested?.Invoke();
    }

    // 반짝일 번호를 꺼낸다 — 없으면 0 (도감이 그릴 준비가 됐을 때 호출 · 한 번만 꺼내진다).
    public int TakeHighlight()
    {
        int tid = _pendingHighlight;

        _pendingHighlight = 0;

        return tid;
    }
}
