/* ============================================================
   ShareSync - Portfolio Queries
   File: 02_portfolio_queries.sql

   PURPOSE:
   These queries turn raw transaction history into useful
   portfolio information.

   We will calculate:

   1. Current Holdings
   2. Total Buy Cost
   3. Total Sell Value
   4. Weighted Average Buy Price
   5. Current Market Value
   6. Unrealized Profit / Loss

   IMPORTANT:
   BUY  = money goes out, shares come in
   SELL = shares go out, money comes in

   The TRANSACTIONS table stores the history.
   These queries calculate the current portfolio situation
   from that history.
   ============================================================ */


/* ============================================================
   1. CURRENT HOLDINGS
   ============================================================

   QUESTION:
   "How many shares of each company does the user currently own?"

   Logic:

   BUY  -> ADD shares
   SELL -> REMOVE shares

   Example:

   BUY  100
   BUY   50
   SELL  30
   ------------
   Current = 120

   ============================================================ */

SELECT
    t.portfolio_id,
    p.portfolio_name,

    t.company_id,
    c.company_name,
    c.ticker_symbol,

    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity

            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity

            ELSE 0
        END
    ) AS current_quantity

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
    c.ticker_symbol

HAVING
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity

            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity

            ELSE 0
        END
    ) > 0

ORDER BY
    t.portfolio_id,
    t.company_id;


/* ============================================================
   2. TOTAL BUY COST
   ============================================================

   QUESTION:
   "How much money has the user spent buying each company?"

   Formula:

   Buy Cost = Quantity × Price Per Share

   Example:

   BUY 100 shares @ ৳120
   = 100 × 120
   = ৳12,000

   BUY 50 shares @ ৳130
   = 50 × 130
   = ৳6,500

   Total Buy Cost
   = ৳12,000 + ৳6,500
   = ৳18,500

   SELL transactions are ignored here because this query
   is specifically about money spent on BUY transactions.

   ============================================================ */

SELECT
    t.portfolio_id,
    p.portfolio_name,

    t.company_id,
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
    t.portfolio_id,
    p.portfolio_name,
    t.company_id,
    c.company_name,
    c.ticker_symbol

ORDER BY
    t.portfolio_id,
    t.company_id;


/* ============================================================
   3. TOTAL SELL VALUE
   ============================================================

   QUESTION:
   "How much money has the user received from selling shares?"

   Formula:

   Sell Value = Quantity × Price Per Share

   Example:

   SELL 30 shares @ ৳140

   = 30 × 140
   = ৳4,200

   BUY transactions are ignored because we only want the
   money received from selling.

   ============================================================ */

SELECT
    t.portfolio_id,
    p.portfolio_name,

    t.company_id,
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
    t.portfolio_id,
    p.portfolio_name,
    t.company_id,
    c.company_name,
    c.ticker_symbol

ORDER BY
    t.portfolio_id,
    t.company_id;


/* ============================================================
   4. WEIGHTED AVERAGE BUY PRICE
   ============================================================

   QUESTION:
   "On average, how much did the user pay for each share?"

   Formula:

                     Total Buy Cost
   Average Price = --------------------
                    Total Buy Quantity

   Example:

   BUY 100 shares @ ৳120
   BUY  50 shares @ ৳130

   Total Buy Quantity
   = 100 + 50
   = 150 shares

   Total Buy Cost
   = (100 × 120) + (50 × 130)
   = 12,000 + 6,500
   = ৳18,500

   Weighted Average Buy Price
   = 18,500 / 150
   = ৳123.33 approximately

   Why "weighted"?

   Because buying 100 shares at ৳120 should have more influence
   than buying only 50 shares at ৳130.

   ============================================================ */

SELECT
    t.portfolio_id,
    p.portfolio_name,

    t.company_id,
    c.company_name,
    c.ticker_symbol,

    /* Total number of shares bought */
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity
            ELSE 0
        END
    ) AS total_buy_quantity,

    /* Total money spent on buying */
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity * t.price_per_share
            ELSE 0
        END
    ) AS total_buy_cost,

    /*
       Weighted average:

       Total Buy Cost / Total Buy Quantity

       The CASE prevents division by zero if there are no
       BUY transactions.
    */
    CASE
        WHEN SUM(
            CASE
                WHEN t.transaction_type = 'BUY'
                    THEN t.quantity
                ELSE 0
            END
        ) > 0

        THEN
            SUM(
                CASE
                    WHEN t.transaction_type = 'BUY'
                        THEN t.quantity * t.price_per_share
                    ELSE 0
                END
            )
            /
            SUM(
                CASE
                    WHEN t.transaction_type = 'BUY'
                        THEN t.quantity
                    ELSE 0
                END
            )

        ELSE 0
    END AS weighted_average_buy_price

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
    c.ticker_symbol

ORDER BY
    t.portfolio_id,
    t.company_id;


/* ============================================================
   5. CURRENT MARKET VALUE
   ============================================================

   QUESTION:
   "What are the shares currently worth?"

   First calculate:

   Current Quantity =
   BUY quantity - SELL quantity

   Then:

   Current Market Value =
   Current Quantity × Current Company Price

   Example:

   Current Quantity = 120 shares

   Current Company Price = ৳150

   Market Value
   = 120 × 150
   = ৳18,000

   IMPORTANT:

   t.price_per_share
       = historical transaction price

   c.current_price
       = current price stored for the company

   These are NOT the same thing.

   ============================================================ */

SELECT
    t.portfolio_id,
    p.portfolio_name,

    t.company_id,
    c.company_name,
    c.ticker_symbol,

    /* Calculate shares currently held */
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity

            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity

            ELSE 0
        END
    ) AS current_quantity,

    /* Current company price */
    c.current_price,

    /* Current quantity × current price */
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity

            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity

            ELSE 0
        END
    ) * c.current_price AS current_market_value

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

HAVING
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity

            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity

            ELSE 0
        END
    ) > 0

ORDER BY
    t.portfolio_id,
    t.company_id;


/* ============================================================
   6. UNREALIZED PROFIT / LOSS
   ============================================================

   QUESTION:
   "If the user sold the shares at the current price,
    approximately how much profit or loss would they have?"

   This is called UNREALIZED profit/loss because the shares
   have NOT actually been sold yet.

   Formula:

   Current Market Value
   -
   Current Quantity × Weighted Average Buy Price

   Example:

   Current Quantity = 120

   Current Price = ৳150

   Current Market Value
   = 120 × 150
   = ৳18,000

   Weighted Average Buy Price
   ≈ ৳123.33

   Estimated Cost of Current Shares
   = 120 × 123.33
   ≈ ৳14,800

   Unrealized Profit
   ≈ 18,000 - 14,800
   ≈ ৳3,200

   IMPORTANT:

   This is a simplified weighted-average calculation.

   Exact realized profit/loss can become more complicated when
   there are many BUY and SELL transactions because we may need
   to track individual purchase lots.

   ============================================================ */

SELECT
    t.portfolio_id,
    p.portfolio_name,

    t.company_id,
    c.company_name,
    c.ticker_symbol,

    /* -----------------------------------------
       Current quantity
       ----------------------------------------- */
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity

            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity

            ELSE 0
        END
    ) AS current_quantity,

    /* -----------------------------------------
       Current company price
       ----------------------------------------- */
    c.current_price,

    /* -----------------------------------------
       Current market value
       ----------------------------------------- */
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity

            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity

            ELSE 0
        END
    ) * c.current_price AS current_market_value,

    /* -----------------------------------------
       Weighted average buy price
       ----------------------------------------- */
    CASE
        WHEN SUM(
            CASE
                WHEN t.transaction_type = 'BUY'
                    THEN t.quantity
                ELSE 0
            END
        ) > 0

        THEN
            SUM(
                CASE
                    WHEN t.transaction_type = 'BUY'
                        THEN t.quantity * t.price_per_share
                    ELSE 0
                END
            )
            /
            SUM(
                CASE
                    WHEN t.transaction_type = 'BUY'
                        THEN t.quantity
                    ELSE 0
                END
            )

        ELSE 0
    END AS weighted_average_buy_price,

    /* -----------------------------------------
       Unrealized Profit / Loss

       Current Market Value
       -
       Current Quantity × Average Buy Price
       ----------------------------------------- */
    (
        SUM(
            CASE
                WHEN t.transaction_type = 'BUY'
                    THEN t.quantity

                WHEN t.transaction_type = 'SELL'
                    THEN -t.quantity

                ELSE 0
            END
        ) * c.current_price
    )
    -
    (
        SUM(
            CASE
                WHEN t.transaction_type = 'BUY'
                    THEN t.quantity

                WHEN t.transaction_type = 'SELL'
                    THEN -t.quantity

                ELSE 0
            END
        )
        *
        CASE
            WHEN SUM(
                CASE
                    WHEN t.transaction_type = 'BUY'
                        THEN t.quantity
                    ELSE 0
                END
            ) > 0

            THEN
                SUM(
                    CASE
                        WHEN t.transaction_type = 'BUY'
                            THEN t.quantity * t.price_per_share
                        ELSE 0
                    END
                )
                /
                SUM(
                    CASE
                        WHEN t.transaction_type = 'BUY'
                            THEN t.quantity
                        ELSE 0
                    END
                )

            ELSE 0
        END
    ) AS unrealized_profit_loss

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

HAVING
    SUM(
        CASE
            WHEN t.transaction_type = 'BUY'
                THEN t.quantity

            WHEN t.transaction_type = 'SELL'
                THEN -t.quantity

            ELSE 0
        END
    ) > 0

ORDER BY
    t.portfolio_id,
    t.company_id;