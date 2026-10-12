================================================================================
                    SHARESYNC - DATABASE FEATURES SUMMARY
                Database Systems Course Project Evaluation
================================================================================

Project Name: ShareSync (Share Market Portfolio Tracker)
Database System: MySQL 8.x
Storage Engine: InnoDB (utf8mb4_unicode_ci)
Backend Framework: ASP.NET Core 8.x / Entity Framework Core (Pomelo MySQL)

--------------------------------------------------------------------------------
1. DATABASE ENGINE AND STORAGE CONFIGURATION
--------------------------------------------------------------------------------
1.1. MySQL 8.x with InnoDB Storage Engine:
     We chose MySQL's default InnoDB engine for all tables because our project
     deals with real financial records (transactions, portfolio values, and cash
     balances). InnoDB provides full ACID guarantees, row-level locking, and
     foreign key enforcement, which ensures that balance sheet data cannot become
     corrupted or inconsistent.

1.2. Character Set and Collation (utf8mb4 / utf8mb4_unicode_ci):
     The database is configured with utf8mb4 encoding so that text columns can
     safely store any international characters, currency symbols (such as BDT
     or USD), and company names without encoding issues.

--------------------------------------------------------------------------------
2. DATABASE TABLES AND THEIR PURPOSES (17 CORE TABLES)
--------------------------------------------------------------------------------
Our database schema is organized into 17 normalized tables:

1. app_users:
   Stores user account records, including name, email, hashed password, role
   (INVESTOR or ADMIN), and account active status.

2. sectors:
   Stores stock market industry sectors (such as Pharmaceuticals, Banking,
   Telecommunication, Fuel & Power, and Food & Allied).

3. companies:
   Catalog of listed securities. Stores ticker symbol (e.g., GP, BATBC), company
   name, current market price, sector reference, currency, and asset type (EQUITY,
   ETF, MUTUAL_FUND, CASH).

4. portfolios:
   Stores user investment portfolios (e.g., Main Investment Portfolio, Dividend
   Income Portfolio), preferred currency, and owner reference.

5. transactions:
   The core financial ledger that records every BUY and SELL order with share
   quantity, execution price, brokerage fee, and execution date.

6. dividends:
   Records corporate cash dividend payouts for companies, along with declaration
   and payment dates.

7. watchlists:
   Stores custom stock watchlists created by investors to monitor market symbols.

8. watchlist_items:
   Junction table linking watchlists to tracked companies, along with an optional
   target purchase price.

9. transaction_audit:
   Forensic audit table that automatically logs every INSERT, UPDATE, or DELETE
   action performed on the transactions table.

10. portfolio_snapshots:
    Stores historical daily total portfolio valuations used for charting portfolio
    performance and value trends over time.

11. company_price_history:
    Stores daily historical price data (open, high, low, close, volume) for
    technical indicator calculations (SMA 50, SMA 200, volatility).

12. alerts:
    Stores user-configured price and portfolio threshold rules (such as
    PRICE_ABOVE or PRICE_BELOW) that trigger notifications when conditions are met.

13. notifications:
    In-app user notification feed for triggered alerts, dividend payments, and
    system updates.

14. portfolio_goals:
    Stores financial target milestones set by users (e.g., target portfolio value
    or target return percentage) along with target completion dates.

15. inflation_rates:
    Stores historical annual macroeconomic inflation rates by country and year,
    used to calculate inflation-adjusted real returns using the Fisher Equation.

16. currency_exchange_rates:
    Stores foreign exchange conversion rates (e.g., USD to BDT) with effective
    dates to support multi-currency portfolio valuation.

17. benchmark_price_history:
    Stores daily historical index closing values for Dhaka Stock Exchange indices
    (DSEX, DS30, DSES) to calculate portfolio alpha and benchmark outperformance.

*(Note: We also created two staging tables, historical_price_staging and
benchmark_price_staging, to validate raw CSV datasets before loading them).*

--------------------------------------------------------------------------------
3. PRIMARY KEYS AND FOREIGN KEYS
--------------------------------------------------------------------------------
3.1. Single-Column Surrogate Primary Keys:
     Most tables use an auto-incrementing integer as the primary key
     (e.g., user_id in app_users, transaction_id in transactions, portfolio_id in
     portfolios). This guarantees each record has a unique, compact identifier.

3.2. Composite Primary Key:
     The watchlist_items table uses a composite primary key made of two columns:
     (watchlist_id, company_id).
     - Why we used it: An investor should never add the exact same stock twice to
       the same watchlist. The composite primary key enforces this uniqueness rule
       at the database engine level without needing a synthetic ID.

3.3. Foreign Keys with Referential Integrity Rules:
     We defined foreign keys to connect related tables and used appropriate
     deletion rules to prevent orphaned records:
     - ON DELETE RESTRICT:
       Applied to critical financial relationships such as:
       * transactions -> portfolios (fk_transactions_portfolio)
       * transactions -> companies (fk_transactions_company)
       * portfolios -> app_users (fk_portfolios_user)
       * companies -> sectors (fk_companies_sector)
       If a portfolio or company has recorded transactions, the database prevents
       it from being deleted. This protects the historical financial ledger.
     - ON DELETE CASCADE:
       Applied to dependent child records:
       * watchlist_items -> watchlists (deleting a watchlist deletes its items)
       * alerts -> app_users (deleting a user removes their alerts)
       * portfolio_snapshots -> portfolios (deleting a portfolio removes snapshots)
     - ON DELETE SET NULL:
       Applied to transaction_audit -> transactions (fk_transaction_audit_transaction).
       If a transaction row is deleted, the audit trail row remains preserved in the
       audit table with transaction_id set to NULL, keeping full history intact.

--------------------------------------------------------------------------------
4. TABLE RELATIONSHIPS
--------------------------------------------------------------------------------
4.1. One-to-Many Relationships (1:N):
     - app_users to portfolios: One user can create and manage multiple portfolios.
     - sectors to companies: One market sector contains multiple listed companies.
     - portfolios to transactions: One portfolio contains many trade records.
     - companies to dividends: One company issues many dividend payouts over time.
     - companies to company_price_history: One company has many daily price records.
     - portfolios to portfolio_snapshots: One portfolio has many daily snapshots.
     - app_users to alerts, notifications, and portfolio_goals.

4.2. Many-to-Many Relationships (M:N):
     - watchlists and companies: A watchlist can contain multiple companies, and
       the same company can appear in multiple watchlists created by different users.
       We implemented this using the associative junction table watchlist_items.

--------------------------------------------------------------------------------
5. DATA TYPES AND INTEGRITY CONSTRAINTS
--------------------------------------------------------------------------------
5.1. Exact Numeric Types for Financial Precision:
     Floating-point types (FLOAT, DOUBLE) cause rounding errors in finance.
     Instead, we used:
     - DECIMAL(14,2) for share prices, fees, and dividends (e.g., 380.50).
     - DECIMAL(14,4) for share quantities to support high-precision math.
     - DECIMAL(20,2) for large total values such as market capitalization and
       portfolio valuation.

5.2. Date and Time Types:
     - DATE: For calendar-only values (e.g., trading_date, snapshot_date, payment_date).
     - TIMESTAMP / DATETIME: For chronological events and audit stamps, with
       DEFAULT CURRENT_TIMESTAMP.

5.3. NOT NULL Constraints:
     Enforced on all mandatory fields such as email, ticker symbol, transaction
     quantity, and execution price.

5.4. UNIQUE Constraints:
     Enforced to prevent duplicate data:
     - uq_app_users_email on app_users(email)
     - uq_sectors_name on sectors(sector_name)
     - uq_companies_ticker on companies(ticker_symbol)
     - uq_portfolios_user_name on portfolios(user_id, portfolio_name) -> prevents
       the same user from naming two portfolios identically.
     - uq_portfolio_snapshot_date on portfolio_snapshots(portfolio_id, snapshot_date)
       -> prevents duplicate snapshots for the same portfolio on the same day.
     - uq_price_hist_comp_trading_date on company_price_history(company_id, trading_date)
     - uq_bench_code_date on benchmark_price_history(benchmark_code, trading_date)
     - uq_inflation_rates_country_year on inflation_rates(country_code, year)
     - uq_currency_exchange_rates on currency_exchange_rates(from_currency, to_currency, effective_date)

5.5. CHECK Constraints:
     Enforced at the MySQL engine level to validate business rules:
     - ck_transactions_type: CHECK (transaction_type IN ('BUY', 'SELL'))
     - ck_transactions_quantity: CHECK (quantity > 0)
     - ck_transactions_price: CHECK (price_per_share > 0)
     - ck_transactions_fee: CHECK (fee >= 0)
     - ck_companies_price: CHECK (current_price > 0)
     - ck_companies_asset_type: CHECK (asset_type IN ('EQUITY', 'ETF', 'MUTUAL_FUND', 'CASH'))
     - ck_companies_currency: CHECK (currency IN ('BDT', 'USD'))
     - ck_app_users_role: CHECK (role IN ('INVESTOR', 'ADMIN'))
     - ck_dividends_dates: CHECK (payment_date >= declaration_date)
     - ck_alerts_type: CHECK (alert_type IN ('PRICE_ABOVE', 'PRICE_BELOW', 'PORTFOLIO_VALUE_ABOVE', 'PORTFOLIO_VALUE_BELOW'))
     - ck_portfolio_goals_type: CHECK (goal_type IN ('TARGET_PORTFOLIO_VALUE', 'TARGET_RETURN', 'TARGET_DIVIDEND_INCOME'))
     - ck_inflation_rates_year: CHECK (year >= 1900 AND year <= 2100)
     - ck_transaction_audit_action: CHECK (action_type IN ('INSERT', 'UPDATE', 'DELETE'))

--------------------------------------------------------------------------------
6. DATABASE NORMALIZATION (1NF, 2NF, 3NF)
--------------------------------------------------------------------------------
Our schema is designed in Third Normal Form (3NF) to eliminate redundant data:

6.1. First Normal Form (1NF):
     - Every column holds atomic (single) values. We do not store comma-separated
       lists of stocks or values inside a single column.
     - Each table has a defined primary key that uniquely identifies each row.

6.2. Second Normal Form (2NF):
     - The schema satisfies 1NF, and all non-key columns depend on the entire
       primary key.
     - In the composite-key table watchlist_items(watchlist_id, company_id), the
       target_price attribute depends on both the watchlist and the company together.

6.3. Third Normal Form (3NF):
     - The schema satisfies 2NF, and there are no transitive functional dependencies
       (non-key columns do not depend on other non-key columns).
     - Example: In the companies table, we store sector_id rather than repeating
       the sector name and description in every company row.
     - Example: In transactions, we store portfolio_id and company_id. We do not
       store user name or company name in transactions because those can be reached
       through the respective parent tables.
     - Dynamic values like net holdings and unrealized profit/loss are computed
       on the fly using views and functions rather than stored redundantly in a table,
       preventing update anomalies.

--------------------------------------------------------------------------------
7. CRUD OPERATIONS (CREATE, READ, UPDATE, DELETE)
--------------------------------------------------------------------------------
Our application performs standard CRUD operations across its modules:

7.1. Create (INSERT):
     - Registering new users in app_users.
     - Creating portfolios, watchlists, alerts, and goals.
     - Recording BUY and SELL trade executions into transactions.
     - Automatic insertion into transaction_audit via database triggers.

7.2. Read (SELECT):
     - Fetching portfolio holdings, account ledgers, and market quotes.
     - Reading calculated positions through the vw_portfolio_holdings view.
     - Paginated transaction queries filtered by portfolio, date, or ticker symbol.

7.3. Update (UPDATE):
     - Updating user profile details and settings.
     - Updating live stock prices in companies based on market data feeds.
     - Updating alert active flags when thresholds are triggered.
     - Upserting daily portfolio snapshots using INSERT ... ON DUPLICATE KEY UPDATE.

7.4. Delete (DELETE):
     - Removing a stock from a watchlist (deleting from watchlist_items).
     - Deleting alerts or goals when dismissed by the user.
     - Deleting transactions (the trigger automatically logs the deletion in the
       audit table, and foreign key SET NULL preserves the audit entry).

--------------------------------------------------------------------------------
8. SQL QUERIES, JOINS, GROUPING, AND AGGREGATIONS
--------------------------------------------------------------------------------
8.1. Multi-Table JOINs:
     - INNER JOIN: Links transactions with portfolios and companies to show
       complete trade summaries with company names, ticker symbols, and owner names.
     - LEFT JOIN: Used in sector summary queries to include all sectors even if
       a sector currently has zero listed companies.

8.2. Filtering, Sorting, and Pagination:
     - WHERE clauses filter transactions by user ownership, date ranges, and type.
     - ORDER BY sorts transactions chronologically or ranks companies by market cap.
     - LIMIT and OFFSET are used in backend queries for database-level pagination.

8.3. Aggregations (GROUP BY & HAVING):
     - Aggregate functions (SUM, AVG, COUNT, MIN, MAX) compute total invested capital,
       total volume traded, and dividend totals.
     - HAVING clause is used to filter aggregated groups. For example:
       HAVING SUM(BUY) - SUM(SELL) > 0 ensures that our holdings calculations
       only include active open positions and exclude fully sold positions.

8.4. Subqueries and Correlated Subqueries:
     - In 03_queries.sql, we use a correlated subquery to find companies whose
       current price exceeds the average price of companies in their own sector:
       WHERE c.current_price > (SELECT AVG(c2.current_price) FROM companies c2
                                WHERE c2.sector_id = c.sector_id)

8.5. Common Table Expressions (CTEs):
     - Used in analytical queries to organize multi-step calculations cleanly
       (e.g., WITH SectorRankings AS (...) to rank companies, and
       WITH TxSummary AS (...) to compute daily cash flow summaries).

8.6. Window Functions:
     - RANK() OVER (PARTITION BY sector_id ORDER BY current_price DESC):
       Ranks companies within each market sector by share price.
     - SUM() OVER (PARTITION BY portfolio_id ORDER BY tx_date):
       Computes running cumulative cash invested over time per portfolio.

--------------------------------------------------------------------------------
9. DATABASE INDEXES FOR PERFORMANCE OPTIMIZATION
--------------------------------------------------------------------------------
In addition to the default primary key clustered B-Tree indexes, we created
targeted secondary B-Tree indexes to accelerate our most frequent queries:

9.1. Transaction Ledger Indexes:
     - idx_transactions_portfolio_date on (portfolio_id, transaction_date):
       Accelerates ledger retrieval and date-range filtering for an investor's trades.
     - idx_transactions_company on (company_id):
       Speeds up queries looking for all trades of a specific company.

9.2. Price History and Benchmark Indexes:
     - idx_price_hist_comp_date on (company_id, recorded_at):
       Enables fast retrieval of historical price points for stock price charts.
     - idx_bench_code_date on (benchmark_code, trading_date):
       Optimizes historical index lookups (DSEX) for benchmark calculations.

9.3. Alert and Notification Indexes:
     - idx_alerts_user on (user_id, is_active):
       Quickly fetches active alert rules for a specific investor.
     - idx_alerts_price_eval on (is_active, company_id, alert_type):
       Allows the price evaluation engine to immediately find active alerts for a stock.
     - idx_notifications_user on (user_id, is_read, created_at):
       Optimizes unread notification count queries and feed display.

9.4. EXPLAIN Query Plan Analysis:
     - We tested our queries using MySQL's EXPLAIN command (demonstrated in
       03_queries.sql) to verify that the query planner uses these indexes instead
       of doing full table scans.

--------------------------------------------------------------------------------
10. DATABASE VIEWS
--------------------------------------------------------------------------------
View Name: vw_portfolio_holdings
File: database/mysql/01_schema.sql (Lines 569-598)

- What it is:
  A stored virtual query that dynamically calculates an investor's current active
  stock positions without storing duplicate balance columns in a table.

- What it calculates dynamically:
  1. Current Quantity = SUM(BUY quantity) - SUM(SELL quantity)
  2. Weighted Average Buy Price = SUM(BUY qty * BUY price) / SUM(BUY qty)
  3. Current Market Value = Current Quantity * Current Market Price
  4. Unrealized Profit/Loss = Current Market Value - (Current Quantity * Avg Buy Price)

- Why it is useful:
  Instead of repeating complex aggregation and math in multiple places in C#
  or SQL, any feature can simply query:
  SELECT * FROM vw_portfolio_holdings WHERE portfolio_id = 1;

- Liquidated position filtering:
  Uses a HAVING clause (HAVING current_quantity > 0) to automatically exclude
  positions that have been fully sold out.

--------------------------------------------------------------------------------
11. STORED FUNCTIONS (USER-DEFINED FUNCTIONS)
--------------------------------------------------------------------------------
We created 5 deterministic stored functions in MySQL (file: 04_functions.sql):

1. fn_get_weighted_avg_price(p_portfolio_id, p_company_id):
   - Computes the volume-weighted average purchase price across historical BUY trades.
   - Formula: SUM(quantity * price_per_share) / SUM(quantity).

2. fn_calculate_unrealized_pl(p_portfolio_id, p_company_id):
   - Computes mark-to-market unrealized gain or loss for an open stock position.
   - Formula: Current Quantity * (Current Market Price - Weighted Average Buy Price).

3. fn_calculate_realized_pl(p_portfolio_id, p_company_id):
   - Computes cumulative realized profit or loss generated from executed SELL trades.
   - Formula: SUM((Sell Qty * Sell Price - Fee) - (Sell Qty * Weighted Avg Buy Price)).

4. fn_calculate_net_cash_flow(p_portfolio_id):
   - Computes the net capital injected or withdrawn across a portfolio.
   - Formula: Total Net Sale Inflows - Total Net Purchase Outflows (including fees).

5. fn_calculate_real_return(p_nominal_return, p_inflation_rate):
   - Implements the financial Fisher Equation to convert nominal investment return
     into purchasing-power real return adjusted for official inflation.
   - Formula: [((1 + Nominal / 100) / (1 + Inflation / 100)) - 1] * 100.

--------------------------------------------------------------------------------
12. STORED PROCEDURES
--------------------------------------------------------------------------------
We implemented 2 stored procedures in MySQL (file: 05_procedures.sql):

1. sp_record_transaction:
   - What it does:
     Safely executes a BUY or SELL order with business rule validation and
     oversell prevention.
   - Parameters:
     IN  p_portfolio_id, p_company_id, p_transaction_type, p_quantity,
         p_price_per_share, p_fee, p_changed_by
     OUT p_transaction_id, p_status, p_message
   - Business Logic & Oversell Protection:
     1. Validates that quantity is a positive whole integer (rejects fractional shares).
     2. Validates positive price and non-negative fee.
     3. Verifies that the referenced portfolio and company actually exist.
     4. For SELL orders: Computes available shares (total bought - total sold).
        If requested sell quantity exceeds available holdings, it immediately
        aborts and returns status 'OVERSELL_REJECTED'.
     5. If all checks pass, it inserts the transaction atomically and returns
        the generated transaction ID and status 'SUCCESS'.

2. sp_generate_portfolio_snapshot:
   - What it does:
     Calculates total mark-to-market valuation across all active holdings in a
     portfolio on a given date and saves it into portfolio_snapshots.
   - Uses MySQL's INSERT ... ON DUPLICATE KEY UPDATE syntax so running it
     multiple times on the same date safely updates the valuation instead of failing.

--------------------------------------------------------------------------------
13. DATABASE TRIGGERS AND FORENSIC AUDITING
--------------------------------------------------------------------------------
We created 3 row-level triggers on the transactions table (file: 06_triggers.sql)
that automatically maintain an immutable audit trail in transaction_audit:

1. trg_transactions_after_insert:
   - Fires AFTER INSERT ON transactions FOR EACH ROW.
   - Writes an 'INSERT' record to transaction_audit capturing the new transaction
     ID, transaction type, share quantity, execution price, fee, portfolio ID,
     company ID, and the executing user.

2. trg_transactions_after_update:
   - Fires AFTER UPDATE ON transactions FOR EACH ROW.
   - Writes an 'UPDATE' record capturing the old and new values
     (e.g., 'Qty 10 -> 15, Price 380 -> 382').

3. trg_transactions_after_delete:
   - Fires AFTER DELETE ON transactions FOR EACH ROW.
   - Writes a 'DELETE' record preserving the deleted trade's details even though
     the row was removed from the active transactions ledger.

- Why database triggers are important:
  Because triggers execute directly inside the MySQL database engine, an audit
  record is created no matter how the data was modified (whether through our
  web interface, a background service, or an interactive SQL query).

--------------------------------------------------------------------------------
14. TRANSACTIONS, ACID PROPERTIES, AND DATA CONSISTENCY
--------------------------------------------------------------------------------
14.1. ACID Properties with MySQL InnoDB:
     - Atomicity: Multi-step database operations succeed completely or fail
       completely with no partial state saved.
     - Consistency: Relational constraints (foreign keys, unique keys, and check
       constraints) guarantee that only valid data enters the system.
     - Isolation: Row-level locking ensures concurrent user sessions do not read
       uncommitted data or interfere with each other.
     - Durability: Committed transactions are permanently written to disk logs
       and survive server crashes or restarts.

14.2. SQL Transaction Commands:
     Demonstrated in 03_queries.sql:
     - START TRANSACTION: Begins an atomic unit of work.
     - SAVEPOINT: Sets a named checkpoint within a transaction.
     - ROLLBACK TO SAVEPOINT / ROLLBACK: Reverts uncommitted changes back to
       the previous clean state.
     - COMMIT: Permanently saves changes to the database.

14.3. Application-Level Transaction Handling:
     In our ASP.NET Core backend (TransactionService.cs), order placement uses
     Entity Framework Core transaction scopes:
     BeginTransactionAsync -> SaveChangesAsync -> CommitAsync, wrapped in
     try/catch blocks with automatic rollback if an error occurs.

--------------------------------------------------------------------------------
15. DATABASE SECURITY AND INTEGRITY PROTECTION
--------------------------------------------------------------------------------
15.1. SQL Injection Protection:
     - In our backend C# code, all queries use EF Core LINQ expressions which are
       compiled into parameterized SQL with bind variables (@p0, @p1).
     - User inputs are never concatenated into raw SQL strings.
     - Stored procedures use strongly typed IN parameters, preventing injection.

15.2. Credential Security:
     - Passwords are never stored in plain text. The app_users table stores
       secure password hashes in password_hash VARCHAR(255).

15.3. Role-Based Access Control:
     - Enforced at the database level with ck_app_users_role (CHECK role IN
       ('INVESTOR', 'ADMIN')) and in the application authorization layer.

--------------------------------------------------------------------------------
16. DATABASE TESTING AND EVALUATION SCRIPTS
--------------------------------------------------------------------------------
We created ready-to-run SQL scripts in database/mysql/ to test and verify our database:

1. setup.sql:
   Master script that executes the complete database deployment in correct
   dependency order.

2. 01_schema.sql:
   Contains the authoritative DDL for all 17 tables, indexes, constraints, and view.

3. 02_seed.sql:
   Populates realistic evaluation data (4 portfolios, 69 transactions, 15 companies).

4. 03_demo_validation.sql:
   Audits row counts, referential integrity, and financial consistency.

5. 03_queries.sql:
   Interactive demonstration queries for viva examination (JOINs, CTEs, Windows,
   subqueries, transactions, and index EXPLAIN plans).

6. 04_functions.sql, 05_procedures.sql, 06_triggers.sql:
   All programmable database objects.

7. 07_tests.sql:
   Automated end-to-end test script verifying table counts, trigger firing, view
   calculations, and procedure return values.

8. reset_demo_data.sql:
   Quick reset script that restores clean demonstration baseline data at any time
   during evaluation.

================================================================================
