# ShareSync MySQL — Financial & Analytical Formula Inventory

**Document ID:** FA-MYSQL-2026-10-08  
**Branch:** `mysql-version`  
**Phase:** Data Accuracy Phase 3 — Mathematical Validation & Equation Verification  
**Status:** Authoritative / Complete  

---

## 1. Overview & Mathematical Framework

This inventory documents every mathematical formula, accounting convention, and analytical algorithm currently implemented across the ShareSync MySQL application.

Each formula specifies:
1. Exact mathematical equation
2. Source data entities and columns
3. C# Application service and methods
4. Database view, procedure, or SQL query (where applicable)
5. Web API endpoint and DTO mapping
6. Frontend consumer and presentation rendering

All monetary calculations use `decimal` in C# and `DECIMAL(14,2)` or `DECIMAL(20,2)` in MySQL, preventing floating-point drift. Presentation rounding is performed using standard half-up or banking round (`Math.Round(val, 2)`).

---

## 2. Complete Metric Inventory

### 2.1 Holdings Quantity
- **Mathematical Formula:**
  $$\text{Current Quantity} = \sum_{t \in \text{BUY}} \text{Quantity}_t - \sum_{t \in \text{SELL}} \text{Quantity}_t$$
- **Source Data:** `transactions.quantity`, `transactions.transaction_type`
- **Application Service:** `ReportService.GetHoldingsReportAsync`, `TransactionService.CreateTransactionAsync`
- **Database View:** `vw_portfolio_holdings.current_quantity`
- **API Endpoint:** `GET /api/reports/holdings`, `GET /api/analytics`
- **Frontend Presentation:** `analytics.html`, `dashboard.html`

### 2.2 Weighted Average Cost Basis
- **Mathematical Formula:**
  $$\text{Weighted Average Buy Price} = \frac{\sum_{t \in \text{BUY}} (\text{Quantity}_t \times \text{PricePerShare}_t)}{\sum_{t \in \text{BUY}} \text{Quantity}_t}$$
  *(When no BUY transactions exist, cost basis is $0$)*
- **Source Data:** `transactions.price_per_share`, `transactions.quantity`
- **Application Service:** `TransactionService.GetUserTransactionsAsync`, `ReportService.GetHoldingsReportAsync`, `AnalyticsService.GetAnalyticsAsync`
- **Database View:** `vw_portfolio_holdings.weighted_average_buy_price`
- **API Endpoint:** `GET /api/reports/holdings`, `GET /api/transactions`
- **Frontend Presentation:** `analytics.html`, `transactions.html`

### 2.3 Current Market Value
- **Mathematical Formula:**
  $$\text{Market Value} = \text{Current Quantity} \times \text{Company Current Price} \times \text{FX Rate}$$
- **Source Data:** `companies.current_price`, `currency_exchange_rates.rate`, `transactions`
- **Application Service:** `ReportService.GetHoldingsReportAsync`, `AnalyticsService.GetAnalyticsAsync`
- **Database View:** `vw_portfolio_holdings.current_market_value`
- **API Endpoint:** `GET /api/reports/holdings`, `GET /api/analytics`
- **Frontend Presentation:** `dashboard.html`, `analytics.html`

### 2.4 Realized Profit / Loss
- **Mathematical Formula:**
  $$\text{Realized P/L} = \sum_{s \in \text{SELL}} [(\text{PricePerShare}_s \times \text{Quantity}_s - \text{Fee}_s) - (\text{Quantity}_s \times \text{Weighted Average Buy Price})]$$
  *(Net Sale Proceeds minus Cost Basis of Shares Sold)*
- **Source Data:** `transactions` (`price_per_share`, `quantity`, `fee`, `transaction_type = 'SELL'`), prior `BUY` transactions
- **Application Service:** `TransactionService.CreateTransactionAsync`, `TransactionService.GetUserTransactionsAsync`, `AnalyticsService.GetAnalyticsAsync`, `ReportService.GetTransactionHistoryReportAsync`
- **API Endpoint:** `GET /api/transactions`, `GET /api/analytics`
- **Frontend Presentation:** `transactions.html`, `analytics.html`

### 2.5 Unrealized Profit / Loss
- **Mathematical Formula:**
  $$\text{Unrealized P/L} = \text{Current Market Value} - (\text{Current Quantity} \times \text{Weighted Average Buy Price} \times \text{FX Rate})$$
  $$\text{Unrealized P/L \%} = \frac{\text{Unrealized P/L}}{\text{Invested Value}} \times 100$$
- **Source Data:** `companies.current_price`, `transactions`, `currency_exchange_rates`
- **Application Service:** `ReportService.GetHoldingsReportAsync`, `AnalyticsService.GetAnalyticsAsync`
- **Database View:** `vw_portfolio_holdings.unrealized_profit_loss`
- **API Endpoint:** `GET /api/reports/holdings`, `GET /api/analytics`
- **Frontend Presentation:** `dashboard.html`, `analytics.html`

### 2.6 Total Portfolio Return
- **Mathematical Formula:**
  $$\text{Total Return (Absolute)} = \text{Total Unrealized P/L} + \text{Total Realized P/L} + \text{Total Dividend Income}$$
  $$\text{Total Return \%} = \frac{\text{Total Return (Absolute)}}{\text{Total Invested Value}} \times 100$$
- **Source Data:** `vw_portfolio_holdings`, `transactions`, `dividends`
- **Application Service:** `AnalyticsService.GetAnalyticsAsync`
- **API Endpoint:** `GET /api/analytics`
- **Frontend Presentation:** `analytics.html` (`analyticsTotalReturn`)

### 2.7 Fees & Net Cash Flow
- **Mathematical Formula:**
  - For BUY: $\text{Net Cash Outflow} = (\text{Quantity} \times \text{PricePerShare}) + \text{Fee}$
  - For SELL: $\text{Net Cash Inflow} = (\text{Quantity} \times \text{PricePerShare}) - \text{Fee}$
  - Cumulative Net Cash Flow:
    $$\text{Net Cash Flow} = (\sum \text{Gross}_{\text{SELL}} - \sum \text{Fee}_{\text{SELL}}) - (\sum \text{Gross}_{\text{BUY}} + \sum \text{Fee}_{\text{BUY}})$$
- **Source Data:** `transactions.fee`, `transactions.price_per_share`, `transactions.quantity`
- **Application Service:** `TransactionService.MapToDto`, `AnalyticsService.GetAnalyticsAsync`
- **API Endpoint:** `GET /api/transactions`, `GET /api/analytics`
- **Frontend Presentation:** `transactions.html`, `analytics.html`

### 2.8 Time-Weighted Snapshot Portfolio Return
- **Mathematical Formula:**
  $$\text{Return \%} = \frac{\text{TotalValue}_{\text{End}} - \text{TotalValue}_{\text{Start}}}{\text{TotalValue}_{\text{Start}}} \times 100$$
- **Source Data:** `portfolio_snapshots.total_value`, `snapshot_date`
- **Application Service:** `PortfolioSnapshotService.GetPerformanceDataAsync`, `BenchmarkService.ComparePortfolioAsync`
- **API Endpoint:** `GET /api/portfolios/{id}/performance`, `POST /api/benchmarks/compare`
- **Frontend Presentation:** `dashboard.html`, `analytics.html`

### 2.9 Compound Annual Growth Rate (CAGR)
- **Mathematical Formula:**
  $$\text{CAGR} = \left[\left(\frac{\text{Ending Price}}{\text{Starting Price}}\right)^{\frac{1}{\text{Years}}} - 1\right] \times 100$$
  $$\text{Years} = \frac{\text{Total Calendar Days between Start and End Date}}{365.25}$$
  *(When $\text{Years} \ge 1.0$, CAGR is computed with the annualization exponent. When $\text{Years} < 1.0$, metric falls back to simple period return labeled `Period Return` (`AnnualizedMetricLabel = "Period Return"`) to prevent exponential distortion)*
- **Source Data:** `company_price_history.price`, `trading_date`
- **Application Service:** `InvestmentIntelligenceService.GetHistoricalInvestmentProfileAsync`
- **API Endpoint:** `GET /api/intelligence/{companyId}`
- **Frontend Presentation:** `intelligence.html`

### 2.10 Daily Return
- **Mathematical Formula:**
  $$\text{Daily Return}_t = \left(\frac{\text{Price}_t}{\text{Price}_{t-1}} - 1\right) \times 100$$
  *(Observation at index $0$ produces no daily return; only days with a valid prior observation produce returns)*
- **Source Data:** `company_price_history.price` (chronologically sorted)
- **Application Service:** `InvestmentIntelligenceService.GetHistoricalInvestmentProfileAsync`
- **API Endpoint:** `GET /api/intelligence/{companyId}`
- **Frontend Presentation:** `intelligence.html`

### 2.11 Annualized Volatility
- **Mathematical Formula:**
  $$\text{Daily Volatility} = \sqrt{\frac{1}{N - 1} \sum_{i=1}^N (R_i - \bar{R})^2}$$
  $$\text{Annualized Volatility} = \text{Daily Volatility} \times \sqrt{252}$$
  *(Sample standard deviation using $N-1$ degrees of freedom and standard trading-day convention of 252 days)*
- **Source Data:** Daily returns sequence from `company_price_history`
- **Application Service:** `InvestmentIntelligenceService.GetHistoricalInvestmentProfileAsync`
- **API Endpoint:** `GET /api/intelligence/{companyId}`
- **Frontend Presentation:** `intelligence.html`

### 2.12 Maximum Peak-to-Trough Drawdown
- **Mathematical Formula:**
  $$\text{Running Peak}_t = \max_{\tau \le t} \text{Price}_\tau$$
  $$\text{Drawdown}_t = \left(\frac{\text{Price}_t - \text{Running Peak}_t}{\text{Running Peak}_t}\right) \times 100$$
  $$\text{Maximum Drawdown} = \min_{t} \text{Drawdown}_t$$
  *(Calculated sequentially without future-data look-ahead)*
- **Source Data:** `company_price_history.price` (chronologically sorted)
- **Application Service:** `InvestmentIntelligenceService.GetHistoricalInvestmentProfileAsync`
- **API Endpoint:** `GET /api/intelligence/{companyId}`
- **Frontend Presentation:** `intelligence.html`

### 2.13 Simple Moving Averages (SMA 50 & SMA 200)
- **Mathematical Formula:**
  $$\text{SMA}_{50}(t) = \frac{1}{50} \sum_{k=0}^{49} \text{Price}_{t-k}$$
  $$\text{SMA}_{200}(t) = \frac{1}{200} \sum_{k=0}^{199} \text{Price}_{t-k}$$
  *(If fewer than $N$ prior observations exist up to date $t$, the SMA returns `null`. Values are never fabricated)*
- **Source Data:** Full ascending history from `company_price_history`
- **Application Service:** `InvestmentIntelligenceService.GetHistoricalInvestmentProfileAsync`
- **API Endpoint:** `GET /api/intelligence/{companyId}`
- **Frontend Presentation:** `intelligence.html`

### 2.14 Benchmark Return & Outperformance
- **Mathematical Formula:**
  $$\text{Benchmark Return \%} = \frac{\text{CloseValue}_{\text{End}} - \text{CloseValue}_{\text{Start}}}{\text{CloseValue}_{\text{Start}}} \times 100$$
  $$\text{Outperformance \%} = \text{Portfolio Return \%} - \text{Benchmark Return \%}$$
  *(Presented as `Benchmark Outperformance` and `Active Return` (`ActiveReturnPercentage`). Evaluated strictly over overlapping, date-aligned observations)*
- **Source Data:** `benchmark_price_history.close_value`, `trading_date`, `portfolio_snapshots`
- **Application Service:** `BenchmarkService.ComparePortfolioAsync`
- **API Endpoint:** `POST /api/benchmarks/compare`
- **Frontend Presentation:** `analytics.html`

### 2.15 Inflation-Adjusted (Real) Return
- **Mathematical Formula:**
  $$\text{Real Return \%} = \left[\frac{1 + \text{Nominal Return} / 100}{1 + \text{Cumulative Inflation} / 100} - 1\right] \times 100$$
  $$\text{Cumulative Inflation} = \left[\prod_{y=\text{StartYear}}^{\text{EndYear}} \left(1 + \frac{\text{Rate}_y}{100}\right) - 1\right] \times 100$$
  *(If inflation rates are missing for any year in the period, returns `null` rather than assuming zero)*
- **Source Data:** `inflation_rates.rate`, `country_code`, `year`
- **Application Service:** `InflationService.CalculateRealReturn`, `InflationService.GetCumulativeInflationRateAsync`
- **API Endpoint:** `GET /api/inflation/real-return`, `GET /api/analytics`
- **Frontend Presentation:** `analytics.html`

### 2.16 Dividend Income & Yield
- **Mathematical Formula:**
  $$\text{Dividend Income} = \sum (\text{User Shares Held} \times \text{DividendPerShare})$$
  $$\text{Dividend Yield \%} = \frac{\text{Total Annual Dividend Income}}{\text{Total Portfolio Market Value}} \times 100$$
- **Source Data:** `dividends.dividend_per_share`, `transactions`, `companies.current_price`
- **Application Service:** `DividendService.GetDividendSummaryAsync`, `AnalyticsService.GetAnalyticsAsync`
- **API Endpoint:** `GET /api/dividends/summary`, `GET /api/analytics`
- **Frontend Presentation:** `dividends.html`, `analytics.html`

### 2.17 Portfolio Goal Progress
- **Mathematical Formula:**
  $$\text{Progress \%} = \frac{\text{Current Valuation Metric}}{\text{Target Value}} \times 100$$
  $$\text{Remaining Value} = \max(0, \text{Target Value} - \text{Current Value})$$
- **Source Data:** `portfolio_goals`, `transactions`, `companies.current_price`, `dividends`
- **Application Service:** `PortfolioGoalService.CalculateGoalProgress`
- **API Endpoint:** `GET /api/goals`
- **Frontend Presentation:** `goals.html`

### 2.18 Investment Intelligence Composite Score
- **Mathematical Formula:**
  $$\text{Composite Score} = (0.30 \times S_{\text{Return}}) + (0.20 \times S_{\text{Risk}}) + (0.20 \times S_{\text{Drawdown}}) + (0.15 \times S_{\text{Consistency}}) + (0.15 \times S_{\text{Trend}})$$
  Where normalized components are clamped to $[0, 100]$:
  - $S_{\text{Return}} = \text{Clamp}\left(\frac{\text{CAGR} - (-10)}{20 - (-10)} \times 100, 0, 100\right)$
  - $S_{\text{Risk}} = \text{Clamp}\left(\frac{\text{Annualized Volatility} - 75}{15 - 75} \times 100, 0, 100\right)$ *(Inverted: lower volatility yields higher score)*
  - $S_{\text{Drawdown}} = \text{Clamp}\left(\frac{\text{Max Drawdown} - (-65)}{-10 - (-65)} \times 100, 0, 100\right)$ *(Shallower drawdown yields higher score)*
  - $S_{\text{Consistency}} = \text{Clamp}\left(\frac{\text{Positive Day \%} - 20}{50 - 20} \times 100, 0, 100\right)$
  - $S_{\text{Trend}} = 100$ (STRONGER_UPTREND), $75$ (UPTREND), $50$ (WEAKENING_OR_MIXED), $25$ (DOWNTREND), $0$ (INSUFFICIENT)
- **Source Data:** `company_price_history.price`, `trading_date`
- **Application Service:** `InvestmentIntelligenceService.GetHistoricalInvestmentProfileAsync`
- **API Endpoint:** `GET /api/intelligence/{companyId}`
- **Frontend Presentation:** `intelligence.html`

### 2.19 Trend Classification
- **Classification Rules:**
  - `STRONGER_UPTREND`: $\text{Price} > \text{SMA}_{50} \land \text{SMA}_{50} > \text{SMA}_{200}$
  - `UPTREND`: $\text{Price} > \text{SMA}_{50} \land \text{SMA}_{50} \le \text{SMA}_{200}$
  - `DOWNTREND`: $\text{Price} < \text{SMA}_{50} \land \text{SMA}_{50} < \text{SMA}_{200}$
  - `WEAKENING_OR_MIXED`: all other price/MA relationships
  - `TREND_INSUFFICIENT_DATA`: when either $\text{SMA}_{50}$ or $\text{SMA}_{200}$ is unavailable (`null`)
- **Source Data:** `company_price_history.price`, `trading_date`
- **Application Service:** `InvestmentIntelligenceService.GetHistoricalInvestmentProfileAsync`
- **API Endpoint:** `GET /api/intelligence/{companyId}`
- **Frontend Presentation:** `intelligence.html`
