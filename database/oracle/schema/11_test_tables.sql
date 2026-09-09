SELECT
    wi.watchlist_id,
    w.watchlist_name,
    wi.company_id,
    c.company_name,
    c.ticker_symbol,
    wi.target_price,
    wi.added_at
FROM watchlist_items wi
JOIN watchlists w
    ON wi.watchlist_id = w.watchlist_id
JOIN companies c
    ON wi.company_id = c.company_id
ORDER BY wi.watchlist_id, wi.company_id;