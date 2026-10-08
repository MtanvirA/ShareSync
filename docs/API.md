# API Documentation

ShareSync endpoints are organized logically around resources.

## Authentication & Account Security (LF-32)
- `POST /api/auth/register` - Registers new user with PBKDF2 hashed password
- `POST /api/auth/login` - Returns JWT Bearer Token
- `PUT /api/auth/change-password` - Updates password with verification and anti-reuse check
- `POST /api/auth/logout` - Clears authenticated user session
- `GET /api/auth/mfa/status` - Returns TOTP RFC 6238 readiness and parameters
- `POST /api/auth/mfa/generate` - Generates Base32 TOTP setup key and otpauth:// URI
- `POST /api/auth/mfa/verify` - Tests TOTP code validation with ±1 timestep drift tolerance

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

## Benchmarks & Market History (LF-30 & LF-31)
- `GET /api/benchmarks`
  - Returns list of available benchmarks (`DSEX`, `DS30`, `DSES`/`DSEG`) with date range, observation count, and latest close value.
- `GET /api/benchmarks/compare?portfolioId={id}&benchmarkCode={code}`
  - Returns date-aligned performance comparison between portfolio snapshots and database benchmark history.
  - Computes `portfolioReturnPercentage`, `benchmarkReturnPercentage`, and `outperformancePercentage` over strictly overlapping dates.
  - Returns `hasSufficientData = false` with explanatory message if overlapping points < 2.

## Investment Intelligence
- `GET /api/investment-intelligence/{companyId}`
  - Returns `InvestmentProfileDto` containing Score, CAGR, Volatility, Drawdown, and Moving Averages.

*(Note: All non-auth endpoints require a valid JWT `Authorization: Bearer <token>` header)*
