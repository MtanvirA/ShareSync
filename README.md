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

- [MySQL Setup Guide](docs/SETUP.md)
- [Teacher Demonstration Guide](docs/MYSQL_DEMO_GUIDE.md)
- [Database Systems Viva Cheat Sheet](docs/MYSQL_VIVA.md)
- [Database Schema & Programmability](docs/DATABASE.md)
- [System Architecture](docs/ARCHITECTURE.md)
- [Automated Testing](docs/TESTING.md)
- [MySQL Kit & SQL Scripts](database/mysql/README.md)
- [Dataset Details](docs/DATASET.md)
- [Security Guidelines](docs/SECURITY.md)

---

## Database Architecture

The MySQL database schema (`sharesync`) consists of **14 normalized relational tables** engineered in Third Normal Form (3NF):

```
sharesync
├── app_users                  # Investor and administrator credentials & roles
├── sectors                    # Industry classifications
├── companies                  # Stock listings & current market prices
├── portfolios                 # Investor wealth portfolios
├── transactions               # Immutable BUY and SELL order records
├── transaction_audit          # Automated audit trail captured via database triggers
├── dividends                  # Cash dividend declarations & corporate payouts
├── watchlists                 # Investor watchlists
├── watchlist_items            # Composite-keyed bridge table for watchlist tracking
├── portfolio_snapshots        # Historical mark-to-market valuation snapshots
├── company_price_history      # Historical OHLCV market observations
├── alerts                     # Price and portfolio value threshold alerts
├── notifications              # User notifications center
└── portfolio_goals            # Financial goals & progress tracking
```

### Programmability Highlights
- **View (`vw_portfolio_holdings`):** Calculates net shares, volume-weighted average cost, market value, and unrealized P/L on demand.
- **Function (`fn_get_weighted_avg_price`):** Calculates volume-weighted average purchase price for any portfolio and company.
- **Function (`fn_calculate_unrealized_pl`):** Computes mark-to-market unrealized gain or loss.
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

Run the full automated test suite containing **350 tests**:
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
    └── ShareSync.Tests/        # 350 unit and integration tests
```

---

## Academic Disclaimer
**Dataset Source:** Dhaka Stock Exchange Historical Data (1999-2025), Harvard Dataverse (`DOI: 10.7910/DVN/XIFYT1`).  
**Notice:** ShareSync is an academic software project. The Investment Intelligence and valuation modules compute retrospective statistical metrics and do NOT provide financial advice, price forecasting, or investment recommendations.