# ShareSync — Project Structure & Repository Organization

This document provides a complete architectural map of the **ShareSync MySQL** repository (`mysql-version`), designed for academic evaluators, teachers, and developers navigating the codebase.

---

## High-Level Repository Layout

```
ShareSync/
├── src/                               # Backend Clean Architecture projects & console utilities
│   ├── ShareSync.Domain/              # Enterprise entities, value objects, and database views
│   ├── ShareSync.Application/         # Application business logic, services, DTOs, and interfaces
│   ├── ShareSync.Infrastructure/      # MySQL EF Core DbContext, security services, and market sync
│   ├── ShareSync.Web/                 # ASP.NET Core Web API controllers and static file host
│   └── ShareSync.DataImporter/        # High-throughput batch importer for canonical historical DSE data
│
├── frontend/                          # Client web application (HTML5, Bootstrap 5, Vanilla JS, Chart.js)
│   ├── *.html                         # 17 responsive web pages (Dashboard, Transactions, Intel, etc.)
│   ├── css/style.css                  # Custom styling, dark/light themes, and UI enhancements
│   └── js/                            # Modular client logic (app.js, investment-intelligence.js)
│
├── database/                          # Database kits & initialization scripts
│   ├── mysql/                         # Authoritative MySQL 8.x schema, routines, seeds & tests
│   ├── oracle/                        # Oracle SQL scripts (retained for dual-version reference)
│   └── verification/                  # Database audit and verification scripts
│
├── docs/                              # Project documentation, guides, reports, and viva preparation
│   ├── reports/                       # Machine-readable evaluation reports (DataQualityReport.json)
│   ├── ARCHITECTURE.md                # Clean Architecture layers and technology stack
│   ├── DATABASE.md                    # MySQL database schema, constraints, views & routines
│   ├── MYSQL_DEMO_GUIDE.md            # Step-by-step teacher demonstration script
│   ├── MYSQL_VIVA.md                  # Comprehensive academic viva voce Q&A guide
│   ├── TESTING.md                     # Automated testing guide (xUnit test suite)
│   └── ...                            # SQA, security, data accuracy, and formula audits
│
├── tests/                             # Automated test suite
│   └── ShareSync.Tests/               # 490 unit, integration, and mathematical accuracy tests
│
├── .github/workflows/                 # Continuous Integration workflows (GitHub Actions)
├── ShareSync.bat                      # One-click Windows launcher (preflight checks & health probe)
├── ShareSync.sln                      # Visual Studio / .NET SDK solution file
├── Directory.Build.props              # MSBuild configuration (Major version roll-forward)
├── README.md                          # Main project overview and quick-start guide
└── .gitignore                         # Hardened Git exclusions
```

---

## Backend Clean Architecture (`src/`)

### 1. `ShareSync.Domain` (Enterprise Business Rules)
- **`Entities/`**:
  - Core Entities: `AppUser.cs`, `Sector.cs`, `Company.cs`, `Portfolio.cs`, `Transaction.cs`
  - Financial Operations: `Dividend.cs`, `TransactionAudit.cs`, `PortfolioSnapshot.cs`, `PortfolioGoal.cs`
  - Market & Macro Data: `CompanyPriceHistory.cs`, `BenchmarkPriceHistory.cs`, `InflationRate.cs`, `CurrencyExchangeRate.cs`
  - Watchlists & Alerts: `Watchlist.cs`, `WatchlistItem.cs`, `Alert.cs`, `Notification.cs`
  - Database Views: `PortfolioHoldingView.cs` (maps MySQL view `vw_portfolio_holdings`)

### 2. `ShareSync.Application` (Application Business Rules)
- **`Common/`**:
  - `Exceptions/`: Standard domain and HTTP exceptions (`AppException.cs`, `NotFoundException.cs`, etc.)
  - `Helpers/`: `HistoricalReconciliationEngine.cs`, `MarketDataPathResolver.cs`
  - `Interfaces/`: `IApplicationDbContext.cs`, `ICurrentUserService.cs`, `IJwtTokenGenerator.cs`, `IPasswordHasher.cs`, `IMfaService.cs`
  - `Models/`: Standard API response envelopes (`ApiResponse.cs`, `PaginatedList.cs`)
- **`Interfaces/`**: 21 contracts defining use-case operations (`IPortfolioService`, `ITransactionService`, `IInvestmentIntelligenceService`, `IBenchmarkService`, etc.)
- **`Services/`**: Complete business implementations with validation and mathematical accounting.
- **`DTOs/`**: Grouped by feature domain (Auth, Portfolios, Transactions, Reports, Benchmarks, Analytics, etc.).

### 3. `ShareSync.Infrastructure` (Data & External Services)
- **`Data/`**: `ShareSyncDbContext.cs` (EF Core mapping for MySQL 8.x using Pomelo driver), `DbInitializer.cs`
- **`Security/`**: `PasswordHasher.cs` (PBKDF2 SHA256), `JwtTokenGenerator.cs`, `TotpMfaService.cs`
- **`Services/`**: `DsePriceService.cs` (resilient multi-tier live quote retrieval with circuit breaking and fallback)
- **`DependencyInjection.cs`**: Extension method registering infrastructure services.

### 4. `ShareSync.Web` (Presentation & Web API)
- **`Controllers/`**: 23 RESTful API controllers with JWT authorization and role-based policies.
- **`Middleware/`**: `ExceptionHandlingMiddleware.cs` (global error formatting).
- **`Services/`**: `CurrentUserService.cs`, `DsePriceBackgroundService.cs` (hosted background market sync).
- **`Program.cs`**: ASP.NET Core application entry point, DI container setup, CORS, and static file middleware.

### 5. `ShareSync.DataImporter` (Historical Batch Processing)
- High-performance batch console importer that parses raw DSE CSV datasets, applies OHLC geometry and deduplication filters, and idempotently stages market history.

---

## Frontend Web Application (`frontend/`)

- **Pages (`*.html`)**:
  - `index.html`: Public landing page and marketing overview
  - `login.html` & `register.html`: User onboarding and authentication
  - `portfolio.html`: Multi-portfolio management, allocation, and net holdings
  - `transactions.html`: Chronological transaction ledger with multi-asset and broker fee inputs
  - `reports.html`: PDF and Excel exportable valuation, cash-flow, and dividend reports
  - `analytics.html`: Real-time sector exposure and database-backed benchmark comparisons (LF-30)
  - `investment-intelligence.html`: Quantitative statistical intelligence engine (LF-31)
  - `company.html`: Real-time stock detail, candlestick views, and historical charts
  - `watchlist.html`: Investor watchlists with target price alerts
  - `dividends.html`: Corporate action tracking and dividend yield timeline
  - `alerts.html`: Price ceiling, floor, and portfolio milestone alert management
  - `goals.html`: Financial target setting and progress tracking
  - `activity.html`: Real-time timeline of user actions and audit trail
  - `simulator.html`: Trade simulation and hypothetical return calculator
  - `settings.html`: Account profile, password change, and MFA configuration
  - `admin.html`: System administration portal (user management, diagnostics, health metrics)
- **Assets**:
  - `css/style.css`: Clean responsive design system with dark/light mode tokens
  - `js/app.js`: Core client-side API integrations, state management, and Chart.js visualizations
  - `js/investment-intelligence.js`: Specialized statistical radar and metric calculations

---

## Database Management Kit (`database/mysql/`)

| Script | Purpose |
|---|---|
| `setup.sql` | Master deployment script executing full sequential initialization |
| `reset_demo_data.sql` | Atomic reset script restoring pristine evaluation baseline without dropping schema |
| `01_schema.sql` | DDL creating 14 3NF tables, indexes, constraints, and `vw_portfolio_holdings` |
| `02_seed.sql` | Comprehensive evaluation dataset (users, multi-asset portfolios, transactions, dividends) |
| `03_demo_validation.sql` | Automated data integrity audit testing 17 entity populations and 13 constraints |
| `03_queries.sql` | Demonstration queries for viva evaluations (multi-table JOINs, CTEs, Windows) |
| `04_functions.sql` | Stored functions (`fn_get_weighted_avg_price`, `fn_calculate_unrealized_pl`) |
| `05_procedures.sql` | Stored procedures (`sp_record_transaction`, `sp_generate_portfolio_snapshot`) |
| `06_triggers.sql` | Audit triggers capturing INSERT, UPDATE, and DELETE actions into `transaction_audit` |
| `07_tests.sql` | End-to-end SQL verification script confirming routine and trigger functionality |
| `README.md` | Comprehensive database setup and administration instructions |

---

## Automated Test Suite (`tests/ShareSync.Tests/`)

The test suite contains **490 automated tests** covering:
- **Unit & Domain Tests**: Validation, entity constraints, password hashing, JWT generation
- **Application & Service Tests**: CRUD operations, portfolio management, watchlist handling, alerts
- **Mathematical & Financial Accuracy**: Volume-weighted average price, realized P/L, broker fee deduction, net cash flow
- **Macro & Analytical Tests**: Inflation-adjusted returns, database benchmark tracking, Investment Intelligence metrics
- **Data Integrity Tests**: Deduplication assertions, cross-source price reconciliation, zero-volume quotation handling
