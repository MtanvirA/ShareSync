# Architecture

ShareSync uses a standard ASP.NET Core Clean Architecture.

```mermaid
flowchart TD
    A[Frontend (HTML/Vanilla JS)] --> B[ASP.NET Core Web API]
    B --> C[Application Services]
    C --> D[Infrastructure / EF Core]
    D --> E[(Oracle Database 26ai)]
```

- **Web API Layer:** RESTful endpoints and JWT validation.
- **Application Layer:** Business logic, DTOs, and Interfaces (e.g. `IInvestmentIntelligenceService`).
- **Infrastructure Layer:** EF Core DbContext mapping to Oracle Database.
- **Domain Layer:** Core entities (`User`, `Portfolio`, `CompanyPriceHistory`).
