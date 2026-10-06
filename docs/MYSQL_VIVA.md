# ShareSync — MySQL Database Systems Viva & Oral Exam Cheat Sheet

This document prepares you for technical oral questions during the Database Systems course evaluation for **ShareSync**.

---

### 1. Architectural & Database Fundamentals

#### Q: Why MySQL?
**A:** MySQL 8.x is an industry-standard, ACID-compliant open-source relational database management system. It provides high-performance transactional processing via the InnoDB engine, robust support for foreign keys, check constraints, row-level locking, views, stored routines, triggers, Common Table Expressions (CTEs), and native integration with modern .NET via the Pomelo MySQL EF Core provider.

#### Q: Why a Relational Database instead of NoSQL?
**A:** Financial portfolio tracking demands **strict transactional consistency (ACID)**. Transactions, cash balances, and holdings must never be partially written or out of sync. Relational databases enforce referential integrity across users, portfolios, companies, and transactions via declarative primary and foreign key constraints that prevent orphaned records and data corruption.

#### Q: Why normalization?
**A:** Normalization organizes relational data to eliminate redundancy and prevent insertion, update, and deletion anomalies. In ShareSync, company details and sector names are stored once in their respective tables rather than duplicated across every single transaction or watchlist row.

#### Q: Explain 1NF, 2NF, and 3NF in ShareSync.
- **1NF (First Normal Form):** Each table cell contains atomic, single-valued attributes (no comma-separated lists of tickers or prices), and every table possesses a primary key.
- **2NF (Second Normal Form):** Satisfies 1NF, and all non-key attributes are fully functionally dependent on the entire primary key. In `watchlist_items`, where the composite key is `(watchlist_id, company_id)`, attributes such as `target_price` and `notes` depend on both keys, while company metadata remains isolated in `companies`.
- **3NF (Third Normal Form):** Satisfies 2NF, and has no transitive dependencies (non-key columns dependent on other non-key columns). In `companies`, we store `sector_id` rather than the sector name and description, removing transitive dependence through the company.

---

### 2. Keys & Relationships

#### Q: What is a Primary Key?
**A:** A primary key is a column (or set of columns) that uniquely identifies each tuple in a table. It enforces entity integrity by ensuring values are non-null and unique (e.g., `user_id` in `app_users`, `transaction_id` in `transactions`).

#### Q: What is a Foreign Key?
**A:** A foreign key is an attribute in one table that references the primary key of another table, enforcing referential integrity. For example, `transactions.portfolio_id` references `portfolios.portfolio_id` with `ON DELETE RESTRICT` to ensure a portfolio with existing financial transactions cannot be accidentally dropped.

#### Q: Why does `watchlist_items` have a Composite Key?
**A:** `watchlist_items` has a composite primary key composed of `(watchlist_id, company_id)`. This natural bridge table structure ensures an investor cannot accidentally track the exact same stock multiple times within the same watchlist, enforcing uniqueness without requiring an artificial surrogate ID.

#### Q: Why does `transactions` NOT redundantly store `user_id`?
**A:** In 3NF schema design, `transactions` belongs to a `portfolio_id`, and that `portfolio_id` belongs to a `user_id` in `portfolios`. Storing `user_id` directly in `transactions` would introduce a transitive dependency (`transaction_id -> portfolio_id -> user_id`) and risk inconsistency if the portfolio ownership were changed.

---

### 3. SQL Queries & Database Programming

#### Q: What is a JOIN?
**A:** A JOIN combines rows from two or more tables based on a related column. ShareSync uses `INNER JOIN` to link transactions with their respective portfolios and companies, and `LEFT JOIN` in reporting to list sectors even if no company is actively assigned to that sector.

#### Q: Why `GROUP BY` and what is `HAVING`?
- **`GROUP BY`:** Collapses rows with identical values in specified columns into summary rows (e.g., grouping `transactions` by `portfolio_id` and `company_id` to compute net holdings).
- **`HAVING`:** Filters grouped results based on aggregate calculations (e.g., `HAVING SUM(...) > 0` to return only active holdings with remaining positive shares, whereas `WHERE` filters rows before grouping).

#### Q: What is a Subquery?
**A:** A subquery is a query nested inside another query (e.g., finding all companies whose current price exceeds the average price of their sector using `WHERE current_price > (SELECT AVG(...) FROM companies c2 WHERE c2.sector_id = c.sector_id)`).

#### Q: What is a CTE (Common Table Expression)?
**A:** A CTE is a temporary named result set defined using the `WITH` clause that exists only within the execution scope of a statement. ShareSync uses CTEs to calculate cumulative cash flow timelines and rank top-performing stocks per sector before joining them with portfolio metadata.

#### Q: What is a VIEW in ShareSync?
**A:** A view is a virtual table defined by an underlying stored SQL query. ShareSync implements `vw_portfolio_holdings` to calculate volume-weighted average buy prices (`weighted_average_buy_price`), current market value (`current_market_value`), and mark-to-market unrealized profit/loss (`unrealized_profit_loss`) on the fly, guaranteeing consistent financial reporting across the application without storing derived state.

#### Q: What is the difference between a Stored Function and a Stored Procedure?
- **Function (`fn_*`):** Must return a single scalar value, cannot commit or roll back transactions, and can be used directly inside `SELECT`, `WHERE`, and `HAVING` expressions (e.g., `fn_get_weighted_avg_price` and `fn_calculate_unrealized_pl`).
- **Procedure (`sp_*`):** Can perform multiple operations, return output parameters or result sets, manage transactions, and execute business logic (e.g., `sp_record_transaction` which validates parameters, checks for oversell violations, and performs atomic inserts).

#### Q: What is a Trigger and how is it used in ShareSync?
**A:** A trigger is procedural SQL code automatically executed in response to table modifications (`AFTER INSERT`, `AFTER UPDATE`, `AFTER DELETE`). In ShareSync, triggers on the `transactions` table (`trg_transactions_after_*`) capture all modifications into `transaction_audit`, recording the old/new quantities, prices, timestamps, and invoking user for forensic auditability.

#### Q: What is an Index and where are they applied?
**A:** An index is a B-tree data structure that accelerates row retrieval by avoiding full table scans. ShareSync establishes:
- Composite index `idx_transactions_portfolio_date` on `transactions(portfolio_id, transaction_date)` for fast portfolio history queries.
- Composite index `idx_price_hist_comp_date` on `company_price_history(company_id, recorded_at)` for instant historical market charting.
- Unique index on `companies(ticker_symbol)` and `app_users(email)`.

---

### 4. Transactions, Security & Architecture

#### Q: What are ACID properties?
- **Atomicity:** All operations in a transaction succeed, or all are rolled back.
- **Consistency:** The database transitions from one valid state to another, obeying all constraints.
- **Isolation:** Concurrent transactions execute without cross-session contamination (InnoDB default: REPEATABLE READ).
- **Durability:** Committed transactions persist even in the event of power loss.

#### Q: Explain `COMMIT`, `ROLLBACK`, and `SAVEPOINT`.
- **`COMMIT`:** Permanently saves all changes made during the current transaction.
- **`ROLLBACK`:** Reverts all modifications made during the current transaction back to the beginning.
- **`SAVEPOINT`:** Creates an intermediate rollback checkpoint within an active transaction, enabling partial rollback without aborting the entire transaction.

#### Q: How is SQL Injection prevented?
**A:** SQL injection is prevented by utilizing Entity Framework Core parameterized queries (`DbCommand` with SQL parameters) and stored procedures with typed parameters, ensuring user inputs are treated strictly as data literals and never executed as raw SQL.

#### Q: How is Authentication handled?
**A:** User passwords are never stored in plaintext; they are hashed using **PBKDF2 with HMAC-SHA256** and a cryptographically secure 128-bit salt. On login, the server issues a digitally signed **JWT Bearer Token** validated on every subsequent HTTP request.

#### Q: Why Entity Framework Core with MySQL?
**A:** EF Core bridges C# domain objects with MySQL relational tables. We use `Pomelo.EntityFrameworkCore.MySql`, which translates LINQ queries into optimized MySQL 8.0 SQL syntax, manages connection pooling, and handles entity change tracking.

#### Q: How are Holdings calculated?
**A:** Holdings are computed strictly using cumulative transaction history:
$$\text{Current Quantity} = \sum \text{BUY Quantity} - \sum \text{SELL Quantity}$$
$$\text{Weighted Average Cost} = \frac{\sum (\text{BUY Quantity} \times \text{Buy Price})}{\sum \text{BUY Quantity}}$$
$$\text{Unrealized P/L} = (\text{Current Quantity} \times \text{Current Price}) - (\text{Current Quantity} \times \text{Weighted Average Cost})$$

#### Q: How does Investment Intelligence work?
**A:** It computes deterministic historical quantitative metrics over observations from `company_price_history` sourced from Harvard Dataverse:
- **CAGR:** Compound Annual Growth Rate over the observation period.
- **Annualized Volatility:** Sample standard deviation of daily logarithmic returns scaled by $\sqrt{252}$.
- **Maximum Drawdown:** Peak-to-trough historical drop in price.
- **Overall Score:** Weighted multi-factor score out of 100 based on return, volatility, drawdown, consistency, and moving-average trend metrics.
