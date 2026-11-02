// 소리 채널 — 설정 화면의 음량 줄과 1:1이다. 저장 키도 이 이름을 쓴다('SoundManager.PrefsKey').
// 실제 음량 = Master × 채널. 효과음을 Sfx·Ui로 가른 것은 "버튼 소리만 끄기"를 따로 주려고다.
public enum SoundChannel
{
    Master,
    Bgm,
    Sfx,
    Ui,
}
