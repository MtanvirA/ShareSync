# ShareSync — Teacher Demonstration Runbook
## Structured 15–20 Minute Presentation Guide for Evaluators & Instructors

**Project:** ShareSync — Share Market Portfolio Tracker  
**Branch:** `mysql-version`  
**Target Audience:** Database Systems & Web Engineering Course Evaluator / Teacher  
**Presenter:** Md. Tanvir Anjum  
**Live Demo URL:** `http://localhost:5000`  
**Demo Account:** `investor@sharesync.com` / `Password123#` (User ID: 1, Role: `INVESTOR`)  
**Admin Account:** `admin@sharesync.com` / `Admin123#` (User ID: 3, Role: `ADMIN`)  

---

## Demonstration Overview & Timing Map

| Segment | Estimated Time | Focus Area | Key Visuals |
|---|---|---|---|
| **Phase 1: Problem & System Overview** | 2 mins | Project motivation, architecture, multi-portfolio concept | Landing page & Login |
| **Phase 2: Core Investor Workflow** | 5 mins | Dashboard, Portfolios, Holdings, Transactions, P/L | Dashboard, Portfolio #1 & #4, Holdings Table |
| **Phase 3: Financial Analytics & Intelligence** | 5 mins | Benchmark (DSEX), Inflation (Fisher Eq), Intelligence Score | Analytics & Intelligence views |
| **Phase 4: Reports & Auditing** | 3 mins | PDF/CSV exports, Holdings Reconciliation, Activity Trail | Reports & Audit views |
| **Phase 5: Under the Hood: Database & Code** | 4 mins | MySQL Workbench, Stored Routines, Triggers, EF Core | MySQL CLI/Workbench, VS Code/IDE |

---

## Step-by-Step Demonstration Sequence

### Step 1: System Introduction & Login
- **Page / URL:** `/login.html` (or root `/`)
- **What to Click:** Enter `investor@sharesync.com` / `Password123#` -> Click **Sign In**.
- **What Teacher Sees:** Instant transition to Dashboard, welcome banner for "Tanvir Anjum", active session indicator.
- **Feature Demonstrated:** Authentication & Session Management (JWT Bearer Token + Role Claims).
- **Database Concept:** Secure password verification using PBKDF2 with HMAC-SHA256 and cryptographic salt stored in `app_users(password_hash)`.
- **One-Sentence Explanation:** *"ShareSync secures investor financial data by enforcing PBKDF2 password hashing and stateless JWT bearer authentication before granting access to multi-portfolio records."*
- **Deeper Explanation if Asked:** On login, `AuthService.LoginAsync` fetches the user by normalized email, validates the password hash via salt comparison, and generates a digitally signed JWT token carrying claims for `UserId=1` and `Role=INVESTOR`. The frontend stores this token in `localStorage` and injects it into every API request header via `Authorization: Bearer <token>`.

---

### Step 2: Consolidated Investor Dashboard
- **Page / URL:** `/index.html` (Dashboard)
- **What to Click:** Observe the 4 KPI metric cards at the top and the portfolio distribution chart.
- **What Teacher Sees:**
  - Total Portfolio Value: **৳4,869,720.00**
  - Total Invested Amount: **৳4,648,956.71**
  - Unrealized Gain/Loss: **+৳220,763.29 (+4.75%)**
  - Total Dividends: **৳34,500.00**
  - Breakdown across 4 active portfolios.
- **Feature Demonstrated:** Real-time multi-portfolio financial aggregation.
- **Database Concept:** Relational aggregation across multiple portfolios, transactions, and foreign exchange rates using optimized SQL queries and LINQ grouping.
- **One-Sentence Explanation:** *"The dashboard aggregates live valuations and profit metrics across all of the investor's sub-portfolios without requiring manual spreadsheet calculations."*
- **Deeper Explanation if Asked:** In `DashboardService.GetDashboardSummaryAsync`, the server retrieves all active portfolios for User #1, calculates current holdings from transaction records, maps mark-to-market prices from `companies`, converts foreign currencies using `currency_exchange_rates`, and produces an aggregated mark-to-market summary with mathematical consistency.

---

### Step 3: Multi-Portfolio Structure
- **Page / URL:** `/portfolio.html`
- **What to Click:** Click **Portfolios** in navbar. Show the list of 4 distinct portfolios:
  1. Main Investment Portfolio (BDT) — ৳797,830.00
  2. Long-Term Growth Portfolio (BDT) — ৳640,680.00
  3. Dividend Income Portfolio (BDT) — ৳629,860.00
  4. Global & Multi-Asset Portfolio (BDT) — ৳2,801,350.00
- **What Teacher Sees:** Clean card layout showing allocation, cash balances, and risk labels for each portfolio.
- **Feature Demonstrated:** Segregated portfolio strategy (1 User : N Portfolios).
- **Database Concept:** 1:N Foreign Key relationship between `app_users(user_id)` and `portfolios(user_id)` with user ownership validation.
- **One-Sentence Explanation:** *"ShareSync allows an investor to separate conservative dividend holdings from aggressive growth or international equities under distinct portfolio entities."*
- **Deeper Explanation if Asked:** Each portfolio is a standalone relational entity in `portfolios`. All child transactions, snapshots, and goals reference `portfolio_id`. The backend enforces horizontal isolation in `PortfolioService` by verifying `portfolio.UserId == currentUserId` before returning or modifying any data.

---

### Step 4: Holdings & Weighted Average Buy Price
- **Page / URL:** `/portfolio.html` -> Click on **Main Investment Portfolio** -> **Holdings Tab**
- **What to Click:** Point out holding rows for **GP** (Grameenphone) and **SQURPHARMA** (Square Pharmaceuticals).
- **What Teacher Sees:** Ticker, Company Name, Current Shares, Current Price, Weighted Average Buy Price, Total Market Value, Unrealized P/L.
- **Feature Demonstrated:** Automated Volume-Weighted Average Price (VWAP) cost basis calculation.
- **Database Concept:** Database View `vw_portfolio_holdings` and Stored Function `fn_get_weighted_avg_price`.
- **One-Sentence Explanation:** *"Holdings and average purchase costs are calculated strictly from transactional order history rather than being stored as error-prone static balances."*
- **Deeper Explanation if Asked:**
  $$\text{Weighted Average Buy Price} = \frac{\sum (\text{BUY Quantity} \times \text{Price})}{\sum \text{BUY Quantity}}$$
  When an investor buys 500 shares at ৳320 and 500 shares at ৳340, the average cost basis is exactly ৳330. Subsequent SELL orders do not alter this historical buy average, perfectly adhering to tax and accounting standards.

---

### Step 5: BUY / SELL Transactions & Realized P/L
- **Page / URL:** `/transactions.html`
- **What to Click:** View the chronological transaction ledger. Filter by **SELL** orders to show closed positions.
- **What Teacher Sees:** Table with Transaction ID, Date, Portfolio, Ticker, Type (BUY/SELL), Quantity, Execution Price, Broker Fee, and Realized P/L.
- **Feature Demonstrated:** Realized Profit/Loss (LF-26) and Brokerage Fee Tracking (LF-27).
- **Database Concept:** Immutable transaction ledger, Stored Procedure `sp_record_transaction`, and Stored Function `fn_calculate_realized_pl`.
- **One-Sentence Explanation:** *"When a position is liquidated, ShareSync computes the realized gain by subtracting the volume-weighted buy cost and transaction fees from the gross sale proceeds."*
- **Deeper Explanation if Asked:**
  $$\text{Realized P/L} = (\text{Sell Qty} \times \text{Sell Price} - \text{Sell Fee}) - (\text{Sell Qty} \times \text{Weighted Avg Buy Price})$$
  The database procedure `sp_record_transaction` validates whole-share integers, ensures positive prices, checks against overselling (`requested_qty > available_shares`), and writes to `transactions` within an atomic transaction.

---

### Step 6: Multi-Currency & Foreign Asset Handling
- **Page / URL:** `/portfolio.html` -> Click **Global & Multi-Asset Portfolio** (Portfolio #4)
- **What to Click:** Point to holdings for **AAPL** (Apple Inc), **MSFT** (Microsoft), and **SPY** (S&P 500 ETF).
- **What Teacher Sees:**
  - Foreign Asset Currency: **USD**
  - Traded Price: e.g., $185.00
  - Converted Base Valuation in **BDT** using current FX exchange rate (1 USD = 121.50 BDT).
  - Total Portfolio Value: **৳2,801,350.00**.
- **Feature Demonstrated:** Multi-Asset and Multi-Currency valuation (LF-29).
- **Database Concept:** `currency_exchange_rates` reference table with currency pair indexing `(from_currency, to_currency)`.
- **One-Sentence Explanation:** *"Foreign dollar-denominated assets are converted to the portfolio's base currency using database-backed FX rates so that portfolio totals remain financially accurate."*
- **Deeper Explanation if Asked:** Many systems naively sum USD and BDT as 1:1, producing nonsense metrics. ShareSync stores the native asset currency in `companies.currency` and applies FX conversion in `CurrencyService` and `ReportService`:
  $$\text{Holding Value (BDT)} = \text{Shares} \times \text{USD Price} \times \text{FX Rate (USD}\to\text{BDT)}$$

---

### Step 7: Dividend Tracking & Income Yield
- **Page / URL:** `/dividends.html`
- **What to Click:** Click **Dividends** in navbar. View the dividend timeline and distribution chart.
- **What Teacher Sees:** 11 recorded dividend payouts totaling **৳34,500.00**, payment dates, cash amounts, per-share dividend yields, and portfolio associations.
- **Feature Demonstrated:** Corporate action and cash dividend tracking.
- **Database Concept:** `dividends` table with foreign keys referencing `portfolios` and `companies`, maintaining referential integrity.
- **One-Sentence Explanation:** *"ShareSync records cash dividend distributions, enabling investors to evaluate passive dividend yield independently from capital appreciation."*
- **Deeper Explanation if Asked:** `DividendService` links corporate distribution announcements to investor portfolios. Total return calculations integrate both capital gains and cumulative cash dividends:
  $$\text{Total Return} = \frac{\text{Current Value} + \text{Realized P/L} + \text{Dividends} - \text{Net Invested}}{\text{Net Invested}} \times 100$$

---

### Step 8: Watchlists & Target Price Alerts
- **Page / URL:** `/watchlist.html`
- **What to Click:** View the 4 thematic watchlists (e.g., "Blue Chips", "DSE Top 30", "Tech Stocks").
- **What Teacher Sees:** Tracked companies, target entry prices, current price delta, and alert triggers.
- **Feature Demonstrated:** Investment monitoring and price tracking.
- **Database Concept:** Composite Primary Key on `watchlist_items(watchlist_id, company_id)` preventing duplicate tracking.
- **One-Sentence Explanation:** *"Watchlists use composite primary keys to allow investors to track target buy levels across multiple categories without redundant rows."*
- **Deeper Explanation if Asked:** If a student asks why we didn't use an artificial surrogate `id` on `watchlist_items`, point out that `(watchlist_id, company_id)` is a natural candidate key that declaratively guarantees a stock cannot be added twice to the same list at the database level.

---

### Step 9: Financial Goals & Progress Tracking
- **Page / URL:** `/goals.html`
- **What to Click:** Click **Goals** in navbar.
- **What Teacher Sees:** 5 financial goals (e.g., "Retirement Corpus", "Emergency Fund", "House Down Payment"), target amounts, target dates, progress bars, and on-track status.
- **Feature Demonstrated:** Goal-based investing and progress tracking.
- **Database Concept:** `portfolio_goals` table linking target amounts to live portfolio valuations.
- **One-Sentence Explanation:** *"Goals track an investor's milestone progress by comparing current portfolio mark-to-market balances against user-defined financial targets."*
- **Deeper Explanation if Asked:** In `PortfolioGoalService`, current milestone progress is dynamically evaluated by fetching the portfolio's live valuation and computing the completion percentage:
  $$\text{Progress \%} = \min\left(100.0, \frac{\text{Current Portfolio Value}}{\text{Target Amount}} \times 100\right)$$

---

### Step 10: Advanced Analytics: Volatility, Drawdown & CAGR
- **Page / URL:** `/analytics.html`
- **What to Click:** Select **Main Investment Portfolio** from the portfolio dropdown.
- **What Teacher Sees:**
  - CAGR (Compound Annual Growth Rate): **+14.2%**
  - Annualized Volatility: **16.8%**
  - Maximum Drawdown (MDD): **-8.4%**
  - 50-day and 200-day Simple Moving Averages (SMA50 / SMA200) trend lines.
- **Feature Demonstrated:** Quantitative risk and return analytics.
- **Database Concept:** Time-series aggregation over `portfolio_snapshots` and `company_price_history`.
- **One-Sentence Explanation:** *"ShareSync evaluates risk-adjusted performance using standard institutional financial formulas including CAGR, annualized volatility, and peak-to-trough drawdown."*
- **Deeper Explanation if Asked:**
  - **CAGR:** $\left(\frac{V_{\text{final}}}{V_{\text{initial}}}\right)^{1/\text{years}} - 1$
  - **Annualized Volatility:** $\sigma_{\text{daily}} \times \sqrt{252}$ (standard trading days per year).
  - **Maximum Drawdown:** $\min_{t} \left(\frac{V_t - \text{Peak}_t}{\text{Peak}_t}\right)$, measuring the worst peak-to-trough loss.

---

### Step 11: Benchmark Outperformance (DSEX Comparison)
- **Page / URL:** `/analytics.html` -> Scroll to **Benchmark Comparison**
- **What to Click:** Toggle between **DSEX**, **DS30**, and **DSES**.
- **What Teacher Sees:** Direct comparison chart between Portfolio Return (+18.4%) and DSEX Benchmark Return (+11.2%), displaying an Alpha of **+7.2%**.
- **Feature Demonstrated:** Benchmark relative performance comparison.
- **Database Concept:** `benchmark_price_history` table storing official exchange index closing levels indexed by `(benchmark_code, trading_date)`.
- **One-Sentence Explanation:** *"Investors can compare their portfolio returns directly against the Dhaka Stock Exchange benchmark (DSEX) to verify whether they generated true market alpha."*
- **Deeper Explanation if Asked:** `BenchmarkService` loads benchmark time-series from `benchmark_price_history`, normalizes both the portfolio and index returns to a common base date, and calculates relative outperformance ($\text{Alpha} = R_{\text{portfolio}} - R_{\text{benchmark}}$).

---

### Step 12: Inflation-Adjusted Real Return (Fisher Equation)
- **Page / URL:** `/analytics.html` -> Scroll to **Inflation Analysis**
- **What to Click:** View the Nominal vs. Real Return card.
- **What Teacher Sees:**
  - Nominal Return: **+18.40%**
  - Bangladesh Annual Inflation: **9.70%** (from `inflation_rates`)
  - Real Return: **+7.93%**
- **Feature Demonstrated:** Inflation-Adjusted Return (LF-28).
- **Database Concept:** `inflation_rates` table and Stored Function `fn_calculate_real_return`.
- **One-Sentence Explanation:** *"ShareSync uses the exact Fisher Equation to adjust nominal returns for Bangladesh inflation, revealing the investor's true purchasing power gain."*
- **Deeper Explanation if Asked:** Rather than using the naive approximation ($R_{\text{real}} \approx R_{\text{nom}} - i$), which diverges at high inflation, ShareSync executes the exact Fisher Equation:
  $$\text{Real Return} = \left[ \frac{1 + \frac{R_{\text{nominal}}}{100}}{1 + \frac{i}{100}} - 1 \right] \times 100$$
  Both the C# `InflationService` and the MySQL stored function `fn_calculate_real_return` produce identical results.

---

### Step 13: Investment Intelligence Engine
- **Page / URL:** `/investment-intelligence.html`
- **What to Click:** Click **Investment Intelligence** in navbar. View the composite scores for DSE equities.
- **What Teacher Sees:** Multi-factor analysis cards: Quality, Value, Momentum, and Volatility factors combined into an overall score (0–100), accompanied by honest risk disclaimers.
- **Feature Demonstrated:** Quantitative multi-factor equity scoring engine.
- **Database Concept:** Analytical synthesis of fundamental company metrics, historical closing prices, and sector averages.
- **One-Sentence Explanation:** *"Investment Intelligence provides objective multi-factor scoring based on published fundamentals and historical price trends, with clear disclaimers that it is not predictive financial advice."*
- **Deeper Explanation if Asked:** In `InvestmentIntelligenceService.cs`, scores are computed across 4 pillars (25% each):
  1. *Fundamental Quality:* P/E ratio, Debt/Equity, ROE vs sector.
  2. *Momentum:* Golden cross (SMA50 > SMA200), 30-day price momentum.
  3. *Risk:* Historical price volatility and max drawdown.
  4. *Yield:* Dividend payout track record.

---

### Step 14: DSE Live Scraper & Market Search
- **Page / URL:** `/companies.html` or `/dse-market.html`
- **What to Click:** Use global search or the company search bar to search for "BATBC", "BEXIMCO", or "BRACBANK".
- **What Teacher Sees:** Autocomplete modal showing ticker, full company name, sector, current price, and day change.
- **Feature Demonstrated:** Dhaka Stock Exchange market catalog and resilient price retrieval.
- **Database Concept:** `companies` table indexed by `ticker_symbol` and `sector_id`.
- **One-Sentence Explanation:** *"ShareSync indexes Dhaka Stock Exchange listed companies with full-text search and resilient fallback caching if the live exchange portal is offline."*
- **Deeper Explanation if Asked:** `DsePriceService` polls current market prices. If external exchange web endpoints are unreachable, the service gracefully falls back to the latest confirmed database prices in `companies`, ensuring the application remains 100% operational during market closures or network interruptions.

---

### Step 15: Financial Reports & Export Engine
- **Page / URL:** `/reports.html`
- **What to Click:** Select **Holdings Report** -> Click **Generate Report** -> Show preview and **Export CSV** / **Print PDF**.
- **What Teacher Sees:** Beautiful tabular report showing cost basis, market value, unrealized P/L, portfolio weightings, and reconciled summary row.
- **Feature Demonstrated:** Comprehensive financial reporting and export capabilities.
- **Database Concept:** Complex multi-table reporting joins with database view fallback in `ReportService`.
- **One-Sentence Explanation:** *"The reporting engine generates auditor-ready holdings, transaction, and dividend reports exportable to PDF and CSV formats."*
- **Deeper Explanation if Asked:** `ReportService` queries `vw_portfolio_holdings` and `transactions`, applies currency conversion, and guarantees that the report total matches the dashboard aggregate down to four decimal places before rendering.

---

### Step 16: "Show Me the Database" (SQL & Programmability)
- **Tool:** MySQL Workbench or Terminal running `mysql -u root -p sharesync`
- **What to Execute:**
  1. `SHOW TABLES;` (Show all 17 tables)
  2. `SELECT * FROM vw_portfolio_holdings LIMIT 5;` (Show SQL View)
  3. `SELECT fn_get_weighted_avg_price(1, 1);` (Show Stored Function)
  4. `SELECT * FROM transaction_audit ORDER BY audit_id DESC LIMIT 3;` (Show Trigger Output)
- **What Teacher Sees:** Direct proof of schema normalization, view execution, user-defined functions, and automatic trigger execution.
- **Feature Demonstrated:** Native Database Engine Programmability.
- **Database Concept:** Schema design, SQL Views, User-Defined Functions, and Audit Triggers.
- **One-Sentence Explanation:** *"Business logic and audit trails are enforced directly inside MySQL using views, stored functions, and triggers, ensuring data integrity independent of the web tier."*
- **Deeper Explanation if Asked:** Triggers on `transactions` (`trg_transactions_after_insert`, `update`, `delete`) write to `transaction_audit` without requiring manual intervention from application code. Even if a DBA executes a manual `INSERT` in MySQL CLI, the audit record is captured.

---

### Step 17: Automated Testing & Security Conclusion
- **Tool:** Terminal
- **What to Execute:** `dotnet test`
- **What Teacher Sees:** Test execution finishes: **493 Passed, 0 Failed, 0 Skipped**.
- **Feature Demonstrated:** Quality Assurance & Automated Test Harness.
- **Database Concept:** In-memory and integration test suites validating ACID compliance, rounding precision, and authorization gates.
- **One-Sentence Explanation:** *"The entire financial calculation engine, authorization rules, and edge cases are protected by 493 automated unit and integration tests."*
- **Deeper Explanation if Asked:** Tests in `ShareSync.Tests` cover:
  - Financial rounding accuracy (`FinancialCalculationConsistencyTests`)
  - Oversell prevention (`TransactionServiceTests`)
  - Multi-currency conversion (`CurrencyServiceTests`)
  - Role-based authorization (`AuthTests`)
  - In-memory database constraints.

---

## Teacher Time Management Tips
- If the teacher only has **10 minutes**: Focus on Steps 1, 2, 4, 5, 11, 16, and 17.
- If the teacher asks to see **SQL first**: Jump directly to Step 16, run the queries in Workbench, and then show how the web UI reflects those exact numbers.
- If the teacher asks about **Security**: Highlight Step 1 (PBKDF2/JWT) and Step 16 (Auditing triggers and SQL injection protection via EF Core parameters).
