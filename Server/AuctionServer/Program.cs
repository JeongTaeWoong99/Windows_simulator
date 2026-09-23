using AuctionServer;

var builder = WebApplication.CreateBuilder(args);

AuctionHost.ConfigureKestrel(builder);
AuctionHost.ConfigureServices(builder);

var app = builder.Build();

// 인덱스를 다 만든 뒤에 리스너를 연다 — 적재 중에 검색을 받으면 빈 결과가 나간다.
app.Services.GetRequiredService<AuctionEngine>();

AuctionHost.MapEndpoints(app);

app.Run();
