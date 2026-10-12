# ShareSync — Share Market Portfolio Tracker (MySQL Version)

ShareSync is a full-featured, academic web-based portfolio tracking and financial analysis platform built using **ASP.NET Core (C#)**, **MySQL 8.x**, **Entity Framework Core**, and a vanilla **HTML5 / CSS3 / Bootstrap 5 / JavaScript / Chart.js** frontend.

---

## Technical Profile

| Component | Technology | Description |
|---|---|---|
| **Project** | ShareSync | Share Market Portfolio Tracker |
| **Database** | MySQL 8.x | Normalized relational database using InnoDB storage engine (`utf8mb4`) |
| **Backend** | ASP.NET Core Web API (C#) | Clean Architecture Web API targeting .NET 8 / .NET 10 |
| **ORM** | Entity Framework Core | `Pomelo.EntityFrameworkCore.MySql` (v8.*) |
| **Frontend** | Pure Web Standards | HTML5, CSS3, Bootstrap 5, JavaScript (ES6+), Chart.js |
| **Security** | JWT & PBKDF2 | JWT Bearer token authorization with salted PBKDF2 password hashing |

---

## Key Features

1. **Authentication & Multi-Role Access:** Secure investor and administrator login with JWT tokens and salted PBKDF2 password hashes.
2. **Multi-Portfolio Management:** Track multiple independent portfolios per investor with custom allocations and descriptions.
3. **Transactional Financial Ledger:** Full BUY and SELL transaction recording with broker fees, dates, and strict oversell validation.
4. **Holdings & Valuation Engine:** Dynamic computation of volume-weighted average purchase price, market value, and unrealized profit/loss via MySQL database views and stored routines.
5. **Investor Watchlists:** Customizable watchlists with target price variance tracking.
6. **Corporate Action Dividends:** Record and aggregate dividend distributions, historical dividend yield, and payout schedules.
7. **Comprehensive Reporting:** Dedicated reports for portfolio holdings valuation, transaction ledgers, company/sector allocation, and dividend income.
8. **Real-Time Analytics:** Sector exposure, portfolio performance history, and asset allocation breakdown.
9. **Investment Intelligence Engine:** Retrospective quantitative analysis calculating CAGR, Annualized Volatility, Maximum Drawdown, Positive-Day Consistency, Trend classification, and multi-factor scores.
10. **Dhaka Stock Exchange (DSE) Catalog:** Autocomplete search and market tracking across 400+ DSE listed companies.

---

## Documentation Quick Links

- [Codebase Navigation & Feature Tags](docs/CODEBASE_NAVIGATION.md) *(Essential for live demonstration & viva)*
- [Database Objects Viva & Demo Map](docs/DATABASE_VIVA_MAP.md)
- [Teacher Demonstration Runbook](docs/TEACHER_DEMO_RUNBOOK.md)
- [Teacher Viva Cheat Sheet](docs/TEACHER_VIVA_CHEAT_SHEET.md)
- [MySQL Setup Guide](docs/SETUP.md)
- [Database Schema & Programmability](docs/DATABASE.md)
- [System Architecture](docs/ARCHITECTURE.md)
- [Project Structure & Organization](docs/PROJECT_STRUCTURE.md)
- [Automated Testing](docs/TESTING.md)
- [MySQL Kit & SQL Scripts](database/mysql/README.md)
- [Dataset Details](docs/DATASET.md)
- [Security Guidelines](docs/SECURITY.md)

---

## Live Demonstration Feature Search (`Ctrl + Shift + F`)

Every major module in the repository includes standardized tags across C# XML documentation, frontend JavaScript, and MySQL SQL scripts. Evaluators can instantly press **`Ctrl + Shift + F`** in VS Code to locate any feature implementation:

| Standard Feature Tag | Scope |
|---|---|
| `FEATURE: Authentication and Authorization` | JWT login, registration, PBKDF2 hashing, multi-role security |
| `FEATURE: Company Catalog and Search` | 400+ DSE company listings, sector filtering, ticker search |
| `FEATURE: Portfolio Management` | Multi-portfolio isolation, cash balances, asset allocation |
| `FEATURE: Portfolio Valuation` | Dynamic valuation, daily snapshots, VWAP accounting |
| `FEATURE: Transactions` | Atomic BUY/SELL execution, fee deduction, oversell prevention |
| `FEATURE: Realized and Unrealized P/L` | Mark-to-market calculations, stored functions, gain/loss ledger |
| `FEATURE: Dividend Management` | Corporate cash distributions, historical dividend yield |
| `FEATURE: DSE Market Data` | Real-time quote retrieval, StockChartBD integration, rate limiting |
| `FEATURE: Historical Price Data` | Harvard Dataverse DSE 1999–2025 dataset, daily OHLCV series |
| `FEATURE: Benchmark Analysis` | DSEX, DS30, and DSES index tracking and portfolio active returns |
| `FEATURE: Portfolio Analytics` | Sector exposure, performance breakdown, asset concentration |
| `FEATURE: Investment Intelligence` | Retrospective CAGR, Annualized Volatility, Max Drawdown, scoring |
| `FEATURE: Inflation and Real Return` | Bangladesh Bank / BBS CPI indices, Fisher Equation real returns |
| `FEATURE: Currency Conversion` | Multi-currency FX conversion rates (BDT, USD, EUR, GBP) |
| `FEATURE: Portfolio Goals` | Target milestone tracking and real-time progress calculations |
| `FEATURE: Watchlists` | Custom investor watchlists with target price alerts |
| `FEATURE: Alerts and Notifications` | Real-time price ceiling/floor alerts and user notification feed |
| `FEATURE: Reports and Exports` | PDF/Excel/CSV exportable financial reports and audit ledgers |
| `FEATURE: Simulator` | Hypothetical execution sandbox and return simulator |
| `FEATURE: CSV Import` | Bulk transaction upload with format validation |
| `FEATURE: MySQL Database and Data Integrity` | 18 3NF tables, constraints, foreign keys, triggers, stored routines |
| `FEATURE: Historical Reconciliation` | OHLC geometry validation, deduplication, quarantine engine |
| `FEATURE: Learning Center` | Interactive financial literacy, DSE market mechanics hub |

*For complete file-by-file symbol mapping, see [docs/CODEBASE_NAVIGATION.md](docs/CODEBASE_NAVIGATION.md).*

---

## Database Architecture

The MySQL database schema (`sharesync`) consists of **18 normalized relational tables** engineered in Third Normal Form (3NF):

```
sharesync
├── app_users                  # Investor and administrator credentials & roles
├── sectors                    # Industry classifications
├── companies                  # Stock listings & current market prices (multi-asset, multi-currency)
├── portfolios                 # Investor wealth portfolios (multi-currency base)
├── transactions               # Immutable BUY and SELL order records with fees
├── transaction_audit          # Automated audit trail captured via database triggers
├── dividends                  # Cash dividend declarations & corporate payouts
├── watchlists                 # Investor watchlists
├── watchlist_items            # Composite-keyed bridge table for watchlist tracking
├── portfolio_snapshots        # Historical mark-to-market valuation snapshots
├── company_price_history      # Historical OHLCV market observations
├── benchmark_price_history    # Market indices (DSEX, DS30, DSES) price history (LF-30)
├── alerts                     # Price and portfolio value threshold alerts
├── notifications              # User notifications center
├── portfolio_goals            # Financial goals & progress tracking
├── inflation_rates            # Annual consumer price index inflation rates (LF-28)
├── cpi_index_observations     # Monthly official CPI index series for inflation (LF-28)
└── currency_exchange_rates    # Foreign currency exchange rates for FX valuation (LF-29)
```

### Programmability Highlights
- **View (`vw_portfolio_holdings`):** Calculates net shares, volume-weighted average cost, market value, and unrealized P/L on demand.
- **Function (`fn_get_weighted_avg_price`):** Calculates volume-weighted average purchase price for any portfolio and company.
- **Function (`fn_calculate_unrealized_pl`):** Computes mark-to-market unrealized gain or loss.
- **Function (`fn_calculate_realized_pl`):** Computes realized profit or loss from liquidated shares.
- **Function (`fn_calculate_net_cash_flow`):** Computes cumulative net capital cash flow.
- **Function (`fn_calculate_real_return`):** Computes purchasing-power real return via Fisher Equation.
- **Procedure (`sp_record_transaction`):** Validates transaction parameters, checks for oversell violations on SELL orders, and inserts atomically.
- **Procedure (`sp_generate_portfolio_snapshot`):** Aggregates net holdings valuation and upserts daily snapshots.
- **Triggers (`trg_transactions_after_*`):** Transparently writes full before/after audit entries to `transaction_audit`.

---

## Quick Start & Setup Guide

### 1. Prerequisites
- **MySQL Server 8.0+** running locally on port `3306`
- **.NET 8.0 SDK** (or .NET 10 SDK with `DOTNET_ROLL_FORWARD=Major`)

### 2. Deploy MySQL Database
From the project root:
```powershell
# Deploy complete schema, programmability, seed data, and tests:
Get-Content database\mysql\setup.sql -Raw | mysql -u root -p
```

### 3. Configure Connection String
Set your password in `src/ShareSync.Web/appsettings.json` or export via environment variable:
```powershell
$env:ConnectionStrings__MySqlConnection = "Server=localhost;Port=3306;Database=sharesync;User=root;Password=YOUR_PASSWORD;CharSet=utf8mb4;"
```

### 4. Run the Application
```powershell
dotnet build
dotnet run --project src/ShareSync.Web
```

Open your browser to: **`http://localhost:5000`**

### Demo Login Accounts
| Role | Email | Password |
|---|---|---|
| **Investor** | `investor@sharesync.com` | `Password123#` |
| **Investor** | `tanvir@sharesync.com` | `Password123#` |
| **Administrator** | `admin@sharesync.com` | `Admin123#` |

---

## Automated Testing

Run the full automated test suite containing **493 tests**:
```powershell
dotnet test
```

Execute direct database verification in MySQL:
```powershell
Get-Content database\mysql\07_tests.sql -Raw | mysql -u root -p sharesync
```

---

## Project Structure

```
ShareSync/
├── database/
│   └── mysql/                  # Complete MySQL SQL scripts (01 to 07, setup.sql, README)
├── docs/                       # Project documentation, guides, viva cheat sheet
├── frontend/                   # HTML5, Bootstrap 5, CSS, and JavaScript single-page views
├── src/
│   ├── ShareSync.Domain/       # Domain entities and core models
│   ├── ShareSync.Application/  # Business logic, interfaces, services, DTOs
│   ├── ShareSync.Infrastructure/# Pomelo MySQL EF Core provider, DbContext, auth
│   ├── ShareSync.Web/          # ASP.NET Core Web API controllers & static host
│   └── ShareSync.DataImporter/ # Console utility for historical dataset ingestion
└── tests/
    └── ShareSync.Tests/        # 493 unit and integration tests
```

---

## Academic Disclaimer
**Dataset Source:** Dhaka Stock Exchange Historical Data (1999-2025), Harvard Dataverse (`DOI: 10.7910/DVN/XIFYT1`).  
**Notice:** ShareSync is an academic software project. The Investment Intelligence and valuation modules compute retrospective statistical metrics and do NOT provide financial advice, price forecasting, or investment recommendations.