# ShareSync — MySQL Database Architecture & Schema Specification

The authoritative database for this branch is **MySQL 8.x** utilizing the **InnoDB storage engine** and the `utf8mb4_unicode_ci` character encoding.

---

## 1. Complete Relational Table Schema (14 Tables)

| # | Table Name | Purpose | Primary Key | Key Foreign Keys |
|---|---|---|---|---|
| 1 | `app_users` | Investor & Administrator credentials and roles | `user_id` (INT AUTO_INCREMENT) | None |
| 2 | `sectors` | Industry classifications | `sector_id` (INT AUTO_INCREMENT) | None |
| 3 | `companies` | Traded stock listings & current market prices | `company_id` (INT AUTO_INCREMENT) | `sector_id` &rarr; `sectors` |
| 4 | `portfolios` | Multi-portfolio wealth management containers | `portfolio_id` (INT AUTO_INCREMENT) | `user_id` &rarr; `app_users` |
| 5 | `watchlists` | Investor watchlists | `watchlist_id` (INT AUTO_INCREMENT) | `user_id` &rarr; `app_users` |
| 6 | `watchlist_items` | Companies tracked in watchlists with target prices | Composite `(watchlist_id, company_id)` | `watchlist_id`, `company_id` |
| 7 | `transactions` | Ledger of BUY & SELL executions | `transaction_id` (INT AUTO_INCREMENT) | `portfolio_id`, `company_id` |
| 8 | `dividends` | Cash dividend declarations & corporate payouts | `dividend_id` (INT AUTO_INCREMENT) | `company_id` &rarr; `companies` |
| 9 | `transaction_audit`| Forensic audit log for all transaction events | `audit_id` (INT AUTO_INCREMENT) | `transaction_id` (SET NULL on delete) |
| 10 | `portfolio_snapshots` | Daily mark-to-market historical valuations | `snapshot_id` (INT AUTO_INCREMENT) | `portfolio_id` &rarr; `portfolios` |
| 11 | `company_price_history` | Historical OHLCV market observations | `price_history_id` (INT AUTO_INCREMENT) | `company_id` &rarr; `companies` |
| 12 | `alerts` | Price & portfolio valuation threshold alerts | `alert_id` (INT AUTO_INCREMENT) | `user_id`, `company_id`, `portfolio_id` |
| 13 | `notifications` | Notification center for alerts and corporate events | `notification_id` (INT AUTO_INCREMENT) | `user_id` &rarr; `app_users` |
| 14 | `portfolio_goals` | Financial investment targets (value, return, income)| `goal_id` (INT AUTO_INCREMENT) | `user_id`, `portfolio_id` |

---

## 2. Integrity Constraints & Business Rules

1. **Unique Constraints:**
   - `uq_app_users_email` on `app_users(email)`
   - `uq_companies_ticker` on `companies(ticker_symbol)`
   - `uq_portfolio_snapshot_date` on `portfolio_snapshots(portfolio_id, snapshot_date)`
2. **Check Constraints:**
   - `ck_transactions_type`: `transaction_type IN ('BUY', 'SELL')`
   - `ck_transactions_quantity`: `quantity > 0`
   - `ck_transactions_price`: `price_per_share > 0`
   - `ck_transaction_audit_action`: `action_type IN ('INSERT', 'UPDATE', 'DELETE')`
   - `ck_alerts_type`: `alert_type IN ('PRICE_ABOVE', 'PRICE_BELOW', 'PORTFOLIO_VALUE_ABOVE', 'PORTFOLIO_VALUE_BELOW')`
   - `ck_alerts_threshold`: `threshold_value > 0`
3. **Foreign Key Deletion Rules:**
   - `transactions` &rarr; `portfolios`, `companies`: `ON DELETE RESTRICT` (prevents deleting active trading records)
   - `watchlist_items`, `alerts`, `notifications`, `portfolio_goals` &rarr; `ON DELETE CASCADE`
   - `transaction_audit` &rarr; `transactions`: `ON DELETE SET NULL` (preserves historical audit trail permanently even if original transaction record is purged)

---

## 3. Performance & Reporting Indexes

- `idx_transactions_portfolio_date`: `transactions(portfolio_id, transaction_date)`
- `idx_transactions_company`: `transactions(company_id)`
- `idx_dividends_company_dates`: `dividends(company_id, payment_date)`
- `idx_price_hist_comp_date`: `company_price_history(company_id, recorded_at)`
- `idx_alerts_user`: `alerts(user_id, is_active)`
- `idx_notifications_user`: `notifications(user_id, is_read, created_at)`
- `idx_portfolio_goals_user`: `portfolio_goals(user_id, is_active, created_at)`

---

## 4. Database Programmability

### 4.1 Holdings View (`vw_portfolio_holdings`)
Aggregates net share counts, current market value, volume-weighted average purchase price, and mark-to-market unrealized profit/loss across all portfolios dynamically:
```sql
SELECT * FROM vw_portfolio_holdings;
```

### 4.2 Stored Functions
- **`fn_get_weighted_avg_price(p_portfolio_id, p_company_id)`**: Returns volume-weighted average buy price (`DECIMAL(14,2)`).
- **`fn_calculate_unrealized_pl(p_portfolio_id, p_company_id)`**: Calculates mark-to-market unrealized gain or loss (`DECIMAL(20,2)`).

### 4.3 Stored Procedures
- **`sp_record_transaction(...)`**: Enforces strict oversell checks on `SELL` orders, inserts into `transactions` atomically, and outputs the generated ID.
- **`sp_generate_portfolio_snapshot(...)`**: Aggregates total portfolio valuation from active holdings and executes `ON DUPLICATE KEY UPDATE` into `portfolio_snapshots`.

### 4.4 Audit Triggers
- **`trg_transactions_after_insert`**: Logs created transaction into `transaction_audit`.
- **`trg_transactions_after_update`**: Logs updated transaction details into `transaction_audit`.
- **`trg_transactions_after_delete`**: Logs deletion event into `transaction_audit`.

---

## 5. SQL Script Kit Location

All scripts reside in `database/mysql/`:
- [`setup.sql`](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/setup.sql) — Master deployment script
- [`01_schema.sql`](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/01_schema.sql) — Tables, constraints, indexes & holdings view
- [`02_seed.sql`](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/02_seed.sql) — Demonstration dataset
- [`03_queries.sql`](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/03_queries.sql) — Demonstration queries (JOINs, CTEs, window functions, transactions)
- [`04_functions.sql`](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/04_functions.sql) — Analytical user-defined functions
- [`05_procedures.sql`](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/05_procedures.sql) — Atomic business logic stored procedures
- [`06_triggers.sql`](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/06_triggers.sql) — Audit logging triggers
- [`07_tests.sql`](file:///e:/Projects/Oracle+WebProgramming/ShareSync_MySQL/database/mysql/07_tests.sql) — Verification test script
