# API Documentation

ShareSync endpoints are organized logically around resources.

## Authentication
- `POST /api/auth/register`
- `POST /api/auth/login` - Returns JWT Bearer Token

## Portfolios & Transactions
- `GET /api/portfolios`
- `POST /api/transactions/buy`
- `POST /api/transactions/sell`

## Investment Intelligence
- `GET /api/investment-intelligence/{companyId}`
  - Returns `InvestmentProfileDto` containing Score, CAGR, Volatility, Drawdown, and Moving Averages.

*(Note: All non-auth endpoints require a valid JWT `Authorization: Bearer <token>` header)*
