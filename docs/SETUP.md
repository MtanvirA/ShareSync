# Setup Guide

## Prerequisites
- **.NET 8 SDK** (or .NET 10 with roll-forward)
- **Oracle Database Express Edition / 26ai Free** (Port 1521)

## Steps
1. **Clone the Repository**.
2. **Configure Oracle:** Ensure the `FREEPDB1` pluggable database is active.
3. **Application Settings:** Copy `src/ShareSync.Web/appsettings.example.json` to `appsettings.json`. Update the `OracleConnection` string and `Jwt:Secret` with a strong local key.
4. **Database Initialization:** Open `database/verification/ShareSync_Demo.sql` or run EF Core Migrations to generate the schema.
5. **Run Backend:** `cd src/ShareSync.Web && dotnet run`
6. **Run Frontend:** Open `http://localhost:5000` (or the Kestrel port shown in the console) in your web browser.
