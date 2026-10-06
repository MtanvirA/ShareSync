# ShareSync — MySQL Setup & Installation Guide

This document provides a single, reproducible setup path for running the **ShareSync MySQL Version** on a fresh developer or evaluator machine.

---

## 1. Prerequisites

Before starting, ensure the following software is installed:
1. **.NET 8.0 SDK** (or .NET 10 SDK with `DOTNET_ROLL_FORWARD=Major`):
   ```powershell
   dotnet --version
   ```
2. **MySQL Server 8.0+** running locally on port `3306`:
   ```powershell
   Get-Service -Name MySQL80   # Windows service verification
   ```
3. **Git**:
   ```powershell
   git --version
   ```

---

## 2. Step-by-Step Installation & Deployment

### Step 1: Create the Database & Deploy Schema
Using the MySQL command line utility (`mysql.exe`), run the master automated deployment script:
```powershell
# Prompts for your local MySQL root password:
Get-Content database\mysql\setup.sql -Raw | mysql -u root -p
```
*Note: If executing scripts individually, run them in this exact order:*
```powershell
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS sharesync CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
Get-Content database\mysql\01_schema.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\04_functions.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\05_procedures.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\06_triggers.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\02_seed.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\07_tests.sql -Raw | mysql -u root -p sharesync
```

### Step 2: Configure MySQL Connection String
You can set the connection string using a PowerShell environment variable (recommended to avoid committing passwords to version control):
```powershell
# Set in current PowerShell session:
$env:ConnectionStrings__MySqlConnection = "Server=localhost;Port=3306;Database=sharesync;User=root;Password=YOUR_LOCAL_PASSWORD;CharSet=utf8mb4;"
```
Or edit `src/ShareSync.Web/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "MySqlConnection": "Server=localhost;Port=3306;Database=sharesync;User=root;Password=YOUR_LOCAL_PASSWORD;CharSet=utf8mb4;"
  }
}
```

### Step 3: Build the Solution
Verify clean compilation across all projects:
```powershell
dotnet build
```
*Expected Result:* `0 Error(s)`.

### Step 4: Run Automated Tests
Execute the unit and integration test suite:
```powershell
dotnet test
```
*Expected Result:* `Passed! - Failed: 0, Passed: 350, Skipped: 0, Total: 350`.

### Step 5: Start the Web Application
```powershell
dotnet run --project src/ShareSync.Web
```
The server will start listening at: **`http://localhost:5000`**

### Step 6: Verify Database Connectivity
Open a separate terminal and run:
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/health" -Method Get
```
*Expected Output:*
```json
{
  "success": true,
  "data": {
    "status": "Healthy",
    "service": "ShareSync Backend API"
  }
}
```

---

## 3. Demo Login Credentials

| Role | Email | Password |
|---|---|---|
| **Investor** | `investor@sharesync.com` | `Password123#` |
| **Investor** | `tanvir@sharesync.com` | `Password123#` |
| **Administrator** | `admin@sharesync.com` | `Admin123#` |

---

## 4. Verification & Troubleshooting

- **MySQL Service not running:** Start it via PowerShell: `Start-Service -Name MySQL80` or `net start MySQL80`.
- **Port 5000 in use:** Launch on an alternate port:
  ```powershell
  $env:ASPNETCORE_URLS = "http://localhost:5050"
  dotnet run --project src/ShareSync.Web
  ```
- **Password authentication failed:** Ensure the user specified in the connection string has full `ALL PRIVILEGES` on the `sharesync` database.
