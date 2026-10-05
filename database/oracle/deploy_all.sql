-- ============================================================================
-- ShareSync - Master Oracle Deployment Script
-- File: deploy_all.sql
-- Description: Executes all schema, index, view, function, procedure, trigger,
--              and seed scripts in topological dependency order.
-- ============================================================================

SET ECHO ON;
SET FEEDBACK ON;
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK;

PROMPT ============================================================================
PROMPT Starting ShareSync Database Deployment
PROMPT ============================================================================

PROMPT 1. Creating Core Schema Tables...
@@schema/01_app_users.sql;
@@schema/02_sectors.sql;
@@schema/03_companies.sql;
@@schema/04_portfolios.sql;
@@schema/05_watchlists.sql;
@@schema/06_watchlist_items.sql;
@@schema/07_transactions.sql;
@@schema/08_dividends.sql;
@@schema/09_transaction_audit.sql;
@@schema/10_portfolio_snapshots.sql;
@@schema/12_company_price_history.sql;
@@schema/13_alerts.sql;
@@schema/15_notifications.sql;
@@schema/16_portfolio_goals.sql;

PROMPT 2. Creating Performance & Analytical Indexes...
@@schema/11_reporting_indexes.sql;

PROMPT 3. Creating Analytical Views...
@@views/01_vw_portfolio_holdings.sql;

PROMPT 4. Creating Stored Functions...
@@functions/01_fn_calculate_unrealized_pl.sql;
@@functions/02_fn_get_weighted_avg_price.sql;

PROMPT 5. Creating Stored Procedures...
@@procedures/01_sp_record_transaction.sql;
@@procedures/02_sp_generate_portfolio_snapshot.sql;

PROMPT 6. Creating Database Triggers...
@@triggers/01_trg_transactions_audit.sql;

PROMPT 7. Loading Reference & Seed Data...
@@seed/01_insert_sectors.sql;
@@seed/02_insert_companies.sql;
@@seed/03_insert_portfolios.sql;
@@seed/04_insert_watchlists.sql;
@@seed/05_insert_watchlist_items.sql;
@@seed/06_insert_transactions.sql;
@@seed/07_insert_dividends.sql;
@@seed/08_insert_portfolio_snapshots.sql;
@@seed/09_insert_company_price_history.sql;
@@seed/10_demo_dataset.sql;

COMMIT;

PROMPT ============================================================================
PROMPT ShareSync Database Deployment Complete
PROMPT ============================================================================
