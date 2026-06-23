using JobRadar.Infrastructure;
using JobRadar.Worker;
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

builder.Services.AddSingleton<IVacancyMessageProducer, KafkaVacancyProducer>();

builder.Services.AddHostedService<VacancyConsumer>();
builder.Services.AddHostedService<RemotiveCollector>();

var host = builder.Build();
host.Run();
