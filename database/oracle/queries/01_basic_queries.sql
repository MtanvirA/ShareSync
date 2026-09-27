SELECT
    user_id,
    name,
    email,
    created_at
FROM app_users
ORDER BY user_id;

SELECT
    c.company_id,
    c.company_name,
    c.ticker_symbol,
    c.current_price,
    s.sector_name
FROM companies c
JOIN sectors s
    ON c.sector_id = s.sector_id
ORDER BY s.sector_name, c.company_name;

SELECT
    c.company_id,
    c.company_name,
    c.ticker_symbol,
    c.current_price,
    s.sector_name
FROM companies c
JOIN sectors s
    ON c.sector_id = s.sector_id
ORDER BY s.sector_name, c.company_name;

SELECT
    p.portfolio_id,
    p.portfolio_name,
    u.name AS owner_name,
    u.email
FROM portfolios p
JOIN app_users u
    ON p.user_id = u.user_id
ORDER BY u.name, p.portfolio_name;

SELECT
    w.watchlist_name,
    c.company_name,
    c.ticker_symbol,
    wi.target_price,
    c.current_price
FROM watchlist_items wi
JOIN watchlists w
    ON wi.watchlist_id = w.watchlist_id
JOIN companies c
    ON wi.company_id = c.company_id
ORDER BY w.watchlist_name, c.company_name;

SELECT
    t.transaction_id,
    p.portfolio_name,
    c.company_name,
    c.ticker_symbol,
    t.transaction_type,
    t.quantity,
    t.price_per_share,
    t.quantity * t.price_per_share AS transaction_value,
    t.transaction_date
FROM transactions t
JOIN portfolios p
    ON t.portfolio_id = p.portfolio_id
JOIN companies c
    ON t.company_id = c.company_id
ORDER BY t.transaction_date;


SELECT
    s.sector_name,
    COUNT(c.company_id) AS company_count
FROM sectors s
LEFT JOIN companies c
    ON s.sector_id = c.sector_id
GROUP BY
    s.sector_id,
    s.sector_name
ORDER BY company_count DESC;


SELECT
    transaction_type,
    COUNT(*) AS transaction_count,
    SUM(quantity) AS total_shares
FROM transactions
GROUP BY transaction_type
ORDER BY transaction_type;


SELECT
    SUM(quantity * price_per_share) AS total_transaction_value
FROM transactions;



SELECT
    c.company_name,
    c.ticker_symbol,
    COUNT(wi.watchlist_id) AS watchlist_count
FROM companies c
JOIN watchlist_items wi
    ON c.company_id = wi.company_id
GROUP BY
    c.company_id,
    c.company_name,
    c.ticker_symbol
HAVING COUNT(wi.watchlist_id) > 1
ORDER BY watchlist_count DESC;


SELECT
    c.company_name,
    c.ticker_symbol,
    COUNT(d.dividend_id) AS dividend_count,
    SUM(d.dividend_per_share) AS total_dividend_per_share
FROM companies c
LEFT JOIN dividends d
    ON c.company_id = d.company_id
GROUP BY
    c.company_id,
    c.company_name,
    c.ticker_symbol
ORDER BY total_dividend_per_share DESC NULLS LAST;


SELECT
    p.portfolio_name,
    COUNT(t.transaction_id) AS transaction_count,
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
            THEN t.quantity * t.price_per_share
            ELSE 0
        END
    ) AS total_buy_value,
    SUM(
        CASE
            WHEN t.transaction_type = 'SELL'
            THEN t.quantity * t.price_per_share
            ELSE 0
        END
    ) AS total_sell_value
FROM portfolios p
LEFT JOIN transactions t
    ON p.portfolio_id = t.portfolio_id
GROUP BY
    p.portfolio_id,
    p.portfolio_name
ORDER BY p.portfolio_name;