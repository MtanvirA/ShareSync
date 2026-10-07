# API Documentation

ShareSync endpoints are organized logically around resources.

## Authentication
- `POST /api/auth/register`
- `POST /api/auth/login` - Returns JWT Bearer Token

## Portfolios & Transactions
- `GET /api/portfolios`
- `GET /api/portfolios/{id}` - Returns portfolio detail including `currency`, `inflationRate`, and `realReturnPercentage`
- `POST /api/transactions/buy`
- `POST /api/transactions/sell`

## Inflation (LF-28)
- `GET /api/inflation/rates?country=BD` - Retrieves historical annual inflation rate series
- `GET /api/inflation/real-return?nominalReturn=10&startYear=2023&endYear=2024&country=BD` - Returns real return calculation with data availability check

## Currencies & Multi-Asset (LF-29)
- `GET /api/currencies/supported` - Returns supported currencies (`BDT`, `USD`) and asset types (`EQUITY`, `ETF`, `MUTUAL_FUND`, `CASH`)
- `GET /api/currencies/rates` - Returns exchange rates
- `GET /api/currencies/convert?amount=100&from=USD&to=BDT` - Converts amounts with detailed breakdown

## Investment Intelligence
- `GET /api/investment-intelligence/{companyId}`
  - Returns `InvestmentProfileDto` containing Score, CAGR, Volatility, Drawdown, and Moving Averages.

*(Note: All non-auth endpoints require a valid JWT `Authorization: Bearer <token>` header)*
