using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace AuctionServer;

/// <summary>호스트 조립. 테스트는 같은 조립에 TestServer만 끼워 쓴다 — 운영과 다른 배선을 따로 두지 않는다.</summary>
public static class AuctionHost
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection("Auction").Get<AuctionOptions>() ?? new AuctionOptions();

        builder.Services.AddSingleton(options);
        builder.Services.TryAddSingletonTimeProvider();
        builder.Services.AddSingleton(sp =>
        {
            var engine = new AuctionEngine(AuctionStore.Open(options.DbPath), sp.GetRequiredService<TimeProvider>(), options);
            engine.Start();
            return engine;
        });
        builder.Services.AddHostedService<SweepService>();
        builder.Services.AddGrpc();
    }

    public static void MapEndpoints(WebApplication app)
    {
        app.MapGrpcService<AuctionGrpcService>();
    }

    /// <summary>gRPC 리스너 하나만 연다. HTTP/2 전용 — h2c는 TLS 없이 HTTP/2를 쓰려면 프로토콜을 못 박아야 한다.</summary>
    public static void ConfigureKestrel(WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection("Auction").Get<AuctionOptions>() ?? new AuctionOptions();

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(System.Net.IPAddress.Parse(options.ListenAddress), options.ListenPort, listen =>
            {
                listen.Protocols = HttpProtocols.Http2;

                if (options.Tls is { } tls)
                {
                    listen.UseHttps(https => ConfigureMutualTls(https, tls));
                }
            });
        });
    }

    // 메인 서버 인증서만 받는다. 정산 API가 열리면 곧 골드 복제라, 네트워크 층에서 호출자를 못 박는다.
    private static void ConfigureMutualTls(HttpsConnectionAdapterOptions https, TlsOptions tls)
    {
        https.ServerCertificate     = X509CertificateLoader.LoadPkcs12FromFile(tls.CertificatePath, tls.CertificatePassword);
        https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;

        var ca = X509CertificateLoader.LoadCertificateFromFile(tls.ClientCaPath);
        https.ClientCertificateValidation = (certificate, _, _) =>
        {
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode         = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.RevocationMode    = X509RevocationMode.NoCheck;
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            return chain.Build(certificate);
        };
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
