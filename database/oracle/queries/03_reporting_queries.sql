-- ============================================================
-- ShareSync - Six Core Academic Reporting Queries (Oracle SQL)
-- File: 03_reporting_queries.sql
-- ============================================================

-- ============================================================
-- REPORT 1: Portfolio Holdings Report
-- Displays all current stock holdings for a portfolio with
-- market value, cost basis, and unrealized profit/loss.
-- ============================================================
SELECT
    p.portfolio_id,
    p.portfolio_name,
    c.ticker_symbol,
    c.company_name,
    s.sector_name,
    SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE -t.quantity END) AS current_quantity,
    c.current_price,
    ROUND(SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE -t.quantity END) * c.current_price, 2) AS market_value,
    ROUND(
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) /
        NULLIF(SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END), 0),
        2
    ) AS weighted_avg_buy_price,
    ROUND(
        (SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE -t.quantity END) * c.current_price) -
        (SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE -t.quantity END) * (
            SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) /
            NULLIF(SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END), 0)
        )),
        2
    ) AS unrealized_profit_loss
FROM transactions t
JOIN portfolios p ON t.portfolio_id = p.portfolio_id
JOIN companies c  ON t.company_id = c.company_id
JOIN sectors s    ON c.sector_id = s.sector_id
WHERE p.portfolio_id = 1
GROUP BY p.portfolio_id, p.portfolio_name, c.ticker_symbol, c.company_name, s.sector_name, c.current_price
HAVING SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE -t.quantity END) > 0
ORDER BY market_value DESC;


-- ============================================================
-- REPORT 2: Portfolio Performance & Overall Profit/Loss Report
-- Aggregates total invested cost, current valuation, and ROI %.
-- ============================================================
WITH PortfolioHoldings AS (
    SELECT
        t.portfolio_id,
        t.company_id,
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE -t.quantity END) AS current_qty,
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity * t.price_per_share ELSE 0 END) /
            NULLIF(SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE 0 END), 0) AS avg_cost,
        c.current_price
    FROM transactions t
    JOIN companies c ON t.company_id = c.company_id
    WHERE t.portfolio_id = 1
    GROUP BY t.portfolio_id, t.company_id, c.current_price
    HAVING SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE -t.quantity END) > 0
)
SELECT
    p.portfolio_id,
    p.portfolio_name,
    COUNT(h.company_id) AS total_companies_held,
    ROUND(SUM(h.current_qty * h.avg_cost), 2) AS total_cost_basis,
    ROUND(SUM(h.current_qty * h.current_price), 2) AS current_portfolio_value,
    ROUND(SUM(h.current_qty * h.current_price) - SUM(h.current_qty * h.avg_cost), 2) AS total_unrealized_profit_loss,
    ROUND(
        ((SUM(h.current_qty * h.current_price) - SUM(h.current_qty * h.avg_cost)) /
         NULLIF(SUM(h.current_qty * h.avg_cost), 0)) * 100,
        2
    ) AS return_percentage
FROM portfolios p
LEFT JOIN PortfolioHoldings h ON p.portfolio_id = h.portfolio_id
WHERE p.portfolio_id = 1
GROUP BY p.portfolio_id, p.portfolio_name;


-- ============================================================
-- REPORT 3: Transaction History Report
-- Chronological audit-grade ledger of trades with total amounts.
-- ============================================================
SELECT
    t.transaction_id,
    p.portfolio_name,
    c.ticker_symbol,
    c.company_name,
    t.transaction_type,
    t.quantity,
    t.price_per_share,
    ROUND(t.quantity * t.price_per_share, 2) AS total_amount,
    TO_CHAR(t.transaction_date, 'YYYY-MM-DD HH24:MI:SS') AS formatted_date
FROM transactions t
JOIN portfolios p ON t.portfolio_id = p.portfolio_id
JOIN companies c  ON t.company_id = c.company_id
WHERE p.portfolio_id = 1
ORDER BY t.transaction_date DESC, t.transaction_id DESC;


-- ============================================================
-- REPORT 4: Company / Sector Investment Report
-- Evaluates portfolio diversification across different market sectors.
-- ============================================================
WITH SectorTotals AS (
    SELECT
        s.sector_name,
        SUM(t.quantity * c.current_price) AS sector_market_val,
        COUNT(DISTINCT c.company_id) AS companies_in_sector
    FROM transactions t
    JOIN companies c ON t.company_id = c.company_id
    JOIN sectors s   ON c.sector_id = s.sector_id
    WHERE t.portfolio_id = 1
    GROUP BY s.sector_name
)
SELECT
    sector_name,
    companies_in_sector,
    ROUND(sector_market_val, 2) AS sector_investment_value,
    ROUND((sector_market_val / NULLIF(SUM(sector_market_val) OVER(), 0)) * 100, 2) AS allocation_percentage
FROM SectorTotals
ORDER BY sector_investment_value DESC;


-- ============================================================
-- REPORT 5: Dividend Income Report
-- Tracks historical and declared dividend payouts for owned shares.
-- ============================================================
SELECT
    d.dividend_id,
    c.ticker_symbol,
    c.company_name,
    d.dividend_per_share,
    d.declaration_date,
    d.payment_date,
    NVL(h.current_shares, 0) AS shares_held,
    ROUND(d.dividend_per_share * NVL(h.current_shares, 0), 2) AS estimated_payout
FROM dividends d
JOIN companies c ON d.company_id = c.company_id
LEFT JOIN (
    SELECT
        t.company_id,
        SUM(CASE WHEN t.transaction_type = 'BUY' THEN t.quantity ELSE -t.quantity END) AS current_shares
    FROM transactions t
    WHERE t.portfolio_id = 1
    GROUP BY t.company_id
) h ON c.company_id = h.company_id
ORDER BY d.payment_date DESC;


-- ============================================================
-- REPORT 6: Watchlist / Target Price Report
-- Compares watchlist target price against live price with alert status.
-- ============================================================
SELECT
    w.watchlist_id,
    w.watchlist_name,
    c.ticker_symbol,
    c.company_name,
    c.current_price,
    wi.target_price,
    ROUND(c.current_price - wi.target_price, 2) AS price_difference,
    ROUND(((c.current_price - wi.target_price) / NULLIF(wi.target_price, 0)) * 100, 2) AS diff_percentage,
    CASE
        WHEN wi.target_price IS NOT NULL AND c.current_price <= wi.target_price THEN 'BUY_TARGET_REACHED'
        WHEN wi.target_price IS NOT NULL AND c.current_price > wi.target_price THEN 'ABOVE_TARGET'
        ELSE 'NO_TARGET_SET'
    END AS alert_status
FROM watchlist_items wi
JOIN watchlists w ON wi.watchlist_id = w.watchlist_id
JOIN companies c  ON wi.company_id = c.company_id
WHERE w.user_id = 1
ORDER BY w.watchlist_name, c.ticker_symbol;
