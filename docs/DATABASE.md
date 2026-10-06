# Database — MySQL Version

The authoritative database for this branch is **MySQL 8.x** (InnoDB storage engine, `utf8mb4_unicode_ci` character set).

---

## Complete Table Schema (14 Tables)

1. `APP_USERS`: Investor and administrator authentication credentials and roles.
2. `SECTORS`: Industry classification and market categorization.
3. `COMPANIES`: Stock trading entities with current market pricing and capitalization.
4. `PORTFOLIOS`: User portfolio groupings for multi-portfolio wealth management.
5. `WATCHLISTS`: Customizable investor watchlists.
6. `WATCHLIST_ITEMS`: Companies tracked within watchlists with price targets.
7. `TRANSACTIONS`: Immutable record of BUY and SELL executions with audit linkage.
8. `DIVIDENDS`: Corporate action cash dividend declarations and payouts.
9. `TRANSACTION_AUDIT`: Historical audit trail capturing INSERT, UPDATE, and DELETE operations.
10. `PORTFOLIO_SNAPSHOTS`: Periodic and daily mark-to-market portfolio valuations.
11. `COMPANY_PRICE_HISTORY`: Historical OHLCV market observations for Investment Intelligence.
12. `ALERTS`: Real-time investor threshold alerts on company prices and portfolio values.
13. `NOTIFICATIONS`: In-app notification center for alerts, dividends, and system events.
14. `PORTFOLIO_GOALS`: Goal-oriented investment tracking (target valuation, return, dividend income).

---

## Database Programmability

- **Holdings View (`vw_portfolio_holdings`)**: Dynamically computes holdings, current market value, volume-weighted average purchase price, and unrealized profit/loss.
- **Stored Functions**:
  - `fn_get_weighted_avg_price(portfolio_id, company_id)`
  - `fn_calculate_unrealized_pl(portfolio_id, company_id)`
- **Stored Procedures**:
  - `sp_record_transaction(portfolio_id, company_id, type, qty, price, changed_by, OUT id, OUT status, OUT msg)`: Enforces oversell checks and inserts atomically.
  - `sp_generate_portfolio_snapshot(portfolio_id, date, OUT value, OUT status, OUT msg)`: Upserts mark-to-market valuation.
- **Audit Triggers**:
  - `trg_transactions_after_insert`: Logs new transactions.
  - `trg_transactions_after_update`: Logs changes in quantities and prices.
  - `trg_transactions_after_delete`: Logs transaction deletions while preserving permanent audit records with `transaction_id = NULL`.

---

## SQL Scripts Location
All MySQL scripts are located in `database/mysql/`:
- `setup.sql` — Master reproducible deployment script
- `01_schema.sql` — Complete DDL schema
- `02_seed.sql` — Complete demonstration dataset
- `03_queries.sql` — Demonstration query pack
- `04_functions.sql` — User-defined functions
- `05_procedures.sql` — Stored procedures
- `06_triggers.sql` — Audit triggers
- `07_tests.sql` — Automated verification script
