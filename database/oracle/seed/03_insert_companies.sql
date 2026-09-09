INSERT INTO companies (
    company_name,
    ticker_symbol,
    sector_id,
    current_price,
    market_cap
)
VALUES (
    'Example Technology Ltd.',
    'EXTECH',
    1,
    150.00,
    5000000000
);

INSERT INTO companies (
    company_name,
    ticker_symbol,
    sector_id,
    current_price,
    market_cap
)
VALUES (
    'Example Bank Ltd.',
    'EXBANK',
    2,
    85.50,
    3000000000
);

INSERT INTO companies (
    company_name,
    ticker_symbol,
    sector_id,
    current_price,
    market_cap
)
VALUES (
    'Example Pharma Ltd.',
    'EXPHARMA',
    3,
    120.75,
    2500000000
);

COMMIT;