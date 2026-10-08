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
├── reset_demo_data.sql    # Clean reset script restoring demo evaluation baseline
├── 01_schema.sql          # 14 application tables, indexes, constraints & holdings view
├── 02_seed.sql            # Full demonstration dataset across all 14 tables
├── 03_demo_validation.sql # Demo dataset audit & referential integrity validation script
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
Get-Content database\mysql\03_demo_validation.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\07_tests.sql -Raw | mysql -u root -p sharesync
```

To reset the demonstration dataset at any time during teacher evaluations:
```powershell
Get-Content database\mysql\reset_demo_data.sql -Raw | mysql -u root -p sharesync
```

To audit and validate demo data integrity at any time:
```powershell
Get-Content database\mysql\03_demo_validation.sql -Raw | mysql -u root -p sharesync
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

| Role | Name | Email | Password | Purpose |
|---|---|---|---|---|
| **Primary Investor** | `Tanvir Anjum` | `investor@sharesync.com` | `Password123#` | Complete, active evaluation account |
| **Secondary User** | `Tanvir Anjum (Personal)` | `tanvir@sharesync.com` | `Password123#` | Personal companion portfolio |
| **Administrator** | `System Administrator` | `admin@sharesync.com` | `Admin123#` | Platform & user management |

---

## Realistic Investor Demonstration Structure

The primary demo account (`investor@sharesync.com`) is populated with 4 purpose-driven portfolios, 69 chronological transactions across 15 securities, 21 active positions, 11 dividends, 16 watchlist items, 8 alerts, 5 goals, 12 notifications, and 43 valuation snapshots.

### Demo Portfolios

| ID | Portfolio Name | Currency | Description | Active Positions |
|---|---|---|---|---|
| 1 | **Main Investment Portfolio** | BDT | Core diversified blue chips (GP, SQUAREPHARMA, BRACBANK, BATBC, MARICO, CASH-BDT) | 6 active holdings |
| 2 | **Long-Term Growth Portfolio** | BDT | High compound growth (RENATA, WALTONHIL, LHBL, BXPHARMA, AMCL(PRAN)) | 5 active holdings |
| 3 | **Dividend Income Portfolio** | BDT | High dividend yield cash cows (BATBC, GP, MARICO, SQUAREPHARMA, BRACBANK) | 5 active holdings |
| 4 | **Global & Multi-Asset Portfolio** | BDT / USD | Mixed asset allocation (DSE30ETF, GRAMEEN1MF, CASH-BDT, SPY [USD], CASH-USD [USD]) | 5 active holdings |

### Where to Evaluate Key Features

| Academic Feature | Navigation / UI Path | What to Inspect |
|---|---|---|
| **Realized P/L** | `transactions.html` & `reports.html` | Profitable SELLs (GP, BRACBANK, BATBC, LHBL) & loss-making SELLs (SQUAREPHARMA, BXPHARMA, RENATA). |
| **Unrealized P/L** | `portfolio.html` & `vw_portfolio_holdings` | Dynamic mark-to-market gain/loss vs volume-weighted average buy price. |
| **Fees & Cash Flow** | `transactions.html` & `reports.html` | Brokerage commissions (total fees ৳824) and net capital cash flow per portfolio. |
| **Multi-Asset / FX** | `portfolio.html` & `transactions.html` | Equities, ETFs, Mutual Funds, and USD assets (SPY, CASH-USD) auto-converted via `currency_exchange_rates`. |
| **Real Return (LF-28)** | `analytics.html` | Purchasing-power adjusted return via Fisher Equation using official BBS/BLS CPI inflation data. |
| **Benchmark (LF-30)** | `analytics.html` | Active return / outperformance vs real 5,952 database-backed DSEX, DS30, and DSES index records. |
| **Investment Intelligence** | `investment-intelligence.html` | 5-year quantitative metrics (CAGR, MDD, Volatility, SMA 50/200) on GP, SQUAREPHARMA, BATBC, etc. |

---

## Database Programming Demonstrations

1. **Portfolio Holdings View (`vw_portfolio_holdings`)**:
   Computes current share counts, market value, weighted average buy cost, and unrealized profit/loss across all portfolios dynamically.
2. **Weighted Average Cost Function (`fn_get_weighted_avg_price`)**:
   Deterministic function calculating volume-weighted average purchase price.
3. **Unrealized P/L Function (`fn_calculate_unrealized_pl`)**:
   Deterministic function returning mark-to-market gain/loss.
4. **Realized P/L Function (`fn_calculate_realized_pl`)**:
   Calculates realized capital gains/losses on all liquidated SELL quantities.
5. **Net Cash Flow Function (`fn_calculate_net_cash_flow`)**:
   Calculates net capital flows including trade fees.
6. **Transaction Procedure (`sp_record_transaction`)**:
   Validates inputs, performs oversell checks for SELL orders, executes atomic insert, and outputs generated ID and status message.
7. **Snapshot Procedure (`sp_generate_portfolio_snapshot`)**:
   Calculates portfolio valuation and performs `ON DUPLICATE KEY UPDATE` upsert into `portfolio_snapshots`.
8. **Audit Triggers (`trg_transactions_after_*`)**:
   Audits all INSERT, UPDATE, and DELETE actions on `transactions` into `transaction_audit`. On DELETE, historical audit rows are preserved with `transaction_id = NULL` and full metadata.

