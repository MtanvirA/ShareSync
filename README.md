# ShareSync: Share Market Portfolio Tracker (MySQL Version)

ShareSync is an academic web-based portfolio tracking and market-analysis system built using ASP.NET Core, MySQL 8.x, Entity Framework Core, and a modern HTML/CSS/JavaScript frontend.

## Documentation Links
- [Architecture](docs/ARCHITECTURE.md)
- [Database (MySQL)](docs/DATABASE.md)
- [MySQL Kit](database/mysql/README.md)
- [API](docs/API.md)
- [Features](docs/FEATURES.md)
- [Investment Intelligence](docs/INVESTMENT_INTELLIGENCE.md)
- [Setup Guide](docs/SETUP.md)
- [Testing](docs/TESTING.md)
- [Security](docs/SECURITY.md)
- [Dataset](docs/DATASET.md)
- [Demo Guide](docs/DEMO_GUIDE.md)
- [Project Structure](docs/PROJECT_STRUCTURE.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Contributing](docs/CONTRIBUTING.md)
- [Release Notes](docs/RELEASE_NOTES.md)

## 1. Project Overview
Managing stock portfolios manually makes it difficult to track holdings, transactions, dividends, watchlists, reports, and historical performance.
ShareSync provides a centralized web-based platform for managing portfolio-related information and analyzing historical market data, cleanly distinguishing user portfolio data from external historical market data.

## 2. Key Features
- Multi-portfolio management
- ACID-compliant transactions
- Detailed reporting & dynamic holdings valuation
- Historical risk/return evaluation (Investment Intelligence)
See [Features](docs/FEATURES.md) for details.

## 3. Investment Intelligence
The module evaluates historical market behavior using deterministic statistical formulas (CAGR, Annualized Volatility, Max Drawdown). It is designed to act as a *Historical Investment Profile* without making explicit financial advice or predicting future prices.
See [Investment Intelligence](docs/INVESTMENT_INTELLIGENCE.md).

## 4. Technology Stack
- **Backend:** C# / ASP.NET Core Web API (.NET 8)
- **Database:** MySQL 8.x (`Pomelo.EntityFrameworkCore.MySql`)
- **Frontend:** Pure HTML5, CSS3, Bootstrap 5, Vanilla JavaScript, Chart.js
- **Security:** PBKDF2 password hashing, JWT Bearer Authentication

## 5. System Architecture
Uses ASP.NET Core Clean Architecture.
See [Architecture](docs/ARCHITECTURE.md).

## 6. Database
Strictly normalized relational schema with core entities including `APP_USERS`, `SECTORS`, `COMPANIES`, `PORTFOLIOS`, `WATCHLISTS`, `TRANSACTIONS`, `DIVIDENDS`, `TRANSACTION_AUDIT`, `PORTFOLIO_SNAPSHOTS`, `COMPANY_PRICE_HISTORY`, `ALERTS`, `NOTIFICATIONS`, and `PORTFOLIO_GOALS`.
See [Database](docs/DATABASE.md) and [MySQL Kit](database/mysql/README.md).

## 7. Security
Token-based authentication, password hashing, and parameterized queries.
See [Security](docs/SECURITY.md).

## 8. Project Structure
See [Project Structure](docs/PROJECT_STRUCTURE.md).

## 9. Requirements
- .NET 8 SDK
- MySQL Server 8.0+ (Port 3306)

## 10. Installation & Setup
See [Setup Guide](docs/SETUP.md).

## 11. Configuration
Uses `appsettings.json`. Examples provided in `appsettings.example.json`.

## 12. Historical Dataset Import
The project imports `Dhaka Stock Exchange Historical Data (1999-2025)` via a `.NET 10` Console Data Importer. See [Dataset](docs/DATASET.md).

## 13. Running the Application
`cd src/ShareSync.Web && dotnet run`

## 14. Testing
Contains 350+ automated unit/integration tests.
See [Testing](docs/TESTING.md).

## 15. Demo
See [Demo Guide](docs/DEMO_GUIDE.md).

## 16. Known Limitations
- The Investment Intelligence models use unadjusted historical closing prices and are computationally static (not ML-driven).
- Frontend may stagger if plotting 5,000+ points on low-end mobile devices.

## 17. Data Source & Academic Disclaimer
**Source:** `Harvard Dataverse` (DOI: `10.7910/DVN/XIFYT1`).
**Disclaimer:** This is a retrospective statistical assessment. It is NOT a prediction system, NOT financial advice, and does NOT guarantee future profitability.