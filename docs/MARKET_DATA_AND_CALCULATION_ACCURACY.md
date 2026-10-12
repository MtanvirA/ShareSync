# ShareSync MySQL — Market Data & Calculation Accuracy Specification

**Document ID:** MDCA-MYSQL-2026-10-08  
**Branch:** `mysql-version`  
**Phase:** Data Accuracy Phase 3 — Mathematical Validation & Equation Verification  
**Status:** Authoritative / Verified  

---

## 1. Executive Summary

This specification defines the canonical rules, precision standards, date alignment conventions, missing data policies, and mathematical algorithms implemented across the ShareSync MySQL financial engine.

Every algorithm and financial equation defined herein has been independently validated using external reference arithmetic (`IndependentReferenceCalculator`) without reliance on internal production code paths.

---

## 2. Numerical Precision and Types

### 2.1 Storage & Runtime Types
- **Monetary Quantities & Values:** `decimal` in C# (.NET 8.0), `DECIMAL(14,2)` or `DECIMAL(20,2)` in MySQL 8.0.
- **Stock Quantities:** `decimal` in C#, `DECIMAL(12,2)` in MySQL.
- **Exchange Rates & Inflation Rates:** `decimal` in C#, `DECIMAL(10,4)` or `DECIMAL(6,2)` in MySQL.
- **Price Series:** `decimal` in C#, `DECIMAL(12,2)` in MySQL.

### 2.2 Rounding & Floating-Point Drift Mitigation
- **Strict Prohibition of `double` / `float` for Monetary Balances:** Floating-point representations (`double`, `float`) are prohibited for financial balances, cash flows, and cost bases to eliminate IEEE 754 precision drift.
- **Intermediate Calculations:** All cumulative sums, products, and quotients maintain full 28-29 significant digit `decimal` precision throughout the calculation pipeline.
- **Presentation Rounding:** Rounding occurs exclusively at presentation boundaries (DTO mapping and database view projections) using standard half-up / banking rounding (`Math.Round(val, 2)`).
- **Zero Values:** Negative zero (`-0.00`) is sanitized to `0.00` upon formatting.

---

## 3. Date & Chronological Ordering Policies

### 3.1 Strict Chronological Sequencing
- All time-series data (historical prices, snapshots, benchmark values, dividends) are queried with explicit `ORDER BY TradingDate ASC` (or `SnapshotDate ASC`).
- Time-series calculations (CAGR, daily returns, volatility, moving averages, running peak drawdowns) mandate ascending chronological order:
  $$t_0 < t_1 < t_2 < \dots < t_N$$

### 3.2 Look-Ahead Bias Prevention
- In any analysis evaluated "as of" date $T$, observations with timestamp $> T$ are strictly filtered out before analytics pipelines execute (`TradingDate <= toDate`).
- Addition of observation $T+1$ produces zero variance in metrics evaluated as of $T$. This invariant is enforced by automated regression testing (`LookAheadBiasTest_FutureObservationAtTPlusOne_DoesNotAffectAsOfT`).

---

## 4. Missing Data & Edge Case Policies

| Scenario | Application Policy | Mathematical Output | User Feedback |
| :--- | :--- | :--- | :--- |
| **Zero observations** | Graceful degradation | Profile with empty metrics | Warning: `"Company has no valid historical data."` |
| **Single observation** | Cannot calculate return/volatility | Return: `0.00%`, Volatility: `0.00%`, CAGR: `0.00%` | Warning: `"Less than five years of valid history."` |
| **Insufficient SMA 50 (< 50 points)** | Window not satisfied | `null` | Warning emitted; no synthetic SMA created. |
| **Insufficient SMA 200 (< 200 points)** | Window not satisfied | `null` | Warning: `"Moving averages could not be calculated because insufficient observations were available."` |
| **Insufficient 5-Year History (< 1825 days)** | Analytics run on available subset | Metrics computed on valid window | `HasFullFiveYearHistory = false`, Warning: `"Less than five years of valid history."` |
| **Missing Inflation Data** | No inflation assumption | `null` real return | System does NOT assume 0% inflation; returns `null`. |
| **Missing Benchmark History** | Fallback provider engaged | `IsFallback = true` | Fallback model calibrated; clearly labeled in DTO. |
| **Zero/Negative Cost Basis** | Div-by-zero protection | Return: `0.00%` | Prevents arithmetic overflow or `NaN`. |

---

## 5. Mathematical Formulas & Methodologies

### 5.1 Holdings & Weighted Average Cost
- **Holding Quantity:**
  $$Q = \sum Q_{\text{BUY}} - \sum Q_{\text{SELL}}$$
- **Weighted Average Cost Basis:**
  $$\bar{P}_{\text{cost}} = \frac{\sum (Q_{\text{BUY}} \times P_{\text{BUY}})}{\sum Q_{\text{BUY}}}$$
  *Note:* Sales reduce the remaining quantity but do not alter the historical weighted average purchase price.

### 5.2 Realized & Unrealized Profit / Loss
- **Realized P/L:**
  $$\text{Realized P/L} = \sum_{s \in \text{SELL}} [(P_s \times Q_s - \text{Fee}_s) - (Q_s \times \bar{P}_{\text{cost}})]$$
  *Rule:* Transaction fees on sale reduce net proceeds, directly reducing realized gain or increasing realized loss.
- **Unrealized P/L:**
  $$\text{Unrealized P/L} = (Q_{\text{current}} \times P_{\text{market}}) - (Q_{\text{current}} \times \bar{P}_{\text{cost}})$$
  *Rule:* Unrealized P/L is evaluated strictly on unsold shares against prevailing market quotes. It is never commingled with realized cash gains.

### 5.3 Cash Flow & Transaction Fee Conventions
- **BUY Order:** $\text{Net Outflow} = (Q \times P) + \text{Fee}$
- **SELL Order:** $\text{Net Inflow} = (Q \times P) - \text{Fee}$

### 5.4 Historical Return & Compound Annual Growth Rate (CAGR)
- **Simple Period Return:**
  $$R = \frac{P_{\text{end}} - P_{\text{start}}}{P_{\text{start}}} \times 100$$
- **Compound Annual Growth Rate (CAGR):**
  $$\text{Years} = \frac{\text{EndDate} - \text{StartDate}}{365.25}$$
  $$\text{CAGR} = \left[\left(\frac{P_{\text{end}}}{P_{\text{start}}}\right)^{\frac{1}{\text{Years}}} - 1\right] \times 100$$
  *Guard:* If $\text{Years} < 1$, CAGR is reported as simple return to prevent exponential distortion over micro-periods.

### 5.5 Daily Returns & Annualized Volatility
- **Daily Return Series:**
  $$r_t = \frac{P_t - P_{t-1}}{P_{t-1}} \quad \text{for } t = 1, \dots, N-1$$
  *(Observation $t=0$ has no return; returns series contains $N-1$ entries).*
- **Sample Standard Deviation:**
  $$s = \sqrt{\frac{1}{(N-1) - 1} \sum_{t=1}^{N-1} (r_t - \bar{r})^2}$$
- **Annualized Volatility (DSE Trading Days):**
  $$\sigma_{\text{ann}} = s \times \sqrt{252} \times 100$$
  *Convention:* Uses unbiased sample standard deviation ($N-1$ divisor) scaled by the international standard 252 annual trading days.

### 5.6 Maximum Drawdown (Sequential Peak-to-Trough)
- **Running Peak:**
  $$\text{Peak}_t = \max_{0 \le i \le t} (P_i)$$
- **Instantaneous Drawdown:**
  $$\text{DD}_t = \frac{P_t - \text{Peak}_t}{\text{Peak}_t} \times 100$$
- **Maximum Drawdown:**
  $$\text{MDD} = \min_{0 \le t \le N-1} (\text{DD}_t)$$
  *Rule:* MDD reflects the worst peak-to-trough decline over the period. It does not measure drawdown from the final peak or an arbitrary global maximum after the observation point.

### 5.7 Simple Moving Averages (SMA 50 / SMA 200)
- **SMA Calculation:**
  $$\text{SMA}_k(t) = \frac{1}{k} \sum_{i=0}^{k-1} P_{t-i} \quad \text{for } t \ge k-1$$
  *Rule:* If total chronological observations $N < k$, SMA is strictly `null`. The system never synthesizes moving averages from insufficient sample windows.

### 5.8 Benchmark Return & Comparison Methodology
- **Benchmark Selection:** Canonical MySQL table `benchmark_histories` (containing DSEX historical points) is queried by default.
- **Period Alignment:** Benchmark return is measured between the exact start date and end date matching the portfolio or asset analysis window:
  $$R_{\text{bench}} = \frac{B_{\text{end}} - B_{\text{start}}}{B_{\text{start}}} \times 100$$
- **Outperformance Metric:**
  $$\alpha = R_{\text{portfolio}} - R_{\text{bench}}$$
- **Fallback Distinction:** When benchmark history is missing, `BenchmarkDataProvider` returns a calibrated fallback with explicit provenance flag `IsFallback = true` and `BenchmarkSource = "Static Calibrated Model (Fallback)"`.

### 5.9 Inflation-Adjusted Return (Fisher Equation)
- **Real Rate Formula:**
  $$1 + r_{\text{real}} = \frac{1 + r_{\text{nominal}}}{1 + i}$$
  $$r_{\text{real}} = \left(\frac{1 + \frac{R_{\text{nom}}}{100}}{1 + \frac{I}{100}} - 1\right) \times 100$$
- **Multi-Year Cumulative Compounding:**
  $$1 + I_{\text{cumulative}} = \prod_{y=1}^{Y} \left(1 + \frac{I_y}{100}\right)$$

### 5.10 Price-Return vs. Total-Return Distinction
- **Capital Price Return:** Harvard Dataverse DSE daily records contain unadjusted trade prices (no dividend/split adjustments). All stock price analytics (CAGR, historical return, chart series) represent capital price appreciation.
- **Dividend Income Accounting:** Dividends are tracked as separate cash-flow items in `dividends` and aggregated in `DividendService`.
- **Integrity Rule:** Price appreciation is never mislabeled as "Total Shareholder Return" (TSR), preventing dividend double-counting.

---

## 6. Investment Intelligence Scoring Methodology

### 6.1 Multi-Factor Composite Architecture
The Investment Intelligence model aggregates five financial dimensions into a deterministic 0–100 score:

| Factor | Metric | Calibration Range [Min, Max] | Weight | Direction |
| :--- | :--- | :--- | :--- | :--- |
| **Return** | 5-Yr CAGR | $[-10.0\%, +20.0\%]$ | 30% | Higher is better |
| **Risk** | Annualized Volatility | $[75.0\%, 15.0\%]$ | 20% | Lower is better |
| **Drawdown** | Max Drawdown | $[-65.0\%, -10.0\%]$ | 20% | Shallower is better |
| **Consistency** | Positive Day % | $[20.0\%, 50.0\%]$ | 15% | Higher is better |
| **Trend** | Moving Average Stack | Discrete $\{0, 25, 50, 75, 100\}$ | 15% | Stronger uptrend is better |

### 6.2 Normalization Function
Each continuous factor is bounded and mapped to $[0, 100]$:
$$S(x, \min, \max) = \text{clamp}\left(\frac{x - \min}{\max - \min} \times 100, 0, 100\right)$$

### 6.3 Trend Classification Map
- `STRONGER_UPTREND` ($P > \text{SMA}_{50} > \text{SMA}_{200}$): Score = 100
- `UPTREND` ($P > \text{SMA}_{50}$ and $\text{SMA}_{50} \le \text{SMA}_{200}$): Score = 75
- `WEAKENING_OR_MIXED`: Score = 50
- `DOWNTREND` ($P < \text{SMA}_{50} < \text{SMA}_{200}$): Score = 25
- `TREND_INSUFFICIENT_DATA`: Score = 0

### 6.4 Composite Formula & Classification Bands
$$\text{OverallScore} = 0.30 \cdot S_{\text{return}} + 0.20 \cdot S_{\text{risk}} + 0.20 \cdot S_{\text{drawdown}} + 0.15 \cdot S_{\text{consistency}} + 0.15 \cdot S_{\text{trend}}$$

| Overall Score Range | Classification Profile |
| :--- | :--- |
| $\ge 75.00$ | Strong Historical Profile |
| $[60.00, 74.99]$ | Favorable Historical Profile |
| $[45.00, 59.99]$ | Moderate Historical Profile |
| $[30.00, 44.99]$ | Higher Historical Risk |
| $< 30.00$ | High Historical Risk |

- **Determinism:** The score is 100% deterministic with zero stochastic elements. Given identical price histories, the score will always evaluate to the exact same value.

---

## 7. Company Isolation Invariant

- Company historical data is partitioned by `CompanyId`.
- Query filters mandate `Where(h => h.CompanyId == targetCompanyId)`.
- Cross-contamination between companies is strictly prohibited and validated by automated isolation tests (`CompanyIsolation_IndependentEvaluations_NoCrossContamination`).

---

## 8. Live Market Data Pipeline & Cross-Source Reconciliation

### 8.1 Primary Live Source & Provenance
- **Primary Live Feed:** StockChartBD Public REST API (`https://stockchartbd.com/api/v1.php`).
- **Data Attribution:** Underlying public exchange quotations sourced from Dhaka Stock Exchange (`dsebd.org`) via DSE Intelligence / StockChartBD.
- **Network Routing & Fault-Tolerance:** DNS A-record failover is active. Connections to `stockchartbd.com` are explicitly routed to the live server IP (`147.79.69.124`), bypassing the non-responsive secondary record (`88.222.243.235`) with graceful DNS fallback.
- **Rate-Limiting Architecture:** Enforces a conservative rolling 1-hour window budget of $\le 100$ requests/hour (below the provider's documented 120 req/hour/IP quota). Respects HTTP 429 `Retry-After` headers with backoff and avoids request storms via multi-tier caching (quotes: 1 min, market summary: 5 mins, catalog: 60 mins).
- **Supported Endpoints:**
  - `?endpoint=market`: Market summary, benchmark indices (DSEX, DS30, DSES, CDSET), and trading breadth.
  - `?endpoint=companies`: Complete 400+ DSE listed company directory with market caps and sectors.
  - `?endpoint=quote&symbol={symbol}`: Real-time stock quote with session OHLCV and trading stats.
  - `?endpoint=history&symbol={symbol}&limit={N}`: Historical daily OHLCV series.
  - `?endpoint=financials&symbol={symbol}`: Yearly and interim financial statement metrics.
  - `?endpoint=health&symbol={symbol}`: Exchange operational solvency and category health indicators.
  - `?endpoint=sectors`: DSE sector breakdown with constituent counts and capitalization.
- **Fallback Feed:** Automatic fallback to official Dhaka Stock Exchange portal (`dsebd.org`) for illiquid or unlisted symbols (e.g. `AMCLPRAN`).
- **Provenance States:**
  - `LIVE`: Valid real-time quote received with recent timestamp from StockChartBD.
  - `STALE`: Quote timestamp older than 7 calendar days (e.g. trading holiday or suspended instrument).
  - `PRESERVED_FALLBACK`: External API offline, timed out, or returned 4xx/5xx; database preserves last validated current price.
  - `REJECTED_ANOMALY`: Extreme price jump (> 1000%) or flash collapse (< 0.05x) rejected to protect portfolio valuation.

### 8.2 Timestamp & Sanity Policies
- **Future Timestamp Protection:** Timestamps $> 15$ minutes in the future relative to server time are rejected as clock anomalies.
- **Price Range Validation:** Prices must satisfy $0 < P \le 500,000$ BDT.
- **OHLC Harmonization:** Automatically ensures $\text{Low} \le \text{Open} \le \text{High}$ and $\text{Low} \le \text{Close} \le \text{High}$.
- **Idempotent Storage:** Daily background sync upserts intraday rows for `(CompanyId, TradingDate)` in-place, preventing duplicate records.
- **Historical Dataset Coexistence:** Recent API history (2024-Present) complements rather than replaces the validated 1999-2025 Harvard Dataverse research archive (`10.7910/DVN/XIFYT1`).

### 8.3 Terminology Alignments
- **Benchmark Outperformance / Active Return:** The difference between portfolio return and benchmark return is explicitly presented as `Benchmark Outperformance` and `Active Return` (`ActiveReturnPercentage`).
- **Sub-One-Year Returns:** When evaluation spans under 1 calendar year, annualization distortion is avoided by reporting simple return labeled `Period Return` (`AnnualizedMetricLabel = "Period Return"`).
- **Calendar-Based 5-Year Window:** Analysis period boundary is determined via exact calendar arithmetic (`endDate.AddYears(-5)`) rather than assuming fixed 1,825 days, precisely handling leap years.

---

## 9. Verification & Test Evidence

All mathematical, portfolio calculation, and live market data pipeline properties are covered by regression test suites:
- **Total Test Suite:** 514 tests
- **Passing:** 514 (100%)
- **Failing:** 0
- **Skipped:** 0
- **Reference Calculator:** `IndependentReferenceCalculator.cs`
- **Audit Suite:** `MathematicalAccuracyAuditTests.cs`
- **Live Reconciliation Suite:** `LiveMarketDataReconciliationTests.cs`
- **StockChartBD Integration & Rate Limiter Suite:** `StockChartBdClientAndRateLimiterTests.cs`
