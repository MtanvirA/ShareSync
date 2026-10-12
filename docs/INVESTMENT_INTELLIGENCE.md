# Investment Intelligence

## 1. Purpose
The Investment Intelligence module assesses a company's historical performance over a strict **5-year analysis window** using deterministic financial mathematics. It outputs an explainable Historical Investment Profile Score (0-100).

**Academic Disclaimer:** This is a retrospective statistical assessment. It is **NOT** a prediction system, **NOT** financial advice, and does **NOT** guarantee future profitability.

## 2. Mathematical Formulations
- **Historical Return:** `((CurrentPrice - StartPrice) / StartPrice) * 100`
- **CAGR:** `(Ending Price / Starting Price)^(1 / Years) - 1`
- **Annualized Volatility:** `Daily Standard Deviation * √252`
- **Maximum Drawdown:** `Minimum( (Current Close - Running Peak) / Running Peak )`
- **Moving Averages (MA50/MA200):** 50-day and 200-day unweighted rolling averages.

## 3. Score Weights (100 Total)
- **Historical Return (30%)**
- **Risk & Volatility (20%)**
- **Consistency & Drawdown (20%)**
- **Positive Days (15%)**
- **Trend / MAs (15%)**

---

## 4. Benchmark Performance Comparison (LF-30)
In addition to company-level risk profiles, ShareSync supports macro comparative analytics:
- **Available Benchmarks:** DSE Broad Index (`DSEX`), DSE 30 Blue Chip (`DS30`), and DSE Shariah Index (`DSES` / `DSEG`).
- **Date Alignment Formulation:**
  - `Portfolio Return = ((V_end - V_start) / V_start) * 100`
  - `Benchmark Return = ((B_end - B_start) / B_start) * 100`
  - `Outperformance = Portfolio Return - Benchmark Return`
- **Integrity Rule:** Only strictly overlapping observation dates between portfolio snapshots and database benchmark records are used. Where non-trading dates occur (e.g. weekends/holidays), the closest prior available trading day observation is resolved. Mismatched or non-overlapping periods are never compared.

---

## 5. Expanded Company Universe (LF-31)
- Investment Intelligence queries are bounded to a maximum of 5 years back from the latest observation date.
- Indexed lookups via `idx_price_hist_comp_date` on `(company_id, recorded_at)` ensure sub-millisecond query execution across the expanded 13-security universe without loading excessive records into client memory.
