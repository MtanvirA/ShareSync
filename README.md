# 📈 ShareSync: Share Market Portfolio Tracker

ShareSync is an enterprise-grade full-stack web application designed for investors and portfolio managers to track share-market portfolios, multi-asset transactions, watchlists, corporate dividend distributions, and quantitative portfolio performance from an interactive, unified dashboard.

Developed as a semester project for **CCE-224 Database Systems Sessional**, ShareSync combines an **Oracle Database** backend (with PL/SQL procedures, functions, triggers, and views) with an **ASP.NET Core 8 Web API** and a responsive **HTML5 / CSS3 Glassmorphism / Chart.js / Vanilla JavaScript** frontend.

---

## 🚀 Key Architectural Features

- **Multi-Portfolio Management**: Create, update, track, and analyze isolated portfolios with real-time valuation and ROI.
- **Trade Execution & Holdings Engine**: BUY and SELL transaction processing with weighted-average cost basis calculation, realized gain/loss tracking, and strict oversell prevention.
- **ACID Transaction Safeguards**: Atomic application and database transaction boundaries (`BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`, and savepoint isolation in stored procedures) to guarantee relational integrity.
- **Comprehensive Audit Logging**: Bi-directional audit trail capturing inserts, updates, and deletes in `TRANSACTION_AUDIT` via Oracle triggers and application services with historical record preservation (`ON DELETE SET NULL`).
- **Target Watchlists**: Composite primary key watchlist management (`WATCHLIST_ID`, `COMPANY_ID`) with target price proximity calculation and alerts.
- **Corporate Dividends**: Tracking dividend declarations, payment schedules, and yield attribution per portfolio.
- **Valuation Snapshots & Charting**: Historical portfolio equity snapshots feeding dynamic Chart.js time-series visualizations.
- **Historical Market Price Tracking**: Continuous capture and storage of equity market prices over time with multi-period trend visualizations (1D, 1W, 1M, 3M, 6M, 1Y, ALL) and duplicate snapshot suppression.
- **Company Detail Pages**: Comprehensive company profiles (`/company.html?id={companyId}`) showcasing real-time pricing, sector classification, market cap, interactive time-series historical price charts, authenticated user position breakdowns (shares held, average cost, cost basis, current market value, unrealized P/L), corporate dividend distribution history with estimated user income, and interactive watchlist target price management.
- **Price and Portfolio Alerts (Feature 4)**: Real-time threshold monitoring and notification engine (`/alerts.html` and `/api/alerts`) supporting `PRICE_ABOVE`, `PRICE_BELOW`, `PORTFOLIO_VALUE_ABOVE`, and `PORTFOLIO_VALUE_BELOW` with single-trigger execution policy, repeated trigger prevention, and seamless DSE synchronization integration.
- **Six Academic Reports**:
  1. *Portfolio Holdings Report*
  2. *Portfolio Performance & Profit/Loss Report*
  3. *Transaction History Report*
  4. *Company & Sector Investment Allocation Report*
  5. *Dividend Income Report*
  6. *Watchlist Target Price Proximity Report*
- **Live Market Synchronization**: Resilient background synchronization with Dhaka Stock Exchange (DSE) via StockChartBD quote API with safe socket timeouts, failure isolation, concurrency protection, and historical price snapshot recording.

---

## 🛠️ Technology Stack

| Layer | Technology |
|---|---|
| **Database** | Oracle Database 23ai / 21c Free (PDB: `FREEPDB1`), PL/SQL Views, Stored Procedures, Functions, Triggers |
| **Data Access** | Entity Framework Core 8 (`Oracle.EntityFrameworkCore`), ADO.NET Relational Commands |
| **Backend API** | ASP.NET Core 8 Web API, C# 12, Thin Controllers, Domain Services, RESTful Architecture |
| **Security** | JWT (JSON Web Tokens), PBKDF2 Password Hashing (100k iterations, SHA-256, 128-bit salt), Fail-fast configuration |
| **Frontend** | HTML5, CSS3 Modern Glassmorphism (Light & Dark Theme), Bootstrap 5.3, Bootstrap Icons, Chart.js 4.4, Vanilla JavaScript (ES6+) |
| **Testing** | xUnit, Moq, FluentAssertions, PowerShell Automated Integration, Security Penetration & SQA Suites |

---

## 🗄️ Database Architecture & Core Tables

ShareSync models the domain with 12 relational tables, foreign keys, CHECK constraints, and reporting performance indexes:

```text
APP_USERS (User Accounts & Authentication)
   │
   ├── PORTFOLIOS (User Portfolios)
   │       │
   ├── TRANSACTIONS (BUY/SELL Records) ─────── TRANSACTION_AUDIT (Audit Trail)
   │       │       │
   │       │       └── COMPANIES (Listed Equities)
   │       │               │
   │       │               ├── SECTORS (Market Classification)
   │       │               ├── DIVIDENDS (Corporate Distributions)
   │       │               └── COMPANY_PRICE_HISTORY (Market Price Snapshots)
   │       │
   │       └── PORTFOLIO_SNAPSHOTS (Valuation History)
   │
   └── WATCHLISTS (User Watchlists)
           │
           └── WATCHLIST_ITEMS (Composite PK: WatchlistId + CompanyId)
                   │
                   └── COMPANIES
```

### Relational Tables
1. **`APP_USERS`**: User identity, roles (`INVESTOR`, `ADMIN`), and PBKDF2 password hashes.
2. **`SECTORS`**: Industrial sectors (e.g., Pharmaceuticals, Telecommunication, Financial Services).
3. **`COMPANIES`**: Equities traded on the exchange with ticker symbols, current market prices, and market caps.
4. **`PORTFOLIOS`**: User-owned investment portfolios with isolated tenancy.
5. **`WATCHLISTS`**: User-defined stock monitoring groups.
6. **`WATCHLIST_ITEMS`**: Composite PK join table (`WATCHLIST_ID`, `COMPANY_ID`) tracking target purchase prices.
7. **`TRANSACTIONS`**: Immutable order ledger capturing BUY and SELL orders, quantities, prices, and timestamps.
8. **`DIVIDENDS`**: Corporate dividend payouts per share, declaration dates, and payment dates.
9. **`TRANSACTION_AUDIT`**: Historical ledger tracking all `INSERT`, `UPDATE`, and `DELETE` actions on transactions (`TRANSACTION_ID` is nullable with `ON DELETE SET NULL` to preserve historical audit data permanently).
10. **`PORTFOLIO_SNAPSHOTS`**: Daily portfolio valuation snapshots for historical performance charting.
11. **`COMPANY_PRICE_HISTORY`**: Time-series historical equity market prices (`PRICE_HISTORY_ID`, `COMPANY_ID`, `PRICE`, `OPEN_PRICE`, `HIGH_PRICE`, `LOW_PRICE`, `VOLUME`, `RECORDED_AT`, `CREATED_AT`) with composite index `(COMPANY_ID, RECORDED_AT)` and CHECK constraints.
12. **`ALERTS`**: Normalized price and portfolio threshold monitors (`ALERT_ID`, `USER_ID`, `COMPANY_ID`, `PORTFOLIO_ID`, `ALERT_TYPE`, `THRESHOLD_VALUE`, `IS_ACTIVE`, `CREATED_AT`, `TRIGGERED_AT`, `MESSAGE`) with tenant isolation, CHECK constraints, and evaluation indexes (`IDX_ALERTS_USER`, `IDX_ALERTS_PRICE_EVAL`, `IDX_ALERTS_PORT_EVAL`).

### Programmable Database Objects
- **Views**:
  - `VW_PORTFOLIO_HOLDINGS`: Evaluates net held quantity, weighted average purchase price, current market value, and unrealized profit/loss.
- **Stored Procedures**:
  - `sp_record_transaction`: Performs atomic transaction validation, oversell rejection, and ledger entry with savepoint isolation.
  - `sp_generate_portfolio_snapshot`: Computes portfolio valuation from active holdings and upserts into `PORTFOLIO_SNAPSHOTS`.
- **User-Defined Functions**:
  - `fn_calculate_unrealized_pl(p_portfolio_id, p_company_id)`: Computes exact unrealized profit/loss based on weighted average cost.
  - `fn_get_weighted_avg_price(p_portfolio_id, p_company_id)`: Computes cumulative weighted average buy price.
- **Triggers**:
  - `trg_transactions_audit`: Fires `AFTER INSERT OR UPDATE OR DELETE ON transactions` to ensure zero database modifications bypass the audit trail.

---

## 📋 Prerequisites

Before running the application, ensure the following are installed:

1. **.NET 8.0 SDK** ([Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0))
2. **Oracle Database 23ai / 21c XE / Free** with a pluggable database (e.g., `FREEPDB1` or `XEPDB1`)
3. **Oracle SQL*Plus** or **Oracle SQL Developer**
4. Modern Web Browser (Chrome, Edge, Firefox)
5. PowerShell 7+ or Windows PowerShell

---

## ⚙️ Database Setup

### 1. Create Oracle User Schema
Open SQL*Plus as `SYSDBA`:

```sql
sqlplus sys/YourSysPassword@localhost:1521/FREEPDB1 as sysdba
```

Execute the user setup script:

```sql
CREATE USER sharesync IDENTIFIED BY ShareSync2026#
  DEFAULT TABLESPACE users
  TEMPORARY TABLESPACE temp
  QUOTA UNLIMITED ON users;

GRANT CONNECT, RESOURCE, CREATE VIEW, CREATE PROCEDURE, CREATE TRIGGER TO sharesync;
EXIT;
```

### 2. One-Step Master Database Deployment
Deploy all 10 schema tables, indexes, views, functions, procedures, triggers, and reference seed data in one single command:

```bash
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/deploy_all.sql
```

---

## 🔐 Configuration & Secrets Management

ShareSync provides an example configuration template in:
`src/ShareSync.Web/appsettings.example.json`

### Fail-Fast Production Behavior
In `Production` environments, the application **fails fast** on startup if required secrets are missing or set to placeholder defaults:
- `ConnectionStrings:OracleConnection` must be provided.
- `Jwt:Secret` must be provided and must be at least 32 characters long.
- `Cors:AllowedOrigins` enforces an explicit whitelist (wildcard `AllowAnyOrigin` is strictly prohibited).

### Setting Secrets in Development
For development, use .NET User Secrets or environment variables:

```bash
# Using .NET User Secrets
cd src/ShareSync.Web
dotnet user-secrets set "ConnectionStrings:OracleConnection" "Data Source=localhost:1521/FREEPDB1;User Id=sharesync;Password=YourPasswordHere;"
dotnet user-secrets set "Jwt:Secret" "YourStrongSecretKeyAtLeast32CharactersLong2026#"

# Or using Environment Variables
set ConnectionStrings__OracleConnection="Data Source=localhost:1521/FREEPDB1;User Id=sharesync;Password=YourPasswordHere;"
set Jwt__Secret="YourStrongSecretKeyAtLeast32CharactersLong2026#"
```

---

## 🚀 Running the Application

### 1. Start Backend API & Frontend Server
Run the project via the .NET CLI:

```bash
dotnet run --project src/ShareSync.Web/ShareSync.Web.csproj --urls http://localhost:5000
```

Alternatively, use the convenience batch runner on Windows:

```cmd
ShareSync.bat
```

### 2. Access the Application
Open your browser to:
👉 **`http://localhost:5000`**

The ASP.NET Core backend automatically serves the frontend static assets from `frontend/` at the root URL while routing API requests to `/api/*`. Swagger API documentation is available in development at `http://localhost:5000/swagger`.

### Default Seed Accounts
- **Email:** `tanvir@sharesync.com` | **Password:** `Password123#`

---

## 🧪 Testing & SQA Verification

### 1. Unit & Consistency Tests (xUnit)
Runs all 147 unit, price synchronization, duplicate prevention, company detail, and financial calculation consistency tests:

```bash
dotnet test tests/ShareSync.Tests/ShareSync.Tests.csproj
```

### 2. Advanced Portfolio Analytics Live Verification Suite
Executes live automated API tests for consolidated/single portfolio analytics, financial model consistency (unrealized P/L parity with `VW_PORTFOLIO_HOLDINGS`), realized P/L from closed SELL transactions, dividend yield attribution, Herfindahl-Hirschman Index (HHI) concentration scoring, historical performance curves across timeframes (`1M`, `3M`, `6M`, `1Y`, `ALL`), cross-user authorization isolation (403), empty portfolio handling, and frontend DOM integrity:

```powershell
powershell -ExecutionPolicy Bypass -File scratch/test_portfolio_analytics_feature.ps1
```

### 3. Company Detail Pages Live Verification Suite
Executes live automated API tests for company detail retrieval, user holdings & financial parity, corporate dividend history, watchlist management, target price editing, cross-user isolation, and error handling:

```powershell
powershell -ExecutionPolicy Bypass -File scratch/test_company_detail_feature.ps1
```

### 4. Historical Market Price Tracking Live Verification Suite
Executes live automated API tests for historical price retrieval across all timeframes (1D, 1W, 1M, 3M, 6M, 1Y, ALL), custom date ranges, authorization (401), invalid company (404), live DSE sync, and duplicate snapshot prevention policy:

```powershell
powershell -ExecutionPolicy Bypass -File scratch/test_price_history_feature.ps1
```

### 5. Comprehensive Integration & Security Test Suite
Executes 45 automated API tests verifying user registration, JWT authentication, User A vs User B authorization isolation, BUY/SELL trades, oversell protection, watchlists, dividends, snapshots, all 6 reports, and SQL injection penetration:

```powershell
powershell -ExecutionPolicy Bypass -File scratch/sqa_comprehensive_suite.ps1
```

### 6. Oracle Object Health Check
Verifies that all 11 tables, foreign keys, CHECK constraints, composite indexes (`IDX_PRICE_HIST_COMP_DATE`), views, procedures, functions, and triggers are compiled and valid in Oracle:

```bash
sqlplus -S sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @scratch/verify_price_history_db.sql
```

---

## 📁 Repository Structure

```text
ShareSync/
├── database/
│   └── oracle/
│       ├── deploy_all.sql                     # Master deployment script (runs in dependency order)
│       ├── schema/                            # 11 core table DDL scripts + reporting indexes
│       ├── views/                             # Analytical views (VW_PORTFOLIO_HOLDINGS)
│       ├── procedures/                        # Stored procedures (sp_record_transaction, sp_generate_portfolio_snapshot)
│       ├── functions/                         # PL/SQL calculation functions (unrealized P/L, weighted avg price)
│       ├── triggers/                          # Audit logging triggers (trg_transactions_audit)
│       ├── seed/                              # Initial reference & sample seed data
│       ├── queries/                           # Academic report queries
│       └── reset/                             # Controlled reset script
├── frontend/                                  # Vanilla JavaScript (ES6+), HTML5, CSS3 Glassmorphism UI
│   ├── index.html                             # Main Dashboard
│   ├── alerts.html                            # Price & Portfolio Alerts Management (Feature 4)
│   ├── analytics.html                         # Advanced Portfolio Analytics & Concentration (Feature 3)
│   ├── company.html                           # Company Detail Page & Price Chart (Feature 2)
│   ├── portfolio.html                         # Portfolio management & holdings
│   ├── transactions.html                      # Transaction ledger & BUY/SELL execution
│   ├── watchlist.html                         # Target price watchlists
│   ├── dividends.html                         # Dividend payouts & tracking
│   ├── reports.html                           # 6 Academic analytical reports
│   ├── settings.html                          # Profile & dark/light theme settings
│   ├── login.html & register.html             # User authentication
│   ├── css/                                   # Modern stylesheets (Light/Dark themes)
│   └── js/app.js                              # Unified clientside API & UI engine
├── src/
│   ├── ShareSync.Domain/                      # Core entities, enums & domain contracts
│   ├── ShareSync.Application/                 # Application services, business logic, DTOs & interfaces
│   ├── ShareSync.Infrastructure/              # EF Core context, Oracle mappings, password hashing & DSE price service
│   └── ShareSync.Web/                         # ASP.NET Core controllers, middleware, CORS & background workers
│       ├── appsettings.json                   # Base configuration
│       └── appsettings.example.json           # Sanitized configuration template
├── tests/
│   └── ShareSync.Tests/                       # Automated xUnit test suite (171 tests)
├── scratch/                                   # SQA integration, security & verification scripts
└── README.md
```

---

## 👥 Academic Project Information

- **Course:** CCE-224: Database Systems Sessional
- **Project Title:** ShareSync - Share Market Portfolio Tracker
- **Academic Year:** 2026