# Project Structure

```text
src/
  ShareSync.Domain/          # Core entities and interfaces
  ShareSync.Application/     # Services and DTOs
  ShareSync.Infrastructure/  # EF Core DbContexts and Oracle mapping
  ShareSync.Web/             # ASP.NET Core API controllers and entry point
  ShareSync.DataImporter/    # Console utility for loading DSE history
frontend/                    # Vanilla HTML/JS/CSS client application
database/                    # Oracle SQL initialization and demo scripts
docs/                        # Project documentation
tests/                       # xUnit Test suite
```
