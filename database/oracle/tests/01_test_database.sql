SELECT
    t.portfolio_id,
    p.portfolio_name,
    t.company_id,
    c.company_name,
    c.ticker_symbol,

    SUM(CASE
        WHEN t.transaction_type = 'BUY'
        THEN t.quantity
        WHEN t.transaction_type = 'SELL'
        THEN -t.quantity
        ELSE 0
    END) AS current_quantity,

    c.current_price,

    SUM(CASE
        WHEN t.transaction_type = 'BUY'
        THEN t.quantity
        WHEN t.transaction_type = 'SELL'
        THEN -t.quantity
        ELSE 0
    END) * c.current_price AS current_market_value

FROM transactions t

JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id

JOIN companies c
    ON t.company_id = c.company_id

GROUP BY
    t.portfolio_id,
    p.portfolio_name,
    t.company_id,
    c.company_name,
    c.ticker_symbol,
    c.current_price

ORDER BY
    t.portfolio_id,
    t.company_id;