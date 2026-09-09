INSERT INTO dividends (
    company_id,
    dividend_per_share,
    declaration_date,
    payment_date
)
VALUES (
    1,
    2.50,
    DATE '2026-06-01',
    DATE '2026-06-30'
);

INSERT INTO dividends (
    company_id,
    dividend_per_share,
    declaration_date,
    payment_date
)
VALUES (
    1,
    3.00,
    DATE '2026-09-01',
    DATE '2026-09-30'
);

INSERT INTO dividends (
    company_id,
    dividend_per_share,
    declaration_date,
    payment_date
)
VALUES (
    2,
    1.75,
    DATE '2026-07-01',
    DATE '2026-07-31'
);

COMMIT;