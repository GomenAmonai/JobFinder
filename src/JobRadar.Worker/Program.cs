using JobRadar.Application.Ingestion;
using JobRadar.Infrastructure;
using JobRadar.Worker.Collectors;
using JobRadar.Worker.Ingestion;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(KafkaSettings.SectionName));

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("Missing connection string 'Postgres'."));

builder.Services.AddHttpClient("remotive", client =>
    {
        client.BaseAddress = new Uri("https://remotive.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient("remoteok", client =>
    {
        client.BaseAddress = new Uri("https://remoteok.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
        // RemoteOK отдаёт 403 на запросы без User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("JobRadar/1.0 (+https://github.com/GomenAmonai/JobFinder)");
    })
    .AddStandardResilienceHandler();

builder.Services.AddSingleton<IKafkaPublisher, KafkaPublisher>();

// Топики создаём первыми, до consumer/collector.
builder.Services.AddHostedService<KafkaTopicInitializer>();
builder.Services.AddHostedService<VacancyConsumer>();
builder.Services.AddHostedService<RemotiveCollector>();
builder.Services.AddHostedService<RemoteOkCollector>();

var host = builder.Build();
host.Run();
