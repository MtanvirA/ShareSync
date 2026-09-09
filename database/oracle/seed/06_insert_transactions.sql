INSERT INTO transactions (
    portfolio_id,
    company_id,
    transaction_type,
    quantity,
    price_per_share,
    transaction_date
)
VALUES (
    1,
    1,
    'BUY',
    100,
    120.00,
    TIMESTAMP '2026-09-01 10:00:00'
);

INSERT INTO transactions (
    portfolio_id,
    company_id,
    transaction_type,
    quantity,
    price_per_share,
    transaction_date
)
VALUES (
    1,
    1,
    'BUY',
    50,
    130.00,
    TIMESTAMP '2026-09-03 11:30:00'
);

INSERT INTO transactions (
    portfolio_id,
    company_id,
    transaction_type,
    quantity,
    price_per_share,
    transaction_date
)
VALUES (
    1,
    1,
    'SELL',
    30,
    140.00,
    TIMESTAMP '2026-09-08 14:00:00'
);

COMMIT;