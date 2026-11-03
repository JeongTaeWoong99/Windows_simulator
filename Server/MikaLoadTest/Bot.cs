using MikaNetwork;
using MikaProtocol;

namespace MikaLoadTest;

/// <summary>
/// 유저 한 명을 흉내 내는 봇. 로그인 → 슬롯 배치 → 5초마다 핑(실제 클라 PingManager와 같은 주기).
/// heavy면 치트로 슬롯 7칸을 열고 캐릭터를 받아 전부 돌린다 — 판정·저장·푸시가 가장 많은 유저다.
/// </summary>
public sealed class Bot
{
    private const int  DefaultCharacterTid = 1001;
    private const int  SlotCount           = 7;
    private const int  FirstSlotUnlockTid  = 1002;   // 작업슬롯 2~7번 칸 = 1002~1007

    private static readonly EIndustryType[] Industries =
        { EIndustryType.Farming, EIndustryType.Excavation, EIndustryType.Mining, EIndustryType.Logging, EIndustryType.Hunting };

    private readonly MikaClient        _client  = new();
    private readonly MikaPacketManager _packets = new();
    private readonly string _id;
    private readonly bool   _heavy;
    private bool _assigned;

    public long BytesReceived;
    public long PacketsReceived;
    public long GatherResults;
    public bool LoggedIn;
    public bool Disconnected;

    public Bot(string id, bool heavy)
    {
        _id    = id;
        _heavy = heavy;

        _packets.Register<S_LoginResponse>((ushort)PacketId.S_LoginResponse, (_, res) => OnLogin(res));
        _packets.Register<S_CharacterListResponse>((ushort)PacketId.S_CharacterListResponse, (_, res) => OnCharacters(res));
        _packets.Register<S_GatherResultResponse>((ushort)PacketId.S_GatherResultResponse, (_, _) => Interlocked.Increment(ref GatherResults));

        _client.PacketReceived += (session, data) =>
        {
            Interlocked.Add(ref BytesReceived, data.Length);
            Interlocked.Increment(ref PacketsReceived);
            _packets.OnRecvPacket(session, data);
            return default;
        };
    }

    public async Task StartAsync(string host, int port)
    {
        await _client.ConnectAsync(host, port);
        _client.Session!.Disconnected += _ => Disconnected = true;
        _client.Send(new C_LoginRequest { Id = _id });
    }

    public void Ping() => _client.Send(new C_PingRequest());

    public void Stop() => _client.Disconnect();

    private void OnLogin(S_LoginResponse res)
    {
        if (res.Result != EResultCode.Ok)
        {
            Console.WriteLine($"[{_id}] 로그인 실패 {res.Result}");
            return;
        }

        LoggedIn = true;
        if (!_heavy)
        {
            return;
        }

        // 다시 돌릴 때 이미 열린 칸·받은 캐릭터는 서버가 거절하거나 늘리기만 한다 — 배치는 목록이 차면 한 번만 한다.
        for (var i = 0; i < SlotCount - 1; i++)
        {
            _client.Send(new C_CheatRequest { Command = ECheatCommand.Unlock, Arg1 = FirstSlotUnlockTid + i });
        }

        _client.Send(new C_CheatRequest { Command = ECheatCommand.GiveCharacter, Arg1 = DefaultCharacterTid, Arg2 = SlotCount - 1 });
    }

    private void OnCharacters(S_CharacterListResponse res)
    {
        var characters = res.Characters ?? new List<CharacterInfo>();
        var need       = _heavy ? SlotCount : 1;
        if (_assigned || characters.Count < need)
        {
            return;
        }

        _assigned = true;
        for (var slot = 0; slot < need; slot++)
        {
            _client.Send(new C_WorkStationAssignRequest
            {
                SlotIndex     = slot,
                Industry      = Industries[slot % Industries.Length],
                IndustryLevel = 1,
                CharacterId   = characters[slot].CharacterId,
            });
        }
    }
}
