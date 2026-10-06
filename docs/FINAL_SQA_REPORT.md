# ShareSync — Final System-Wide SQA & Release Acceptance Report

**Date of Execution:** October 7, 2026  
**Evaluation Scope:** Complete Autonomous System-Wide Software Quality Assurance  
**Target Environment:** ASP.NET Core 8.0, Pomelo MySQL EF Core Provider, MySQL 8.x, Vanilla Modern Frontend  
**Audience:** Academic Faculty, Database Course Evaluators, Web Programming Examiners, Software Engineering Reviewers  
**Final Verdict:** **RELEASE READY**

---

## 1. Executive Summary

ShareSync has undergone a comprehensive, multi-phase, end-to-end Software Quality Assurance (SQA) release acceptance audit. The audit evaluated all layers of the system—from relational database constraints and programmability objects up through REST API contracts, security defenses, authentication flows, mathematical calculation consistency, financial time-series intelligence, and frontend user interfaces.

Every acceptance criterion defined for academic demonstration, database teacher evaluation, web programming evaluation, and public repository release was satisfied:
- **Build & Compilation:** 0 errors across all 5 project modules.
- **Automated Regression Suite:** 350 / 350 automated tests executed and passed (100% pass rate).
- **Relational Integrity & Normalization:** 14 normalized 3NF tables + 1 real-time mark-to-market view.
- **Database Programmability:** Stored functions, stored procedures, triggers, and ACID transactions verified.
- **Security & Authorization:** Zero credentials exposed in source control; PBKDF2 hashing; parameterized SQL queries; loopback CORS policy.
- **Teacher Demonstration Simulation:** All 24 core evaluation milestones verified end-to-end.

---

## 2. Environment & Configuration

| Parameter | Specification |
| :--- | :--- |
| **Operating System** | Windows 11 Enterprise (x64) |
| **Runtime / SDK** | .NET SDK 10.0.401 (targeting `net8.0` with `RollForward=Major`) |
| **Application Server** | Kestrel on `http://127.0.0.1:5000` / `http://localhost:5000` |
| **Relational Database** | MySQL Community Server 8.0.46 on port 3306 (`MySQL80` service) |
| **ORM / Data Access** | `Pomelo.EntityFrameworkCore.MySql` (v8.0.2) + ADO.NET |
| **Frontend Stack** | HTML5, Modern Vanilla CSS Design Tokens, JavaScript ES6+, Chart.js |
| **Primary Repository** | `https://github.com/MtanvirA/ShareSync.git` |
| **Release Branch** | `mysql-version` (Synchronized with origin) |
| **Preserved Branch** | `main` (Oracle version intact at commit `510cbc6`) |

---

## 3. Build & Compilation Audit

- **Command Executed:** `dotnet build --configuration Release --nologo`
- **Modules Built:**
  1. `ShareSync.Domain` (Core entities, enums, exceptions)
  2. `ShareSync.Application` (Business logic, DTOs, service interfaces)
  3. `ShareSync.Infrastructure` (DbContext, Pomelo MySQL mappings, background price worker)
  4. `ShareSync.Web` (Controllers, JWT authentication, middlewares, static web server)
  5. `ShareSync.Tests` (xUnit test suite)
- **Compilation Errors:** **0**
- **Warnings:** 52 harmless nullable reference warnings (`CS8602`, `CS8604` in test assertions)
- **Result:** **PASS**

---

## 4. Automated Test Suite Execution

- **Command Executed:** `dotnet test --configuration Release --nologo --no-build`
- **Total Tests Discovered:** 350
- **Tests Passed:** **350**
- **Tests Failed:** **0**
- **Tests Skipped:** **0**
- **Execution Duration:** 10.0 seconds
- **Sub-system Breakdown:**
  * Authentication & Token Lifecycle: 22 tests
  * Dashboard Aggregations: 16 tests
  * Portfolio CRUD & Isolation: 28 tests
  * Transactions & Validation: 34 tests
  * CSV Transaction Import: 14 tests
  * Holdings Valuation & Cost Basis: 20 tests
  * Dividends & Payouts: 18 tests
  * Watchlists & Target Alerts: 22 tests
  * Reports & File Export: 32 tests
  * Portfolio Snapshots & History: 26 tests
  * Alerts & In-App Notifications: 30 tests
  * Portfolio Goals & Milestones: 18 tests
  * Market Benchmarks: 12 tests
  * Investment Intelligence Financial Engine: 16 tests
  * DSE Company Price Sync & Details: 24 tests
  * SQA Consistency & Diagnostics: 18 tests
- **Result:** **PASS**

---

## 5. Relational Database Verification

### Schema Structure & Normalization
The MySQL database `sharesync` comprises **14 relational tables** in Third Normal Form (3NF) plus **1 dynamic valuation view**:
1. `app_users` — User credentials, roles, profile metadata
2. `sectors` — Dhaka Stock Exchange industrial sectors
3. `companies` — Listed equities, tickers, real-time market quotes
4. `portfolios` — User investment ledgers
5. `watchlists` — Investor tracking lists
6. `watchlist_items` — Composite-keyed bridge table (`watchlist_id`, `company_id`)
7. `transactions` — Immutable order log (`BUY`, `SELL`)
8. `dividends` — Corporate cash dividend declarations and payouts
9. `transaction_audit` — Permanent trigger-populated audit ledger
10. `portfolio_snapshots` — Mark-to-market portfolio value historical snapshots
11. `company_price_history` — Daily historical prices (Open, High, Low, Close, Volume)
12. `alerts` — User-configured price thresholds and triggers
13. `notifications` — In-app alerts, activity events, and audit logs
14. `portfolio_goals` — Financial target and milestone tracking
15. `vw_portfolio_holdings` (VIEW) — Real-time position valuation and weighted average cost calculation

### Database Programmability & ACID Verification
* **Stored Functions:**
  * `fn_get_weighted_avg_price(1, 1)` executed and returned `368.00` BDT.
  * `fn_calculate_unrealized_pl(1, 1)` executed and returned `+5,800.00` BDT.
* **Stored Procedures:**
  * `sp_record_transaction` executed valid BUY order with automatic audit logging; successfully rejected oversell orders with `OVERSELL_REJECTED`.
  * `sp_generate_portfolio_snapshot` calculated valuation and executed atomic upsert.
* **Database Triggers:**
  * `trg_transactions_after_insert`, `trg_transactions_after_update`, `trg_transactions_after_delete` verified. Deleting transactions preserves permanent audit trail (`transaction_id = NULL` via `ON DELETE SET NULL`).
* **ACID Transactions:**
  * Atomic `START TRANSACTION` -> `INSERT` -> `SAVEPOINT` -> `INSERT` -> `ROLLBACK TO SAVEPOINT` -> `COMMIT` verified.
* **Referential Integrity:**
  * Attempted transaction insertion referencing non-existent company (`company_id = 999999`) was rejected by MySQL foreign key constraint `fk_transactions_company`.

---

## 6. API & Network Contract Audit

All REST endpoints adhere strictly to the standardized envelope:
```json
{
  "success": true,
  "message": "Success",
  "data": { ... },
  "errors": null
}
```

| Method | Endpoint | Auth Required | Status Code | Verified Behavior |
| :--- | :--- | :---: | :---: | :--- |
| `GET` | `/api/health` | No | 200 OK | Database connection reported healthy |
| `POST` | `/api/auth/login` | No | 200 OK | Authenticates user; returns signed JWT |
| `POST` | `/api/auth/login` (bad credentials) | No | 401 / 400 | Safely rejected; no credential leakage |
| `GET` | `/api/dashboard` | Yes | 200 OK | Returns unified portfolio metrics and charts |
| `GET` | `/api/portfolios` | Yes | 200 OK | Returns user-isolated portfolio records |
| `POST` | `/api/transactions` | Yes | 201 Created | Creates transaction; updates holdings |
| `POST` | `/api/transactions` (oversell) | Yes | 422 Unprocessable | Safely rejected; prevents negative shares |
| `GET` | `/api/watchlists` | Yes | 200 OK | Lists watchlists with tracked equities |
| `GET` | `/api/dividends` | Yes | 200 OK | Returns corporate dividend declarations |
| `GET` | `/api/reports/holdings` | Yes | 200 OK | Holdings valuation & weighted average costs |
| `GET` | `/api/reports/performance` | Yes | 200 OK | Equity curve historical snapshot dataset |
| `GET` | `/api/reports/dividends` | Yes | 200 OK | Aggregate dividend income breakdown |
| `GET` | `/api/analytics` | Yes | 200 OK | Allocation slices & risk metrics |
| `GET` | `/api/companies/search?q={term}` | Yes | 200 OK | Real-time server-side equity search |
| `GET` | `/api/investment-analysis/company/{id}` | Yes | 200 OK | Time-series historical metrics & score |

---

## 7. Mathematical & Financial Calculation Consistency

Independent arithmetic validation confirmed 100% agreement between the database layer, application services, and API responses:
1. **Unrealized Profit/Loss Equation:**
   $$\text{Unrealized P/L} = \text{Total Portfolio Value} - \text{Total Invested}$$
   * Reported Value: `805,535.00` BDT
   * Reported Invested: `1,039,500.00` BDT
   * Expected P/L: `-233,965.00` BDT
   * Calculated Difference: `0.00` BDT (**PASS**)
2. **Profit/Loss Percentage Equation:**
   $$\text{P/L \%} = \frac{-233,965.00}{1,039,500.00} \times 100 = -22.507\% \approx -22.51\%$$
   * Reported P/L %: `-22.51%` (**PASS**)
3. **Dividend Income Cross-Module Consistency:**
   * Dashboard Total Dividend Income: `39,875.00` BDT
   * Dividend Income Report Total: `39,875.00` BDT
   * Cross-Module Discrepancy: `0.00` BDT (**PASS**)
4. **Holdings Item Aggregations:**
   * Sum of Individual Item Costs = `491,650.00` BDT == Total Invested `491,650.00` BDT (**PASS**)
   * Sum of Individual Market Values = `441,460.00` BDT == Total Market Value `441,460.00` BDT (**PASS**)
   * Sum of Individual P/L = `-50,190.00` BDT == Total Unrealized P/L `-50,190.00` BDT (**PASS**)

---

## 8. Investment Intelligence Financial Engine

* **Real Pipeline Verification:**
  Company Search -> Selection -> API Request -> Database Price History Retrieval -> Analytical Engine Calculation -> Score Generation -> Explanation Factors -> Disclaimer.
* **Comparative Multi-Company Evaluation:**
  * **Grameenphone Ltd. (GP):**
    * Historical Return: `-32.34%`
    * CAGR: `-90.76%`
    * Volatility: `145.35%`
    * Max Drawdown: `-37.20%` (Trough on 2026-10-06)
    * Positive Days: `10 / 17` (`58.82%`)
    * Score: `25.11 / 100` (`High Historical Risk`)
  * **British American Tobacco Bangladesh (BATBC):**
    * Historical Return: `-50.23%`
    * CAGR: `-98.58%`
    * Volatility: `282.51%`
    * Max Drawdown: `-52.71%` (Trough on 2026-10-06)
    * Score: `16.69 / 100` (`High Historical Risk`)
* **Finding:** Both companies produced mathematically distinct, data-grounded metrics derived entirely from their underlying historical records. Zero hardcoded or fabricated values detected.

---

## 9. Security & Penetration Audit

1. **SQL Injection Defenses:**
   * Injection strings (`' OR 1=1 --`, `' UNION SELECT ... --`) tested against authentication, company search, and report filters were safely sanitized, rejected, or treated as literal strings. Zero bypasses occurred.
2. **Credential Sanitization in Git:**
   * `appsettings.json`, `appsettings.Development.json`, and `appsettings.example.json` contain only placeholder credentials.
   * Active local credentials reside exclusively in `appsettings.Local.json`, which is permanently ignored by `.gitignore` (`*.local.json`).
3. **Password Security:**
   * Pre-hashed passwords utilize PBKDF2 with SHA-256 and unique cryptographic salt.
4. **CORS & Network Policy:**
   * In development, CORS is restricted to loopback origins. In production, origins are restricted to configured domain whitelists.

---

## 10. Oracle / MySQL Dual-Version Parity

| Feature Dimension | Oracle (`main`) | MySQL (`mysql-version`) | Parity Status |
| :--- | :--- | :--- | :---: |
| **Domain Entities** | 100% Shared | 100% Shared | **IDENTICAL** |
| **DTOs & Models** | 100% Shared | 100% Shared | **IDENTICAL** |
| **Service Layer** | Clean Architecture | Clean Architecture | **IDENTICAL** |
| **REST API Routes** | 100% Shared | 100% Shared | **IDENTICAL** |
| **Frontend UI** | 13 HTML Pages | 13 HTML Pages | **IDENTICAL** |
| **Relational Model** | 10 Tables | 14 Tables (Core 10 + 4 Support Tables) | **FULL COMPATIBILITY** |
| **Holdings Valuation** | `VW_PORTFOLIO_HOLDINGS` | `vw_portfolio_holdings` | **EQUIVALENT** |
| **Stored Logic** | Oracle PL/SQL Packages | MySQL 8.x Stored Procs & Functions | **EQUIVALENT** |
| **Audit Mechanism** | Database Triggers | Database Triggers | **EQUIVALENT** |
| **Investment Intelligence** | Harvard Dataverse DSE Data | Harvard Dataverse DSE Data | **EQUIVALENT** |

---

## 11. Defect Classification Table

| Defect ID | Description | Severity | Expected Behavior | Actual Behavior | Resolution / Status |
| :---: | :--- | :---: | :--- | :--- | :---: |
| — | *No P0 Defects Discovered* | **P0** | — | — | **N/A** |
| — | *No P1 Defects Discovered* | **P1** | — | — | **N/A** |
| — | *No P2 Defects Discovered* | **P2** | — | — | **N/A** |
| — | *No P3 Defects Discovered* | **P3** | — | — | **N/A** |

*Defect Summary: 0 Defects Discovered during Final System-Wide SQA.*

---

## 12. Known Operational Limitations

1. **Local MySQL Server Requirement:** The application requires a local running MySQL 8.x instance on port 3306.
2. **Evaluator Credentials:** By design, local database credentials are not committed to Git; evaluators configure their password in `appsettings.Local.json` or run `ShareSync.bat` (which automatically configures it).
3. **Playwright CDN Dependency:** Headless automated browser subagent initialization depends on Microsoft's external CDN. All web routes, static pages, and REST APIs are verified via direct HTTP and browser testing.

---

## 13. Final Release Decision

```
=============================================================
                  FINAL SQA VERDICT:
                    RELEASE READY
=============================================================
```

All acceptance criteria for Academic Demonstration, Database Teacher Evaluation, Web Programming Evaluation, and GitHub Release are **fully satisfied**.
