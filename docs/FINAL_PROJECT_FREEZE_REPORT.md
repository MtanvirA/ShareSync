# ShareSync (MySQL Version) — Final Project Freeze Report

**Date:** October 9, 2026  
**Auditor / Candidate:** Md. Tanvir Anjum (`investor@sharesync.com`)  
**Repository Branch:** `mysql-version`  
**Latest Freeze Commit:** `528a5be` (`fix: resolve defects found during final manual QA`)  

---

## 1. Project Identity
- **Project Name:** ShareSync: Share Market Portfolio Tracker
- **Academic Domain:** Oracle & MySQL Database Programming, Financial Engineering, Web API Architecture
- **Repository:** `https://github.com/MtanvirA/ShareSync.git`
- **Primary Delivery Branch:** `mysql-version` (Preserved Oracle implementation on `main`)

---

## 2. Current Branch & Release Status
- **Active Branch:** `mysql-version`
- **Working Tree State:** Completely clean, zero untracked files, zero modified files.
- **Oracle Main Branch:** Completely untouched and preserved for independent Oracle grading.

---

## 3. Technology Stack
- **Database Engine:** MySQL Server 8.0+ (InnoDB storage engine, `utf8mb4_unicode_ci` charset)
- **Backend Framework:** ASP.NET Core Web API (.NET 8.0)
- **Object-Relational Mapping (ORM):** Entity Framework Core via `Pomelo.EntityFrameworkCore.MySql` (v8.0.2)
- **Frontend Architecture:** Vanilla HTML5, CSS3 Custom Properties Design System, Bootstrap 5, ES6+ JavaScript, Chart.js
- **Security & Cryptography:** JWT Bearer Token authorization, PBKDF2 with SHA-256 password hashing (100,000 iterations + cryptographically secure 128-bit salt)

---

## 4. Database Architecture & Status
- **Database Name:** `sharesync`
- **Schema Topology:** 17 Normalized Relational Tables engineered to Third Normal Form (3NF):
  1. `app_users` — Investor and administrator credentials & roles
  2. `sectors` — Industry classifications
  3. `companies` — Stock listings & current market prices (multi-asset, multi-currency)
  4. `portfolios` — Multi-portfolio containers (multi-currency base)
  5. `transactions` — Immutable BUY & SELL executions with transaction fees
  6. `transaction_audit` — Forensic before/after audit log captured via triggers
  7. `dividends` — Cash dividend distributions & corporate actions
  8. `watchlists` — Investor watchlists
  9. `watchlist_items` — Composite-keyed bridge table for tracked equities & price targets
  10. `portfolio_snapshots` — Daily mark-to-market historical valuations
  11. `company_price_history` — Daily OHLCV price observations
  12. `benchmark_price_history` — Market indices (DSEX, DS30, DSES) price history
  13. `alerts` — Price and portfolio threshold alerts
  14. `notifications` — In-app notification center
  15. `portfolio_goals` — Financial investment targets (value, return, income)
  16. `inflation_rates` — Annual consumer price index inflation rates (LF-28)
  17. `currency_exchange_rates` — Foreign currency exchange rates for FX valuation (LF-29)

- **Database Programmability:**
  - `vw_portfolio_holdings` & `vw_portfolio_summary` database views
  - `fn_get_weighted_avg_price` & `fn_calculate_unrealized_pl` stored functions
  - `sp_execute_trade`, `sp_record_dividend`, `sp_rebalance_portfolio` stored procedures
  - `trg_audit_transaction`, `trg_prevent_oversell`, `trg_update_company_price` triggers

---

## 5. Build & Compilation Status
- **Build Command:** `dotnet build`
- **Compilation Result:** **Succeeded** (0 Errors, 0 Warnings across all solution assemblies: `ShareSync.Domain`, `ShareSync.Application`, `ShareSync.Infrastructure`, `ShareSync.Web`, `ShareSync.Tests`).

---

## 6. Automated Test Suite Status
- **Test Command:** `dotnet test`
- **Total Tests:** **493**
- **Passed Tests:** **493** (100% Pass Rate)
- **Failed Tests:** **0**
- **Skipped Tests:** **0**
- **Execution Time:** ~3-4 seconds
- **Test Categories:**
  - Mathematical & Financial Consistency Audits
  - Multi-Currency & FX Conversion Audits
  - Cumulative Weighted-Average Cost Basis Lifecycle
  - Realized Profit/Loss & Transaction Fees Accounting
  - Inflation-Adjusted Return (Fisher Equation)
  - Quantitative Investment Intelligence & Technical Indicators (SMA50, SMA200, CAGR, Max Drawdown)
  - DSE Live Market Feed Scraping & Stale Resilience
  - Controller, RBAC, and Authentication Hardening

---

## 7. Demonstration Dataset Summary
Preserved in the live MySQL database for evaluation:
- `APP_USERS`: 3 accounts (Primary demo: `investor@sharesync.com`, admin: `admin@sharesync.com`)
- `PORTFOLIOS`: 5 portfolios (4 investor portfolios for Tanvir, 1 secondary)
- `TRANSACTIONS`: 69 historical trades
- `DIVIDENDS`: 11 cash distributions
- `WATCHLISTS`: 4 watchlists
- `WATCHLIST_ITEMS`: 16 target securities
- `PORTFOLIO_GOALS`: 5 financial targets
- `ALERTS`: 8 threshold monitors
- `NOTIFICATIONS`: 12 system notifications
- `COMPANIES`: 15 fully-seeded companies + 416 DSE market directory tickers
- `SECTORS`: 7 industry sectors

---

## 8. Financial Calculation Verification & Recent Defect Resolutions
All financial modules reconcile to exact zero delta ($\Delta = 0.0000$):
1. **Multi-Currency FX Conversion Fix in ReportService:**
   - *Issue:* Foreign currency holdings (e.g., SPDR S&P 500 ETF in USD) in `ReportService.GetHoldingsReportAsync` originally mapped without applying active exchange rates, causing a discrepancy with Dashboard totals.
   - *Resolution:* Applied currency exchange rate conversion across `InvestedValue`, `CurrentMarketValue`, and `UnrealizedProfitLoss`. Protected with automated regression test `MultiCurrencyHoldings_PortfolioReportAndDashboardConsistency`.
2. **Pomelo MySQL DateTime Translation Fix in DsePriceService:**
   - *Issue:* `(syncTime - h.RecordedAt) < TimeSpan.FromMinutes(10)` failed to translate to a valid SQL literal in Pomelo MySQL provider, throwing `System.FormatException`.
   - *Resolution:* Precomputed `var recentWindow = syncTime.AddMinutes(-10)` and used native DATETIME query comparison `h.RecordedAt > recentWindow`. Protected with automated regression test `SyncAllCompanyPricesAsync_ExistingRecentIntradayHistory_TranslatesAndUpdatesWithoutDuplication`.
3. **Premature Rounding & Fractional Purchase Price Fix in PortfolioService:**
   - *Issue:* `CalculatePortfolioSummaryAsync` recalculated invested capital by multiplying shares by a 2-decimal rounded `AverageBuyPrice`, creating a ৳5.06 discrepancy on fractional average purchase prices across 4 portfolios.
   - *Resolution:* Directly aggregated holding-level unrealized profit/loss and derived invested capital from exact cost basis (`totalValue - unrealizedPL`). Protected with automated regression test `PortfolioHoldings_FractionalAverageBuyPrice_AggregatesWithoutPrematureRoundingDrift`.

### Current Mathematical Parity:
- **Portfolio 1 (Main Investment):** ৳797,830.00 (Invested: ৳765,302.46, UPL: ৳32,527.54)
- **Portfolio 2 (Long-Term Growth):** ৳640,680.00 (Invested: ৳618,698.89, UPL: ৳21,981.11)
- **Portfolio 3 (Dividend Income):** ৳629,860.00 (Invested: ৳598,450.00, UPL: ৳31,410.00)
- **Portfolio 4 (Global & Multi-Asset):** ৳2,801,350.00 (Invested: ৳2,666,505.36, UPL: ৳134,844.64)
- **Sum of All Portfolios:** **৳4,869,720.00**
- **Dashboard Market Value:** **৳4,869,720.00** ($\Delta = 0.00$)
- **Dashboard Invested Capital:** **৳4,648,956.71** ($\Delta = 0.00$)
- **Dashboard Unrealized P/L:** **৳220,763.29** ($\Delta = 0.00$)
- **Accounting Identity:**
  $$\text{Portfolio Value} - \text{Invested Capital} = 4,869,720.00 - 4,648,956.71 = \mathbf{220,763.29} \equiv \text{Unrealized P/L}$$

---

## 9. Multi-Currency Verification (LF-29)
- Active exchange rate table `currency_exchange_rates` converts foreign assets into portfolio base currency.
- Verified in Portfolio 4:
  - SPY (15 shares @ $580.00 USD $\times$ 121.50 BDT/USD = ৳1,057,050.00)
  - AAPL (30 shares @ $225.00 USD $\times$ 121.50 BDT/USD = ৳820,125.00)
  - Cash USD (3,000 USD $\times$ 121.50 BDT/USD = ৳364,500.00)
  - Domestic Equities & Cash BDT = ৳559,675.00
  - Total Portfolio 4 Market Value = ৳2,801,350.00 (100% matched across views).

---

## 10. Frontend UI, Themes & Accessibility
- **Pages Audited:** All 17 HTML documents validated and rendered via headless browser with 0 console errors.
- **Theme Consistency:** Light theme foundation and dark theme overrides (`#0B111E` background, `#1E293B` cards) persist in `localStorage`.
- **Responsive Layout:** Audited at 1920px, 1440px, 1280px, 1024px, 768px, 480px, and 375px viewports with zero horizontal document scrolling (`body` overflow-x: hidden enforced).
- **Accessibility:** Numeric values use `font-variant-numeric: tabular-nums`, focus states feature high-visibility `:focus-visible` borders, and color contrasts exceed WCAG AA.

---

## 11. Security & Credentials Audit
- Zero plain-text credentials or production secrets committed in the repository.
- `appsettings.json` and `appsettings.Development.json` use placeholder `YOUR_PASSWORD` values.
- `appsettings.Local.json` is untracked and excluded in `.gitignore`.
- JWT token expiration is enforced with bearer middleware validation.
- RBAC guards protect administrative endpoints (`/api/admin/*` returns HTTP 403 Forbidden for investor accounts).

---

## 12. Repository Cleanliness & .gitignore
- Generated build outputs (`bin/`, `obj/`), IDE files (`.vs/`, `.idea/`), logs, and scratch scripts are excluded by `.gitignore`.
- Git status confirms working tree is clean.

---

## 13. Known Limitations
1. **Public DSE Web Scraper:** Relies on third-party public web feeds. When markets are closed or external servers timeout, the system gracefully preserves last known prices and marks provenance as STALE without crashing.
2. **Chart Rendering on Large Datasets:** Extremely dense intraday price charts (>2,000 points) on ultra-low-spec mobile devices may experience minor frame latency during canvas re-renders.

---

## 14. Teacher Demonstration Path (Viva & Evaluation)
A visiting professor or evaluator can verify the complete system in 15 minutes:
1. **Launch:** Run `ShareSync.bat` or `dotnet run --project src/ShareSync.Web` and navigate to `http://localhost:5000`.
2. **Login:** Authenticate as `investor@sharesync.com` / `Password123#`.
3. **Dashboard:** Inspect the aggregate portfolio value (৳4,869,720.00), invested capital (৳4,648,956.71), and unrealized gain (৳220,763.29).
4. **Portfolios:** Review the 4 portfolios and note that Portfolio 4 seamlessly integrates USD assets at the active FX rate (121.50).
5. **Trade Execution:** Open Transactions, record a BUY or SELL, observe automated broker fee calculation and oversell validation.
6. **Reports:** Generate the Holdings Valuation Report, Export CSV/PDF, and confirm totals match Dashboard totals to the cent.
7. **Investment Intelligence:** Open Company Detail for GP, inspect the 5-year retrospective quantitative analysis (CAGR, Volatility, Max Drawdown, Trend Score).
8. **Automated Tests:** In terminal, execute `dotnet test` to confirm all 493 tests pass in 3 seconds.

---

## 15. Final Release Freeze Decision
# **RELEASE FREEZE CONFIRMED — READY FOR FINAL SUBMISSION**
The codebase on branch `mysql-version` is complete, verified, mathematically consistent, clean, and permanently frozen for evaluation.
