# ShareSync — Teacher Viva & Oral Examination Cheat Sheet
## Concise 10-Second & 30-Second Answers for Technical Evaluation

**Project:** ShareSync — Share Market Portfolio Tracker  
**Branch:** `mysql-version`  
**Candidate:** Md. Tanvir Anjum  
**Format:**  
- **10-Second Answer:** Quick, confident summary for fast-paced Q&A.  
- **30-Second Answer:** Deeper architectural/mathematical elaboration if pressed.  
- **Where to Show in Code:** Exact source file path.  
- **Key Search Term:** String to search with `Ctrl+Shift+F`.

---

## 1. Project & Requirements

### Q: What is ShareSync?
- **10-Second Answer:** ShareSync is a professional multi-portfolio equity tracking and risk analytics platform built with ASP.NET Core, MySQL 8.0, and modern vanilla JavaScript.
- **30-Second Answer:** It automates real-time portfolio valuation, cost basis tracking, realized and unrealized gain/loss calculations, multi-currency conversion, benchmark outperformance against the Dhaka Stock Exchange (DSEX), and risk metrics like volatility and maximum drawdown.
- **Where to Show in Code:** `README.md`, `src/ShareSync.Web/Program.cs`
- **Key Search Term:** `"ShareSync: Share Market Portfolio Tracker"`

### Q: What problem does it solve?
- **10-Second Answer:** It replaces inaccurate manual spreadsheets with an automated, transactional double-entry ledger that guarantees mathematical precision.
- **30-Second Answer:** Retail investors struggle with tracking multiple brokerage accounts, calculating weighted average purchase prices across multiple buy orders, computing tax-compliant realized gains on partial sells, and adjusting returns for currency exchange and high domestic inflation. ShareSync solves all of these automatically.
- **Where to Show in Code:** `docs/FINAL_PROJECT_FREEZE_REPORT.md`
- **Key Search Term:** `"financial reconciliation"`

### Q: Why did you choose this project?
- **10-Second Answer:** It provides a rich domain to demonstrate relational database normalization, ACID transactions, financial mathematics, and full-stack engineering.
- **30-Second Answer:** Financial portfolios present classic database engineering challenges: strict referential integrity, historical time-series ledgers, non-commutative operations like weighted average cost, and multi-table reporting where a single rounding error breaks balance sheet consistency.
- **Where to Show in Code:** `database/mysql/01_schema.sql`
- **Key Search Term:** `"vw_portfolio_holdings"`

### Q: Who is the target user?
- **10-Second Answer:** Active individual investors and wealth managers tracking multiple equity portfolios across local and foreign markets.
- **30-Second Answer:** The platform supports both retail investors who want automated dividend and goal tracking, and sophisticated investors seeking institutional risk analytics like Sharpe-like volatility, beta, and benchmark alpha comparisons.
- **Where to Show in Code:** `src/ShareSync.Application/Services/PortfolioService.cs`
- **Key Search Term:** `"GetPortfoliosByUserIdAsync"`

### Q: Is this a real trading system that executes live market orders?
- **10-Second Answer:** No, it is an investment tracking, accounting, and analytical system, not a licensed broker execution gateway.
- **30-Second Answer:** ShareSync does not send FIX protocol orders to brokerage clearing houses. It records confirmed trade executions, analyzes holdings, and tracks performance. We also include an isolated paper-trading simulator for hypothetical order modeling.
- **Where to Show in Code:** `src/ShareSync.Application/Services/SimulatorService.cs`
- **Key Search Term:** `"SimulatorService"`

### Q: What is the scope of the project?
- **10-Second Answer:** Complete full-stack system: 17 MySQL tables, .NET 8 backend, 493 automated tests, and 17 interactive web interfaces.
- **30-Second Answer:** Scope includes user authentication, role-based access control, multi-portfolio management, transactional order ledgers, dividend accounting, watchlists, financial goal tracking, DSEX benchmark comparisons, Fisher inflation adjustments, multi-currency FX conversion, and PDF/CSV report exports.
- **Where to Show in Code:** `docs/FEATURES.md`
- **Key Search Term:** `"Features Overview"`

---

## 2. Database Engineering & MySQL

### Q: Why MySQL instead of NoSQL (e.g., MongoDB)?
- **10-Second Answer:** Financial ledgers require strict ACID guarantees and foreign key constraints to prevent orphaned records or balance sheet corruption.
- **30-Second Answer:** In MongoDB, documents are denormalized. If an investor updates a transaction or a company changes tickers, data can become desynchronized across documents. MySQL's InnoDB engine enforces relational constraints, row-level locking, and transactional rollbacks, guaranteeing data integrity.
- **Where to Show in Code:** `database/mysql/01_schema.sql`
- **Key Search Term:** `"ENGINE=InnoDB"`

### Q: Why normalization? Explain 1NF, 2NF, and 3NF.
- **10-Second Answer:** Normalization eliminates redundant data storage and prevents insertion, update, and deletion anomalies.
- **30-Second Answer:**
  - **1NF:** Every column contains atomic values (no comma-separated ticker lists) and each table has a primary key.
  - **2NF:** 1NF + all non-key columns depend on the whole primary key. In `watchlist_items(watchlist_id, company_id)`, `target_price` depends on both keys.
  - **3NF:** 2NF + no transitive dependencies. In `companies`, we store `sector_id` rather than duplicating sector descriptions.
- **Where to Show in Code:** `database/mysql/01_schema.sql`
- **Key Search Term:** `"CREATE TABLE companies"`

### Q: Why do you have Foreign Keys with ON DELETE RESTRICT?
- **10-Second Answer:** To prevent accidental deletion of parent records that would leave orphaned transactional records.
- **30-Second Answer:** A user cannot delete a company or a portfolio that has recorded transactions (`ON DELETE RESTRICT`). This prevents irreversible corruption of historical transaction records and ensures audit trails remain intact.
- **Where to Show in Code:** `database/mysql/01_schema.sql`
- **Key Search Term:** `"ON DELETE RESTRICT"`

### Q: Why does `watchlist_items` have a Composite Primary Key?
- **10-Second Answer:** To naturally enforce that a stock can only be added once per watchlist without needing a synthetic ID.
- **30-Second Answer:** The composite key `(watchlist_id, company_id)` declaratively guarantees uniqueness at the database engine level. Inserting the same stock twice into the same watchlist triggers a duplicate key violation.
- **Where to Show in Code:** `database/mysql/01_schema.sql`
- **Key Search Term:** `"CONSTRAINT pk_watchlist_items PRIMARY KEY (watchlist_id, company_id)"`

### Q: What is the purpose of Indexes in your database?
- **10-Second Answer:** Indexes prevent slow full-table scans on frequently filtered and sorted columns.
- **30-Second Answer:** We established composite indexes: `idx_transactions_portfolio_date` on `(portfolio_id, transaction_date)` for fast ledger retrieval, and `idx_price_hist_comp_date` on `(company_id, recorded_at)` for instant charting.
- **Where to Show in Code:** `database/mysql/01_schema.sql`
- **Key Search Term:** `"idx_transactions_portfolio_date"`

### Q: What is a Database View and why did you use `vw_portfolio_holdings`?
- **10-Second Answer:** A view is a stored virtual query that encapsulates complex aggregations without duplicating underlying data.
- **30-Second Answer:** `vw_portfolio_holdings` dynamically computes net shares (`BUY - SELL`), market values, weighted average buy costs, and unrealized gains across all transactions. This ensures all parts of the application share a single source of truth for holdings.
- **Where to Show in Code:** `database/mysql/01_schema.sql` (Line 569)
- **Key Search Term:** `"CREATE OR REPLACE VIEW vw_portfolio_holdings"`

### Q: What is the difference between a Stored Procedure and a Stored Function?
- **10-Second Answer:** A function returns a single scalar value and can be called inside SQL queries; a procedure executes business logic and can perform multiple modifications.
- **30-Second Answer:**
  - `fn_get_weighted_avg_price` is a deterministic function called inside queries like `SELECT fn_get_weighted_avg_price(1, 2);`.
  - `sp_record_transaction` is a procedure that validates whole-share inputs, checks for overselling, and atomically inserts the order.
- **Where to Show in Code:** `database/mysql/04_functions.sql`, `05_procedures.sql`
- **Key Search Term:** `"CREATE FUNCTION fn_get_weighted_avg_price"`, `"CREATE PROCEDURE sp_record_transaction"`

### Q: Where and why are Triggers used?
- **10-Second Answer:** Triggers automatically record an immutable audit log whenever a transaction is inserted, updated, or deleted.
- **30-Second Answer:** `trg_transactions_after_insert`, `update`, and `delete` write directly to `transaction_audit`. Because triggers run inside MySQL, audit records are created even if a developer or DBA executes a query outside the application.
- **Where to Show in Code:** `database/mysql/06_triggers.sql`
- **Key Search Term:** `"CREATE TRIGGER trg_transactions_after_insert"`

---

## 3. Application Architecture

### Q: Why ASP.NET Core 8?
- **10-Second Answer:** High-performance, cross-platform enterprise framework with built-in dependency injection, middleware, and strong typing.
- **30-Second Answer:** ASP.NET Core provides asynchronous I/O, compile-time safety, seamless integration with Entity Framework Core, structured logging, and robust security primitives out of the box.
- **Where to Show in Code:** `src/ShareSync.Web/Program.cs`
- **Key Search Term:** `"var builder = WebApplication.CreateBuilder"`

### Q: Explain the Clean Architecture layers.
- **10-Second Answer:**
  - **Domain:** Core entities and domain rules (no external dependencies).
  - **Application:** Business logic, service contracts, and DTOs.
  - **Infrastructure:** Database access (EF Core), external APIs (DSE scraper), and security implementations.
  - **Web:** API controllers, authentication filters, and frontend assets.
- **30-Second Answer:** This separation of concerns adheres to the Dependency Inversion Principle. The business logic in `ShareSync.Application` does not depend on MySQL or HTTP controllers, allowing services to be tested independently in memory without a live database.
- **Where to Show in Code:** Solution Explorer / `docs/ARCHITECTURE.md`
- **Key Search Term:** `"ShareSync.Domain"`, `"ShareSync.Application"`

### Q: Why Entity Framework Core with the Pomelo MySQL provider?
- **10-Second Answer:** Pomelo translates LINQ queries into optimized MySQL 8.0 SQL syntax while providing connection pooling and change tracking.
- **30-Second Answer:** EF Core eliminates manual ADO.NET boilerplate and SQL string concatenation. Pomelo is the leading open-source provider for MySQL, supporting MySQL 8 features like window functions, CTEs, and datetime precision mapping.
- **Where to Show in Code:** `src/ShareSync.Web/Program.cs`
- **Key Search Term:** `"UseMySql"`

---

## 4. Authentication, Authorization & Security

### Q: How does Authentication work?
- **10-Second Answer:** Passwords are verified using PBKDF2 with HMAC-SHA256 and salted hashes, issuing a signed JWT Bearer token upon success.
- **30-Second Answer:** When the user logs in via `POST /api/auth/login`, `AuthService` extracts the salt from the stored hash in `app_users`, computes the PBKDF2 hash of the supplied password, compares them in constant time, and generates a JWT token containing user ID, email, and role claims.
- **Where to Show in Code:** `src/ShareSync.Application/Services/AuthService.cs`
- **Key Search Term:** `"VerifyPassword"`, `"GenerateJwtToken"`

### Q: How is Authorization enforced? How do you prevent horizontal privilege escalation?
- **10-Second Answer:** Controllers use `[Authorize]` and verify that the target portfolio belongs to the authenticated user ID extracted from JWT claims.
- **30-Second Answer:** Every service method (e.g., `PortfolioService.GetPortfolioByIdAsync`) compares `portfolio.UserId` against the requesting user's ID. If User #2 attempts to access User #1's portfolio ID, the system rejects the request with HTTP 403 Forbidden or 404 Not Found.
- **Where to Show in Code:** `src/ShareSync.Web/Controllers/PortfoliosController.cs`
- **Key Search Term:** `"GetUserId()"`, `"portfolio.UserId != userId"`

### Q: How is SQL Injection prevented?
- **10-Second Answer:** EF Core uses parameterized SQL commands exclusively; user inputs are passed as parameters, never concatenated into SQL strings.
- **30-Second Answer:** Even when raw SQL or stored procedures are invoked (e.g., in reporting or migration scripts), parameters use typed `MySqlParameter` instances. User input cannot break out of data literals to execute arbitrary SQL commands.
- **Where to Show in Code:** `src/ShareSync.Infrastructure/Data/ShareSyncDbContext.cs`
- **Key Search Term:** `"FromSqlInterpolated"`, `"DbSet<"`

---

## 5. Holdings & Financial Calculations

### Q: How is current holding quantity calculated?
- **10-Second Answer:** Cumulative BUY quantity minus cumulative SELL quantity for that company within the portfolio.
- **30-Second Answer:**
  $$\text{Current Quantity} = \sum_{\text{BUY}} \text{quantity} - \sum_{\text{SELL}} \text{quantity}$$
  If the sum is 0, the position is closed. Negative quantities are prohibited by oversell validation.
- **Where to Show in Code:** `src/ShareSync.Application/Services/PortfolioService.cs` (Line 324)
- **Key Search Term:** `"CalculateHoldingSummary"`

### Q: How is Weighted Average Buy Price calculated? Why not simple average?
- **10-Second Answer:** Total money spent on BUY orders divided by total shares bought: $\frac{\sum(Q_{\text{buy}} \times P_{\text{buy}})}{\sum Q_{\text{buy}}}$.
- **30-Second Answer:** A simple average treats 10 shares bought at ৳100 the same as 1,000 shares bought at ৳200. Weighted average accounts for order size, providing the true cost basis per share as required by tax and accounting standards.
- **Where to Show in Code:** `src/ShareSync.Application/Services/PortfolioService.cs`
- **Key Search Term:** `"totalBuyCost / totalBuyQuantity"`

### Q: What is the difference between Realized and Unrealized P/L?
- **10-Second Answer:** Unrealized P/L is "paper gain" on open positions; Realized P/L is cash profit locked in from executed SELL orders.
- **30-Second Answer:**
  - $\text{Unrealized P/L} = \text{Current Shares} \times (\text{Current Price} - \text{Weighted Average Buy Price})$
  - $\text{Realized P/L} = (\text{Sold Shares} \times \text{Sale Price} - \text{Fees}) - (\text{Sold Shares} \times \text{Weighted Average Buy Price})$
- **Where to Show in Code:** `src/ShareSync.Application/Services/TransactionService.cs`
- **Key Search Term:** `"CalculateRealizedProfitLossAsync"`

### Q: How do you prevent Overselling (selling shares you don't own)?
- **10-Second Answer:** The backend and database verify that current held shares are greater than or equal to the requested sell quantity before executing.
- **30-Second Answer:** In both `TransactionService.cs` and the stored procedure `sp_record_transaction`, the system queries existing BUYs minus existing SELLs. If `requested_quantity > available_shares`, the transaction is aborted with an `OVERSELL_REJECTED` error.
- **Where to Show in Code:** `src/ShareSync.Application/Services/TransactionService.cs` (Line 150), `database/mysql/05_procedures.sql` (Line 124)
- **Key Search Term:** `"Cannot sell more shares than currently owned"`, `"OVERSELL_REJECTED"`

### Q: How are Transaction Fees and Net Cash Flow handled?
- **10-Second Answer:** Fees increase cash outflow on BUY orders and decrease net proceeds on SELL orders.
- **30-Second Answer:**
  - $\text{BUY Cash Outflow} = (\text{Quantity} \times \text{Price}) + \text{Fee}$
  - $\text{SELL Cash Inflow} = (\text{Quantity} \times \text{Price}) - \text{Fee}$
  - $\text{Net Cash Flow} = \sum \text{SELL Inflows} - \sum \text{BUY Outflows}$
- **Where to Show in Code:** `src/ShareSync.Application/Services/TransactionService.cs`
- **Key Search Term:** `"NetCashFlow"`

### Q: How is CAGR calculated?
- **10-Second Answer:**
  $$\text{CAGR} = \left(\frac{V_{\text{final}}}{V_{\text{initial}}}\right)^{1/\text{years}} - 1$$
- **30-Second Answer:** It measures the constant annual growth rate that would compound an initial investment to its final value over multiple years, dampening short-term volatility spikes.
- **Where to Show in Code:** `src/ShareSync.Application/Services/AnalyticsService.cs` (Line 250)
- **Key Search Term:** `"Math.Pow((double)(endingValue / beginningValue)"`

### Q: How is Volatility and Maximum Drawdown calculated?
- **10-Second Answer:** Annualized volatility is daily return standard deviation multiplied by $\sqrt{252}$; Max Drawdown is the largest percentage drop from a historical peak.
- **30-Second Answer:**
  - $\sigma_{\text{annual}} = \text{StdDev}(\text{Daily Returns}) \times \sqrt{252}$
  - $\text{MDD} = \min_{t} \left(\frac{V_t - \text{Peak}_t}{\text{Peak}_t}\right)$
- **Where to Show in Code:** `src/ShareSync.Application/Services/AnalyticsService.cs`
- **Key Search Term:** `"CalculateAnnualizedVolatility"`, `"CalculateMaxDrawdown"`

### Q: How does Inflation-Adjusted Return work?
- **10-Second Answer:** We apply the Fisher Equation using annual inflation rates from the `inflation_rates` table.
- **30-Second Answer:**
  $$\text{Real Return} = \left[ \frac{1 + \frac{R_{\text{nominal}}}{100}}{1 + \frac{i}{100}} - 1 \right] \times 100$$
  This converts nominal financial gains into actual purchasing power increases.
- **Where to Show in Code:** `src/ShareSync.Application/Services/InflationService.cs`, `database/mysql/04_functions.sql`
- **Key Search Term:** `"fn_calculate_real_return"`, `"GetRealReturnAsync"`

---

## 6. Multi-Currency (FX) Conversion

### Q: How can a portfolio hold both USD and BDT assets?
- **10-Second Answer:** Each asset stores its native trading currency; the valuation engine converts foreign assets into the portfolio's base currency using exchange rates.
- **30-Second Answer:** Foreign assets (like AAPL or MSFT in USD) are valued by multiplying shares by the foreign price and the active exchange rate in `currency_exchange_rates`. If 1 USD = 121.50 BDT, a $100 stock holding is recorded as ৳12,150.00.
- **Where to Show in Code:** `src/ShareSync.Application/Services/CurrencyService.cs`
- **Key Search Term:** `"ConvertAmountAsync"`, `"currency_exchange_rates"`

### Q: Why not simply add USD and BDT as 1:1?
- **10-Second Answer:** That would corrupt the portfolio's valuation by treating 1 US Dollar as equal to 1 Bangladeshi Taka.
- **30-Second Answer:** In Portfolio #4 ("Global & Multi-Asset Portfolio"), adding 100 USD shares at $185 without conversion would result in ৳18,500 instead of the true value of ৳2,247,750 (at 121.50 BDT/USD). ShareSync prevents this currency distortion.
- **Where to Show in Code:** `src/ShareSync.Application/Services/ReportService.cs`
- **Key Search Term:** `"exchangeRate"`

---

## 7. Market Data & DSE Integration

### Q: Where does company and price data come from?
- **10-Second Answer:** From the `companies` table, kept updated via the `DsePriceService` market scraper.
- **30-Second Answer:** `DsePriceService` fetches current price and trading volume data from Dhaka Stock Exchange endpoints. If the external portal is offline, the service falls back to confirmed prices in `companies`.
- **Where to Show in Code:** `src/ShareSync.Infrastructure/Services/DsePriceService.cs`
- **Key Search Term:** `"DsePriceService"`, `"FallbackToDatabasePrices"`

### Q: What are the limitations of the historical price data?
- **10-Second Answer:** Historical prices are daily closing mock series modeled on realistic DSE trading ranges, not continuous real-time tick feeds.
- **30-Second Answer:** The dataset simulates multi-year daily closing series to test analytics calculations (CAGR, SMA50/SMA200, volatility) without incurring commercial exchange data licensing fees.
- **Where to Show in Code:** `docs/HISTORICAL_DATA_QUALITY.md`
- **Key Search Term:** `"Historical Data Limitations"`

---

## 8. Investment Intelligence Engine

### Q: Is Investment Intelligence using Machine Learning or Statistical Analysis?
- **10-Second Answer:** It is an objective quantitative multi-factor statistical model, not a black-box neural network.
- **30-Second Answer:** It scores equities from 0 to 100 across four institutional factors (25% weight each): Fundamental Quality (P/E, ROE), Technical Trend (SMA50 vs SMA200 golden cross), Volatility Risk (historical beta/variance), and Dividend Yield.
- **Where to Show in Code:** `src/ShareSync.Application/Services/InvestmentIntelligenceService.cs`
- **Key Search Term:** `"CalculateCompositeScoreAsync"`

### Q: Can Investment Intelligence guarantee future profits?
- **10-Second Answer:** Absolutely not. Past performance does not guarantee future results.
- **30-Second Answer:** The system clearly displays compliance disclaimers explaining that scores represent retrospective mathematical analysis of published financial ratios and price history, not guaranteed predictive investment advice.
- **Where to Show in Code:** `frontend/js/investment-intelligence.js`
- **Key Search Term:** `"disclaimer"`

---

## 9. Automated Testing & Verification

### Q: Why do you have automated tests? What does 493/493 mean?
- **10-Second Answer:** All 493 unit and integration tests execute and pass with zero failures on every build.
- **30-Second Answer:** Testing guarantees that changes to the frontend or reporting views cannot break financial calculations, oversell guards, or multi-currency conversions. The suite covers domain math, database queries, and API authorization controllers.
- **Where to Show in Code:** `tests/ShareSync.Tests/`
- **Key Search Term:** `"FinancialCalculationConsistencyTests"`

---

## 10. Emergency / Tough Viva Questions

### Q: Why calculate holdings dynamically from transactions instead of storing a running balance column?
- **10-Second Answer:** Storing a mutable balance invites synchronization errors; calculating from an immutable ledger guarantees an audit trail.
- **30-Second Answer:** In accounting, ledgers are immutable. If someone edits or voids a transaction in a system that stores a static `balance` column, the balance drifts. By computing holdings as $\sum \text{BUY} - \sum \text{SELL}$, the balance is mathematically guaranteed to match the transaction history.
- **Where to Show in Code:** `database/mysql/01_schema.sql` (View `vw_portfolio_holdings`)
- **Key Search Term:** `"vw_portfolio_holdings"`

### Q: What is ACID and where do you demonstrate Atomicity?
- **10-Second Answer:** Atomicity, Consistency, Isolation, Durability. Demonstrated in `sp_record_transaction` and EF Core transactions.
- **30-Second Answer:** When inserting a transaction, the insert, audit trigger, and snapshot generation must all succeed together. If any check fails, the entire transaction rolls back via `ROLLBACK`, leaving no partial records.
- **Where to Show in Code:** `database/mysql/05_procedures.sql`
- **Key Search Term:** `"LEAVE proc_label"`

### Q: What is the biggest limitation of your application?
- **10-Second Answer:** Lack of real-time broker execution and dependence on external exchange web availability for live scraping.
- **30-Second Answer:** Since retail broker APIs are not publicly available in Bangladesh, orders are recorded by the user rather than auto-synced from a CDS broker account, and market prices rely on polling rather than direct exchange web-sockets.
- **Where to Show in Code:** `docs/FINAL_PROJECT_FREEZE_REPORT.md`
- **Key Search Term:** `"Remaining limitations"`

### Q: What would you improve if given another semester?
- **10-Second Answer:** Add automated corporate action adjustments (stock splits, bonus shares), mobile push notifications, and live broker CSV statement auto-parsers.
- **30-Second Answer:** I would implement a background worker to automatically adjust cost basis on stock dividend/split announcements, integrate real-time exchange WebSocket streaming, and build an automated PDF bank statement extractor.
