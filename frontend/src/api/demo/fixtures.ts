// Seed data for demo mode. Companies are fictional on purpose: the demo must not
// pass off invented listings as real openings at real employers. Market/level/stack
// values use the exact tokens the backend normalizer produces (see FilterBar).

export interface VacancySeed {
  source: 'Remotive' | 'RemoteOK' | 'JobRadar';
  title: string;
  company: string;
  market: string;
  level: string;
  stack: string;
  location: string;
  salaryRaw: string | null;
  skills: string;
}

export const SEED_VACANCIES: VacancySeed[] = [
  { source: 'Remotive', title: 'Senior .NET Engineer (Payments)', company: 'Kestrel Pay', market: 'Европа', level: 'senior+', stack: 'C#/.NET', location: 'Remote, EU', salaryRaw: '€85k–€105k', skills: 'C#, ASP.NET Core, PostgreSQL, Kafka, Azure' },
  { source: 'RemoteOK', title: 'Backend Developer — Go / PostgreSQL', company: 'Lumen Freight', market: 'Worldwide', level: 'middle', stack: 'backend', location: 'Anywhere', salaryRaw: '$70k–$90k', skills: 'Go, PostgreSQL, gRPC, Docker, Kubernetes' },
  { source: 'Remotive', title: 'Middle C# Developer', company: 'Quarry Systems', market: 'Европа', level: 'middle', stack: 'C#/.NET', location: 'Remote (Poland, Germany)', salaryRaw: '€55k–€70k', skills: 'C#, .NET 8, EF Core, SQL Server, RabbitMQ' },
  { source: 'RemoteOK', title: 'Staff Backend Engineer, Platform', company: 'Orbitly', market: 'США', level: 'senior+', stack: 'backend', location: 'Remote, US', salaryRaw: '$180k–$220k', skills: 'Kotlin, PostgreSQL, Kafka, AWS, Terraform' },
  { source: 'Remotive', title: 'Junior .NET Developer', company: 'Brightforge', market: 'Европа', level: 'junior', stack: 'C#/.NET', location: 'Remote, Portugal', salaryRaw: '€30k–€38k', skills: 'C#, ASP.NET Core, SQL, Git' },
  { source: 'RemoteOK', title: 'Senior Software Engineer (.NET, Azure)', company: 'Maple Grid', market: 'Канада', level: 'senior+', stack: 'C#/.NET', location: 'Remote, Canada', salaryRaw: 'CA$140k–CA$165k', skills: 'C#, Azure Functions, Cosmos DB, Service Bus, Terraform' },
  { source: 'Remotive', title: 'Backend-разработчик C# (Middle+)', company: 'Vostok Soft', market: 'Россия', level: 'middle', stack: 'C#/.NET', location: 'Удалённо, РФ', salaryRaw: '250 000–320 000 ₽', skills: 'C#, ASP.NET Core, PostgreSQL, Redis, Docker' },
  { source: 'RemoteOK', title: 'Python Backend Engineer', company: 'Tidewater Health', market: 'США', level: 'middle', stack: 'backend', location: 'Remote, US time zones', salaryRaw: '$120k–$145k', skills: 'Python, Django, PostgreSQL, Celery, AWS' },
  { source: 'Remotive', title: 'Lead .NET Architect', company: 'Meridian Bank Tech', market: 'Европа', level: 'senior+', stack: 'C#/.NET', location: 'Remote, UK/EU', salaryRaw: '£95k–£115k', skills: 'C#, Microservices, DDD, Kafka, Kubernetes, OpenTelemetry' },
  { source: 'RemoteOK', title: 'Backend Engineer (Node.js)', company: 'Pinecone Games', market: 'Worldwide', level: 'mid/unknown', stack: 'backend', location: 'Worldwide', salaryRaw: null, skills: 'Node.js, TypeScript, PostgreSQL, Redis' },
  { source: 'Remotive', title: 'Senior Backend Developer (C#)', company: 'Caspian Digital', market: 'СНГ', level: 'senior+', stack: 'C#/.NET', location: 'Remote, Kazakhstan / Armenia', salaryRaw: '$4,000–$5,500 / month', skills: 'C#, .NET 8, PostgreSQL, ClickHouse, Kafka' },
  { source: 'RemoteOK', title: 'Backend Engineer, Mobility Platform', company: 'Sakura Mobility', market: 'Япония', level: 'middle', stack: 'backend', location: 'Remote, Japan', salaryRaw: '¥8M–¥11M', skills: 'Go, MySQL, GCP, Pub/Sub' },
  { source: 'Remotive', title: '.NET Developer — Integrations', company: 'Atlas Logistics', market: 'Европа', level: 'middle', stack: 'C#/.NET', location: 'Remote, Netherlands', salaryRaw: '€60k–€75k', skills: 'C#, REST, SOAP, Azure Service Bus, SQL Server' },
  { source: 'RemoteOK', title: 'Senior Backend Engineer (Java)', company: 'Polar Ledger', market: 'Европа', level: 'senior+', stack: 'backend', location: 'Remote, Nordics', salaryRaw: '€90k–€110k', skills: 'Java, Spring Boot, PostgreSQL, Kafka' },
  { source: 'Remotive', title: 'C# Engineer — Real-time Systems', company: 'Corvid Security', market: 'США', level: 'senior+', stack: 'C#/.NET', location: 'Remote, US', salaryRaw: '$150k–$175k', skills: 'C#, SignalR, Redis, PostgreSQL, gRPC' },
  { source: 'RemoteOK', title: 'Junior Backend Developer', company: 'Nimbus Retail', market: 'Worldwide', level: 'junior', stack: 'backend', location: 'Anywhere', salaryRaw: '$35k–$45k', skills: 'Python, FastAPI, PostgreSQL, Docker' },
  { source: 'Remotive', title: 'Middle .NET Developer (Fintech)', company: 'Fjord Analytics', market: 'Европа', level: 'middle', stack: 'C#/.NET', location: 'Remote, Estonia', salaryRaw: '€50k–€65k', skills: 'C#, ASP.NET Core, PostgreSQL, Dapper, xUnit' },
  { source: 'RemoteOK', title: 'Platform Engineer (Backend)', company: 'Helix Insurance Tech', market: 'Канада', level: 'middle', stack: 'backend', location: 'Remote, Canada', salaryRaw: 'CA$110k–CA$130k', skills: 'Go, Kubernetes, PostgreSQL, Prometheus' },
  { source: 'Remotive', title: 'Senior C# Developer — Trading', company: 'Redwood Analytics', market: 'США', level: 'senior+', stack: 'C#/.NET', location: 'Remote, US', salaryRaw: '$170k–$200k', skills: 'C#, .NET 9, low-latency, Kafka, TimescaleDB' },
  { source: 'RemoteOK', title: 'Backend-разработчик Go', company: 'Vostok Soft', market: 'Россия', level: 'senior+', stack: 'backend', location: 'Удалённо, РФ', salaryRaw: '350 000–450 000 ₽', skills: 'Go, PostgreSQL, Kafka, Kubernetes' },
  { source: 'Remotive', title: 'ASP.NET Core Developer', company: 'Brightforge', market: 'Европа', level: 'mid/unknown', stack: 'C#/.NET', location: 'Remote, Spain', salaryRaw: null, skills: 'C#, ASP.NET Core, Blazor, SQL Server' },
  { source: 'RemoteOK', title: 'Backend Engineer, Data Pipelines', company: 'Fjord Analytics', market: 'Worldwide', level: 'senior+', stack: 'backend', location: 'Worldwide', salaryRaw: '$110k–$140k', skills: 'Python, Kafka, Airflow, ClickHouse' },
  { source: 'Remotive', title: '.NET Backend Engineer (Healthcare)', company: 'Tidewater Health', market: 'Канада', level: 'middle', stack: 'C#/.NET', location: 'Remote, Canada', salaryRaw: 'CA$100k–CA$120k', skills: 'C#, .NET 8, FHIR, PostgreSQL, Azure' },
  { source: 'RemoteOK', title: 'Senior .NET Engineer, APAC', company: 'Sakura Mobility', market: 'Азия', level: 'senior+', stack: 'C#/.NET', location: 'Remote, Singapore / Tokyo', salaryRaw: 'S$120k–S$150k', skills: 'C#, ASP.NET Core, PostgreSQL, AWS, Terraform' },
  { source: 'Remotive', title: 'Junior C# Developer (Internship → Full-time)', company: 'Quarry Systems', market: 'СНГ', level: 'junior', stack: 'C#/.NET', location: 'Remote, CIS', salaryRaw: '$1,200–$1,600 / month', skills: 'C#, .NET, SQL, Git' },
  { source: 'RemoteOK', title: 'Backend Engineer (Rust)', company: 'Corvid Security', market: 'Европа', level: 'senior+', stack: 'backend', location: 'Remote, EU', salaryRaw: '€95k–€120k', skills: 'Rust, Tokio, PostgreSQL, gRPC' },
  { source: 'Remotive', title: 'Middle .NET Developer — Logistics', company: 'Lumen Freight', market: 'Азия', level: 'middle', stack: 'C#/.NET', location: 'Remote, Asia time zones', salaryRaw: '$3,500–$4,500 / month', skills: 'C#, EF Core, PostgreSQL, RabbitMQ, Docker' },
  { source: 'RemoteOK', title: 'Senior Backend Developer (PHP → Go migration)', company: 'Nimbus Retail', market: 'Европа', level: 'senior+', stack: 'backend', location: 'Remote, EU', salaryRaw: '€80k–€95k', skills: 'Go, PHP, MySQL, Kafka' },
  { source: 'JobRadar', title: 'Senior .NET Engineer — Ingestion Platform', company: 'Northwind Labs', market: 'Worldwide', level: 'senior+', stack: 'C#/.NET', location: 'Remote, worldwide', salaryRaw: '$120k–$150k', skills: 'C#, .NET 10, Kafka, PostgreSQL, OpenTelemetry' },
  { source: 'JobRadar', title: 'Middle Backend Developer (C#)', company: 'Northwind Labs', market: 'Европа', level: 'middle', stack: 'C#/.NET', location: 'Remote, Europe', salaryRaw: '€60k–€75k', skills: 'C#, ASP.NET Core, EF Core, SignalR' },
  { source: 'JobRadar', title: 'Junior .NET Developer', company: 'Northwind Labs', market: 'СНГ', level: 'junior', stack: 'C#/.NET', location: 'Remote, CIS', salaryRaw: '$1,500–$2,000 / month', skills: 'C#, SQL, Git, xUnit' },
];

// Vacancies the simulated ingestion worker "discovers" while the demo is open.
export const INCOMING_VACANCIES: VacancySeed[] = [
  { source: 'Remotive', title: 'Senior .NET Developer — Event Sourcing', company: 'Polar Ledger', market: 'Европа', level: 'senior+', stack: 'C#/.NET', location: 'Remote, EU', salaryRaw: '€90k–€110k', skills: 'C#, EventStoreDB, Kafka, PostgreSQL' },
  { source: 'RemoteOK', title: 'Backend Engineer (Go, Payments)', company: 'Kestrel Pay', market: 'Worldwide', level: 'middle', stack: 'backend', location: 'Anywhere', salaryRaw: '$85k–$105k', skills: 'Go, PostgreSQL, Kafka' },
  { source: 'Remotive', title: 'Middle C# Developer — B2B SaaS', company: 'Atlas Logistics', market: 'СНГ', level: 'middle', stack: 'C#/.NET', location: 'Remote, CIS', salaryRaw: '$2,800–$3,600 / month', skills: 'C#, ASP.NET Core, PostgreSQL, Redis' },
  { source: 'RemoteOK', title: 'Senior Backend Engineer, Search', company: 'Orbitly', market: 'США', level: 'senior+', stack: 'backend', location: 'Remote, US', salaryRaw: '$170k–$195k', skills: 'Java, Elasticsearch, Kafka' },
  { source: 'Remotive', title: 'Junior Backend Developer (.NET)', company: 'Maple Grid', market: 'Канада', level: 'junior', stack: 'C#/.NET', location: 'Remote, Canada', salaryRaw: 'CA$60k–CA$70k', skills: 'C#, .NET 8, SQL Server' },
  { source: 'RemoteOK', title: 'Senior .NET Engineer — Observability', company: 'Meridian Bank Tech', market: 'Европа', level: 'senior+', stack: 'C#/.NET', location: 'Remote, UK/EU', salaryRaw: '£85k–£100k', skills: 'C#, OpenTelemetry, Grafana, Kubernetes' },
  { source: 'Remotive', title: 'Backend Developer (Python, Logistics)', company: 'Lumen Freight', market: 'Азия', level: 'middle', stack: 'backend', location: 'Remote, APAC', salaryRaw: '$3,000–$4,000 / month', skills: 'Python, FastAPI, PostgreSQL' },
  { source: 'RemoteOK', title: 'C#/.NET Developer — Integrations', company: 'Caspian Digital', market: 'Россия', level: 'middle', stack: 'C#/.NET', location: 'Удалённо, РФ', salaryRaw: '220 000–280 000 ₽', skills: 'C#, REST, PostgreSQL, RabbitMQ' },
];

export const NATIVE_COMPANY = 'Northwind Labs';

export interface CandidateSeed {
  email: string;
  displayName: string;
}

// Applicants the demo employer sees in their inbox (reserved example.com domain).
export const SEED_APPLICANTS: CandidateSeed[] = [
  { email: 'maria.k@example.com', displayName: 'Maria K.' },
  { email: 'tom.becker@example.com', displayName: 'Tom Becker' },
  { email: 'li.wei@example.com', displayName: 'Li Wei' },
];
