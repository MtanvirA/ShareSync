# ShareSync — Final Teacher Demonstration Checklist
## Practical Pre-Flight, Live Execution, and Post-Demo Checklist

**Project:** ShareSync — Share Market Portfolio Tracker  
**Branch:** `mysql-version`  
**Presenter:** Md. Tanvir Anjum  
**Target Environment:** Localhost (`http://localhost:5000`) with MySQL 8.0 on port 3306  

---

## 1. Before Demonstration (Pre-Flight Checks)

Complete these verification steps 15–30 minutes before the evaluator arrives:

- [ ] **Git Branch Safety:**
  - Run `git branch` -> Verify you are on `mysql-version`.
  - Run `git status` -> Confirm clean working tree.
  - Verify `main` (Oracle version) is untouched.
- [ ] **MySQL Service Health:**
  - Verify MySQL 8.0 is running (`Get-Service MySQL*` or check MySQL Workbench).
  - Open MySQL Workbench / CLI and run: `USE sharesync; SHOW TABLES;` (17 tables displayed).
- [ ] **Demo Data Intactness:**
  - Verify User #1 exists: `SELECT user_id, email, role FROM app_users WHERE user_id = 1;` (`investor@sharesync.com`).
  - Verify 4 portfolios exist: `SELECT COUNT(*) FROM portfolios WHERE user_id = 1;` (Count: 4).
  - Verify transaction count: `SELECT COUNT(*) FROM transactions;` (Count: 69).
  - Reconciled Portfolio Value: ৳4,869,720.00 across all 4 portfolios.
- [ ] **Application Build & Tests:**
  - Run `dotnet build` -> Ensure 0 Errors.
  - Run `dotnet test` -> Confirm **493 Passed, 0 Failed, 0 Skipped**.
  - Run `node -c frontend/js/app.js` and `node -c frontend/js/investment-intelligence.js` -> 0 syntax errors.
- [ ] **Application Startup:**
  - Launch via `ShareSync.bat` or run:
    ```bash
    dotnet run --project src/ShareSync.Web
    ```
  - Confirm backend listening on `http://localhost:5000`.
- [ ] **Browser Readiness:**
  - Open Chrome/Edge in Incognito or clean profile at `http://localhost:5000`.
  - Set default theme to **Dark Mode** (or evaluator's preference).
  - Have MySQL Workbench or terminal window open side-by-side in background.

---

## 2. During Demonstration (Live Execution Flow)

Follow the 17-point demonstration path calmly:

- [ ] **1. Login & Auth:** Log in with `investor@sharesync.com` / `Password123#`. Show JWT token in dev tools if asked.
- [ ] **2. Dashboard:** Point out 4 top KPI cards (Total Value ৳4,869,720.00, Invested ৳4,648,956.71, Unrealized P/L +৳220,763.29, Dividends ৳34,500.00).
- [ ] **3. Portfolios:** Navigate to Portfolios view. Show 4 distinct portfolios (Main, Long-Term, Dividend, Global).
- [ ] **4. Holdings & VWAP:** Open **Main Investment Portfolio**. Explain Volume-Weighted Average Price formula on GP/SQURPHARMA.
- [ ] **5. Transactions:** Show transactions ledger. Filter by SELL orders to show realized gain/loss and broker commission fee tracking.
- [ ] **6. Multi-Currency:** Open **Global & Multi-Asset Portfolio** (Portfolio #4). Show AAPL/MSFT in USD being dynamically converted to BDT at 121.50 FX rate.
- [ ] **7. Dividends:** Show dividend income timeline (11 payouts, ৳34,500 total).
- [ ] **8. Watchlists:** Show blue-chip watchlists with composite primary keys preventing duplicate entries.
- [ ] **9. Financial Goals:** Show retirement/corpus milestone progress bars linked to live valuations.
- [ ] **10. Analytics:** Select portfolio; show CAGR (+14.2%), Annualized Volatility (16.8%), and Maximum Drawdown (-8.4%).
- [ ] **11. Benchmark (DSEX):** Show relative outperformance chart against Dhaka Stock Exchange benchmark (Alpha = +7.2%).
- [ ] **12. Inflation (Fisher Equation):** Show nominal return vs. Bangladesh inflation adjusted real purchasing power return.
- [ ] **13. Investment Intelligence:** Show multi-factor equity scoring cards and explain disclaimer that it is quantitative analysis, not financial advice.
- [ ] **14. DSE Market Search:** Search for tickers like "BATBC" or "BEXIMCO". Explain resilient fallback caching if DSE exchange portal is down.
- [ ] **15. Reports & Export:** Preview Holdings Report. Show that the total matches the dashboard exactly down to four decimal places. Click Export CSV.
- [ ] **16. Database Objects:** Switch to MySQL Workbench. Show:
  - `SHOW TABLES;` (17 tables)
  - `SELECT * FROM vw_portfolio_holdings LIMIT 3;` (SQL View)
  - `SELECT fn_get_weighted_avg_price(1, 1);` (Stored Function)
  - `SELECT * FROM transaction_audit ORDER BY audit_id DESC LIMIT 3;` (Trigger Output)
- [ ] **17. Automated Tests:** Switch to terminal, run `dotnet test`, show 493 passing tests.

---

## 3. After Demonstration (Teardown & Verification)

Ensure the demo environment remains clean and frozen:

- [ ] **1. Sign Out:** Click user profile in top-right navbar -> Click **Sign Out**. Confirm redirect to `/login.html` and token clearance from `localStorage`.
- [ ] **2. Check for Accidental Mutations:**
  - Verify database row count: `SELECT COUNT(*) FROM transactions;` should still be 69.
  - If a test transaction was inserted during live demonstration, delete that specific test row or run `database/mysql/reset_demo_data.sql`.
- [ ] **3. Terminal Cleanup:** Gracefully stop the web application process (`Ctrl+C`).
- [ ] **4. Git Verification:** Run `git status` -> Verify working tree is clean. Confirm no unintended files or local database dumps were created.
