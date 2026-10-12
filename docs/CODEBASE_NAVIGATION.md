# ShareSync MySQL — Codebase Source Navigation Guide

> **Academic Evaluation & Viva Reference Manual**  
> **Repository Target:** `mysql-version` (Dhaka Stock Exchange Portfolio Tracker)  
> **Audience:** Database Teachers, Web Programming Teachers, Viva Examiners, and Code Reviewers.

---

## Standardized Feature Tags (Instant `Ctrl + Shift + F` Navigation)

Every module in the ShareSync MySQL codebase contains standardized, searchable tags in C# XML docs, frontend JavaScript, and MySQL SQL scripts. Press **`Ctrl + Shift + F`** in your IDE and search any of these exact tags:

| # | Standardized Feature Tag | C# Web Controller | C# Application Service | Frontend View / Script | MySQL Schema / Routine |
|---|---|---|---|---|---|
| **1** | `FEATURE: Authentication and Authorization` | `AuthController.cs` | `AuthService.cs` | `login.html`, `register.html`, `app.js` | `app_users` table |
| **2** | `FEATURE: Company Catalog and Search` | `CompaniesController.cs`, `SearchController.cs`, `SectorsController.cs` | `CompanyService.cs`, `SearchService.cs` | `company.html`, `app.js` | `companies`, `sectors` tables |
| **3** | `FEATURE: Portfolio Management` | `PortfoliosController.cs`, `DashboardController.cs`, `ActivitiesController.cs` | `PortfolioService.cs` | `portfolio.html`, `index.html`, `app.js` | `portfolios` table |
| **4** | `FEATURE: Portfolio Valuation` | `PortfoliosController.cs`, `SnapshotsController.cs`, `DashboardController.cs` | `PortfolioService.cs`, `PortfolioSnapshotService.cs` | `portfolio.html`, `app.js` | `vw_portfolio_holdings`, `sp_generate_portfolio_snapshot`, `fn_get_weighted_avg_price` |
| **5** | `FEATURE: Transactions` | `TransactionsController.cs`, `ActivitiesController.cs` | `TransactionService.cs`, `CsvTransactionImportService.cs` | `transactions.html`, `activity.html`, `app.js` | `transactions`, `sp_record_transaction`, `trg_transactions_after_*` |
| **6** | `FEATURE: Realized and Unrealized P/L` | `TransactionsController.cs`, `ReportsController.cs` | `TransactionService.cs`, `PortfolioService.cs`, `ReportService.cs` | `transactions.html`, `reports.html`, `app.js` | `fn_calculate_realized_pl`, `fn_calculate_unrealized_pl`, `vw_portfolio_holdings` |
| **7** | `FEATURE: Dividend Management` | `DividendsController.cs` | `DividendService.cs` | `dividends.html`, `app.js` | `dividends` table |
| **8** | `FEATURE: DSE Market Data` | `CompaniesController.cs` | `DsePriceService.cs`, `StockChartBdClient.cs`, `DsePriceBackgroundService.cs` | `company.html`, `app.js` | `companies.current_price`, live quotes |
| **9** | `FEATURE: Historical Price Data` | `CompaniesController.cs` | `CompanyService.cs` | `company.html`, `app.js` | `company_price_history` table |
| **10** | `FEATURE: Benchmark Analysis` | `BenchmarksController.cs` | `BenchmarkService.cs` | `analytics.html`, `app.js` | `benchmark_price_history` table |
| **11** | `FEATURE: Portfolio Analytics` | `AnalyticsController.cs` | `AnalyticsService.cs` | `analytics.html`, `app.js` | Sector exposures, performance history |
| **12** | `FEATURE: Investment Intelligence` | `InvestmentIntelligenceController.cs` | `InvestmentIntelligenceService.cs` | `investment-intelligence.html`, `investment-intelligence.js` | Multi-year statistical profile & scoring |
| **13** | `FEATURE: Inflation and Real Return` | `InflationController.cs`, `ReportsController.cs` | `InflationService.cs`, `ReportService.cs` | `reports.html`, `app.js` | `inflation_rates`, `cpi_index_observations`, `fn_calculate_real_return` |
| **14** | `FEATURE: Currency Conversion` | `CurrenciesController.cs` | `CurrencyService.cs`, `PortfolioService.cs` | `portfolio.html`, `app.js` | `currency_exchange_rates` table |
| **15** | `FEATURE: Portfolio Goals` | `GoalsController.cs` | `PortfolioGoalService.cs` | `goals.html`, `app.js` | `portfolio_goals` table |
| **16** | `FEATURE: Watchlists` | `WatchlistsController.cs` | `WatchlistService.cs` | `watchlist.html`, `app.js` | `watchlists`, `watchlist_items` tables |
| **17** | `FEATURE: Alerts and Notifications` | `AlertsController.cs`, `NotificationsController.cs` | `AlertService.cs`, `NotificationService.cs` | `alerts.html`, `app.js` | `alerts`, `notifications` tables |
| **18** | `FEATURE: Reports and Exports` | `ReportsController.cs` | `ReportService.cs`, `ReportExportService.cs` | `reports.html`, `app.js` | `fn_calculate_net_cash_flow`, CSV/PDF exports |
| **19** | `FEATURE: Simulator` | `SimulatorController.cs` | `SimulatorService.cs` | `simulator.html`, `app.js` | Hypothetical execution sandbox |
| **20** | `FEATURE: CSV Import` | `TransactionsController.cs` | `CsvTransactionImportService.cs` | `transactions.html`, `app.js` | Transaction batch parser |
| **21** | `FEATURE: MySQL Database and Data Integrity` | `AdminController.cs`, `HealthController.cs` | `ShareSyncDbContext.cs`, `AdminService.cs` | `admin.html`, `app.js` | `01_schema.sql`, `02_seed.sql`, `07_tests.sql`, `setup.sql` |
| **22** | `FEATURE: Historical Reconciliation` | `AdminController.cs` | `HistoricalReconciliationEngine.cs`, `AdminService.cs` | `admin.html`, `app.js` | OHLC geometry, deduplication, Harvard Dataverse |
| **23** | `FEATURE: Learning Center` | N/A (Client educational module) | N/A | `learning-center.html`, `learning-center.js`, `learning-center.css` | Educational financial terms, DSE market mechanics |

---

## 28 Core Concepts Source Code Mapping

### 1. Authentication
- **File:** [AuthService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/AuthService.cs) & [AuthController.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Web/Controllers/AuthController.cs)
- **Class:** `AuthService`, `AuthController`
- **Method:** `RegisterAsync`, `LoginAsync`
- **Searchable Tag:** `SECURITY: Authentication`
- **Mechanism:** Salted PBKDF2 HMAC-SHA256 hash comparison with timing-attack resistant verification. Issues signed RFC 7519 JWT Bearer tokens with subject `UserId` and role claims.

### 2. Users
- **File:** [AppUser.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/AppUser.cs)
- **Class:** `AppUser`
- **Table:** `app_users`
- **Searchable Tag:** `DATABASE: Table — app_users`
- **Mechanism:** Stores user credentials, email uniqueness constraints (`UQ_APP_USERS_EMAIL`), and role enforcement (`INVESTOR`, `ADMIN`).

### 3. Companies
- **File:** [Company.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/Company.cs) & [CompaniesController.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Web/Controllers/CompaniesController.cs)
- **Class:** `Company`, `CompaniesController`
- **Table:** `companies`
- **Searchable Tag:** `DSE: Company Catalog`
- **Mechanism:** Represents listed equity instruments, currency denomination (`BDT`, `USD`), asset classification (`EQUITY`, `ETF`), and real-time trading quotes.

### 4. Portfolios
- **File:** [PortfolioService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/PortfolioService.cs) & [Portfolio.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/Portfolio.cs)
- **Class:** `PortfolioService`, `Portfolio`
- **Method:** `GetUserPortfoliosAsync`, `GetPortfolioByIdAsync`
- **Searchable Tag:** `FEATURE: Multi-Portfolio Management`
- **Mechanism:** Owns trades and positions. Isolates user assets (`SECURITY: Ownership Isolation`) and aggregates holdings into portfolio base currency.

### 5. Transactions
- **File:** [TransactionService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/TransactionService.cs) & [Transaction.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/Transaction.cs)
- **Class:** `TransactionService`, `Transaction`
- **Method:** `CreateTransactionAsync`
- **Searchable Tag:** `TRANSACTION: BUY/SELL Processing`
- **Mechanism:** Atomic recording of BUY and SELL orders with whole-share integer validation and oversell defense.

### 6. Holdings
- **File:** [PortfolioService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/PortfolioService.cs)
- **Class:** `PortfolioService`
- **Method:** `CalculateHoldingsFromTransactions`
- **Searchable Tag:** `HOLDINGS: Position Aggregation & Valuation`
- **Formula:** $\text{Quantity} = \sum \text{BUY} - \sum \text{SELL}$, $\text{Market Value} = \text{Quantity} \times \text{Current Price}$.

### 7. Realized P/L (LF-26)
- **File:** [TransactionService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/TransactionService.cs) & [04_functions.sql](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/04_functions.sql)
- **Class / Routine:** `TransactionService.CreateTransactionAsync`, `fn_calculate_realized_pl`
- **Searchable Tag:** `FEATURE: Realized P/L` / `CALCULATION: Realized P/L`
- **Formula:** $\text{Realized P/L} = (\text{Sell Qty} \times \text{Sell Price} - \text{Fee}) - (\text{Sell Qty} \times \text{Weighted Average Buy Cost})$.

### 8. Fees (LF-27)
- **File:** [Transaction.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/Transaction.cs) & [ReportService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/ReportService.cs)
- **Class:** `Transaction`, `ReportService`
- **Method:** `GetTransactionHistoryReportAsync`
- **Searchable Tag:** `CALCULATION: Transaction Fee`
- **Mechanism:** Explicit brokerage and exchange fees deducted from SELL proceeds and added to BUY capital expenditures.

### 9. Net Cash Flow (LF-27)
- **File:** [ReportService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/ReportService.cs) & [04_functions.sql](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/04_functions.sql)
- **Class / Routine:** `ReportService`, `fn_calculate_net_cash_flow`
- **Searchable Tag:** `CALCULATION: Net Cash Flow`
- **Formula:** $\text{Net Cash Flow} = \sum(\text{Net SELL Inflow}) - \sum(\text{Net BUY Outflow})$.

### 10. Dividends
- **File:** [Dividend.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/Dividend.cs) & [ReportService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/ReportService.cs)
- **Class:** `Dividend`, `ReportService`
- **Method:** `GetDividendIncomeReportAsync`
- **Searchable Tag:** `FEATURE: Dividend Income Tracking`
- **Mechanism:** Matches dividend declaration record dates against historical investor ownership quantities.

### 11. Watchlists
- **File:** [Watchlist.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/Watchlist.cs) & [WatchlistItem.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/WatchlistItem.cs)
- **Class:** `Watchlist`, `WatchlistItem`
- **Table:** `watchlists`, `watchlist_items`
- **Searchable Tag:** `FEATURE: Watchlists`
- **Mechanism:** Composite primary key (`watchlist_id`, `company_id`) supporting personalized price targets and tracking.

### 12. Alerts
- **File:** [Alert.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/Alert.cs)
- **Class:** `Alert`
- **Table:** `alerts`
- **Searchable Tag:** `FEATURE: Price & Portfolio Alerts`
- **Mechanism:** Threshold rules evaluated during price synchronization triggers.

### 13. Goals
- **File:** [PortfolioGoal.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/PortfolioGoal.cs)
- **Class:** `PortfolioGoal`
- **Table:** `portfolio_goals`
- **Searchable Tag:** `FEATURE: Portfolio Goals`
- **Formula:** $\text{Progress Percentage} = (\text{Current Portfolio Value} / \text{Target Amount}) \times 100$.

### 14. Reports
- **File:** [ReportService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/ReportService.cs) & [ReportsController.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Web/Controllers/ReportsController.cs)
- **Class:** `ReportService`, `ReportsController`
- **Method:** `GetHoldingsReportAsync`, `GetPerformanceReportAsync`, `GetTransactionHistoryReportAsync`
- **Searchable Tag:** `REPORT: Comprehensive Financial Reporting`
- **Mechanism:** High-performance EF Core `AsNoTracking()` queries across 7 accounting modules.

### 15. Analytics
- **File:** [AnalyticsService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/AnalyticsService.cs)
- **Class:** `AnalyticsService`
- **Method:** `GetPortfolioAnalyticsAsync`
- **Searchable Tag:** `FEATURE: Portfolio Analytics`
- **Mechanism:** Computes sector concentrations, asset allocations, and historical beta metrics.

### 16. Inflation (LF-28)
- **File:** [InflationService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/InflationService.cs) & [InflationRate.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/InflationRate.cs)
- **Class:** `InflationService`, `InflationRate`
- **Method:** `CalculateRealReturn`, `GetCumulativeInflationRateAsync`
- **Searchable Tag:** `FEATURE: Inflation-Adjusted Return` / `CALCULATION: Real Return`
- **Formula:** $\text{Real Return} = \left(\frac{1 + \text{Nominal Return}}{1 + \text{Cumulative Inflation}}\right) - 1$ (Fisher Equation).

### 17. Currency (LF-29)
- **File:** [CurrencyService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/CurrencyService.cs) & [CurrencyExchangeRate.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/CurrencyExchangeRate.cs)
- **Class:** `CurrencyService`, `CurrencyExchangeRate`
- **Method:** `ConvertAsync`, `GetExchangeRateAsync`
- **Searchable Tag:** `FEATURE: Multi-Currency` / `CALCULATION: Currency Conversion`
- **Formula:** $\text{Converted Amount} = \text{Amount} \times \text{Exchange Rate}$.

### 18. Benchmarks (LF-30)
- **File:** [BenchmarkService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/BenchmarkService.cs) & [BenchmarkPriceHistory.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Domain/Entities/BenchmarkPriceHistory.cs)
- **Class:** `BenchmarkService`, `BenchmarkPriceHistory`
- **Method:** `ComparePortfolioAsync`
- **Searchable Tag:** `FEATURE: Benchmark Comparison` / `BENCHMARK: Active Return`
- **Formula:** $\text{Active Return / Outperformance} = \text{Portfolio Return} - \text{Benchmark Return}$.

### 19. DSE Market Data (LF-31)
- **File:** [DsePriceService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Infrastructure/Services/DsePriceService.cs)
- **Class:** `DsePriceService`
- **Method:** `SyncAllCompanyPricesAsync`, `FetchQuoteDetailsAsync`
- **Searchable Tag:** `DSE: Price Synchronization`
- **Mechanism:** HTTP polling of StockChartBD feed, User-Agent identification, and 30ms polite throttling.

### 20. Historical Reconciliation (LF-31)
- **File:** [HistoricalReconciliationEngine.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Common/Helpers/HistoricalReconciliationEngine.cs)
- **Class:** `HistoricalReconciliationEngine`
- **Method:** `StageCompanyObservation`, `ReconcileDuplicates`
- **Searchable Tag:** `DATA QUALITY: OHLC` / `DATA QUALITY: Duplicate Handling`
- **Rules:** Enforces $\text{Low} \le \text{Open} \le \text{High}$ and $\text{Low} \le \text{Close} \le \text{High}$. Quarantines conflicting OHLC records.

### 21. Investment Intelligence Engine (LF-31)
- **File:** [InvestmentIntelligenceService.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Application/Services/InvestmentIntelligenceService.cs)
- **Class:** `InvestmentIntelligenceService`
- **Method:** `GetHistoricalInvestmentProfileAsync`, `CalculateCompositeScore`
- **Searchable Tag:** `INVESTMENT INTELLIGENCE: Score` / `CALCULATION: CAGR`
- **Formula:** $\text{CAGR} = (\text{Ending} / \text{Starting})^{1 / \text{Years}} - 1$. Score weighted across Return (30), Volatility (25), Drawdown (25), and Trend (20).

### 22. MySQL DbContext
- **File:** [ShareSyncDbContext.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/src/ShareSync.Infrastructure/Data/ShareSyncDbContext.cs)
- **Class:** `ShareSyncDbContext`
- **Method:** `OnModelCreating`
- **Searchable Tag:** `DATABASE: DbContext`
- **Provider:** `Pomelo.EntityFrameworkCore.MySql` with strict foreign keys and precision configs.

### 23. Relational Views
- **File:** [01_schema.sql](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/01_schema.sql)
- **View:** `vw_portfolio_holdings`
- **Searchable Tag:** `DATABASE: View — vw_portfolio_holdings`
- **SQL Concept:** `SQL CONCEPT: VIEW`
- **Mechanism:** Live SQL aggregation of transactions by portfolio and company, computing mark-to-market valuations and filtering liquidated positions (`HAVING quantity > 0`).

### 24. Stored Functions
- **File:** [04_functions.sql](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/04_functions.sql)
- **Routines:** `fn_get_weighted_avg_price`, `fn_calculate_unrealized_pl`, `fn_calculate_realized_pl`, `fn_calculate_net_cash_flow`, `fn_calculate_real_return`
- **Searchable Tag:** `DATABASE: Stored Function`
- **SQL Concept:** `SQL CONCEPT: FUNCTION`

### 25. Stored Procedures
- **File:** [05_procedures.sql](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/05_procedures.sql)
- **Routines:** `sp_record_transaction`, `sp_generate_portfolio_snapshot`
- **Searchable Tag:** `DATABASE: Stored Procedure`
- **SQL Concept:** `SQL CONCEPT: PROCEDURE`
- **Mechanism:** Implements defensive oversell rejection (`OVERSELL_REJECTED`) and daily portfolio snapshot generation with `ON DUPLICATE KEY UPDATE`.

### 26. Database Triggers
- **File:** [06_triggers.sql](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/06_triggers.sql)
- **Triggers:** `trg_transactions_after_insert`, `trg_transactions_after_update`, `trg_transactions_after_delete`
- **Searchable Tag:** `DATABASE: Trigger`
- **SQL Concept:** `SQL CONCEPT: TRIGGER`
- **Mechanism:** Automatically generates persistent audit records in `transaction_audit` for all transaction table mutations.

### 27. Indexes
- **File:** [01_schema.sql](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/01_schema.sql) & [03_queries.sql](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/03_queries.sql)
- **Indexes:** `idx_transactions_portfolio_date`, `idx_bench_code_date`, `idx_price_hist_comp_date`
- **Searchable Tag:** `SQL CONCEPT: INDEX` / `SQL CONCEPT: EXPLAIN`
- **Mechanism:** Composite B-Tree indexes eliminating full table scans on large chronological time series.

### 28. Automated Tests
- **File:** [MathematicalAccuracyAuditTests.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/tests/ShareSync.Tests/MathematicalAccuracyAuditTests.cs) & [IndependentReferenceCalculator.cs](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/tests/ShareSync.Tests/IndependentReferenceCalculator.cs)
- **Classes:** `MathematicalAccuracyAuditTests`, `IndependentReferenceCalculator`
- **Searchable Tag:** `TEST: Mathematical Accuracy Audit Suite`
- **Verification:** 490 automated xUnit tests validating calculations, security policies, and data reconciliation without failure.
