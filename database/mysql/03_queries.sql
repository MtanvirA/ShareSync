-- ============================================================
-- ShareSync - MySQL Demonstration Queries
-- ============================================================

USE sharesync;


-- ============================================================
-- 1. CURRENT HOLDINGS
-- ============================================================

SELECT
    p.portfolio_name,
    c.company_name,
    c.ticker_symbol,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity
        END
    ) AS current_quantity,

    c.current_price,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity
        END
    ) * c.current_price AS current_market_value

FROM transactions t

JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    p.portfolio_id,
    p.portfolio_name,
    c.company_id,
    c.company_name,
    c.ticker_symbol,
    c.current_price

HAVING
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity
        END
    ) > 0

ORDER BY
    current_market_value DESC;



-- ============================================================
-- 2. TOTAL BUY COST
-- ============================================================

SELECT
    p.portfolio_name,
    c.company_name,
    c.ticker_symbol,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity * t.price_per_share
            ELSE 0
        END
    ) AS total_buy_cost

FROM transactions t

JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    p.portfolio_id,
    p.portfolio_name,
    c.company_id,
    c.company_name,
    c.ticker_symbol

ORDER BY
    p.portfolio_name,
    total_buy_cost DESC;




-- ============================================================
-- 3. TOTAL SELL VALUE
-- ============================================================

SELECT
    p.portfolio_name,
    c.company_name,
    c.ticker_symbol,

    SUM(
        CASE
            WHEN t.transaction_type = 'SELL'
                THEN t.quantity * t.price_per_share
            ELSE 0
        END
    ) AS total_sell_value

FROM transactions t

JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    p.portfolio_id,
    p.portfolio_name,
    c.company_id,
    c.company_name,
    c.ticker_symbol

ORDER BY
    p.portfolio_name,
    total_sell_value DESC;




-- ============================================================
-- 4. WEIGHTED AVERAGE BUY PRICE
-- ============================================================

SELECT
    p.portfolio_name,
    c.company_name,
    c.ticker_symbol,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity * t.price_per_share
            ELSE 0
        END
    )
    /
    NULLIF(
        SUM(
            CASE
                WHEN t.transaction_type = 'BUY'
                    THEN t.quantity
                ELSE 0
            END
        ),
        0
    ) AS weighted_average_buy_price

FROM transactions t

JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    p.portfolio_id,
    p.portfolio_name,
    c.company_id,
    c.company_name,
    c.ticker_symbol

ORDER BY
    p.portfolio_name,
    c.ticker_symbol;



-- ============================================================
-- 5. CURRENT MARKET VALUE
-- ============================================================

SELECT
    p.portfolio_name,
    c.company_name,
    c.ticker_symbol,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity
        END
    ) AS current_quantity,

    c.current_price,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity
        END
    ) * c.current_price AS current_market_value

FROM transactions t

JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    p.portfolio_id,
    p.portfolio_name,
    c.company_id,
    c.company_name,
    c.ticker_symbol,
    c.current_price

HAVING
    current_quantity > 0

ORDER BY
    current_market_value DESC;



-- ============================================================
-- 6. UNREALIZED PROFIT / LOSS
-- ============================================================

SELECT
    p.portfolio_name,
    c.company_name,
    c.ticker_symbol,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity
        END
    ) AS current_quantity,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity * t.price_per_share
            ELSE 0
        END
    ) AS total_buy_cost,

    c.current_price,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity
        END
    ) * c.current_price AS current_market_value,

    (
        SUM(
            CASE
                WHEN t.transaction_type = 'BUY'
                    THEN t.quantity
                WHEN t.transaction_type = 'SELL'
                    THEN -t.quantity
            END
        ) * c.current_price
    )
    -
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity * t.price_per_share
            ELSE 0
        END
    ) AS unrealized_profit_loss

FROM transactions t

JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    p.portfolio_id,
    p.portfolio_name,
    c.company_id,
    c.company_name,
    c.ticker_symbol,
    c.current_price

HAVING
    current_quantity > 0

ORDER BY
    unrealized_profit_loss DESC;


-- ============================================================
-- 7. TRANSACTION SUMMARY BY COMPANY
-- ============================================================

SELECT
    c.company_name,
    c.ticker_symbol,

    COUNT(*) AS total_transactions,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            ELSE 0
        END
    ) AS total_bought,

    SUM(
        CASE
            WHEN t.transaction_type = 'SELL'
                THEN t.quantity
            ELSE 0
        END
    ) AS total_sold

FROM transactions t

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    c.company_id,
    c.company_name,
    c.ticker_symbol

ORDER BY
    total_transactions DESC;


-- ============================================================
-- 8. WATCHLIST TARGET PRICE ANALYSIS
-- ============================================================

SELECT
    w.watchlist_name,
    c.company_name,
    c.ticker_symbol,

    wi.target_price,
    c.current_price,

    c.current_price - wi.target_price AS price_difference,

    CASE
        WHEN wi.target_price IS NULL
            THEN 'NO TARGET'

        WHEN c.current_price <= wi.target_price
            THEN 'TARGET REACHED'

        ELSE 'ABOVE TARGET'
    END AS target_status

FROM watchlist_items wi

JOIN watchlists w
    ON wi.watchlist_id = w.watchlist_id

JOIN companies c
    ON wi.company_id = c.company_id

ORDER BY
    w.watchlist_name,
    c.company_name;




-- ============================================================
-- 9. DIVIDEND SUMMARY
-- ============================================================

SELECT
    c.company_name,
    c.ticker_symbol,

    COUNT(d.dividend_id) AS dividend_records,

    SUM(d.dividend_per_share) AS total_dividend_per_share,

    MIN(d.payment_date) AS first_payment_date,

    MAX(d.payment_date) AS latest_payment_date

FROM dividends d

JOIN companies c
    ON d.company_id = c.company_id

GROUP BY
    c.company_id,
    c.company_name,
    c.ticker_symbol

ORDER BY
    total_dividend_per_share DESC;




-- ============================================================
-- 10. PORTFOLIO PERFORMANCE
-- ============================================================

SELECT
    p.portfolio_name,
    ps.snapshot_date,
    ps.total_value,

    ps.total_value
    -
    LAG(ps.total_value) OVER (
        PARTITION BY ps.portfolio_id
        ORDER BY ps.snapshot_date
    ) AS change_from_previous,

    ROUND(
        (
            ps.total_value
            -
            LAG(ps.total_value) OVER (
                PARTITION BY ps.portfolio_id
                ORDER BY ps.snapshot_date
            )
        )
        /
        NULLIF(
            LAG(ps.total_value) OVER (
                PARTITION BY ps.portfolio_id
                ORDER BY ps.snapshot_date
            ),
            0
        ) * 100,
        2
    ) AS percentage_change

FROM portfolio_snapshots ps

JOIN portfolios p
    ON ps.portfolio_id = p.portfolio_id

ORDER BY
    p.portfolio_name,
    ps.snapshot_date;