SELECT
    ps.snapshot_id,
    p.portfolio_name,
    ps.snapshot_date,
    ps.total_value
FROM portfolio_snapshots ps
JOIN portfolios p
    ON ps.portfolio_id = p.portfolio_id
ORDER BY ps.snapshot_date;