# Architecture — MySQL Version

ShareSync uses a standard ASP.NET Core Clean Architecture.

```mermaid
flowchart TD
    A[Frontend: HTML5, CSS3, Bootstrap 5, JS, Chart.js] --> B[ASP.NET Core Web API: C# / .NET 8]
    B --> C[Application Layer: Services, DTOs, Business Logic]
    C --> D[Infrastructure Layer: Pomelo EF Core Provider]
    D --> E[(MySQL Database 8.x)]
```

- **Web API Layer (`ShareSync.Web`):** RESTful controllers, Swagger documentation, JWT bearer authentication middleware, and SPA static file hosting.
- **Application Layer (`ShareSync.Application`):** Core business logic, interfaces, analytical calculations (Investment Intelligence, Portfolio valuation, Reporting).
- **Infrastructure Layer (`ShareSync.Infrastructure`):** Data access via `ShareSyncDbContext`, Pomelo MySQL EF Core provider, BCrypt/PBKDF2 security, and external DSE synchronization.
- **Domain Layer (`ShareSync.Domain`):** Core entity models (`AppUser`, `Portfolio`, `Transaction`, `CompanyPriceHistory`, etc.).
