# Testing — MySQL Version

ShareSync incorporates an automated test suite of **350 automated tests**.

---

## Test Coverage
- **Financial Calculation Consistency Tests:** Validates mathematical identity across PortfolioService, ReportService, and DashboardService using the Cumulative Weighted-Average Cost Basis model matching MySQL stored functions (`fn_get_weighted_avg_price`, `fn_calculate_unrealized_pl`).
- **Investment Intelligence Tests:** Evaluates CAGR, Annualized Volatility, Maximum Drawdown, Trend Classification, and Data Quality warnings across simulated and historical market series.
- **Service & Domain Tests:** Covers Authentication, Portfolios, Transactions, Dividends, Watchlists, Alerts, Notifications, and Reports.
- **Provider & Database Verification:** Validates EF Core mapping against MySQL, including schema validation, trigger audits, and data provenance.

---

## Running Automated Tests

From the solution root:
```powershell
dotnet test
```

### Running MySQL Verification Script
To test database programmability (functions, procedures, triggers, views) directly against MySQL:
```powershell
Get-Content database\mysql\07_tests.sql -Raw | mysql -u root -p sharesync
```
