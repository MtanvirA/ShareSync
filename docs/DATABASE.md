# Database

The authoritative data store is Oracle Database 26ai.

## Core Tables
1. `USERS`: Authentication and identity.
2. `SECTORS`: Industry categorization.
3. `COMPANIES`: Trading entities.
4. `PORTFOLIOS`: User financial grouping.
5. `PORTFOLIO_HOLDINGS`: Aggregated active share quantities and averages.
6. `TRANSACTIONS`: Immutable record of BUY/SELL activity.
7. `DIVIDENDS`: Corporate action records.
8. `WATCHLISTS`: User preferences.
9. `WATCHLIST_ITEMS`: Tracked companies.
10. `COMPANY_PRICE_HISTORY`: High-volume historical market data.
11. `AUDIT_LOGS`: System-wide change tracking.

## Features
- **Referential Integrity:** Enforced via strict Primary and Foreign Keys.
- **Check Constraints:** Ensures `quantity > 0` and `price >= 0`.
- **Audit Triggers:** Oracle-level triggers log mutations automatically to `AUDIT_LOGS`.
