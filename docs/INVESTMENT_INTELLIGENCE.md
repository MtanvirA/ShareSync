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
