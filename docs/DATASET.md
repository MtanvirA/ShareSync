# Market & Benchmark Historical Dataset

## 1. Source Details & Provenance
- **Dataset Name:** Dhaka Stock Exchange Historical Data (1999-2025)
- **Primary Source:** Harvard Dataverse
- **Persistent DOI:** `10.7910/DVN/XIFYT1`
- **Acquisition / Release Date:** December 2022 (Extended research release)
- **Data Frequency:** Daily End-of-Day (EOD) OHLCV series.

---

## 2. Benchmark Index Universe (LF-30)
The benchmark dataset incorporates three official Dhaka Stock Exchange indices stored in the dedicated table `benchmark_price_history`:

| Index Code | Index Name | Raw Records | Date Coverage | Source / Provenance |
|---|---|---|---|---|
| `DSEX` | DSE Broad Index | 1,969 | 2014-01-04 to 2022-12-07 + Recent Demo Series | Harvard Dataverse (`DSEX.csv`) + DSE official historical quotations |
| `DS30` | DSE 30 Blue Chip Index | 1,969 | 2014-01-04 to 2022-12-07 + Recent Demo Series | Harvard Dataverse (`DS30.csv`) + DSE official historical quotations |
| `DSES` / `DSEG` | DSE Shariah Index | 1,969 | 2014-01-04 to 2022-12-07 + Recent Demo Series | Harvard Dataverse (`DSES.csv`) + DSE official historical quotations |

*Note on Alias Resolution:* `DSEG` and `DSES` refer to the same DSE Shariah Index. The application layer seamlessly resolves queries for either symbol.

---

## 3. Expanded Company Universe (LF-31)
The clean canonical market universe covers 13 actively tracked DSE securities across key industrial sectors:

| Ticker | Company Name | Sector | Asset Type |
|---|---|---|---|
| `GP` | Grameenphone Ltd. | Telecommunication | EQUITY |
| `SQUAREPHARMA` | Square Pharmaceuticals PLC | Pharmaceuticals | EQUITY |
| `BATBC` | British American Tobacco Bangladesh | Food & Allied | EQUITY |
| `BRACBANK` | BRAC Bank PLC | Banking | EQUITY |
| `BXPHARMA` | Beximco Pharmaceuticals Ltd. | Pharmaceuticals | EQUITY |
| `RENATA` | Renata PLC | Pharmaceuticals | EQUITY |
| `LHBL` | LafargeHolcim Bangladesh Ltd. | Cement | EQUITY |
| `WALTONHIL` | Walton Hi-Tech Industries PLC | Engineering | EQUITY |
| `BEXIMCO` | Bangladesh Export Import Co. | Diversified | EQUITY |
| `EBL` | Eastern Bank PLC | Banking | EQUITY |
| `SUMITPOWER` | Summit Power Limited | Fuel & Power | EQUITY |
| `1JANATAMF` | First Janata Bank Mutual Fund | Financial | MUTUAL_FUND |

---

## 4. Data Cleaning & Validation Rules
The batch data import engine (`ShareSync.DataImporter`) strictly applies deterministic validation:

1. **Duplicate Detection:** Composite unique key `(entity, trading_date)`. Duplicate observations on identical calendar dates are discarded to preserve chronological monotonicity.
2. **OHLC Structural Validity:**
   - `High >= Low`
   - `High >= Open` and `High >= Close`
   - `Low <= Open` and `Low <= Close`
   - Violating records are recorded and rejected.
3. **Positive Price Requirement:** Price fields must be strictly positive (`Price > 0`). Records with zero or negative prices are excluded.
4. **Zero-Volume Preservation:** Legitimate trading sessions with zero reported volume (e.g., price-discovery freeze or trading suspension days) are preserved when price quotations are valid.
5. **Quality Audit Report:** Import runs output an audit log to `DataQualityReport.json`. Across recent runs:
   - 2,533 duplicate records purged.
   - 6 invalid OHLC benchmark rows filtered.
   - 65 invalid OHLC company rows filtered.

---

## 5. Limitations & Academic Notice
- **Not Real-Time:** The dataset represents historical end-of-day observations. It must not be presented or characterized as a real-time live market feed.
- **Price Return vs Total Return:** Historical series do not account for corporate-action adjustments (cash dividends, stock splits, bonus shares, or rights offerings). Comparisons reflect price return, not total shareholder return.
