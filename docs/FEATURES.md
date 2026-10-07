# Features

- **Dashboard:** Unified portfolio valuation.
- **Portfolio Management:** Track specific stock accumulations.
- **ACID Transactions:** BUY and SELL stocks with guaranteed consistency and fee tracking.
- **LF-26 Realized Profit/Loss:** Accurate calculation of realized profit/loss upon SELL execution using cumulative volume-weighted average cost: `Realized P/L = (Net Sale Proceeds) - (Cost Basis of Shares Sold)`. Clear distinction between Realized, Unrealized, and Total P/L across holdings, portfolios, and reports.
- **LF-27 Transaction Fees & Net Cash Flow:** Full support for non-negative transaction fees on both BUY (gross purchase + fees) and SELL (gross proceeds - fees), with portfolio-wide Net Cash Flow accounting.
- **LF-28 Inflation-Adjusted Return:** Transparent calculation showing the effect of purchasing-power changes on historical returns using the Fisher model: `Real Return = ((1 + Nominal Return / 100) / (1 + Cumulative Inflation / 100) - 1) * 100`. Missing inflation periods are never silently assumed zero or invented, but clearly indicated as unavailable.
- **LF-29 Multi-Asset and Multi-Currency Support:** Extends securities beyond equities to support ETF, Mutual Fund, and Cash holdings. Seamless currency support for BDT (default base) and USD with centralized exchange rate valuation: `Converted Base Value = Original Value × Exchange Rate`.
- **Investment Intelligence:** 5-year mathematical analysis of risk, return, and trends based on deterministic models.
- **Reports:** Download and view structured transaction histories with fee and realized P/L breakdowns across CSV, Excel, and PDF.
