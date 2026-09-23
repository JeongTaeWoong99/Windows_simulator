using System.Security.Cryptography.X509Certificates;
using Grpc.Core;
using Grpc.Net.Client;
using Proto = AuctionProtocol;

namespace WSGameServer;

/// <summary>
/// 경매장 gRPC 호출 경계. <see cref="IDBQueue"/>와 같은 자리다 — 테스트는 가짜를 끼운다.
/// 전송 실패(연결 불가·기한 초과)는 예외로 올라온다. 부르는 쪽은 <b>로직 스레드에서 기다리지 않는다</b>.
/// </summary>
public interface IAuctionClient
{
    Task<Proto.RegisterReply>       RegisterAsync(Proto.RegisterRequest request);
    Task<Proto.ReserveReply>        ReserveAsync(Proto.ReserveRequest request);
    Task                            ConfirmAsync(Proto.ConfirmRequest request);
    Task<Proto.CancelReply>         CancelAsync(Proto.CancelRequest request);
    Task<Proto.SearchReply>         SearchAsync(Proto.SearchRequest request);
    Task<Proto.SellerListingsReply> GetSellerListingsAsync(Proto.SellerListingsRequest request);
    Task<Proto.FetchEventsReply>    FetchEventsAsync(Proto.FetchEventsRequest request);
    Task                            AckEventsAsync(Proto.AckEventsRequest request);
    Task<Proto.ListingStatesReply>  GetListingStatesAsync(Proto.ListingStatesRequest request);
}

/// <summary>경매장 연결 설정. 환경 변수로 덮는다 — 메인은 설정 파일이 없다.</summary>
public sealed class AuctionClientSettings
{
    public string   Address  { get; init; } = "http://127.0.0.1:10060";
    public TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(3);

    // mTLS — 셋 다 있으면 켠다. 주소도 https여야 한다.
    public string? ClientCertificatePath     { get; init; }
    public string? ClientCertificatePassword { get; init; }
    public string? ServerCaPath              { get; init; }

    public static AuctionClientSettings FromEnvironment()
    {
        return new AuctionClientSettings
        {
            Address                   = Environment.GetEnvironmentVariable("WS_AUCTION_ADDRESS") ?? "http://127.0.0.1:10060",
            ClientCertificatePath     = Environment.GetEnvironmentVariable("WS_AUCTION_CLIENT_CERT"),
            ClientCertificatePassword = Environment.GetEnvironmentVariable("WS_AUCTION_CLIENT_CERT_PASSWORD"),
            ServerCaPath              = Environment.GetEnvironmentVariable("WS_AUCTION_SERVER_CA"),
        };
    }
}

/// <summary>운영 구현. 호출마다 기한을 건다 — 경매장이 멈춰도 요청이 무한히 매달리지 않는다.</summary>
public sealed class GrpcAuctionClient : IAuctionClient, IDisposable
{
    private readonly GrpcChannel                _channel;
    private readonly Proto.Auction.AuctionClient _client;
    private readonly TimeSpan                   _deadline;

    public GrpcAuctionClient(AuctionClientSettings settings)
        : this(GrpcChannel.ForAddress(settings.Address, new GrpcChannelOptions { HttpHandler = CreateHandler(settings) }), settings.Deadline)
    {
    }

    /// <summary>채널을 직접 넘긴다 — 테스트가 TestServer 채널을 끼운다.</summary>
    public GrpcAuctionClient(GrpcChannel channel, TimeSpan deadline)
    {
        _channel  = channel;
        _client   = new Proto.Auction.AuctionClient(channel);
        _deadline = deadline;
    }

    private static HttpMessageHandler CreateHandler(AuctionClientSettings settings)
    {
        var handler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
        if (settings.ClientCertificatePath is null || settings.ServerCaPath is null)
        {
            return handler;
        }

        var ca = X509CertificateLoader.LoadCertificateFromFile(settings.ServerCaPath);
        handler.SslOptions.ClientCertificates = new X509CertificateCollection
        {
            X509CertificateLoader.LoadPkcs12FromFile(settings.ClientCertificatePath, settings.ClientCertificatePassword),
        };
        handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, _) =>
        {
            if (certificate is null)
            {
                return false;
            }

            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode      = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            return chain.Build(new X509Certificate2(certificate));
        };
        return handler;
    }

    private CallOptions Options() => new(deadline: DateTime.UtcNow + _deadline);

    public async Task<Proto.RegisterReply> RegisterAsync(Proto.RegisterRequest request)
        => await _client.RegisterAsync(request, Options());

    public async Task<Proto.ReserveReply> ReserveAsync(Proto.ReserveRequest request)
        => await _client.ReserveAsync(request, Options());

    public async Task ConfirmAsync(Proto.ConfirmRequest request)
        => await _client.ConfirmAsync(request, Options());

    public async Task<Proto.CancelReply> CancelAsync(Proto.CancelRequest request)
        => await _client.CancelAsync(request, Options());

    public async Task<Proto.SearchReply> SearchAsync(Proto.SearchRequest request)
        => await _client.SearchAsync(request, Options());

    public async Task<Proto.SellerListingsReply> GetSellerListingsAsync(Proto.SellerListingsRequest request)
        => await _client.GetSellerListingsAsync(request, Options());

    public async Task<Proto.FetchEventsReply> FetchEventsAsync(Proto.FetchEventsRequest request)
        => await _client.FetchEventsAsync(request, Options());

    public async Task AckEventsAsync(Proto.AckEventsRequest request)
        => await _client.AckEventsAsync(request, Options());

    public async Task<Proto.ListingStatesReply> GetListingStatesAsync(Proto.ListingStatesRequest request)
        => await _client.GetListingStatesAsync(request, Options());

    public void Dispose() => _channel.Dispose();
}
