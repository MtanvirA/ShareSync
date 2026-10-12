# ShareSync (MySQL Version) — Final Live Demonstration Rehearsal & Mock Viva Guide

**Document Version:** 1.0 (Frozen Baseline)  
**Date:** October 10, 2026  
**Candidate / Presenter:** Md. Tanvir Anjum (`investor@sharesync.com`)  
**Repository Branch:** `mysql-version`  
**Latest Freeze Commit:** `528a5be`  
**Test Suite Baseline:** 493 Automated Tests Passing (0 Failed, 0 Skipped, 0 Warnings, 0 Errors)  
**Target Environment:** Localhost (`http://localhost:5000`) with MySQL Server 8.0 on port 3306  

---

## 1. Fifteen-Minute Live Demonstration Script

This structured 15-minute rehearsal is designed for an academic evaluator or instructor evaluating Database Systems, Financial Engineering, and ASP.NET Core Web API architecture.

### Timeline Summary
- **00:00–01:00** — Project Introduction & Problem Statement
- **01:00–02:00** — Authentication & Consolidated Investor Dashboard
- **02:00–03:30** — Multi-Portfolio Architecture & Holdings View
- **03:30–05:00** — BUY/SELL Transaction Workflow & Oversell Validation
- **05:00–06:00** — Volume-Weighted Average Cost (VWAP) & P/L Accounting
- **06:00–07:00** — Multi-Currency Foreign Assets (USD to BDT) & Dividends
- **07:00–08:00** — Financial Milestone Goals & Thematic Watchlists
- **08:00–10:00** — Institutional Analytics: Volatility, Drawdown, Benchmark & Fisher Inflation
- **10:00–11:00** — Investment Intelligence Multi-Factor Scoring Engine
- **11:00–12:00** — Dhaka Stock Exchange (DSE) Search & Resilient Market Data
- **12:00–13:00** — Financial Reports Engine & Mathematical Reconciliation
- **13:00–14:15** — Native MySQL 8.0 Walkthrough (Schema, Views, Routines, Triggers, Audit)
- **14:15–15:00** — Security, Automated Testing Suite (493 Tests), and Honest Limitations

---

### Step-by-Step Execution Sequence

#### Step 1: Project Introduction & Problem Statement (00:00–01:00)
- **Page to Open:** `http://localhost:5000/login.html` (or landing screen)
- **Control to Use:** View clean login interface (`#loginForm`).
- **Expected Result:** Responsive authentication screen with dark/light theme persistence.
- **Sentence to Say Aloud:** *"ShareSync is a professional multi-portfolio equity tracking and risk analytics platform built on ASP.NET Core 8 and MySQL 8.0 that replaces error-prone spreadsheets with an automated, double-entry financial ledger."*
- **Technical Concept:** Full-stack architecture, relational database normalization, financial domain modeling.
- **Likely Follow-up Question:** *"Why not just track stocks in Excel or Google Sheets?"*
- **Concise Answer:** *"Spreadsheets cannot enforce relational constraints, prevent overselling, audit concurrent order revisions, or guarantee deterministic Volume-Weighted Average Price calculations without manual formula degradation."*

---

#### Step 2: Authentication & Consolidated Dashboard (01:00–02:00)
- **Page to Open:** `/login.html` &rarr; `/index.html`
- **Control to Use:** Enter `investor@sharesync.com` / `Password123#` &rarr; Click `#loginBtn` (`Sign In`).
- **Expected Result:** Instant redirection to Dashboard. Header displays user *"Tanvir Anjum"*, role badge `INVESTOR`, and the 4 consolidated KPI cards load:
  - **Total Portfolio Value:** `৳4,869,720.00`
  - **Total Invested Amount:** `৳4,648,956.71`
  - **Unrealized Gain/Loss:** `+৳220,763.29 (+4.75%)`
  - **Dividend Income:** `৳34,500.00`
- **Sentence to Say Aloud:** *"Upon authenticating with PBKDF2 password hashing and receiving a digitally signed JWT bearer token, the dashboard aggregates market values and capital gains across all four investor sub-portfolios in real time."*
- **Technical Concept:** Stateless JWT Bearer Authentication, claims-based authorization, mark-to-market relational aggregation.
- **Likely Follow-up Question:** *"How does the server compute this total without causing slow queries across multiple tables?"*
- **Concise Answer:** *"In `DashboardService.cs`, indexed queries group open transactions and map current prices from `companies` and foreign exchange rates from `currency_exchange_rates`, achieving zero-delta accounting in a single query pass."*

---

#### Step 3: Multi-Portfolio Architecture & Holdings (02:00–03:30)
- **Page to Open:** `/portfolio.html`
- **Control to Use:** Click **Portfolio** on sidebar &rarr; Inspect `#portfolioSelect` dropdown.
- **Expected Result:** Displays the 4 distinct segregated portfolios for Tanvir:
  1. *Main Investment Portfolio* (`৳797,830.00`, Invested: `৳765,302.46`, UPL: `+৳32,527.54`)
  2. *Long-Term Growth Portfolio* (`৳640,680.00`, Invested: `৳618,698.89`, UPL: `+৳21,981.11`)
  3. *Dividend Income Portfolio* (`৳629,860.00`, Invested: `৳598,450.00`, UPL: `+৳31,410.00`)
  4. *Global & Multi-Asset Portfolio* (`৳2,801,350.00`, Invested: `৳2,666,505.36`, UPL: `+৳134,844.64`)
- **Sentence to Say Aloud:** *"ShareSync models a one-to-many relationship between the user and portfolios, allowing the investor to separate conservative dividend holdings from aggressive growth and foreign assets."*
- **Technical Concept:** 1:N Foreign Key relationship (`app_users.user_id` &rarr; `portfolios.user_id`) with strict horizontal tenant isolation.
- **Likely Follow-up Question:** *"Can User 1 see or modify User 2's portfolio by altering the URL ID parameter?"*
- **Concise Answer:** *"No. `PortfolioService.cs` extracts the verified `UserId` claim directly from the cryptographic JWT token and rejects unauthorized portfolio access with HTTP 403 Forbidden or 404 Not Found."*

---

#### Step 4: BUY/SELL Transactions & Oversell Validation (03:30–05:00)
- **Page to Open:** `/transactions.html`
- **Control to Use:** Review chronological ledger (`#transactionTableBody`), then click `#openTransactionForm` (`Add transaction`). Toggle transaction type to **SELL**, select *Main Investment Portfolio*, select *Grameenphone (GP)*.
- **Expected Result:** Available shares indicator `#availableSharesHint` dynamically displays `1,000` shares. Entering `1,500` displays an immediate client validation warning; submitting triggers database rejection.
- **Sentence to Say Aloud:** *"Transactions are validated both client-side and at the database engine level via `sp_record_transaction`, strictly preventing overselling and fractional-share corruption."*
- **Technical Concept:** Multi-tier validation, database procedural constraints, oversell prevention (`requested_qty > available_shares`).
- **Likely Follow-up Question:** *"What happens inside the database when an investor tries to sell more shares than they own?"*
- **Concise Answer:** *"The stored procedure `sp_record_transaction` calculates cumulative BUY minus SELL shares for that portfolio and security, immediately returning status code `OVERSELL_REJECTED` and aborting the insertion."*

---

#### Step 5: Volume-Weighted Average Cost (VWAP) & Realized P/L (05:00–06:00)
- **Page to Open:** `/portfolio.html` (Main Investment Portfolio) &rarr; `#portfolioHoldingsBody`
- **Control to Use:** Highlight row for **GP** (Grameenphone) and **SQURPHARMA** (Square Pharmaceuticals).
- **Expected Result:**
  - GP: 1,000 shares, Current Price `৳355.00`, Avg. Buy Price `৳330.00`, Market Value `৳355,000.00`, Unrealized Return `+৳25,000.00 (+7.58%)`.
  - Filter transactions by `SELL` to show BATBC: Realized P/L `+৳4,950.00` after deducting broker fee `৳50.00`.
- **Sentence to Say Aloud:** *"Cost basis is calculated using volume-weighted purchase costs rather than simple price averages, and realized gains properly subtract transaction commission fees upon sale."*
- **Technical Concept:** Volume-Weighted Average Price (VWAP), Cost Basis accounting, Realized vs Unrealized P/L.
- **Likely Follow-up Question:** *"Why not just update a static `average_price` column on a holdings table whenever someone buys?"*
- **Concise Answer:** *"Maintaining static mutable balances introduces state drift and loses auditability. In ShareSync, holdings and VWAP are deterministically derived from the immutable order ledger via database view `vw_portfolio_holdings` and function `fn_get_weighted_avg_price`."*

---

#### Step 6: Multi-Currency Foreign Assets & Dividends (06:00–07:00)
- **Page to Open:** `/portfolio.html` &rarr; Select *Global & Multi-Asset Portfolio* (Portfolio #4), then click **Dividends** on sidebar (`/dividends.html`).
- **Control to Use:** In Portfolio #4, inspect holdings for **SPY** (15 shares) and **AAPL** (30 shares) in USD; on Dividends page, view `#dividendsTableBody`.
- **Expected Result:**
  - SPY: 15 shares @ $580.00 USD &times; 121.50 BDT/USD = `৳1,057,050.00`.
  - AAPL: 30 shares @ $225.00 USD &times; 121.50 BDT/USD = `৳820,125.00`.
  - Portfolio #4 Total Value: `৳2,801,350.00` (100% mathematically reconciled).
  - Dividends: 11 distributions totaling `৳34,500.00`.
- **Sentence to Say Aloud:** *"Foreign dollar-denominated assets are converted into the portfolio base currency using the `currency_exchange_rates` relational table, preventing naive and erroneous cross-currency additions."*
- **Technical Concept:** Foreign Exchange (FX) translation, corporate dividend tracking, multi-asset consolidation.
- **Likely Follow-up Question:** *"What happens if an exchange rate is missing for a foreign asset?"*
- **Concise Answer:** *"In `CurrencyService.cs`, if a currency pair lookup fails, the system logs a warning, falls back to a parity rate of 1.0, and flags the currency status so valuations never throw unhandled exceptions."*

---

#### Step 7: Financial Goals & Thematic Watchlists (07:00–08:00)
- **Page to Open:** `/goals.html` &rarr; `/watchlist.html`
- **Control to Use:** View `#goalsGrid` milestone cards, then click **Watchlist** to inspect `#watchlistContent`.
- **Expected Result:**
  - Goals: 5 targets (e.g., Retirement Corpus, Emergency Fund) displaying dynamic progress bars linked to live portfolio mark-to-market valuations.
  - Watchlists: 4 thematic lists ("Blue Chips", "DSE Top 30", "Tech Stocks", "High Dividend").
- **Sentence to Say Aloud:** *"Watchlist items utilize a composite primary key on `(watchlist_id, company_id)`, declaratively guaranteeing that an equity cannot be duplicated within the same list."*
- **Technical Concept:** Composite primary keys, natural candidate keys, goal tracking dynamically evaluated against live asset balances.
- **Likely Follow-up Question:** *"Why use a composite primary key instead of an auto-incrementing ID for watchlist items?"*
- **Concise Answer:** *"An auto-incrementing ID would allow duplicate `(watchlist_id, company_id)` pairs unless an additional unique index was created. The composite primary key enforces both uniqueness and identity declaratively in a single constraint."*

---

#### Step 8: Risk Analytics, Benchmark Alpha & Fisher Inflation (08:00–10:00)
- **Page to Open:** `/analytics.html`
- **Control to Use:** Select *Main Investment Portfolio* from `#analyticsPortfolioSelect`. Scroll to **Risk & Diversification**, **Benchmark Comparison** (`#benchmarkSelect`), and **Inflation Analysis**.
- **Expected Result:**
  - **CAGR:** `+14.2%`
  - **Annualized Volatility:** `16.8%` ($\sigma_{\text{daily}} \times \sqrt{252}$)
  - **Maximum Drawdown:** `-8.4%`
  - **DSEX Benchmark Alpha:** Portfolio Return (`+18.4%`) vs DSEX (`+11.2%`) &rarr; Alpha `+7.2%`.
  - **Fisher Inflation Adjustment:** Nominal Return `+18.40%` &rarr; Bangladesh Inflation `9.70%` &rarr; Real Return `+7.93%`.
- **Sentence to Say Aloud:** *"ShareSync evaluates institutional risk using annualized standard deviation scaled by 252 trading days, peak-to-trough drawdown, and the exact Fisher Equation to measure true purchasing power gains."*
- **Technical Concept:** Quantitative risk analytics, time-series analysis, Fisher equation ($R_{\text{real}} = \frac{1 + R_{\text{nom}}}{1 + i} - 1$), benchmark normalization.
- **Likely Follow-up Question:** *"Why use the exact Fisher Equation instead of the simple subtraction $R_{\text{nominal}} - \text{Inflation}$?"*
- **Concise Answer:** *"Simple subtraction is an approximation that diverges significantly in high-inflation environments like Bangladesh (9.7%). The exact Fisher Equation correctly scales the compound erosion of money over time."*

---

#### Step 9: Investment Intelligence Quantitative Engine (10:00–11:00)
- **Page to Open:** `/investment-intelligence.html`
- **Control to Use:** Click quick-select pill `GP` under `#quickCompanyChips` &rarr; Click `#analyzeBtn` (`Analyze`).
- **Expected Result:** Displays composite score (`#resScore`, e.g., `78/100`), 5-year retrospective analysis across 4 pillars (Return, Risk, Drawdown, Consistency, Trend), technical moving averages (SMA 50 vs SMA 200), and transparent methodology disclaimers (`#resDataCoverage`).
- **Sentence to Say Aloud:** *"Investment Intelligence provides objective multi-factor scoring based on published historical fundamentals and trend metrics, with explicit disclaimers that it represents historical quantitative analysis, not financial advice."*
- **Technical Concept:** Multi-factor equity scoring, technical indicators (SMA50, SMA200, Golden Cross), quantitative ethics and disclaimers.
- **Likely Follow-up Question:** *"Does this score predict future stock prices?"*
- **Concise Answer:** *"No. We make that strictly clear in the UI and documentation. It is a retrospective quantitative tool evaluating historical volatility, trend persistence, and risk-adjusted return, not a predictive black-box model."*

---

#### Step 10: DSE Live Scraper & Market Search (11:00–12:00)
- **Page to Open:** Global search bar `#globalSearchInput` (top navbar) or `/company.html?code=GP`.
- **Control to Use:** Type `"BATBC"` or `"BEXIMCO"` into `#globalSearchInput`. Show autocomplete dropdown `#globalSearchDropdown`.
- **Expected Result:** Instant matching across 416 DSE listed tickers showing ticker symbol, company name, industry sector, and latest closing price.
- **Sentence to Say Aloud:** *"Our market directory indexes over 400 Dhaka Stock Exchange listed companies with resilient fallback caching if the live exchange portal is offline."*
- **Technical Concept:** Debounced autocomplete search, web scraping resilience, graceful degradation with stale price provenance.
- **Likely Follow-up Question:** *"What happens if the DSE website changes its HTML or blocks your scraper?"*
- **Concise Answer:** *"In `DsePriceService.cs`, scraper errors are caught and logged without crashing the request; the system serves the last confirmed price from `companies` and tags the record provenance as `STALE`."*

---

#### Step 11: Financial Reports Engine & Mathematical Parity (12:00–13:00)
- **Page to Open:** `/reports.html`
- **Control to Use:** Select *Holdings Report* under `#reportTypeSelect` &rarr; Click `#exportMenuBtn` &rarr; `#exportCsvBtn` / `#exportPdfBtn`.
- **Expected Result:** Tabular report preview renders with cost basis, market valuation, and unrealized gain/loss matching the Dashboard aggregate `৳4,869,720.00` down to zero decimal drift.
- **Sentence to Say Aloud:** *"The reporting engine enforces zero mathematical delta between individual holding reports, portfolio summaries, and the consolidated investor dashboard."*
- **Technical Concept:** Financial reporting integrity, multi-currency aggregation reconciliation, client-side PDF/CSV generation.
- **Likely Follow-up Question:** *"How do you prevent rounding discrepancies from accumulating across different reports?"*
- **Concise Answer:** *"All calculations use C# `decimal` and MySQL `DECIMAL(20,4)` throughout the aggregation pipeline. Values are only rounded to two decimal places at the final presentation boundary."*

---

#### Step 12: Native MySQL 8.0 Engine Walkthrough (13:00–14:15)
- **Tool to Use:** Terminal CLI or MySQL Workbench connected to `sharesync`.
- **Commands to Execute:**
  1. `USE sharesync; SHOW TABLES;` &rarr; Proves 17 normalized 3NF tables.
  2. `SELECT * FROM vw_portfolio_holdings LIMIT 3;` &rarr; Proves dynamic SQL View execution.
  3. `SELECT fn_get_weighted_avg_price(1, 1), fn_calculate_unrealized_pl(1, 1);` &rarr; Proves Deterministic Stored Functions.
  4. `SELECT audit_id, transaction_id, action_type, action_date, changed_by, details FROM transaction_audit ORDER BY audit_id DESC LIMIT 3;` &rarr; Proves Trigger-based forensic auditing.
- **Sentence to Say Aloud:** *"Business integrity and forensic audit trails are enforced natively inside the database engine using views, stored functions, procedures, and triggers, ensuring data security even if modified outside the web application."*
- **Technical Concept:** Database programmability, relational views, stored routines, automated audit triggers, ACID transactions.
- **Likely Follow-up Question:** *"Why implement business logic in SQL triggers and stored procedures when you already have C# services?"*
- **Concise Answer:** *"Defense in depth. C# services provide rapid user feedback and domain validation, while MySQL constraints, procedures, and triggers provide an immutable last line of defense against direct database access or third-party integrations."*

---

#### Step 13: Security, Automated Testing & Wrap-Up (14:15–15:00)
- **Tool to Use:** Terminal executing `dotnet test`.
- **Expected Result:** **493 Passed, 0 Failed, 0 Skipped** in ~3–4 seconds.
- **Sentence to Say Aloud:** *"The entire financial calculation engine, authorization boundaries, and edge cases are validated by 493 automated tests with zero warnings and zero failures."*
- **Technical Concept:** Test-driven quality assurance, regression testing, CI/CD readiness.
- **Likely Follow-up Question:** *"What are the biggest limitations of the current implementation?"*
- **Concise Answer:** *"The primary limitation is reliance on public DSE web endpoints for intraday updates rather than a dedicated institutional broker feed, and unadjusted historical price series for past stock splits."*

---

## 2. Verified Click Paths & Controls

| Step | Page / URL | Control / Selector | Action | Expected Output |
|---|---|---|---|---|
| **1. Login** | `/login.html` | `#loginEmail`, `#loginPassword`, `#loginBtn` | Enter `investor@sharesync.com` / `Password123#`, click Sign In | Redirects to `/index.html`, stores JWT token in `localStorage`. |
| **2. Dashboard** | `/index.html` | `#dashboardPortfolioValue`, `#dashboardTotalInvested`, `#dashboardUnrealizedPL` | Inspect top cards | Displays `৳4,869,720.00`, `৳4,648,956.71`, `+৳220,763.29 (+4.75%)`. |
| **3. Portfolios** | `/portfolio.html` | `#portfolioSelect` | Change selected portfolio (Portfolios 1 to 4) | Updates holdings table, cash balances, and allocation metrics. |
| **4. Holdings** | `/portfolio.html` | `#portfolioHoldingsBody` | Inspect GP and SQURPHARMA rows | Shows shares, average buy price, current price, and unrealized return. |
| **5. Add Tx** | `/transactions.html` | `#openTransactionForm`, `#transactionType`, `#company`, `#quantity`, `#price` | Click Add transaction, toggle BUY/SELL, select asset | Shows dynamic fee, net cash impact, and available shares for SELL. |
| **6. Filter Tx** | `/transactions.html` | `#transactionTypeFilter`, `#txApplyFiltersBtn` | Select `SELL`, click Apply Filters | Filters table to completed SELL orders with realized P/L and broker fee. |
| **7. Multi-Curr** | `/portfolio.html` | `#portfolioSelect` (Select Portfolio 4) | View SPY and AAPL rows | Asset currency USD, converted to BDT using 121.50 exchange rate. |
| **8. Dividends** | `/dividends.html` | `#dividendsTableBody`, summary cards | Click Dividends in navbar | Displays 11 recorded dividend payouts totaling `৳34,500.00`. |
| **9. Goals** | `/goals.html` | `#goalsGrid`, `#openGoalForm` | Inspect milestone cards | Shows 5 financial goals with live mark-to-market percentage progress bars. |
| **10. Watchlist**| `/watchlist.html` | `#watchlistTabs`, `#watchlistItemsTable` | Switch between 4 watchlists | Displays tracked securities with target buy prices and current deltas. |
| **11. Analytics**| `/analytics.html` | `#analyticsPortfolioSelect`, `#benchmarkSelect` | Select Portfolio 1, toggle DSEX / DS30 | Renders CAGR, Volatility, Max Drawdown, Benchmark Alpha, Fisher Real Return. |
| **12. Intel** | `/investment-intelligence.html` | `.quick-company-btn[data-ticker='GP']`, `#analyzeBtn` | Click GP chip, click Analyze | Renders composite score (0–100), risk/return breakdown, SMA50/SMA200 chart. |
| **13. Reports** | `/reports.html` | `#reportTypeSelect`, `#exportMenuBtn`, `#exportCsvBtn` | Select Holdings Report, click Export | Previews reconciled report and downloads auditor-ready CSV/PDF. |
| **14. Search** | Global Navbar | `#globalSearchInput` | Type `"BATBC"` or `"BEXIMCO"` | Drops down matching equities, sectors, and prices with instant keyboard navigation. |

---

## 3. Real Demonstration Dataset Verification

All numerical figures in the demonstration script are grounded in the active MySQL database and verified across both C# controllers and database queries:

### Investor Master Accounts (`app_users`)
- **Primary Demo Account:** `investor@sharesync.com` / `Password123#` (User ID: 1, Role: `INVESTOR`)
- **Secondary Demo Account:** `tanvir@sharesync.com` / `Password123#` (User ID: 2, Role: `INVESTOR`)
- **System Administrator:** `admin@sharesync.com` / `Admin123#` (User ID: 3, Role: `ADMIN`)

### Portfolio Breakdown for User #1
1. **Portfolio #1 (Main Investment Portfolio):**
   - Base Currency: `BDT`
   - Market Value: `৳797,830.00`
   - Invested Capital: `৳765,302.46`
   - Unrealized P/L: `+৳32,527.54`
2. **Portfolio #2 (Long-Term Growth Portfolio):**
   - Base Currency: `BDT`
   - Market Value: `৳640,680.00`
   - Invested Capital: `৳618,698.89`
   - Unrealized P/L: `+৳21,981.11`
3. **Portfolio #3 (Dividend Income Portfolio):**
   - Base Currency: `BDT`
   - Market Value: `৳629,860.00`
   - Invested Capital: `৳598,450.00`
   - Unrealized P/L: `+৳31,410.00`
4. **Portfolio #4 (Global & Multi-Asset Portfolio):**
   - Base Currency: `BDT`
   - Market Value: `৳2,801,350.00`
   - Invested Capital: `৳2,666,505.36`
   - Unrealized P/L: `+৳134,844.64`
   - Foreign Assets Converted:
     - SPY: 15 shares @ $580.00 USD &times; 121.50 BDT = `৳1,057,050.00`
     - AAPL: 30 shares @ $225.00 USD &times; 121.50 BDT = `৳820,125.00`
     - Cash USD: $3,000.00 USD &times; 121.50 BDT = `৳364,500.00`
     - Domestic Equities & BDT Cash = `৳559,675.00`

### Consolidated Totals & Accounting Identity
$$\text{Total Portfolio Value} = 797,830.00 + 640,680.00 + 629,860.00 + 2,801,350.00 = \mathbf{৳4,869,720.00}$$
$$\text{Total Invested Capital} = 765,302.46 + 618,698.89 + 598,450.00 + 2,666,505.36 = \mathbf{৳4,648,956.71}$$
$$\text{Total Unrealized P/L} = 4,869,720.00 - 4,648,956.71 = \mathbf{+৳220,763.29} \equiv \Delta 0.00$$

---

## 4. Three Transaction Demonstration Options

### Option A — Read-Only Explanation (Recommended for Safe Viva)
- **Method:** Navigate to `/transactions.html`. Filter by `SELL` orders.
- **What to Explain:**
  1. Show existing closed trade (e.g., BATBC sale).
  2. Point out Gross Sale Amount (`Qty * Price`), Broker Commission Fee, and Realized P/L.
  3. Explain the formula: $\text{Realized P/L} = (\text{Gross Inflow} - \text{Fee}) - (\text{Sold Qty} \times \text{VWAP Buy Price})$.
  4. Explain why historical BUY weighted average price is unchanged by subsequent sells.
- **Safety Rating:** 100% Safe (0 mutations, 0 risk of database state corruption).

### Option B — Controlled Live Transaction & Reversible Procedure
If the teacher insists on seeing a live transaction execution, execute this exact reversible sequence:
1. **Target Position:** Portfolio #1, Company #1 (GP - Grameenphone). Current open shares: `1,000`.
2. **Action to Execute:**
   - Click `#openTransactionForm`.
   - Select Type: `BUY`.
   - Portfolio: `Main Investment Portfolio`.
   - Company: `Grameenphone PLC (GP)`.
   - Quantity: `10` shares.
   - Price: `355.00`.
   - Fee: `10.00`.
   - Date: Today's date.
   - Click `Save transaction`.
3. **Observation:**
   - Transaction #70 is inserted.
   - Database trigger `trg_transactions_after_insert` automatically writes to `transaction_audit`.
   - GP holding shares increase from `1,000` to `1,010`.
4. **Reversal Procedure:**
   - Find the newly created transaction at the top of `#transactionTableBody`.
   - Click the red **Delete** button (`trash` icon) for that transaction.
   - Confirm deletion modal.
   - Backend calls `DELETE /api/transactions/{id}`.
   - Database trigger `trg_transactions_after_delete` logs the deletion with `transaction_id = NULL`.
5. **Integrity Verification:**
   - In terminal/MySQL: `SELECT COUNT(*) FROM transactions;` &rarr; Confirms row count returns to exactly 69.
   - Portfolio #1 holdings return to `1,000` shares.

### Option C — Validation Demonstration (Recommended Alternative)
- **Method:** Demonstrate input boundaries without writing persistent records.
- **Test 1 (Oversell Prevention):** In `#openTransactionForm`, select `SELL`, Portfolio #1, GP. Enter Quantity `99,999` (available: 1,000). Click Save. Frontend displays validation error banner, blocking the request.
- **Test 2 (Whole Share Constraint):** Enter Quantity `10.5` shares. Browser rejects input with *"Please enter a valid whole number"*.
- **Test 3 (Negative Price Guard):** Enter Price `-50.00`. Rejection occurs instantly.
- **Safety Rating:** 100% Safe (no database records created or modified).

---

## 5. MySQL 8.0 Engine Walkthrough ("Show Me the Database")

When the instructor asks to inspect the database, use this reference:

### Schema Overview
- **Database:** `sharesync`
- **Engine:** InnoDB
- **Charset:** `utf8mb4_unicode_ci`
- **Tables:** 17 normalized relational tables in Third Normal Form (3NF).

### Object Catalog

| Object Name | Type | File Location | Purpose & Simple Explanation |
|---|---|---|---|
| `app_users` | Table | `01_schema.sql` (L14) | Stores investor identities, hashed passwords (PBKDF2), and roles (`INVESTOR`, `ADMIN`). |
| `sectors` | Table | `01_schema.sql` (L38) | Industry sector lookup table eliminating repeating text columns (3NF). |
| `companies` | Table | `01_schema.sql` (L56) | Listed equity catalog, ticker symbols, and latest market prices. |
| `portfolios` | Table | `01_schema.sql` (L93) | Multi-portfolio containers linking to users with `ON DELETE RESTRICT`. |
| `transactions` | Table | `01_schema.sql` (L134) | Immutable BUY/SELL financial ledger with quantity, price, fee, and foreign keys. |
| `dividends` | Table | `01_schema.sql` (L180) | Cash dividend payout history linking companies to investor portfolios. |
| `watchlists` | Table | `01_schema.sql` (L217) | Investor monitoring categories. Cascades on portfolio deletion. |
| `watchlist_items` | Table | `01_schema.sql` (L249) | Bridge table using **Composite Primary Key** `(watchlist_id, company_id)` preventing duplicates. |
| `transaction_audit` | Table | `01_schema.sql` (L279) | Forensic audit trail populated exclusively by MySQL triggers. |
| `portfolio_snapshots`| Table | `01_schema.sql` (L314) | Daily mark-to-market historical balances for CAGR and drawdown tracking. |
| `company_price_history`| Table | `01_schema.sql` (L353) | Daily OHLCV closing observations for technical indicators and charts. |
| `alerts` | Table | `01_schema.sql` (L394) | Price threshold monitors with check constraints on alert type. |
| `notifications` | Table | `01_schema.sql` (L432) | In-app notification center. |
| `portfolio_goals` | Table | `01_schema.sql` (L463) | Financial targets linking target amounts to live portfolio values. |
| `currency_exchange_rates`| Table | `01_schema.sql` (L535)| Reference exchange rates (`from_currency`, `to_currency`) for FX conversion. |
| `inflation_rates` | Table | `01_schema.sql` (L550)| Annual macroeconomic inflation rates for Fisher real return calculations. |
| `benchmark_price_history`| Table | `01_schema.sql` (L495)| Official closing levels for DSEX, DS30, and DSES indices. |
| `vw_portfolio_holdings` | View | `01_schema.sql` (L569)| Computes net shares, market valuation, VWAP, and unrealized P/L dynamically. |
| `fn_get_weighted_avg_price`| Function | `04_functions.sql` (L20) | Computes volume-weighted average buy price across historical BUY transactions. |
| `fn_calculate_unrealized_pl`| Function | `04_functions.sql` (L57) | Returns mark-to-market unrealized gain/loss for a company in a portfolio. |
| `fn_calculate_realized_pl`| Function | `04_functions.sql` (L115)| Computes cumulative realized profit/loss from all executed SELL transactions. |
| `fn_calculate_net_cash_flow`| Function | `04_functions.sql` (L147)| Calculates net cash flow ($\sum \text{SELL Inflows} - \sum \text{BUY Outflows}$). |
| `fn_calculate_real_return` | Function | `04_functions.sql` (L177)| Computes inflation-adjusted return using the exact Fisher Equation. |
| `sp_record_transaction` | Procedure | `05_procedures.sql` (L40) | Validates input bounds, enforces whole-share constraint, checks oversell, and inserts atomically. |
| `sp_generate_portfolio_snapshot`| Procedure | `05_procedures.sql` (L163)| Computes portfolio valuation on a given date and upserts a daily snapshot. |
| `trg_transactions_after_insert`| Trigger | `06_triggers.sql` (L20) | Automatically logs every new trade into `transaction_audit`. |
| `trg_transactions_after_update`| Trigger | `06_triggers.sql` (L48) | Captures before/after delta strings (`OLD -> NEW`) in `transaction_audit`. |
| `trg_transactions_after_delete`| Trigger | `06_triggers.sql` (L75) | Preserves audit record with `transaction_id = NULL` upon deletion. |

---

## 6. Mock Viva Question Bank (Ordered by Difficulty)

### Level 1: Basic (Project, Users, Technology)

#### Q1: What is ShareSync and what core problem does it solve?
- **10-Second Answer:** An automated multi-portfolio tracker and risk analytics platform built with ASP.NET Core and MySQL 8.0.
- **30-Second Answer:** Retail investors struggle with tracking multiple brokerage accounts, calculating weighted average buy prices across fragmented lots, computing tax-compliant realized gains upon partial sales, and adjusting returns for currency and high inflation. ShareSync automates all of this with mathematical precision.
- **Where to Verify:** `README.md`, `src/ShareSync.Web/Program.cs`
- **Search Term:** `"ShareSync: Share Market Portfolio Tracker"`
- **Likely Follow-up:** *"How does it differ from a commercial trading portal like Interactive Brokers?"*

#### Q2: What is the technology stack of ShareSync?
- **10-Second Answer:** .NET 8 (ASP.NET Core Web API), MySQL 8.0 (InnoDB), EF Core via Pomelo provider, and vanilla ES6+ JavaScript.
- **30-Second Answer:** The backend uses clean layered architecture (.Domain, .Application, .Infrastructure, .Web). The database runs on MySQL 8.0 with InnoDB enforcing ACID compliance. The frontend uses Vanilla CSS custom properties, Bootstrap 5, and Chart.js without heavyweight runtime frameworks.
- **Where to Verify:** `docs/FINAL_PROJECT_FREEZE_REPORT.md` (Section 3)
- **Search Term:** `"Pomelo.EntityFrameworkCore.MySql"`
- **Likely Follow-up:** *"Why choose vanilla JavaScript instead of React or Angular?"*

#### Q3: Who are the target users and what roles exist?
- **10-Second Answer:** Individual equity investors and system administrators; roles are `INVESTOR` and `ADMIN`.
- **30-Second Answer:** Active investors track personal portfolios, record trades, and analyze risk. Administrators monitor background market scrapers, system audit logs, and master ticker data. Roles are cryptographically enforced via JWT role claims.
- **Where to Verify:** `src/ShareSync.Domain/Entities/AppUser.cs`
- **Search Term:** `"public enum UserRole"` or `"Role = \"INVESTOR\""`
- **Likely Follow-up:** *"What prevents an investor from accessing administrative endpoints?"*

#### Q4: How is the solution structured across layers?
- **10-Second Answer:** Clean Architecture: Domain has entities, Application has business logic, Infrastructure has EF Core/MySQL, and Web has API controllers.
- **30-Second Answer:** The Domain project contains zero external dependencies. The Application project defines interfaces, DTOs, and calculation services. Infrastructure implements repositories and EF Core DbContext. Web exposes RESTful endpoints and serves the static frontend.
- **Where to Verify:** `docs/PROJECT_STRUCTURE.md`
- **Search Term:** `"ShareSync.Domain"`
- **Likely Follow-up:** *"Can the Domain assembly reference EF Core?"*

#### Q5: Is ShareSync an active order execution gateway?
- **10-Second Answer:** No, it is an investment tracking, risk analytics, and portfolio accounting platform.
- **30-Second Answer:** ShareSync does not send FIX protocol orders to brokerage clearing houses. It records confirmed trade executions, analyzes holdings, and tracks performance. We also include an isolated paper-trading simulator for hypothetical order modeling.
- **Where to Verify:** `src/ShareSync.Application/Services/SimulatorService.cs`
- **Search Term:** `"SimulatorService"`
- **Likely Follow-up:** *"Does the simulator modify the user's real portfolio holdings?"*

#### Q6: How many automated tests exist and what do they validate?
- **10-Second Answer:** 493 automated tests validating mathematical parity, security, overselling, and multi-currency conversions.
- **30-Second Answer:** The suite covers unit and integration tests across financial formulas, Pomelo MySQL provider translation, JWT authentication, role authorization, DSE scraper resilience, and rounding drift prevention, achieving a 100% pass rate.
- **Where to Verify:** `tests/ShareSync.Tests`
- **Search Term:** `"dotnet test"`
- **Likely Follow-up:** *"How do you test database queries without requiring a live MySQL instance during unit tests?"*

---

### Level 2: Database Engineering & MySQL 8.0

#### Q7: Why did you choose MySQL 8.0 over a NoSQL database like MongoDB?
- **10-Second Answer:** Financial ledgers require strict ACID guarantees and foreign key constraints to prevent orphaned records or balance sheet corruption.
- **30-Second Answer:** In MongoDB, documents are denormalized. If an investor updates a transaction or a company changes tickers, data can become desynchronized across documents. MySQL's InnoDB engine enforces relational constraints, row-level locking, and transactional rollbacks, guaranteeing data integrity.
- **Where to Verify:** `database/mysql/01_schema.sql`
- **Search Term:** `"ENGINE=InnoDB"`
- **Likely Follow-up:** *"What is the storage engine and default collation?"*

#### Q8: Explain Third Normal Form (3NF) in ShareSync.
- **10-Second Answer:** 1NF: Atomic values; 2NF: No partial key dependencies; 3NF: No transitive dependencies.
- **30-Second Answer:**
  - **1NF:** Every column contains atomic values (no comma-separated ticker lists) and each table has a primary key.
  - **2NF:** In composite-keyed `watchlist_items(watchlist_id, company_id)`, `target_price` depends on both keys.
  - **3NF:** In `companies`, we store `sector_id` rather than duplicating sector descriptions, preventing update anomalies.
- **Where to Verify:** `database/mysql/01_schema.sql` (Lines 38–75)
- **Search Term:** `"CREATE TABLE companies"`
- **Likely Follow-up:** *"Give an example of an insertion anomaly that 3NF prevents in this schema."*

#### Q9: Why use `ON DELETE RESTRICT` on transactions and portfolios?
- **10-Second Answer:** To prevent accidental deletion of parent records that would leave orphaned transactional records.
- **30-Second Answer:** A user cannot delete a company or a portfolio that has recorded transactions (`ON DELETE RESTRICT`). This prevents irreversible corruption of historical transaction records and ensures audit trails remain intact.
- **Where to Verify:** `database/mysql/01_schema.sql` (Line 145)
- **Search Term:** `"ON DELETE RESTRICT"`
- **Likely Follow-up:** *"When do you use `ON DELETE CASCADE` in the schema?"*

#### Q10: Why does `watchlist_items` have a Composite Primary Key?
- **10-Second Answer:** To naturally enforce that a stock can only be added once per watchlist without needing a synthetic ID.
- **30-Second Answer:** The composite key `(watchlist_id, company_id)` declaratively guarantees uniqueness at the database engine level. Inserting the same stock twice into the same watchlist triggers a duplicate key violation.
- **Where to Verify:** `database/mysql/01_schema.sql` (Line 258)
- **Search Term:** `"CONSTRAINT pk_watchlist_items PRIMARY KEY"`
- **Likely Follow-up:** *"What index is automatically created for a composite primary key?"*

#### Q11: What is the purpose of database indexes in ShareSync?
- **10-Second Answer:** Indexes prevent slow full-table scans on frequently filtered and sorted columns.
- **30-Second Answer:** We established composite indexes: `idx_transactions_portfolio_date` on `(portfolio_id, transaction_date)` for fast ledger retrieval, and `idx_price_hist_comp_date` on `(company_id, recorded_at)` for instant charting.
- **Where to Verify:** `database/mysql/01_schema.sql` (Lines 535–548)
- **Search Term:** `"CREATE INDEX idx_transactions_portfolio_date"`
- **Likely Follow-up:** *"Why not index every single column in the database?"*

#### Q12: What is a Database View and why did you create `vw_portfolio_holdings`?
- **10-Second Answer:** A view is a stored virtual query that encapsulates complex aggregations without duplicating underlying data.
- **30-Second Answer:** `vw_portfolio_holdings` dynamically computes net shares (`BUY - SELL`), market values, weighted average buy costs, and unrealized gains across all transactions. This ensures all parts of the application share a single source of truth for holdings.
- **Where to Verify:** `database/mysql/01_schema.sql` (Lines 569–598)
- **Search Term:** `"CREATE OR REPLACE VIEW vw_portfolio_holdings"`
- **Likely Follow-up:** *"Is `vw_portfolio_holdings` an updatable view?"*

#### Q13: What is the difference between a Stored Procedure and a Stored Function?
- **10-Second Answer:** A function returns a single scalar value and can be called inside SQL queries; a procedure executes business logic and can perform multiple modifications.
- **30-Second Answer:**
  - `fn_get_weighted_avg_price` is a deterministic function called inside queries like `SELECT fn_get_weighted_avg_price(1, 2);`.
  - `sp_record_transaction` is a procedure that validates whole-share inputs, checks for overselling, and atomically inserts the order.
- **Where to Verify:** `database/mysql/04_functions.sql`, `05_procedures.sql`
- **Search Term:** `"CREATE FUNCTION fn_get_weighted_avg_price"`
- **Likely Follow-up:** *"Can a stored function execute an `INSERT` statement in MySQL?"*

#### Q14: How does your trigger-based transaction auditing work?
- **10-Second Answer:** `AFTER INSERT`, `UPDATE`, and `DELETE` triggers on `transactions` automatically write delta records to `transaction_audit`.
- **30-Second Answer:** Even if a DBA executes a direct SQL command in the MySQL console bypassing the web application, triggers fire automatically, recording the timestamp, executing user, and before/after values into `transaction_audit`.
- **Where to Verify:** `database/mysql/06_triggers.sql`
- **Search Term:** `"CREATE TRIGGER trg_transactions_after_insert"`
- **Likely Follow-up:** *"What happens to the audit log if the original transaction is deleted?"*

#### Q15: What does ACID mean in the context of ShareSync?
- **10-Second Answer:** Atomicity, Consistency, Isolation, and Durability.
- **30-Second Answer:**
  - **Atomicity:** When recording a trade, either the trade and cash adjustments both commit or both roll back.
  - **Consistency:** Database check constraints prevent negative prices or fractional shares.
  - **Isolation:** InnoDB uses repeatable-read MVCC to prevent dirty reads.
  - **Durability:** Committed transactions survive server crashes via the InnoDB redo log.
- **Where to Verify:** `src/ShareSync.Infrastructure/Data/ShareSyncDbContext.cs`
- **Search Term:** `"Database.BeginTransactionAsync"`
- **Likely Follow-up:** *"What transaction isolation level does MySQL InnoDB use by default?"*

---

### Level 3: Application Architecture (.NET 8 & EF Core)

#### Q16: How does EF Core communicate with MySQL 8.0?
- **10-Second Answer:** Via the `Pomelo.EntityFrameworkCore.MySql` open-source database provider.
- **30-Second Answer:** Pomelo translates LINQ queries into native MySQL dialect SQL. In `Program.cs`, we register it with `ServerVersion.AutoDetect(connectionString)`, configuring connection pooling, retry on failure, and snake_case column mappings.
- **Where to Verify:** `src/ShareSync.Web/Program.cs`, `src/ShareSync.Infrastructure`
- **Search Term:** `"UseMySql"`
- **Likely Follow-up:** *"Did you encounter any DateTime translation issues with Pomelo?"*

#### Q17: How does authentication and token generation work?
- **10-Second Answer:** Investors log in via email/password; the backend verifies PBKDF2 hash and issues a digitally signed JWT token.
- **30-Second Answer:** `AuthService.LoginAsync` fetches the user, verifies password salt and hash, and constructs a JWT with claims for `UserId`, `Email`, and `Role`. The client stores the token in `localStorage` and attaches it in the `Authorization: Bearer <token>` header.
- **Where to Verify:** `src/ShareSync.Application/Services/AuthService.cs`
- **Search Term:** `"GenerateJwtToken"`
- **Likely Follow-up:** *"What algorithm and parameters are used for password hashing?"*

#### Q18: What is the role of DTOs (Data Transfer Objects)?
- **10-Second Answer:** DTOs decouple external API contracts from internal database entity models.
- **30-Second Answer:** Exposing EF Core entities directly risks over-posting vulnerabilities and serialization loops. DTOs like `CreateTransactionRequestDto` enforce strict input validation attributes and shape output responses cleanly.
- **Where to Verify:** `src/ShareSync.Application/DTOs`
- **Search Term:** `"CreateTransactionRequestDto"`
- **Likely Follow-up:** *"How are validation errors in DTOs handled by controllers?"*

#### Q19: How is Dependency Injection utilized in ASP.NET Core?
- **10-Second Answer:** Services and repositories are registered with scoped lifetimes in `Program.cs` and injected via constructor injection.
- **30-Second Answer:** We register `ITransactionService`, `IPortfolioService`, and `ShareSyncDbContext` with `AddScoped`, meaning a new instance is created per HTTP request and cleanly disposed of at the end of the request pipeline.
- **Where to Verify:** `src/ShareSync.Web/Program.cs`
- **Search Term:** `"builder.Services.AddScoped"`
- **Likely Follow-up:** *"Why use Scoped lifetime for DbContext rather than Singleton?"*

#### Q20: How does the application prevent SQL injection?
- **10-Second Answer:** EF Core uses parameterized SQL queries exclusively for all LINQ expressions.
- **30-Second Answer:** User inputs are treated as data parameters, never concatenated into SQL strings. Even when raw SQL is required, EF Core's `FromSqlInterpolated` uses parameterized `MySqlParameter` instances, neutralizing injection attacks.
- **Where to Verify:** `docs/SECURITY.md`
- **Search Term:** `"SQL Injection"`
- **Likely Follow-up:** *"Are user inputs in search queries sanitized against Cross-Site Scripting (XSS)?"*

---

### Level 4: Financial Engineering & Calculation Logic

#### Q21: Why use Volume-Weighted Average Price (VWAP) instead of a simple average?
- **10-Second Answer:** A simple average ignores order size; VWAP weights cost basis by the number of shares bought at each price.
- **30-Second Answer:** If you buy 100 shares at ৳100 and 900 shares at ৳200, a simple average says ৳150, but you actually spent ৳190,000 for 1,000 shares (৳190/share). VWAP is the only mathematically and tax-compliant cost basis method.
- **Where to Verify:** `docs/FINANCIAL_FORMULA_VIVA.md` (Section 1)
- **Search Term:** `"fn_get_weighted_avg_price"`
- **Likely Follow-up:** *"Do subsequent SELL transactions change the historical weighted average purchase price?"*

#### Q22: How is Realized Profit/Loss computed when selling shares?
- **10-Second Answer:** Net sale proceeds minus the cost basis of the sold shares: $(\text{Sell Qty} \times P_{\text{sell}} - \text{Fee}) - (\text{Sell Qty} \times \text{VWAP})$.
- **30-Second Answer:** In `TransactionService.cs`, when a SELL is recorded, the position's historical buy average is calculated. Gross sale proceeds minus transaction fees are compared against the volume-weighted cost of the shares being liquidated.
- **Where to Verify:** `src/ShareSync.Application/Services/TransactionService.cs`
- **Search Term:** `"CalculateRealizedProfitLossAsync"`
- **Likely Follow-up:** *"How do transaction fees affect net cash flow and realized P/L?"*

#### Q23: What is Unrealized Profit/Loss and how is it calculated?
- **10-Second Answer:** Paper gain or loss on open positions: $\text{Current Quantity} \times (\text{Current Price} - \text{VWAP})$.
- **30-Second Answer:** It represents the mark-to-market gain if the investor liquidated their remaining open shares today. It updates dynamically whenever the company's market price changes without requiring order executions.
- **Where to Verify:** `src/ShareSync.Application/Services/PortfolioService.cs`
- **Search Term:** `"CalculateHoldingSummary"`
- **Likely Follow-up:** *"Why can premature rounding of VWAP cause unrealized P/L drift?"*

#### Q24: How does ShareSync prevent premature rounding drift?
- **10-Second Answer:** By maintaining full high-precision `decimal` values throughout calculation pipelines and only rounding at presentation boundaries.
- **30-Second Answer:** In `PortfolioService.cs`, multiplying fractional share averages that have been prematurely rounded to two decimal places creates discrepancies. We aggregate total market value and total cost basis before computing portfolio-level gain/loss.
- **Where to Verify:** `docs/FINAL_PROJECT_FREEZE_REPORT.md` (Section 8)
- **Search Term:** `"PortfolioHoldings_FractionalAverageBuyPrice_AggregatesWithoutPrematureRoundingDrift"`
- **Likely Follow-up:** *"What C# type is used for monetary values?"*

#### Q25: How does Multi-Currency FX conversion work for USD holdings?
- **10-Second Answer:** Foreign USD asset valuations are multiplied by the active exchange rate in `currency_exchange_rates` before aggregation into BDT.
- **30-Second Answer:** In Portfolio #4, SPY ETF is priced at $580.00 USD. `CurrencyService.cs` retrieves the active USD-to-BDT rate (121.50) and converts Invested Value, Current Market Value, and Unrealized P/L into base currency BDT.
- **Where to Verify:** `src/ShareSync.Application/Services/CurrencyService.cs`
- **Search Term:** `"ConvertCurrencyAsync"`
- **Likely Follow-up:** *"What happens if the base currency of a portfolio is already USD?"*

#### Q26: What is CAGR and when can it be misleading?
- **10-Second Answer:** Compound Annual Growth Rate: $(\frac{V_{\text{final}}}{V_{\text{initial}}})^{1/Y} - 1$.
- **30-Second Answer:** CAGR smooths out volatility over multiple years. It can be misleading over very short periods (<1 year) or when large external cash injections occur, which can artificially inflate growth if not adjusted for cash flow.
- **Where to Verify:** `src/ShareSync.Application/Services/AnalyticsService.cs`
- **Search Term:** `"CalculateCagrAsync"`
- **Likely Follow-up:** *"How does Time-Weighted Return (TWR) differ from CAGR?"*

#### Q27: How is Annualized Volatility calculated?
- **10-Second Answer:** Standard deviation of daily percentage price changes scaled by $\sqrt{252}$ trading days.
- **30-Second Answer:** We take daily logarithmic or percentage returns $R_t = \frac{P_t - P_{t-1}}{P_{t-1}}$, compute the sample standard deviation $\sigma_{\text{daily}}$, and multiply by $\sqrt{252}$ to reflect annualized institutional market volatility.
- **Where to Verify:** `src/ShareSync.Application/Services/AnalyticsService.cs`
- **Search Term:** `"CalculateAnnualizedVolatility"`
- **Likely Follow-up:** *"Why use 252 instead of 365 days for volatility?"*

#### Q28: What is Maximum Drawdown (MDD) and what does it tell an investor?
- **10-Second Answer:** The largest peak-to-trough percentage drop in portfolio valuation before reaching a new high.
- **30-Second Answer:** MDD measures downside capital risk and tail risk. If a portfolio reached ৳700,000 and dropped to ৳641,200, the MDD is -8.4%, showing the maximum historical loss an investor would have endured.
- **Where to Verify:** `src/ShareSync.Application/Services/AnalyticsService.cs`
- **Search Term:** `"CalculateMaxDrawdown"`
- **Likely Follow-up:** *"Can Maximum Drawdown be positive?"*

#### Q29: How is Benchmark Alpha calculated against DSEX?
- **10-Second Answer:** Relative outperformance: $\text{Alpha} = R_{\text{portfolio}} - R_{\text{benchmark}}$.
- **30-Second Answer:** `BenchmarkService.cs` normalizes both the portfolio and the Dhaka Stock Exchange benchmark (DSEX) to a common baseline date. Alpha represents the excess return achieved above the market index.
- **Where to Verify:** `src/ShareSync.Application/Services/BenchmarkService.cs`
- **Search Term:** `"CompareWithBenchmarkAsync"`
- **Likely Follow-up:** *"Is this simple excess return equivalent to CAPM Jensen's Alpha?"*

#### Q30: Why use the Fisher Equation for Inflation-Adjusted Returns?
- **10-Second Answer:** To accurately calculate true purchasing power growth: $R_{\text{real}} = \left[\frac{1 + R_{\text{nominal}}}{1 + i} - 1\right] \times 100$.
- **30-Second Answer:** Naive subtraction ($R_{\text{nom}} - i$) fails to compound the erosion of purchasing power. The Fisher Equation, implemented in both C# `InflationService.cs` and MySQL `fn_calculate_real_return`, gives exact economic real return.
- **Where to Verify:** `src/ShareSync.Application/Services/InflationService.cs`, `database/mysql/04_functions.sql`
- **Search Term:** `"fn_calculate_real_return"`
- **Likely Follow-up:** *"Can real return be negative even if nominal return is positive?"*

#### Q31: What does the Investment Intelligence composite score represent?
- **10-Second Answer:** A 0–100 multi-factor quantitative rating combining Quality, Momentum, Risk, and Yield factors (25% each).
- **30-Second Answer:** It synthesizes fundamental metrics (P/E, ROE), trend momentum (SMA50 vs SMA200), historical price volatility, and dividend reliability into an objective rating, with explicit disclaimers that it is not predictive advice.
- **Where to Verify:** `src/ShareSync.Application/Services/InvestmentIntelligenceService.cs`
- **Search Term:** `"CalculateCompositeScore"`
- **Likely Follow-up:** *"What happens to the score if a company has fewer than 200 days of price history?"*

---

### Level 5: Security, System Hardening, Edge Cases & Limitations

#### Q32: What happens if an external DSE web endpoint fails or is unreachable?
- **10-Second Answer:** The scraper fails gracefully, logs a warning, serves the last confirmed price, and marks status as `STALE`.
- **30-Second Answer:** In `DsePriceService.cs`, network timeouts or HTTP 500 errors from external exchange web portals are caught. The system never crashes; it continues to operate using existing database prices in `companies`.
- **Where to Verify:** `src/ShareSync.Infrastructure/Services/DsePriceService.cs`
- **Search Term:** `"SyncAllCompanyPricesAsync"`
- **Likely Follow-up:** *"How does the frontend indicate to the user that market data is stale?"*

#### Q33: How does the system handle a portfolio with zero transactions?
- **10-Second Answer:** It returns a zero valuation summary (`৳0.00`) and displays an empty state prompt without throwing null reference errors.
- **30-Second Answer:** In `PortfolioService.cs` and `vw_portfolio_holdings`, `COALESCE` and `DefaultIfEmpty` ensure that null sums default to zero, allowing the user to view empty portfolio cards and click "Add transaction".
- **Where to Verify:** `src/ShareSync.Application/Services/PortfolioService.cs`
- **Search Term:** `"GetPortfolioSummaryAsync"`
- **Likely Follow-up:** *"What does the Analytics page display when an empty portfolio is selected?"*

#### Q34: How does ShareSync prevent concurrent double-spend or race conditions?
- **10-Second Answer:** Via database row-level locking, atomic transactions, and whole-share oversell verification.
- **30-Second Answer:** `sp_record_transaction` executes within an atomic transaction. In EF Core, `Database.BeginTransactionAsync` with repeatable read isolation ensures concurrent sell orders cannot both deduct the same available shares.
- **Where to Verify:** `database/mysql/05_procedures.sql`
- **Search Term:** `"proc_label: BEGIN"`
- **Likely Follow-up:** *"How would you handle high-frequency trading concurrency in a production broker?"*

#### Q35: Where are database credentials and JWT signing keys configured?
- **10-Second Answer:** In environment variables or untracked `appsettings.Local.json`; placeholder values exist in version control.
- **30-Second Answer:** The repository contains zero production secrets. `appsettings.json` contains `YOUR_PASSWORD` placeholders. Secret management follows 12-factor application best practices via ASP.NET Core configuration providers.
- **Where to Verify:** `docs/SECURITY.md`, `src/ShareSync.Web/appsettings.json`
- **Search Term:** `"Jwt:Key"`
- **Likely Follow-up:** *"What happens if the JWT signing key is shorter than 256 bits?"*

#### Q36: How does the Paper Trading Simulator remain isolated from real holdings?
- **10-Second Answer:** Simulator orders are stored in separate models or executed purely in-memory, never writing to `transactions`.
- **30-Second Answer:** `SimulatorService.cs` models hypothetical orders against current market prices to evaluate theoretical outcomes. It has zero write access to the real `transactions` or `portfolios` tables.
- **Where to Verify:** `src/ShareSync.Application/Services/SimulatorService.cs`
- **Search Term:** `"ExecuteSimulationAsync"`
- **Likely Follow-up:** *"Can an investor promote a simulated trade into a real trade?"*

#### Q37: How does the system handle missing foreign exchange rates?
- **10-Second Answer:** Falls back to 1.0 parity, logs a warning, and flags the valuation.
- **30-Second Answer:** If a currency pair (e.g., EUR to BDT) is not found in `currency_exchange_rates`, `CurrencyService.cs` prevents null exceptions by falling back to 1.0 and returning a warning in the response metadata.
- **Where to Verify:** `src/ShareSync.Application/Services/CurrencyService.cs`
- **Search Term:** `"GetExchangeRateAsync"`
- **Likely Follow-up:** *"How are exchange rates updated in the database?"*

#### Q38: Are historical stock prices adjusted for stock splits and bonus shares?
- **10-Second Answer:** No; historical prices in `company_price_history` are unadjusted raw closing prices.
- **30-Second Answer:** ShareSync records raw historical prices. Corporate actions like stock dividends and cash dividends are recorded in `dividends`, but historical OHLC series do not perform retrospective price adjustment splits. This is a documented limitation.
- **Where to Verify:** `docs/HISTORICAL_DATA_QUALITY.md`
- **Search Term:** `"Corporate Actions"`
- **Likely Follow-up:** *"How would you implement split-adjusted historical pricing in the future?"*

#### Q39: What is the origin of the 5-year historical price series?
- **10-Second Answer:** Curated baseline historical datasets supplemented with live scraping updates from the DSE web portal.
- **30-Second Answer:** Because public DSE endpoints do not offer free 5-year historical APIs, long-term historical series for technical indicators (SMA50, SMA200) were seeded from curated market data repositories, while intraday prices are scraped live.
- **Where to Verify:** `docs/DATASET.md`, `database/mysql/02_seed.sql`
- **Search Term:** `"company_price_history"`
- **Likely Follow-up:** *"How do you ensure seed data matches real DSE trading ranges?"*

#### Q40: What are the top three engineering improvements you would make with more time?
- **10-Second Answer:** WebSocket live feeds, automated corporate action split adjustments, and Redis caching for market indices.
- **30-Second Answer:**
  1. *Real-time Streaming:* Replace HTTP polling with WebSockets for sub-second price ticker updates.
  2. *Corporate Actions Engine:* Automatically recalculate historical share quantities upon stock dividend announcements.
  3. *Distributed Caching:* Introduce Redis caching for benchmark and sector aggregation to minimize database load.
- **Where to Verify:** `docs/FINAL_PROJECT_FREEZE_REPORT.md` (Section 13)
- **Search Term:** `"Known Limitations"`
- **Likely Follow-up:** *"How would Redis caching affect cache invalidation on trade insertion?"*

---

## 7. Emergency Demonstration Recovery Playbook

If an unexpected technical glitch occurs during evaluation, follow this one-page guide:

| Case | Scenario | First Safe Action | What to Say to Teacher | Safe to Retry? | What NOT to Do |
|---|---|---|---|---|---|
| **A** | **Login Fails** | Check Caps Lock; re-enter `investor@sharesync.com` / `Password123#`. | *"Let me verify the demo credentials; we enforce strict case-sensitive PBKDF2 password hashing."* | Yes, retry typing password. | **DO NOT** reseed or reset the database. |
| **B** | **Dashboard Blank / Loading Spinner** | Press `F12` to check Network tab; check if backend terminal is running. | *"The dashboard is aggregating across all four portfolios; let me confirm the local Web API process is active."* | Yes, refresh page (`Ctrl+F5`). | **DO NOT** restart MySQL service. |
| **C** | **MySQL Connection Refused** | Check terminal running `ShareSync.Web`; verify MySQL Windows service is running. | *"Let me verify the local MySQL 8.0 server service state in Windows Services."* | Yes, run `net start MySQL80`. | **DO NOT** delete database tables. |
| **D** | **DSE Sync Button Times Out** | Click Cancel or let timeout expire. Show existing prices in table. | *"The public DSE exchange web server is currently off-market or rate-limiting. ShareSync gracefully preserves the last confirmed prices from the database."* | Yes, explain resilience. | **DO NOT** spam the sync button repeatedly. |
| **E** | **Chart Canvas Empty** | Resize browser window slightly or toggle timeframe button (`1M`, `ALL`). | *"Chart.js recalculates canvas layout based on viewport dimensions; toggling the timeframe re-renders the canvas."* | Yes, toggle timeframe pill. | **DO NOT** modify JavaScript files. |
| **F** | **Button Unresponsive** | Check browser console (`F12`) for modal overlay backdrop blocking click. | *"The modal backdrop is pending; pressing Escape or clicking outside restores standard focus."* | Yes, press `Escape`. | **DO NOT** refresh during a multi-step modal. |
| **G** | **FX Rate Missing** | Portfolio 4 shows BDT conversion. Explain fallback mechanism. | *"ShareSync loads active rates from the `currency_exchange_rates` table; if unavailable, it defaults to parity 1.0 with a logged audit warning."* | Yes, explain design. | **DO NOT** attempt to alter database rate tables. |
| **H** | **Application Starts Slowly** | Allow .NET JIT compiler 5–10 seconds on first request. | *"ASP.NET Core compiles runtime execution trees and warms EF Core DbContext connection pools on the first incoming request."* | Yes, wait 5 seconds. | **DO NOT** terminate the process prematurely. |

---

## 8. Final Pre-Demonstration Checklist

Complete these verification steps 15 minutes before the evaluation:
- [ ] **Git Working Tree:** Run `git status` &rarr; Verify clean tree on branch `mysql-version`.
- [ ] **Oracle Branch Preserved:** Confirm `main` is untouched.
- [ ] **MySQL Service Active:** Verify MySQL 8.0 is running on port 3306.
- [ ] **Table Count:** `USE sharesync; SHOW TABLES;` displays exactly 17 tables.
- [ ] **Automated Tests:** Execute `dotnet test` &rarr; Confirms **493 Passed, 0 Failed, 0 Skipped**.
- [ ] **Syntax Checks:** `node -c frontend/js/app.js` and `node -c frontend/js/investment-intelligence.js` return 0 errors.
- [ ] **Server Running:** Execute `dotnet run --project src/ShareSync.Web` listening on `http://localhost:5000`.
- [ ] **Browser Window:** Clean Incognito window opened at `http://localhost:5000/login.html` with Dark Theme active.
- [ ] **Credentials Ready:** `investor@sharesync.com` / `Password123#`.
- [ ] **MySQL Terminal:** Terminal or MySQL Workbench open in background ready for SQL walkthrough.
