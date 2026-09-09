SELECT
    w.watchlist_id,
    w.watchlist_name,
    u.user_id,
    u.name AS user_name
FROM watchlists w
JOIN app_users u
    ON w.user_id = u.user_id
ORDER BY w.watchlist_id;