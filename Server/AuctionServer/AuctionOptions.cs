namespace AuctionServer;

/// <summary>경매장 설정. <c>appsettings.json</c>의 <c>Auction</c> 절에서 읽는다.</summary>
public sealed class AuctionOptions
{
    /// <summary>경매장 DB 파일. 상대 경로면 실행 폴더 기준이다.</summary>
    public string DbPath { get; set; } = "auction.sqlite3";

    /// <summary>gRPC 리스너. 외부에 열지 않는다 — 내부망 인터페이스에만 바인딩한다.</summary>
    public string ListenAddress { get; set; } = "127.0.0.1";

    public int ListenPort { get; set; } = 10060;

    // 메인이 Reserve 뒤 정산 전에 죽으면 매물이 Reserved에 멈춘다. 이 시간이 지나면 Listed로 돌린다.
    // gRPC 기한(수 초)보다 훨씬 길어야 한다 — 짧으면 정상 정산 중인 매물이 풀려 다른 사람이 예약한다.
    public TimeSpan ReservationTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>시세의 하루 경계 — UTC에 더할 시차. 기본은 한국 시간(+9시간) 자정이다.</summary>
    public TimeSpan MarketDayOffset { get; set; } = TimeSpan.FromHours(9);

    /// <summary>만료·예약 타임아웃 청소 주기.</summary>
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>mTLS. 비워 두면 h2c(평문)다 — 같은 사설망일 때만 쓴다.</summary>
    public TlsOptions? Tls { get; set; }
}

/// <summary>서버 인증서와, 메인 서버 인증서를 검증할 CA. 둘 다 있어야 mTLS가 켜진다.</summary>
public sealed class TlsOptions
{
    public string CertificatePath     { get; set; } = "";
    public string CertificatePassword { get; set; } = "";

    /// <summary>메인 서버 클라이언트 인증서를 발급한 CA. 이 CA가 서명한 인증서만 받는다.</summary>
    public string ClientCaPath { get; set; } = "";
}
