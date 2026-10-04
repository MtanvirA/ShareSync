# 📈 ShareSync: Share Market Portfolio Tracker

ShareSync is an enterprise-grade full-stack web application designed for investors and portfolio managers to track share-market portfolios, multi-asset transactions, watchlists, corporate dividend distributions, and quantitative portfolio performance from an interactive, unified dashboard.

Developed as a semester project for **CCE-224 Database Systems Sessional**, ShareSync combines an **Oracle Database** backend (with PL/SQL procedures, functions, triggers, and views) with an **ASP.NET Core 8 Web API** and a responsive **HTML5/Bootstrap 5/Chart.js/Vanilla JavaScript** frontend.

---

## 🚀 Key Architectural Features

- **Multi-Portfolio Management**: Create, update, track, and analyze isolated portfolios with real-time valuation and ROI.
- **Trade Execution & Holdings Engine**: BUY and SELL transaction processing with weighted average cost basis calculation, realized gain/loss tracking, and strict oversell prevention.
- **ACID Transaction Safeguards**: Database-level check constraints and transaction rollbacks to guarantee relational integrity.
- **Audit Logging**: Autonomous Oracle trigger audit trail capturing inserts, updates, and deletes in `TRANSACTION_AUDIT`.
- **Target Watchlists**: Composite primary key watchlist management with target price proximity calculation and alerts.
- **Corporate Dividends**: Tracking dividend declarations, payment schedules, and yield attribution per portfolio.
- **Valuation Snapshots & Charting**: Historical portfolio equity snapshots feeding dynamic Chart.js time-series visualizations.
- **Six Academic Reports**:
  1. *Portfolio Holdings Report*
  2. *Portfolio Performance & Profit/Loss Report*
  3. *Transaction History Report*
  4. *Company & Sector Investment Allocation Report*
  5. *Dividend Income Report*
  6. *Watchlist Target Price Proximity Report*
- **Live Market Synchronization**: Optional background synchronization with Dhaka Stock Exchange (DSE) via StockChartBD quote API.

---

## 🛠️ Technology Stack

| Layer | Technology |
|---|---|
| **Database** | Oracle Database 23ai / 21c (PDB: `FREEPDB1`), PL/SQL Views, Stored Procedures, Functions, Triggers |
| **Data Access** | Entity Framework Core 8 (`Oracle.EntityFrameworkCore`), ADO.NET Relational Commands |
| **Backend API** | ASP.NET Core 8 Web API, C# 12, Dependency Injection, RESTful Architecture |
| **Security** | JWT (JSON Web Tokens), PBKDF2 Password Hashing (100k iterations, SHA-256, 128-bit salt) |
| **Frontend** | HTML5, CSS3 Modern Glassmorphism, Bootstrap 5.3, Bootstrap Icons, Chart.js 4.4, Vanilla JavaScript (ES6+) |
| **Testing** | xUnit, Moq, FluentAssertions, PowerShell Automated Integration & Security Penetration Suites |

---

## 🗄️ Database Architecture & Schema

ShareSync models the domain with 10 tables, relational foreign keys, CHECK constraints, and optimized performance indexes:

```text
APP_USERS (User Accounts & Authentication)
   │
   ├── PORTFOLIOS (User Portfolios)
   │       │
   │       ├── TRANSACTIONS (BUY/SELL Records)
   │       │       │
   │       │       └── COMPANIES (Listed Equities)
   │       │               │
   │       │               ├── SECTORS (Market Classification)
   │       │               └── DIVIDENDS (Corporate Distributions)
   │       │
   │       └── PORTFOLIO_SNAPSHOTS (Valuation History)
   │
   └── WATCHLISTS (User Watchlists)
           │
           └── WATCHLIST_ITEMS (Composite PK: WatchlistId + CompanyId)
                   │
                   └── COMPANIES
```

### Relational Constraints Enforced
- **Primary Keys**: Auto-generated sequence identities for all primary entities.
- **Composite Primary Key**: `WATCHLIST_ITEMS (WATCHLIST_ID, COMPANY_ID)` preventing duplicate entries.
- **Foreign Keys**: `ON DELETE CASCADE` on child items, `RESTRICT` on companies with active trades.
- **CHECK Constraints**:
  - `CK_TRANSACTIONS_TYPE`: `TRANSACTION_TYPE IN ('BUY', 'SELL')`
  - `CK_TRANSACTIONS_QUANTITY`: `QUANTITY > 0`
  - `CK_TRANSACTIONS_PRICE`: `PRICE_PER_SHARE > 0`
  - `CK_DIVIDENDS_AMOUNT`: `DIVIDEND_PER_SHARE >= 0`
  - `CK_DIVIDENDS_DATES`: `PAYMENT_DATE >= DECLARATION_DATE`
  - `CK_COMPANIES_PRICE`: `CURRENT_PRICE >= 0`
  - `CK_PORTFOLIO_SNAPSHOT_VALUE`: `TOTAL_VALUE >= 0`
  - `CK_WATCHLIST_ITEMS_TARGET`: `TARGET_PRICE IS NULL OR TARGET_PRICE > 0`

---

## 📋 Prerequisites

Before running the application, ensure the following are installed:

1. **.NET 8.0 SDK** ([Download .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0))
2. **Oracle Database 23ai / 21c XE / Free** with a pluggable database (e.g., `FREEPDB1` or `XEPDB1`)
3. **Oracle SQL*Plus** or **Oracle SQL Developer**
4. Modern Web Browser (Chrome, Edge, Firefox)
5. PowerShell 7+ or Windows PowerShell (for running automated test suites)

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

### 2. Deploy Schema, Seed Data & PL/SQL Objects
Connect as `sharesync` user and execute the database scripts:

```bash
# Windows Command Prompt / PowerShell
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/schema.sql
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/seed_data.sql
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/views/01_vw_portfolio_holdings.sql
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/procedures/01_sp_record_transaction.sql
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/procedures/02_sp_generate_portfolio_snapshot.sql
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/functions/01_fn_calculate_unrealized_pl.sql
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/functions/02_fn_get_weighted_avg_price.sql
sqlplus sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @database/oracle/triggers/01_trg_transactions_audit.sql
```

*(Note: When the backend starts up, `DbInitializer` also automatically verifies table creation and constraints).*

---

## 🚀 Running the Application

### 1. Configure Connection String
Verify `src/ShareSync.Web/appsettings.json` has your Oracle credentials:

```json
{
  "ConnectionStrings": {
    "OracleConnection": "Data Source=localhost:1521/FREEPDB1;User Id=sharesync;Password=YourPasswordHere;"
  },
  "Jwt": {
    "Secret": "YourSuperSecretKeyForShareSyncAcademicProjectSecurity2026#",
    "Issuer": "ShareSyncServer",
    "Audience": "ShareSyncClient",
    "ExpiryDays": 7
  }
}
```

### 2. Launch Backend & Frontend Server
Run the project via the .NET CLI:

```bash
dotnet run --project src/ShareSync.Web/ShareSync.Web.csproj --urls http://localhost:5000
```

Alternatively, use the convenience batch runner on Windows:

```cmd
ShareSync.bat
```

Open your browser to:
👉 **`http://localhost:5000`**

### Default Academic Demo Accounts:
- **Email:** `demo@sharesync.com` | **Password:** `Password123#`
- **Email:** `investor@sharesync.com` | **Password:** `Password123#`

---

## 🧪 Running Automated Tests & SQA Suites

### 1. Unit Tests (xUnit)
Run all 136 backend unit tests covering domain calculation, validation, DTO mapping, and authentication logic:

```bash
dotnet test
```

### 2. Database Constraint & PL/SQL Object Test Suite
Verify Oracle primary keys, composite keys, foreign keys, CHECK constraints, ACID rollback, stored procedures, functions, triggers, and views directly in Oracle:

```bash
sqlplus -s sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @scratch/test_db_objects_and_constraints.sql
```

### 3. Comprehensive Integration & Security Test Suite
Executes 45 automated API tests verifying user registration, JWT authentication, User A vs User B authorization isolation, BUY/SELL trades, oversell protection, watchlists, dividends, snapshots, all 6 reports, and SQL injection penetration:

```powershell
powershell -ExecutionPolicy Bypass -File scratch/sqa_comprehensive_suite.ps1
```

### 4. End-to-End User Workflow Test Suite
Executes realistic user journeys 1 through 5, verifying state persistence and dashboard analytics:

```powershell
powershell -ExecutionPolicy Bypass -File scratch/sqa_e2e_workflows.ps1
```

### 5. Performance & EXPLAIN PLAN Audit
Executes Oracle `EXPLAIN PLAN` checks on queries to verify index scans and cost metrics:

```bash
sqlplus -s sharesync/ShareSync2026#@localhost:1521/FREEPDB1 @scratch/test_explain_plan.sql
```

---

## 🔒 Security & Data Integrity Highlights

- **Anti-SQL Injection**: 100% parameterized queries via Entity Framework Core and OracleCommand parameters. Malicious inputs containing quotes, semicolons, stacked queries (`'; DROP TABLE`), and UNION statements are treated strictly as string literals.
- **Zero Secret Exposure**: Passwords are never returned in DTOs or logged. Exception middleware masks all internal database details, returning sanitized client error messages.
- **User Isolation**: Every mutation and query validates ownership against the authenticated JWT user claim, returning HTTP 403 Forbidden on cross-tenant access attempts.
- **Oversell Prevention**: Multi-layer holding checks in the application layer, stored procedures, and check constraints prevent selling shares not held.

---

## 📁 Repository Structure

```text
ShareSync/
├── database/
│   └── oracle/
│       ├── schema.sql                         # Full DDL schema
│       ├── seed_data.sql                      # Initial seed equities & sectors
│       ├── views/                             # Analytical views
│       ├── procedures/                        # Stored procedures
│       ├── functions/                         # PL/SQL calculation functions
│       ├── triggers/                          # Audit logging triggers
│       ├── queries/                           # Academic report queries
│       └── reset/                             # Controlled reset script
├── src/
│   ├── ShareSync.Core/                        # Domain entities & business contracts
│   ├── ShareSync.Application/                 # Business logic, services & DTOs
│   ├── ShareSync.Infrastructure/              # EF Core context, Oracle mapping, security
│   └── ShareSync.Web/                         # ASP.NET Core controllers, middleware & UI assets
│       └── wwwroot/                           # Single-page frontend (HTML, CSS, JS, Chart.js)
├── tests/
│   └── ShareSync.Tests/                       # Automated xUnit test suite
├── scratch/                                   # SQA verification & audit scripts
└── README.md
```

---

## 👥 Academic Project Information

- **Course:** CCE-224: Database Systems Sessional
- **Project Title:** ShareSync - Share Market Portfolio Tracker
- **Academic Year:** 2026