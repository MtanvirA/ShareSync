# ShareSync Demo Guide

## 1. Startup Sequence
1. Ensure Oracle 26ai (or XE) is running locally on port 1521 (`FREEPDB1` service name).
2. Navigate to `src/ShareSync.Web` and execute: `dotnet run` (Requires .NET 8 or newer).
3. Open your browser and navigate to `http://localhost:5000` (or `http://localhost:5194` as indicated by Kestrel).

## 2. Login Credentials
- **Username:** `demo_user` (or register a new user live)
- **Password:** `Demo@123`
- *Note: Passwords are securely hashed with BCrypt. JWT token will be stored in `localStorage` upon login.*

## 3. Recommended Data
- **Demo User:** `demo_user` (owns 'My Main Portfolio')
- **Demo Company 1:** `BATBC` (Stable, high-value, long history)
- **Demo Company 2:** `BEXIMCO` (Highly volatile, popular)
- **Demo Company 3:** `1JANATAMF` (Mutual fund, long history)

## 4. Exact Actions to Perform
1. **Dashboard:** Start here. Show total portfolio value.
2. **Companies:** Search for `BATBC`. Click to view details.
3. **Portfolio:** Open 'My Main Portfolio'. Show pie chart of holdings.
4. **Transactions:** Execute a `BUY` of 100 shares of `BATBC`. Show the portfolio updates automatically.
5. **Watchlist:** Add `BEXIMCO` to the watchlist. 
6. **Investment Intelligence:** 
   - Navigate to the new module.
   - Search for `BATBC`.
   - Demonstrate the mathematical 5-year analysis chart (Price, MA50, MA200).
   - Explain the Risk/Return metrics (CAGR, Volatility).

## 5. Exact Database Queries (Run via SQL*Plus or SQL Developer)
Execute the script at `database/verification/ShareSync_Demo.sql`. Highlights:
- `SELECT table_name FROM user_tables;` (Proves tables exist)
- `SELECT constraint_name, table_name, status FROM user_constraints WHERE constraint_type = 'P';` (Proves PKs exist)
- `EXPLAIN PLAN FOR SELECT trading_date, price FROM company_price_history WHERE company_id = 3 AND trading_date >= SYSDATE - 1825;` (Proves performance optimization)

## 6. Key Mathematical Formulas
- **CAGR:** `(Ending Value / Beginning Value)^(1/5) - 1`
- **Annualized Volatility:** `Daily StdDev * sqrt(252)`
- **Max Drawdown:** `Minimum( (Current Close - Running Peak) / Running Peak )`
- **Investment Score:** Weighted normalization of Return (30%), Risk (20%), Drawdown (20%), Consistency (15%), Trend (15%).

## 7. Expected Outputs
- The charts will render cleanly using Vanilla JS/Chart.js.
- No errors in the console.
- Transactions persist to Oracle securely with ACID compliance.

## 8. Common Teacher Questions & Answers
**Q: How did you calculate the Investment Score?**
*A: The score normalizes historical return, volatility, max drawdown, positive days, and moving average trends into a 0-100 index using a deterministic algorithm in the backend service layer, entirely written in C#.*

**Q: Is this real data?**
*A: Yes, historical OHLC data is from the 'Dhaka Stock Exchange Historical Data' (DOI: 10.7910/DVN/XIFYT1) via Harvard Dataverse.*

**Q: Are you predicting prices?**
*A: No. As stated in the disclaimer on the frontend, this is an academic historical risk/return analysis. It explicitly does not guarantee future profitability.*
