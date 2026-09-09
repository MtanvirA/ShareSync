SELECT
    c.company_id,
    c.company_name,
    c.ticker_symbol,
    s.sector_name,
    c.current_price
FROM companies c
JOIN sectors s
    ON c.sector_id = s.sector_id
ORDER BY c.company_id;