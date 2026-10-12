# ShareSync — Database Objects Viva & Demonstration Map
## Technical Reference for MySQL 8.0 Architecture, Schema, and Programmability

**Database Name:** `sharesync`  
**Storage Engine:** `InnoDB`  
**Character Set:** `utf8mb4` (Collation: `utf8mb4_unicode_ci`)  
**Schema File:** `database/mysql/01_schema.sql`  

---

## 1. Table Architecture Map (18 Normalized Tables)

| # | Table Name | Purpose | Primary Key | Foreign Keys | Constraints / Indexes |
|---|---|---|---|---|---|
| **1** | `app_users` | User identities, authentication, roles | `user_id` (INT AI) | None | `UNIQUE (email)`, `CHECK (role IN ('INVESTOR','ADMIN'))`, `CHECK (is_active IN (0,1))` |
| **2** | `sectors` | Industry sectors for listed companies | `sector_id` (INT AI) | None | `UNIQUE (sector_name)` |
| **3** | `companies` | Listed stock catalog and current prices | `company_id` (INT AI) | `fk_companies_sector` -> `sectors(sector_id)` | `UNIQUE (ticker_symbol)`, `CHECK (current_price >= 0)`, `INDEX idx_companies_sector` |
| **4** | `portfolios` | Investor investment portfolios | `portfolio_id` (INT AI) | `fk_portfolios_user` -> `app_users(user_id)` ON DEL RESTRICT | `CHECK (cash_balance >= 0)`, `INDEX idx_portfolios_user` |
| **5** | `transactions` | Immutable BUY / SELL financial ledger | `transaction_id` (INT AI) | `fk_tx_portfolio` -> `portfolios`, `fk_tx_company` -> `companies` | `CHECK (transaction_type IN ('BUY','SELL'))`, `CHECK (quantity > 0)`, `CHECK (price_per_share > 0)`, `CHECK (fee >= 0)`, `INDEX idx_transactions_portfolio_date` |
| **6** | `dividends` | Corporate cash dividend payouts | `dividend_id` (INT AI) | `fk_div_portfolio` -> `portfolios`, `fk_div_company` -> `companies` | `CHECK (amount >= 0)`, `INDEX idx_dividends_portfolio` |
| **7** | `watchlists` | Investor stock monitoring groups | `watchlist_id` (INT AI) | `fk_wl_portfolio` -> `portfolios` ON DEL CASCADE | `INDEX idx_watchlists_portfolio` |
| **8** | `watchlist_items` | Tracked stocks within watchlists | `(watchlist_id, company_id)` (Composite PK) | `fk_wli_watchlist` -> `watchlists`, `fk_wli_company` -> `companies` | Natural Composite PK prevents duplicate ticker entries per watchlist |
| **9** | `transaction_audit` | Forensic audit log for transactions | `audit_id` (INT AI) | None (Preserves log even if tx deleted) | `INDEX idx_tx_audit_tx_id`, Populated exclusively by MySQL triggers |
| **10** | `portfolio_snapshots`| Daily mark-to-market historical balances | `snapshot_id` (INT AI) | `fk_snap_portfolio` -> `portfolios` | `UNIQUE KEY uq_portfolio_date (portfolio_id, snapshot_date)`, `INDEX idx_snap_portfolio_date` |
| **11** | `company_price_history`| Daily historical closing prices | `price_history_id` (INT AI) | `fk_price_history_company` -> `companies` | `UNIQUE KEY uq_price_hist_comp_trading_date (company_id, trading_date)`, `INDEX idx_price_hist_comp_date` |
| **12** | `alerts` | Price threshold notifications | `alert_id` (INT AI) | `fk_alert_user` -> `app_users`, `fk_alert_company` -> `companies` | `CHECK (alert_type IN ('PRICE_ABOVE','PRICE_BELOW'))` |
| **13** | `notifications` | In-app user notification feed | `notification_id` (INT AI) | `fk_notif_user` -> `app_users` | `CHECK (is_read IN (0,1))` |
| **14** | `portfolio_goals` | Financial milestone targets | `goal_id` (INT AI) | `fk_goal_portfolio` -> `portfolios` | `CHECK (target_amount > 0)`, `CHECK (current_amount >= 0)` |
| **15** | `currency_exchange_rates`| Multi-currency FX conversion rates | `rate_id` (INT AI) | None | `UNIQUE KEY uq_currency_pair (from_currency, to_currency)`, `CHECK (exchange_rate > 0)` |
| **16** | `inflation_rates` | Annual macroeconomic inflation rates | `rate_id` (INT AI) | None | `UNIQUE KEY uq_inflation_rates_country_year (country_code, year)` |
| **17** | `benchmark_price_history`| Exchange index closing series | `history_id` (INT AI) | None | `UNIQUE KEY uq_benchmark_date (benchmark_code, trading_date)` |
| **18** | `cpi_index_observations`| Monthly official CPI index series | `observation_id` (INT AI) | None | `UNIQUE KEY uq_cpi_country_date (country_code, observation_date)` |

---

## 2. Database View (`vw_portfolio_holdings`)

- **View Name:** `vw_portfolio_holdings`
- **File:** `database/mysql/01_schema.sql` (Lines 569–598)
- **Purpose:** Dynamically computes net equity share positions, current market valuation, weighted average purchase cost, and mark-to-market unrealized profit/loss on the fly without storing mutable balance columns.
- **SQL Definition:**
```sql
CREATE OR REPLACE VIEW vw_portfolio_holdings AS
SELECT
    t.portfolio_id,
    p.portfolio_name,
    t.company_id,
    c.company_name,
    c.ticker_symbol,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) AS current_quantity,
    c.current_price,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) * c.current_price AS current_market_value,
    CASE
        WHEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END) > 0
        THEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) / SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END)
        ELSE 0
    END AS weighted_average_buy_price,
    (
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) * c.current_price
    ) - (
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) *
        CASE
            WHEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END) > 0
            THEN SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) / SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END)
            ELSE 0
        END
    ) AS unrealized_profit_loss
FROM transactions t
JOIN portfolios p ON t.portfolio_id = p.portfolio_id
JOIN companies c ON t.company_id = c.company_id
GROUP BY t.portfolio_id, p.portfolio_name, t.company_id, c.company_name, c.ticker_symbol, c.current_price
HAVING SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity WHEN t.transaction_type = 'SELL' THEN -t.quantity ELSE 0 END) > 0;
```
- **How to Demonstrate in CLI:**
```sql
SELECT portfolio_name, ticker_symbol, current_quantity, current_price, weighted_average_buy_price, unrealized_profit_loss 
FROM vw_portfolio_holdings 
WHERE portfolio_id = 1;
```

---

## 3. Stored Functions (User-Defined Functions)

| Function Name | Return Type | File | Purpose |
|---|---|---|---|
| `fn_get_weighted_avg_price(portfolio_id, company_id)` | `DECIMAL(14,2)` | `04_functions.sql` | Computes volume-weighted average purchase price across historical BUY transactions. |
| `fn_calculate_unrealized_pl(portfolio_id, company_id)` | `DECIMAL(20,2)` | `04_functions.sql` | Computes mark-to-market unrealized gain/loss: $(\text{Shares} \times P_{\text{current}}) - (\text{Shares} \times P_{\text{avg}})$. |
| `fn_calculate_realized_pl(portfolio_id, company_id)` | `DECIMAL(20,2)` | `04_functions.sql` | Computes cumulative realized profit from all executed SELL transactions. |
| `fn_calculate_net_cash_flow(portfolio_id)` | `DECIMAL(20,2)` | `04_functions.sql` | Computes net investor capital flow: $\sum \text{SELL Inflows} - \sum \text{BUY Outflows}$. |
| `fn_calculate_real_return(nominal_return, inflation_rate)` | `DECIMAL(14,4)` | `04_functions.sql` | Computes inflation-adjusted purchasing power return using the Fisher Equation. |

- **How to Demonstrate in CLI:**
```sql
-- Compute weighted average cost and unrealized gain for Portfolio #1, Company #1 (GP)
SELECT 
    fn_get_weighted_avg_price(1, 1) AS avg_buy_price,
    fn_calculate_unrealized_pl(1, 1) AS unrealized_gain;

-- Compute Fisher Equation Real Return for 18.4% nominal return at 9.7% inflation
SELECT fn_calculate_real_return(18.40, 9.70) AS purchasing_power_return;
-- Result: 7.9307%
```

---

## 4. Stored Procedures

| Procedure Name | File | Parameters | Purpose |
|---|---|---|---|
| `sp_record_transaction` | `05_procedures.sql` | `IN portfolio_id, company_id, type, qty, price, fee, changed_by`<br>`OUT transaction_id, status, message` | Validates input bounds, enforces whole-share constraint, guards against overselling, and performs atomic insertion. |
| `sp_generate_portfolio_snapshot` | `05_procedures.sql` | `IN portfolio_id, snapshot_date`<br>`OUT total_value, status, message` | Computes portfolio mark-to-market valuation on a target date and upserts a row into `portfolio_snapshots`. |

- **How to Demonstrate in CLI:**
```sql
-- Test oversell prevention in procedure
CALL sp_record_transaction(1, 1, 'SELL', 999999, 350.00, 10.00, 'DEMO_USER', @tx_id, @status, @msg);
SELECT @status AS status, @msg AS reason;
-- Result: status = 'OVERSELL_REJECTED'
```

---

## 5. Database Triggers (Forensic Auditing)

| Trigger Name | Event | Timing | Table | Purpose |
|---|---|---|---|---|
| `trg_transactions_after_insert` | `INSERT` | `AFTER` | `transactions` | Automatically writes an `INSERT` audit record to `transaction_audit` with quantity, price, and executing principal. |
| `trg_transactions_after_update` | `UPDATE` | `AFTER` | `transactions` | Captures before/after delta strings (`OLD.quantity -> NEW.quantity`, `OLD.price -> NEW.price`) in `transaction_audit`. |
| `trg_transactions_after_delete` | `DELETE` | `AFTER` | `transactions` | Preserves audit record with `transaction_id = NULL` and full detail string upon order deletion. |

- **How to Demonstrate in CLI:**
```sql
-- View latest automated audit entries generated by MySQL triggers
SELECT audit_id, transaction_id, action_type, action_date, changed_by, details 
FROM transaction_audit 
ORDER BY audit_id DESC 
LIMIT 3;
```

---

## 6. Teacher "Show Me" Quick Reference Guide

| Teacher Request | Action / Query to Execute | What It Proves |
|---|---|---|
| *"Show me your schema."* | `SHOW TABLES;` | 17 normalized relational tables in 3NF. |
| *"Show me a Foreign Key constraint."* | `SHOW CREATE TABLE transactions\G` | Shows `ON DELETE RESTRICT` foreign keys and column check constraints. |
| *"Show me a View."* | `SHOW CREATE VIEW vw_portfolio_holdings\G` | Shows declarative VWAP and mark-to-market aggregation. |
| *"Show me a Stored Procedure."* | `SHOW CREATE PROCEDURE sp_record_transaction\G` | Shows procedural input validation, oversell check, and atomic insert. |
| *"Show me a Stored Function."* | `SHOW CREATE FUNCTION fn_get_weighted_avg_price\G` | Shows deterministic scalar calculation callable in SQL queries. |
| *"Show me a Trigger."* | `SHOW CREATE TRIGGER trg_transactions_after_insert\G` | Shows database-level automated auditing without application code. |
| *"Show me an Index."* | `SHOW INDEX FROM transactions;` | Shows B-tree composite index `idx_transactions_portfolio_date`. |
| *"Show me a complex JOIN."* | Run query from `database/mysql/03_queries.sql` (Lines 30–45) | Relational join connecting users, portfolios, transactions, companies, and sectors. |
