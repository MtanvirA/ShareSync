# ShareSync — Financial Formula Viva & Mathematical Cheat Sheet
## Mathematical Definitions, Code Locations, and Reconciled Demo Values

**Project:** ShareSync — Share Market Portfolio Tracker  
**Branch:** `mysql-version`  
**Precision Standard:** Financial values computed using high-precision decimals (`decimal` in C# and `DECIMAL(20,4)` in MySQL) before presentation formatting.

---

## 1. Weighted Average Buy Price (VWAP)
- **Mathematical Formula:**
  $$\text{WAP} = \frac{\sum_{i=1}^{n} (Q_{\text{buy}, i} \times P_{\text{buy}, i})}{\sum_{i=1}^{n} Q_{\text{buy}, i}}$$
- **Variable Meaning:**
  - $Q_{\text{buy}, i}$: Share quantity of the $i$-th BUY transaction.
  - $P_{\text{buy}, i}$: Share price of the $i$-th BUY transaction.
  - Subsequent SELL transactions do **not** change the historical weighted average purchase price.
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/PortfolioService.cs` (`CalculateHoldingSummary`)
  - SQL: `database/mysql/04_functions.sql` (`fn_get_weighted_avg_price`)
  - SQL View: `database/mysql/01_schema.sql` (`vw_portfolio_holdings`)
- **Simple Explanation:** Average cost per share weighted by how many shares were purchased at each price level.
- **Demo Data Example (GP in Portfolio #1):**
  - Order 1: 500 shares at ৳320.00 = ৳160,000.00
  - Order 2: 500 shares at ৳340.00 = ৳170,000.00
  - Total: 1,000 shares for ৳330,000.00
  - $\text{Weighted Average Buy Price} = \frac{330,000}{1,000} = \mathbf{৳330.00}$

---

## 2. Cost Basis
- **Mathematical Formula:**
  $$\text{Cost Basis} = \text{Current Quantity} \times \text{Weighted Average Buy Price}$$
- **Variable Meaning:**
  - $\text{Current Quantity}$: Remaining net shares currently held ($\sum Q_{\text{buy}} - \sum Q_{\text{sell}}$).
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/PortfolioService.cs`
  - SQL: `database/mysql/01_schema.sql` (`vw_portfolio_holdings`)
- **Simple Explanation:** The total capital currently committed to open shares of a position.
- **Demo Data Example (SQURPHARMA in Portfolio #1):**
  - Holding: 1,200 shares at average cost ৳248.50.
  - $\text{Cost Basis} = 1,200 \times 248.50 = \mathbf{৳298,200.00}$.

---

## 3. Market Value (Mark-to-Market Valuation)
- **Mathematical Formula:**
  $$\text{Market Value} = \text{Current Quantity} \times \text{Current Market Price}$$
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/PortfolioService.cs`
  - SQL: `database/mysql/01_schema.sql` (`vw_portfolio_holdings`)
- **Simple Explanation:** What the currently held shares are worth right now at current exchange market prices.
- **Demo Data Example (GP in Portfolio #1):**
  - 1,000 shares $\times$ Current Price ৳355.00 = $\mathbf{৳355,000.00}$.

---

## 4. Unrealized Profit / Loss (Paper Gain/Loss)
- **Mathematical Formula:**
  $$\text{Unrealized P/L} = \text{Market Value} - \text{Cost Basis} = \text{Current Quantity} \times (\text{Current Price} - \text{Weighted Average Buy Price})$$
  $$\text{Unrealized P/L \%} = \frac{\text{Unrealized P/L}}{\text{Cost Basis}} \times 100$$
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/PortfolioService.cs`
  - SQL: `database/mysql/04_functions.sql` (`fn_calculate_unrealized_pl`)
- **Simple Explanation:** The gain or loss an investor would realize if they sold their entire open position at current market prices.
- **Demo Data Example (Portfolio #1 Aggregate):**
  - Total Current Value = ৳797,830.00
  - Total Cost Basis = ৳765,307.00
  - $\text{Unrealized P/L} = 797,830.00 - 765,307.00 = \mathbf{+৳32,523.00\ (+4.25\%)}$.

---

## 5. Realized Profit / Loss (Closed Position Gain/Loss)
- **Mathematical Formula:**
  $$\text{Realized P/L} = \sum_{j=1}^{m} \left[ (Q_{\text{sell}, j} \times P_{\text{sell}, j} - \text{Fee}_j) - (Q_{\text{sell}, j} \times \text{WAP}) \right]$$
- **Variable Meaning:**
  - $Q_{\text{sell}, j}$: Number of shares sold in transaction $j$.
  - $P_{\text{sell}, j}$: Sale price per share.
  - $\text{Fee}_j$: Commission fee charged on the sale.
  - $\text{WAP}$: Weighted average purchase price at the time of sale.
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/TransactionService.cs` (`CalculateRealizedProfitLossAsync`)
  - SQL: `database/mysql/04_functions.sql` (`fn_calculate_realized_pl`)
- **Simple Explanation:** Actual net cash profit or loss locked in from completed stock sales after subtracting purchase costs and brokerage fees.
- **Demo Data Example:**
  - Selling 200 shares of BATBC at ৳520.00 (avg buy cost ৳495.00, fee ৳50.00):
  - Gross Inflow: $200 \times 520 = ৳104,000 - ৳50 = ৳103,950.00$.
  - Cost Basis of Sold Shares: $200 \times 495 = ৳99,000.00$.
  - $\text{Realized P/L} = 103,950 - 99,000 = \mathbf{+৳4,950.00}$.

---

## 6. Transaction Fees & Net Cash Flow
- **Mathematical Formula:**
  $$\text{BUY Outflow} = (\text{Quantity} \times \text{Price}) + \text{Fee}$$
  $$\text{SELL Inflow} = (\text{Quantity} \times \text{Price}) - \text{Fee}$$
  $$\text{Net Cash Flow} = \sum \text{SELL Inflows} - \sum \text{BUY Outflows}$$
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/TransactionService.cs`
  - SQL: `database/mysql/04_functions.sql` (`fn_calculate_net_cash_flow`)
- **Simple Explanation:** Total external capital injected or withdrawn across all portfolio transactions, factoring in exchange commissions.

---

## 7. Compound Annual Growth Rate (CAGR)
- **Mathematical Formula:**
  $$\text{CAGR} = \left(\frac{V_{\text{final}}}{V_{\text{initial}}}\right)^{\frac{1}{Y}} - 1$$
- **Variable Meaning:**
  - $V_{\text{final}}$: Ending portfolio mark-to-market valuation.
  - $V_{\text{initial}}$: Beginning portfolio valuation.
  - $Y$: Number of elapsed years ($\frac{\text{Days}}{365.25}$).
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/AnalyticsService.cs` (`CalculateCagrAsync`)
- **Simple Explanation:** The steady annual rate at which an investment would have grown over multiple years.
- **Demo Data Example:**
  - Initial Portfolio: ৳500,000.00; Final: ৳640,680.00 over 1.8 years:
  - $\text{CAGR} = (640,680 / 500,000)^{1 / 1.8} - 1 = \mathbf{+14.8\%}$.

---

## 8. Daily Return & Annualized Volatility
- **Mathematical Formula:**
  $$R_t = \frac{P_t - P_{t-1}}{P_{t-1}}$$
  $$\sigma_{\text{daily}} = \sqrt{\frac{1}{N-1} \sum_{t=1}^{N} (R_t - \bar{R})^2}$$
  $$\sigma_{\text{annualized}} = \sigma_{\text{daily}} \times \sqrt{252}$$
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/AnalyticsService.cs` (`CalculateAnnualizedVolatility`)
- **Simple Explanation:** Standard deviation of daily percentage price changes scaled by institutional trading days ($\sqrt{252}$) to measure portfolio risk.
- **Demo Data Example:**
  - Daily standard deviation $\sigma_{\text{daily}} = 0.0105$ (1.05%).
  - $\sigma_{\text{annual}} = 0.0105 \times \sqrt{252} = \mathbf{16.67\%}$.

---

## 9. Maximum Drawdown (MDD)
- **Mathematical Formula:**
  $$\text{Drawdown}_t = \frac{V_t - \text{Peak}_t}{\text{Peak}_t}, \quad \text{where } \text{Peak}_t = \max_{\tau \le t} (V_\tau)$$
  $$\text{MDD} = \min_{t} (\text{Drawdown}_t)$$
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/AnalyticsService.cs` (`CalculateMaxDrawdown`)
- **Simple Explanation:** The largest peak-to-trough drop in portfolio value before reaching a new high.
- **Demo Data Example:**
  - Historical peak: ৳700,000.00; trough: ৳641,200.00.
  - $\text{MDD} = \frac{641,200 - 700,000}{700,000} = \mathbf{-8.40\%}$.

---

## 10. Simple Moving Averages (SMA50 & SMA200)
- **Mathematical Formula:**
  $$\text{SMA}_k(t) = \frac{1}{k} \sum_{i=0}^{k-1} P_{t-i}, \quad k \in \{50, 200\}$$
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/CompanyService.cs`, `AnalyticsService.cs`
- **Simple Explanation:** Trend-following indicator smoothing out daily noise over short (50-day) and long (200-day) horizons. A Golden Cross occurs when SMA50 crosses above SMA200.

---

## 11. Benchmark Outperformance (Alpha)
- **Mathematical Formula:**
  $$\text{Alpha} = R_{\text{portfolio}} - R_{\text{benchmark}}$$
- **Variable Meaning:**
  - $R_{\text{benchmark}}$: Percentage return of DSEX, DS30, or DSES over identical time periods.
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/BenchmarkService.cs`
- **Simple Explanation:** How much extra return the investor generated compared to simply holding the broad market index.
- **Demo Data Example:**
  - Portfolio Return: +18.40%; DSEX Return: +11.20%.
  - $\text{Alpha} = 18.40 - 11.20 = \mathbf{+7.20\%}$.

---

## 12. Inflation-Adjusted Return (Fisher Equation)
- **Mathematical Formula:**
  $$\text{Real Return} = \left[ \frac{1 + \frac{R_{\text{nominal}}}{100}}{1 + \frac{i}{100}} - 1 \right] \times 100$$
- **Variable Meaning:**
  - $R_{\text{nominal}}$: Nominal percentage return.
  - $i$: Annual inflation rate percentage (from `inflation_rates`).
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/InflationService.cs`
  - SQL: `database/mysql/04_functions.sql` (`fn_calculate_real_return`)
- **Simple Explanation:** Converts nominal Taka profits into true purchasing power growth after accounting for inflation.
- **Demo Data Example:**
  - Nominal Return: +18.40%; Bangladesh Inflation: 9.70%.
  - $\text{Real Return} = \left[ \frac{1.1840}{1.0970} - 1 \right] \times 100 = \mathbf{+7.9307\%}$.

---

## 13. Multi-Currency FX Conversion
- **Mathematical Formula:**
  $$\text{Converted Base Value (BDT)} = \text{Foreign Asset Value (USD)} \times \text{FX Rate (USD}\to\text{BDT)}$$
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/CurrencyService.cs`, `ReportService.cs`
  - Database: `currency_exchange_rates` (where 1 USD = 121.50 BDT).
- **Simple Explanation:** Translates foreign dollar-denominated stocks (like Apple or Microsoft) into base currency Taka for consistent aggregation.
- **Demo Data Example (Portfolio #4 AAPL Holding):**
  - 50 shares $\times$ $185.00 = $9,250.00 USD.
  - Converted Valuation = $\$9,250.00 \times 121.50 = \mathbf{৳1,123,875.00}$.

---

## 14. Investment Intelligence Composite Score (0–100)
- **Mathematical Formula:**
  $$\text{Score} = 0.25 \times S_{\text{Quality}} + 0.25 \times S_{\text{Momentum}} + 0.25 \times S_{\text{Risk}} + 0.25 \times S_{\text{Yield}}$$
- **Where Implemented:**
  - C#: `src/ShareSync.Application/Services/InvestmentIntelligenceService.cs`
- **Simple Explanation:** Multi-factor equity score combining fundamentals, price momentum, volatility risk, and dividend reliability into a balanced quantitative rating.
