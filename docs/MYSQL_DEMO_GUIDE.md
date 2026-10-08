# ShareSync — MySQL Teacher Demonstration Guide

This guide outlines a step-by-step practical script for demonstrating the **ShareSync MySQL Database System** to an instructor or academic evaluator.

---

## Demonstration Overview

| Stage | Focus Area | Tools | Estimated Time |
|---|---|---|---|
| **Part I: Database Architecture & SQL** | Schema, Relational Integrity, Programmability, Transactions | MySQL Workbench / MySQL CLI | 10–12 mins |
| **Part II: Full-Stack Web Application** | Real-time Valuation, Reports, Investment Intelligence | Web Browser (`localhost:5000`) | 8–10 mins |

---

## Part I: Database Engine & SQL Demonstration

### 1. Connect & Select Database
Open MySQL Workbench or MySQL CLI:
```sql
USE sharesync;
```

### 2. Display Table Architecture (`SHOW TABLES`)
```sql
SHOW TABLES;
```
*Talking Point:* The schema comprises **17 strictly normalized tables** divided into core business entities, transactional ledgers, analytical caches, auditing, macro indicators, and database-backed benchmark history (`benchmark_price_history`).

### 3. Explain Normalized Tables
Explain normalization to 3NF:
- `app_users`: User identities with PBKDF2 hashes and roles.
- `sectors` & `companies`: Separate entities to prevent repeated sector description strings.
- `portfolios`: Enables 1:N multi-portfolio management per investor with configurable base currency.
- `transactions`: Pure chronological financial ledger with transaction fee tracking and audit logs.
- `benchmark_price_history`: Persisted historical index values (DSEX, DS30, DSES) with composite unique key on `(benchmark_code, trading_date)`.

### 4. Show Primary Key & Foreign Key Relationships
```sql
SHOW CREATE TABLE transactions\G
```
*Talking Point:* Highlight:
- `pk_transactions` (`transaction_id`)
- `fk_transactions_portfolio` referencing `portfolios(portfolio_id)` with `ON DELETE RESTRICT`
- `fk_transactions_company` referencing `companies(company_id)` with `ON DELETE RESTRICT`
- `CHECK` constraints on `transaction_type` ('BUY', 'SELL') and `quantity > 0`.

### 5. Execute Multi-Table Relational JOIN
Demonstrate multi-table joining between `portfolios`, `transactions`, and `companies`:
```sql
SELECT 
    p.portfolio_name,
    c.ticker_symbol,
    c.company_name,
    t.transaction_type,
    t.quantity,
    t.price_per_share,
    t.transaction_date
FROM transactions t
INNER JOIN portfolios p ON t.portfolio_id = p.portfolio_id
INNER JOIN companies c ON t.company_id = c.company_id
ORDER BY t.transaction_date DESC
LIMIT 5;
```

### 6. Execute Aggregation with GROUP BY & HAVING
```sql
SELECT 
    p.portfolio_name,
    COUNT(t.transaction_id) AS total_orders,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) AS total_invested
FROM portfolios p
INNER JOIN transactions t ON p.portfolio_id = t.portfolio_id
GROUP BY p.portfolio_id, p.portfolio_name
HAVING COUNT(t.transaction_id) >= 2;
```

### 7. Demonstrate Database View (`vw_portfolio_holdings`)
```sql
SELECT 
    portfolio_name,
    ticker_symbol,
    current_quantity,
    current_price,
    weighted_average_buy_price,
    current_market_value,
    unrealized_profit_loss
FROM vw_portfolio_holdings;
```
*Talking Point:* This view dynamically aggregates raw BUY and SELL order records into real-time net share balances and mark-to-market valuations without storing redundant calculated state.

### 8. Execute Stored Functions
Call `fn_get_weighted_avg_price` and `fn_calculate_unrealized_pl`:
```sql
SELECT 
    p.portfolio_name,
    c.ticker_symbol,
    fn_get_weighted_avg_price(p.portfolio_id, c.company_id) AS weighted_avg_cost,
    fn_calculate_unrealized_pl(p.portfolio_id, c.company_id) AS unrealized_pl
FROM portfolios p
CROSS JOIN companies c
WHERE fn_get_weighted_avg_price(p.portfolio_id, c.company_id) > 0;
```

### 9. Execute Stored Procedure with Output Parameters
Record a transaction and demonstrate oversell validation:
```sql
CALL sp_record_transaction(
    1, 2, 'BUY', 20.0000, 240.00, 'teacher_demo',
    @tx_id, @status, @msg
);
SELECT @tx_id AS id, @status AS status, @msg AS message;
```

### 10. Demonstrate Trigger & Audit Trail
Check `transaction_audit` immediately after executing `sp_record_transaction`:
```sql
SELECT audit_id, transaction_id, action_type, action_date, changed_by, details
FROM transaction_audit
ORDER BY audit_id DESC
LIMIT 3;
```
*Talking Point:* The `trg_transactions_after_insert` trigger automatically fired and captured the audit entry atomically.

### 11. Demonstrate Transactions, SAVEPOINT, & ROLLBACK
```sql
START TRANSACTION;

INSERT INTO transactions (portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date)
VALUES (1, 1, 'BUY', 99, 240.00, NOW());

SAVEPOINT sp_demo;

-- Verify row exists inside active transaction
SELECT COUNT(*) FROM transactions WHERE quantity = 99;

-- Rollback to clean state
ROLLBACK TO sp_demo;
ROLLBACK;

-- Verify clean state
SELECT COUNT(*) FROM transactions WHERE quantity = 99;
```

### 12. Show Index Usage & Execution Plan (`EXPLAIN`)
```sql
SHOW INDEX FROM transactions;

EXPLAIN SELECT transaction_id, quantity, price_per_share
FROM transactions
WHERE portfolio_id = 1 AND transaction_date >= '2026-01-01';
```
*Talking Point:* MySQL uses the composite index `idx_transactions_portfolio_date` to perform index range scanning (`type: range`), avoiding full-table scans.

### 12b. Query Database-Backed Benchmark History (LF-30)
Demonstrate querying persisted benchmark series with date filtering and index lookups:
```sql
SELECT 
    benchmark_code,
    benchmark_name,
    MIN(trading_date) AS earliest_date,
    MAX(trading_date) AS latest_date,
    COUNT(*) AS total_observations,
    ROUND(AVG(close_value), 2) AS avg_close
FROM benchmark_price_history
GROUP BY benchmark_code, benchmark_name;

-- Show index-backed range lookup
EXPLAIN SELECT trading_date, close_value 
FROM benchmark_price_history 
WHERE benchmark_code = 'DSEX' AND trading_date BETWEEN '2022-01-01' AND '2022-12-31';
```
*Talking Point:* Benchmark data is indexed via `idx_bench_code_date` on `(benchmark_code, trading_date)`, ensuring sub-millisecond range retrieval without full-table scans.

---

## Part II: Full-Stack Web Application Demonstration

### 13. Launch Application
In terminal:
```powershell
dotnet run --project src/ShareSync.Web
```
Open browser to `http://localhost:5000`.

### 14. Login & Explore Dashboard
1. Log in with:
   - **Email:** `investor@sharesync.com`
   - **Password:** `Password123#`
2. **Dashboard Overview:** Point out total portfolio value, total gain/loss badge, sector distribution donut chart, and portfolio allocation cards.
3. Note how the numbers directly match the values calculated in MySQL `vw_portfolio_holdings`.

### 15. Create a Live Transaction (LF-29 Multi-Asset / Multi-Currency)
1. Navigate to **Transactions** (`transactions.html`).
2. Click **Record Transaction**.
3. Choose Portfolio: `Dividend Income Portfolio`.
4. Choose Company: `Square Pharmaceuticals PLC (SQUAREPHARMA)`.
   - Observe the compact **Security Info Strip**:
     - **Asset Type**: `EQUITY` (badge in primary blue)
     - **Currency**: `BDT`
   - Test selecting `Grameen One Scheme Two Mutual Fund (GRAMEEN1MF)`:
     - **Asset Type**: `MUTUAL_FUND` (badge in info cyan)
     - **Currency**: `BDT`
   - Test selecting `USD Treasury Cash Reserve (CASH-USD)` or `SPY`:
     - **Asset Type**: `CASH` or `ETF`
     - **Currency**: `USD`
     - **Exchange Rate**: `1 USD = 121.50 BDT` (from `currency_exchange_rates`)
     - **Base-Currency Value**: converted dynamically to ৳ BDT
     - Dynamic price input prefix changes from `৳` to `$`
5. Select `SQUAREPHARMA`, Type: `BUY`, Quantity: `50`, Price: `240.00`.
6. Submit. Note immediate update in transaction history table and portfolio holdings.

### 16. Inspect Reports
1. Navigate to **Reports** (`reports.html`).
2. Show **Holdings Valuation Report**: volume-weighted average buy price, current price, unrealized profit/loss.
3. Show **Company & Sector Allocation Report**: percentage capital distribution across telecom, pharmaceuticals, and consumer goods.
4. Show **Dividend Income Report**: dividend timeline and payout aggregations.

### 17. Portfolio Analytics & Benchmark Comparison (LF-30 Benchmark Fallback Visibility)
1. Navigate to **Analytics** (`analytics.html`).
2. In the **Benchmark Comparison** section:
   - Select Portfolio: `Growth Portfolio` (or `Dividend Income Portfolio`).
   - Select Benchmark: `DSEX` (DSE Broad Index), `DS30` (30 Blue Chip), or `DSES` (Shariah Index).
3. Observe the calculated metrics:
   - **Portfolio Return %**
   - **Benchmark Return %**
   - **Outperformance / Difference %**
   - **Comparison Period & Observation Counts**
4. *Talking Point:*
   - Normal operation uses the **5,922 MySQL historical benchmark records** (`IsFallback = false`), so no warning or fallback badge is shown.
   - If historical observations are unavailable for a requested custom period, a clear but unobtrusive `Reference Fallback` badge appears next to the title with an explanatory tooltip (`IsFallback = true`), preserving user trust without alarm.

### 18. Open Investment Intelligence
1. Navigate to **Investment Intel** (`investment-intelligence.html`).
2. Point out the complete, integrated application shell matching the rest of ShareSync.

### 19. Search a Real DSE Company (LF-31 Universe Expansion)
1. In the search box, type `GP`. Note real-time autocomplete suggestion for Grameenphone Ltd.
2. Type `Square`. Note suggestion for Square Pharmaceuticals PLC.
3. Type `BAT`. Note suggestion for British American Tobacco.
4. Type `Walton`. Note suggestion for Walton Hi-Tech Industries PLC.
5. Type `Janata`. Note suggestion for First Janata Bank Mutual Fund (`1JANATAMF`).
6. Type `XYZ999`. Note clean feedback for no matching companies.

### 20. Analyze Historical Performance Profile
1. Select **Grameenphone Ltd. (GP)** and click **Analyze Company**.
2. Explain the retrospective statistical metrics:
   - **Historical Date Range & Starting/Ending Price**
   - **CAGR & Total Historical Return**
   - **Annualized Volatility & Maximum Drawdown**
   - **Score Radar & Risk/Return Classification**
   - **Data Quality & Academic Source Citation** (Harvard Dataverse DOI `10.7910/DVN/XIFYT1`)
3. Select **Square Pharmaceuticals PLC (SQUAREPHARMA)** and click **Analyze Company**. Show how the metrics and overall score adjust dynamically based on historical price observations.

### 21. Open Profile & Logout
1. Click the user avatar in the top right to open the **Profile Dropdown Menu**.
2. Point out that the standalone logout button has been removed from the navbar and placed neatly inside the profile dropdown.
3. Click **Sign Out**.
4. Confirm redirection to `login.html`.
5. Attempt to navigate directly to `http://localhost:5000/portfolio.html` to demonstrate route protection (`401 Unauthorized` redirect).

---

## Part III: Data Importer & Quality Audit Pipeline (LF-31)

### 22. Reproducible Market Data Importer
Demonstrate running the reproducible batch importer tool:
```powershell
dotnet run --project src/ShareSync.DataImporter
```
*Talking Point:*
- Scans `Dhaka Stock Exchange Historical Data (1999-2025)` directory for raw DSE index and company CSVs.
- Enforces strict data quality assertions: filters out duplicate dates, rejects invalid OHLC geometry (`High < Low`, etc.), drops zero or non-positive prices, and retains valid zero-volume quotations.
- Generates `DataQualityReport.json` documenting exact imported counts, deduplication metrics, and invalid row rejections.
