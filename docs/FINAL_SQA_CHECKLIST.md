# ShareSync — Final SQA Acceptance Checklist

**Evaluation Date:** October 7, 2026  
**Evaluated Branch:** `mysql-version` (Commit `a69006c`)  
**Target:** Final Course Submission & Academic Evaluation  

---

## 1. System Build & Dependencies
- [x] Clean solution build with 0 errors (`dotnet build`) — **PASS**
- [x] Solution builds in Release configuration — **PASS**
- [x] Zero missing NuGet dependencies — **PASS**
- [x] Multi-SDK roll-forward enabled via `Directory.Build.props` — **PASS**

## 2. Automated Regression Tests
- [x] Automated test runner discovers all 350 tests — **PASS**
- [x] 350 / 350 tests pass (`dotnet test`) — **PASS**
- [x] 0 test failures, 0 skipped tests — **PASS**
- [x] Test duration is under 15 seconds — **PASS**

## 3. Database Architecture & Schema (MySQL 8.x)
- [x] All 14 relational tables created in Third Normal Form (3NF) — **PASS**
- [x] Primary keys (`INT AUTO_INCREMENT`) configured on all tables — **PASS**
- [x] Foreign key constraints enforce referential integrity — **PASS**
- [x] Check constraints validate positive prices & quantities — **PASS**
- [x] Unique constraints enforce business uniqueness (`ticker_symbol`, `email`) — **PASS**
- [x] Dynamic mark-to-market holdings view (`vw_portfolio_holdings`) operational — **PASS**

## 4. Database Programmability & ACID
- [x] Stored Function: `fn_get_weighted_avg_price` executes correctly — **PASS**
- [x] Stored Function: `fn_calculate_unrealized_pl` executes correctly — **PASS**
- [x] Stored Procedure: `sp_record_transaction` validates & records orders — **PASS**
- [x] Stored Procedure: `sp_record_transaction` rejects oversell orders — **PASS**
- [x] Stored Procedure: `sp_generate_portfolio_snapshot` generates valuation — **PASS**
- [x] Triggers: `trg_transactions_after_insert/update/delete` audit all changes — **PASS**
- [x] Permanent audit records preserved on transaction deletion (`ON DELETE SET NULL`) — **PASS**
- [x] ACID demonstration (`START TRANSACTION`, `SAVEPOINT`, `ROLLBACK`, `COMMIT`) — **PASS**

## 5. Security & Authentication
- [x] Zero real secrets or passwords committed to Git — **PASS**
- [x] Password hashing uses PBKDF2 with SHA-256 and unique salt — **PASS**
- [x] JWT token generation and authorization validation functional — **PASS**
- [x] Unauthenticated access to protected endpoints rejected (HTTP 401) — **PASS**
- [x] SQL injection payloads safely sanitized (no authentication bypass) — **PASS**
- [x] Development CORS restricted to loopback origins — **PASS**

## 6. Core Application Features & Workflows
- [x] Multi-user account isolation (users cannot access other users' portfolios) — **PASS**
- [x] Unified dashboard reflects live database aggregations — **PASS**
- [x] Mathematical agreement: `Total Value - Invested = Unrealized P/L` — **PASS**
- [x] Portfolio CRUD operations persist across restarts — **PASS**
- [x] BUY transactions increment available holdings and cost basis — **PASS**
- [x] SELL transactions decrement available holdings — **PASS**
- [x] Oversell orders safely rejected with HTTP 422 — **PASS**
- [x] Watchlist many-to-many relationship and target price updates work — **PASS**
- [x] Duplicate watchlist item insertions rejected with HTTP 409 — **PASS**
- [x] Dividend income matches between Dashboard and Reports — **PASS**
- [x] All 6 reports (Holdings, Performance, Transactions, Sector, Dividends, Watchlist) load — **PASS**
- [x] Analytics module calculates sector allocations and risk metrics — **PASS**

## 7. Investment Intelligence
- [x] Company search operates server-side against full DSE database — **PASS**
- [x] Search by ticker symbol (`GP`, `BATBC`) works — **PASS**
- [x] Search by company name (`Grameenphone`) works — **PASS**
- [x] Invalid searches return empty list without 500 error — **PASS**
- [x] Time-series historical analysis computes CAGR, Volatility, Max Drawdown — **PASS**
- [x] Different companies produce mathematically distinct evaluations — **PASS**
- [x] Academic disclaimer and data source attribution present — **PASS**

## 8. Frontend User Interface & Launcher
- [x] All 13 HTML pages reachable and serve HTTP 200 OK — **PASS**
- [x] Global navigation shell and profile menu present across all views — **PASS**
- [x] One-click launcher (`ShareSync.bat`) detects prerequisites and auto-starts server — **PASS**
- [x] Browser automatically launches to `http://localhost:5000` on double-click — **PASS**

## 9. Version Parity & Remote Repository
- [x] Oracle version intact on `main` branch (commit `510cbc6`) — **PASS**
- [x] MySQL version complete on `mysql-version` branch (commit `a69006c`) — **PASS**
- [x] Clean Git working tree — **PASS**
- [x] Remote repository synchronized with GitHub (`origin/mysql-version`) — **PASS**

---

### Final Acceptance Summary

| Category | Total Checked | Passed | Failed | Status |
| :--- | :---: | :---: | :---: | :---: |
| Build & Runtime | 4 | 4 | 0 | **PASS** |
| Automated Tests | 4 | 4 | 0 | **PASS** |
| Relational Schema | 6 | 6 | 0 | **PASS** |
| SQL Programmability | 8 | 8 | 0 | **PASS** |
| Security & Auth | 6 | 6 | 0 | **PASS** |
| Core Application Features | 12 | 12 | 0 | **PASS** |
| Investment Intelligence | 7 | 7 | 0 | **PASS** |
| Frontend & Launcher | 4 | 4 | 0 | **PASS** |
| Version Parity & Git | 4 | 4 | 0 | **PASS** |
| **TOTAL** | **55** | **55** | **0** | **RELEASE READY** |
