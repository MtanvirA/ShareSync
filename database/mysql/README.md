# ShareSync — MySQL Database System Kit

This directory provides the authoritative **MySQL 8.x** database implementation for the **ShareSync Portfolio Tracker** application.

---

## Technology Stack

| Layer | Technology |
|---|---|
| **Database** | MySQL 8.x (InnoDB, utf8mb4) |
| **ORM / Data Access** | Entity Framework Core 8.x (`Pomelo.EntityFrameworkCore.MySql`) |
| **Backend** | ASP.NET Core 8.x / C# |
| **Frontend** | HTML5, CSS3, Bootstrap 5, JavaScript (Vanilla ES6+), Chart.js |

---

## Directory Structure

```
database/mysql/
├── setup.sql              # Master automated deployment script
├── 01_schema.sql          # 14 application tables, indexes, constraints & holdings view
├── 02_seed.sql            # Full demonstration dataset across all 14 tables
├── 03_queries.sql         # Interactive viva demonstration queries (JOINs, CTEs, Windows)
├── 04_functions.sql       # Stored functions (fn_get_weighted_avg_price, fn_calculate_unrealized_pl)
├── 05_procedures.sql      # Stored procedures (sp_record_transaction, sp_generate_portfolio_snapshot)
├── 06_triggers.sql        # Audit triggers (INSERT, UPDATE, DELETE auditing)
└── 07_tests.sql           # Automated end-to-end verification script
```

---

## Quick Setup Guide

### 1. Prerequisites
- **MySQL Server 8.0+** running locally on port `3306`.
- **.NET 8.0 SDK** (or .NET 10 SDK with roll-forward enabled).

### 2. Deploy Schema & Data in MySQL

Using MySQL command line client or PowerShell:
```powershell
# Run the master setup script:
Get-Content database\mysql\setup.sql -Raw | mysql -u root -p
```

Or execute sequentially:
```powershell
Get-Content database\mysql\01_schema.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\04_functions.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\05_procedures.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\06_triggers.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\02_seed.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\07_tests.sql -Raw | mysql -u root -p sharesync
```

### 3. Configure Connection String

In `src/ShareSync.Web/appsettings.json` (or via environment variable):
```json
"ConnectionStrings": {
  "MySqlConnection": "Server=localhost;Port=3306;Database=sharesync;User=root;Password=YOUR_PASSWORD;CharSet=utf8mb4;"
}
```

Or set in PowerShell:
```powershell
$env:ConnectionStrings__MySqlConnection = "Server=localhost;Port=3306;Database=sharesync;User=root;Password=YOUR_PASSWORD;CharSet=utf8mb4;"
```

### 4. Run the Application
```powershell
dotnet run --project src/ShareSync.Web
```
Open your browser at `http://localhost:5000`.

---

## Default Demo Credentials

| Role | Email | Password |
|---|---|---|
| **Investor** | `investor@sharesync.com` | `Password123#` |
| **Primary User** | `tanvir@sharesync.com` | `Password123#` |
| **Administrator** | `admin@sharesync.com` | `Admin123#` |

---

## Database Programming Demonstrations

1. **Portfolio Holdings View (`vw_portfolio_holdings`)**:
   Computes current share counts, market value, weighted average buy cost, and unrealized profit/loss across all portfolios dynamically.
2. **Weighted Average Cost Function (`fn_get_weighted_avg_price`)**:
   Deterministic function calculating volume-weighted average purchase price.
3. **Unrealized P/L Function (`fn_calculate_unrealized_pl`)**:
   Deterministic function returning mark-to-market gain/loss.
4. **Transaction Procedure (`sp_record_transaction`)**:
   Validates inputs, performs oversell checks for SELL orders, executes atomic insert, and outputs generated ID and status message.
5. **Snapshot Procedure (`sp_generate_portfolio_snapshot`)**:
   Calculates portfolio valuation and performs `ON DUPLICATE KEY UPDATE` upsert into `portfolio_snapshots`.
6. **Audit Triggers (`trg_transactions_after_*`)**:
   Audits all INSERT, UPDATE, and DELETE actions on `transactions` into `transaction_audit`. On DELETE, historical audit rows are preserved with `transaction_id = NULL` and full metadata.
