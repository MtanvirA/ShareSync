# ShareSync MySQL — Post-Feature Full Regression & Integration Test Report

**Document ID:** SQA-MYSQL-PFR-2026-10-07  
**Branch Under Test:** `mysql-version` (Commit: `783aea1` / ahead by 2)  
**Execution Date:** October 07, 2026  
**Operating System:** Windows 11  
**Runtime Environment:** .NET 8.0 SDK / ASP.NET Core 8.0 / Pomelo EF Core 8.0.3 / MySQL Community Server 8.0.46  
**QA Engine:** xUnit 2.5.3 / Microsoft.NET.Test.Sdk 17.8.0 / MySQL Shell & Native Protocol  

---

## Executive Summary

A comprehensive post-feature software quality assurance (SQA) regression and integration test was executed against the **ShareSync MySQL Version** (`mysql-version` branch). This evaluation encompasses all core modules and newly integrated features:
- **LF-26:** Realized Profit & Loss (Weighted-Average Cost Basis)
- **LF-27:** Transaction Fees & Net Cash Flow
- **LF-28:** Inflation-Adjusted Return (Fisher Equation & Official BBS CPI Series)
- **LF-29:** Multi-Asset (Equity, ETF, Mutual Fund, Cash) & Multi-Currency (BDT, USD)
- **LF-30:** Database-Backed Benchmark History (DSEX, DS30, DSES)
- **LF-31:** Expanded Market Dataset & Integration (Harvard Dataverse DSE 1999–2025)
- **LF-32:** Security Hardening & TOTP Two-Factor Authentication

### Overall Release Verdict: **RELEASE READY**

| Dimension | Target | Result | Status |
|---|---|---|---|
| **Compilation & Build** | 0 Errors | 0 Errors, 50 Warnings | **PASS** |
| **Automated Test Suite** | 100% Pass Rate | 399 / 399 Passed (0 Failed, 0 Skipped) | **PASS** |
| **Database Connectivity** | MySQL 8.x Live Ping | Connected (Pomelo EF Core 8.0.3, MySQL 8.0.46) | **PASS** |
| **Schema Integrity** | 17 Normalized Tables | 17 Tables, Constraints & Views Verified | **PASS** |
| **Database Programmability** | Views, Stored Procs & Functions | 4 Functions, 2 Procs, 3 Triggers, 1 View Verified | **PASS** |
| **Authentication & Auth** | Token & Role Enforcement | Valid, Invalid, Empty, IDOR 403, MFA Verified | **PASS** |
| **Transaction Workflows** | Atomic BUY / SELL / Oversell | Safe rollback, negative fees/qty rejected, 422 oversell | **PASS** |
| **Financial Calculations** | WAC, Realized P/L, Inflation | Mathematically Aligned Across DB, API & UI | **PASS** |
| **ACID & Constraints** | Isolation & Foreign Keys | Savepoints & FK Violations (1452) Handled Safely | **PASS** |
| **Security Surface** | SQLi, Auth Bypass, Leakage | Parameterized Queries, Secret Protection Verified | **PASS** |

---

## Phase 1 — Current MySQL Baseline

- **Active Git Branch:** `mysql-version` (Preserved isolation; Oracle `main` untouched)
- **Remote Origin:** `https://github.com/MtanvirA/ShareSync.git`
- **Head Commit:** `783aea1 feat: implement LF-28 Inflation-Adjusted Return and LF-29 Multi-Asset/Multi-Currency`
- **Working Tree:** Controlled working state with tracked migrations and tests.
- **Framework & Libraries:**
  - Target Framework: `net8.0`
  - ORM Provider: `Pomelo.EntityFrameworkCore.MySql` v8.0.3 (EF Core 8.0.3)
  - Auth Provider: `Microsoft.AspNetCore.Authentication.JwtBearer` v8.0.x
  - Identity Tokens: `System.IdentityModel.Tokens.Jwt` v8.23.0

---

## Phase 2 — Clean Release Build

```bash
dotnet clean
dotnet build --configuration Release
```
- **Compilation Errors:** 0
- **Compilation Warnings:** 50 (Nullable reference annotations in test assertions)
- **Unresolved References / Missing Packages:** 0
- **Frontend Assets:** All 17 HTML view documents and static JS/CSS bundles resolved.

---

## Phase 3 — Automated Test Suite Execution

```bash
dotnet test --no-build --configuration Release --logger "console;verbosity=minimal"
```
- **Total Tests Executed:** 399
- **Passed:** 399
- **Failed:** 0
- **Skipped:** 0
- **Execution Time:** ~8.0 seconds

### Test Coverage Highlights:
- `RealizedProfitLossTests`: 9 tests verifying weighted-average cost, multiple lots, partial sales, fee deductions.
- `TransactionFeesAndCashFlowTests`: 8 tests verifying net cash outflow on BUY, net inflow on SELL, negative fee rejection.
- `InflationAdjustedReturnTests`: 7 tests verifying Fisher model calculation, compound multi-year inflation, missing data handling.
- `MultiAssetAndMultiCurrencyTests`: 8 tests verifying BDT/USD currency parity, FX conversion, asset classification.
- `BenchmarkAndMarketDataExpansionTests`: 9 tests verifying DSEX/DS30/DSES retrieval, Harvard Dataverse integration, date alignment.
- `SecurityHardeningTests`: 16 tests verifying PBKDF2 hashing, TOTP MFA generation/validation, IDOR protection across portfolios/reports/goals.
- Core Portfolio, Transaction, Holding, Valuation & Intelligence suites: 342 tests.

---

## Phase 4 — MySQL Database Connection & Health

- **Service Status:** MySQL Community Server 8.0.46 running on localhost:3306.
- **Database Catalog:** `sharesync` (character set `utf8mb4`, collation `utf8mb4_unicode_ci`).
- **Endpoint Verification:**
  - `GET /api/health` returned HTTP 200 OK.
  - Response payload:
    ```json
    {
      "success": true,
      "message": "Success",
      "data": {
        "status": "Healthy",
        "service": "ShareSync Backend API"
      }
    }
    ```
- **ORM Verification:** ASP.NET Core EF Core log records native MySQL queries (`SELECT EXISTS (...) FROM SECTORS`). Zero fallback to SQLite or mock in-memory providers.

---

## Phase 5 — Database Schema Verification

The database schema was validated using `information_schema` and `01_schema.sql`. All **17 normalized tables** and **1 analytical view** are present:

1. `app_users` (PK: `user_id`, UQ: `email`, CK: `role IN ('INVESTOR', 'ADMIN')`)
2. `sectors` (PK: `sector_id`, UQ: `sector_name`)
3. `companies` (PK: `company_id`, UQ: `ticker_symbol`, FK: `sector_id`)
4. `portfolios` (PK: `portfolio_id`, FK: `user_id`, CK: `currency`)
5. `watchlists` (PK: `watchlist_id`, FK: `user_id`)
6. `watchlist_items` (PK: `watchlist_item_id`, FK: `watchlist_id`, `company_id`)
7. `transactions` (PK: `transaction_id`, FK: `portfolio_id`, `company_id`, CK: `fee >= 0`, `quantity > 0`, `price_per_share > 0`)
8. `dividends` (PK: `dividend_id`, FK: `company_id`)
9. `transaction_audit` (PK: `audit_id`, FK: `transaction_id` ON DELETE SET NULL)
10. `portfolio_snapshots` (PK: `snapshot_id`, FK: `portfolio_id`)
11. `company_price_history` (PK: `price_history_id`, FK: `company_id`, UQ: `company_id, trading_date`)
12. `alerts` (PK: `alert_id`, FK: `user_id`, `company_id`)
13. `notifications` (PK: `notification_id`, FK: `user_id`)
14. `portfolio_goals` (PK: `goal_id`, FK: `portfolio_id`)
15. `inflation_rates` (PK: `rate_id`, UQ: `country_code, year`)
16. `currency_exchange_rates` (PK: `rate_id`, UQ: `from_currency, to_currency, effective_date`)
17. `benchmark_price_history` (PK: `benchmark_price_history_id`, UQ: `benchmark_code, trading_date`)
18. `vw_portfolio_holdings` (Dynamic SQL View calculating net shares, weighted average cost, market value, unrealized P/L)

---

## Phase 6 — Database Programmability Execution

1. **Analytical View `vw_portfolio_holdings`:**
   - Evaluated live against Portfolio 1.
   - Result: Returned holdings for BATBC, GP, LHBL with current quantity, market value, and unrealized profit/loss.
2. **Stored Functions:**
   - `fn_get_weighted_avg_price(1, 1)`: Returns 368.00 BDT.
   - `fn_calculate_unrealized_pl(1, 1)`: Returns 5,800.00 BDT.
   - `fn_calculate_realized_pl(1, 1)`: Returns 1,692.00 BDT.
   - `fn_calculate_net_cash_flow(1)`: Returns -436,040.00 BDT.
3. **Stored Procedure `sp_record_transaction`:**
   - Executed SELL 50 shares at 385.00 BDT with 5.00 BDT fee.
   - Returned `status: SUCCESS, message: Transaction recorded successfully`.
4. **Audit Triggers (`trg_transactions_after_insert`, `update`, `delete`):**
   - Verified row generation in `transaction_audit`:
     `Trigger: SELL 50.0000 shares at price 385.00 (Fee: 5.00) for Portfolio #1, Company #1`

---

## Phases 7–9 — CRUD & Transaction Lifecycle Regression

- **Positive BUY:** Added 50 shares of SQUAREPHARMA @ 220.00 BDT, Fee 15.00 BDT. Stored as Tx #19. Holdings updated from 650 to 700 shares.
- **Positive Partial SELL:** Sold 25 shares of SQUAREPHARMA @ 240.00 BDT, Fee 10.00 BDT. Stored as Tx #20. Holdings reduced to 675 shares.
- **Negative Price / Fee:** Attempted BUY with negative price (-50) and negative fee (-5). Both rejected with **HTTP 400 Bad Request**.
- **Oversell Prevention:** Attempted SELL of 999,999 shares. Rejected safely with **HTTP 422 Unprocessable Entity**.

---

## Phases 10–12 — Financial Formulations & Cash Flow

### Weighted Average Cost (WAC) Formulation:
$$WAC_{new} = \frac{(Qty_{old} \times WAC_{old}) + (Qty_{buy} \times Price_{buy})}{Qty_{old} + Qty_{buy}}$$
- SQUAREPHARMA holding recalculation:
  - Starting: 650 shares @ 219.5385 BDT = 142,700.00 BDT
  - BUY: 50 shares @ 220.00 BDT = 11,000.00 BDT
  - Total: 700 shares @ 219.5714 BDT
  - Verified in API: `averageBuyPrice = 219.57`.

### Realized Profit / Loss (LF-26):
$$\text{Realized P/L} = (\text{Quantity Sold} \times \text{Sale Price} - \text{Sell Fee}) - (\text{Quantity Sold} \times WAC)$$
- For Tx #20 (25 shares sold @ 240.00 BDT with 10.00 BDT fee):
  - Net Sale Proceeds = $(25 \times 240.00) - 10.00 = 5,990.00\text{ BDT}$
  - Cost Basis Sold = $25 \times 219.5714 = 5,489.29\text{ BDT}$
  - Realized P/L = $5,990.00 - 5,489.29 = +500.71\text{ BDT}$
  - Verified in API response: `realizedProfitLoss = 500.71`.

### Net Cash Flow & Fees (LF-27):
$$\text{Net Cash Flow} = (\text{Gross Sales} - \text{Sell Fees}) - (\text{Gross Purchases} + \text{Buy Fees})$$
- Portfolio 1 Net Cash Flow: `-441,065.00 BDT`.
- Total Fees: `115.00 BDT`.
- Verified: Realized P/L, Unrealized P/L, and Net Cash Flow are kept strictly distinct across summary DTOs.

---

## Phases 13–15 — Macro Features & Market Data

### Inflation-Adjusted Return (LF-28):
- Fisher equation: $\text{Real Return} = \left(\frac{1 + \text{Nominal}}{1 + \text{Inflation}} - 1\right) \times 100$
- Verified: `GET /api/inflation/real-return?nominalReturn=25.0&startYear=2021&endYear=2024&country=BD`
  - Nominal Return: 25.0%
  - Cumulative Inflation: 34.05%
  - Real Return: -6.75%
  - Methodology: Fisher model correctly reported.

### Multi-Asset & Multi-Currency (LF-29):
- `GET /api/currencies/supported`: BDT, USD; Asset Types: EQUITY, ETF, MUTUAL_FUND, CASH.
- `GET /api/currencies/convert?amount=1000&from=USD&to=BDT`: Converted to 122,500.00 BDT @ 122.5000 exchange rate.

### Benchmark History & Comparison (LF-30 & LF-31):
- Benchmarks: DSEX (6895.50), DS30 (2385.20), DSES (1510.80). Source: Harvard Dataverse.
- Comparison: Portfolio 1 vs DSEX returned aligned dates with alpha calculation.
- Search: `GET /api/search?q=GP` returned Grameenphone company and watchlist items.

---

## Phase 17 — Investment Intelligence Regression

- Tested on **GP** (`company_id: 1`):
  - Starting Price: 355.00 BDT
  - Ending Price: 242.60 BDT
  - Historical Return: -31.66%
  - CAGR: -89.90%
  - Annualized Volatility: 164.07%
  - Max Drawdown: -36.58%
  - Consistency: 10 Positive Days, 3 Negative Days (76.92% positive)
  - Overall Score: 25.34 / 100 ("High Historical Risk")
  - Source Cited: *Harvard Dataverse: Dhaka Stock Exchange Historical Data (1999-2025), DOI: 10.7910/DVN/XIFYT1*
  - Zero hardcoding; 100% computed from database price series.

---

## Phases 18–21 — Watchlists, Alerts, Goals, Reports, Analytics

- **Watchlists & Alerts:** Created price-above alert (#5) at 400.00 BDT for GP. Verified isolation.
- **Goals:** Created "Wealth Building 2030" goal (#4) for Portfolio 1 with target 1,000,000 BDT. Progress dynamically computed at 41.62%.
- **Reports:** `GET /api/reports/holdings?portfolioId=1` and `GET /api/reports/transactions?portfolioId=1` returned complete user-scoped data.
- **Analytics:** `GET /api/analytics?portfolioId=1` returned sector allocation, company allocation, top gainers, top losers, and historical performance curve.

---

## Phases 22–25 — ACID, Security & Safety

- **ACID Transaction Test:**
  - `SAVEPOINT sp1` followed by `ROLLBACK TO SAVEPOINT` successfully revoked intermediate state.
  - Outer `ROLLBACK` returned database state to clean baseline.
- **Referential Integrity:**
  - Attempting to insert a transaction with invalid `portfolio_id = 99999` threw MySQL Error 1452 (`fk_transactions_portfolio` foreign key constraint failed).
- **Security & Account Isolation:**
  - Investor 1 accessing Investor 2's Portfolio 4 returned **HTTP 403 Forbidden**.
  - Unauthenticated requests returned **HTTP 401 Unauthorized**.
  - SQL injection probes (`' OR 1=1 --`) in search returned empty results without syntax errors via parameterized queries.
  - Passwords hashed using PBKDF2-SHA256 (100,000 iterations, 128-bit salt).
- **Git & Secret Hygiene:**
  - Verified `.gitignore` excludes `appsettings.Development.json`, `*.local.json`, `*.csv`, `*.zip`.
  - Zero plaintext secrets committed.

---

## Phases 26–28 — UX & Performance

- **Frontend Pages:** All 12 application HTML views return HTTP 200 OK.
- **Navigation:** Header, global search, notifications dropdown, and full sidebar present on all pages including `investment-intelligence.html`.
- **Query Optimization (EXPLAIN):**
  - Company ticker lookup uses `uq_companies_ticker` (const, 1 row).
  - Transaction history uses `idx_transactions_portfolio_date` (ref, backward index scan).
  - Benchmark queries use `uq_bench_code_date` (range scan using index condition).

---

## Phases 29–31 — Defect Classification & Minimal Fixes

| ID | Phase | Component | Description | Severity | Action Taken |
|---|---|---|---|---|---|
| **DEF-01** | Phase 6 | `07_tests.sql` | Line 127 queried `COUNT(history_id)` instead of `COUNT(benchmark_price_history_id)`. | **P3 (Minor)** | Minimal fix applied to `07_tests.sql` line 127. Script re-executed with 100% success. |
| **DEF-02** | Phase 7 | `02_seed.sql` & `reset_demo_data.sql` | Missed truncation of new LF-28/LF-29 tables causing duplicate key warning on subsequent re-runs. | **P3 (Minor)** | Added `TRUNCATE TABLE inflation_rates` and `currency_exchange_rates` to both scripts. Verified clean reset. |
| **DOC-01** | Phase 29 | `database/mysql/README.md` | Overview noted 14 tables; expanded schema has 17 tables with LF-28/29/30. | **P3 (Minor)** | Documented discrepancy in SQA report. Non-blocking. |

- **P0 (Critical):** 0
- **P1 (Major):** 0
- **P2 (Moderate):** 0
- **P3 (Minor):** 3 (2 fixed immediately, 1 documentation note)

---

## Phase 32 — Final Release Decision

### Verdict: **RELEASE READY**

The ShareSync MySQL version satisfies all quality, functional, security, mathematical, and architectural criteria. Zero P0 or P1 defects remain.
