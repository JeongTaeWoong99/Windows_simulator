using MikaProtocol;

namespace WSGameServer;

public partial class User
{
    /// <summary>계정(섬주인) 레벨. 캐릭터가 얻은 경험치가 그대로 쌓여 오른다(특성 3장).</summary>
    public int AccountLevel { get; private set; } = 1;

    /// <summary>현재 레벨에서 쌓은 계정 경험치. 곡선이 캐릭터의 8배라 long이다.</summary>
    public long AccountExp { get; private set; }

    /// <summary>DB에서 읽은 계정 행을 적재한다(로그인 시 1회). 행이 없으면 레벨 1 — 재화처럼 가입 시 행을 만들지 않는다.</summary>
    public void LoadAccount(AccountRow? row)
    {
        AccountLevel = row?.level ?? 1;
        AccountExp   = row?.exp ?? 0;
    }

    /// <summary>계정 경험치를 더한다. 계정 레벨은 건물 레벨의 상한이다(T-183). 바뀐 것이 있으면 저장하고 밀어 준다.</summary>
    public void GainAccountExp(long amount, bool notify)
    {
        if (amount <= 0 || AccountLevel >= _accountLevels.MaxLevel)
        {
            return;
        }

        AccountExp += amount;

        while (AccountLevel < _accountLevels.MaxLevel &&
               _accountLevels.TryGetRequiredExp(AccountLevel + 1, out var required) &&
               AccountExp >= required)
        {
            AccountExp -= required;
            AccountLevel++;
        }

        // 만렙에 닿으면 남은 조각은 버린다 — 캐릭터 레벨과 같은 규칙이다.
        if (AccountLevel >= _accountLevels.MaxLevel)
        {
            AccountExp = 0;
        }

        SaveAccount(notify);
    }

    /// <summary>계정 레벨 스냅샷을 보낸다(로그인 직후).</summary>
    public void SendAccountLevel()
    {
        Send(new S_AccountLevelResponse { Level = AccountLevel, Exp = AccountExp });
    }

    private void SaveAccount(bool notify)
    {
        PostDBTask(new SaveAccountRepository(this, AccountLevel, AccountExp));

        if (notify)
        {
            SendAccountLevel();
        }
    }
}
