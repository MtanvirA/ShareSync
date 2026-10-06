# Setup Guide — MySQL Version

## Prerequisites
- **.NET 8 SDK** (or .NET 10 SDK with roll-forward enabled)
- **MySQL Server 8.0+** running on `localhost:3306`

---

## Steps

### 1. Install & Verify MySQL Server 8.x
Ensure MySQL 8.0+ is installed and running:
```powershell
Get-Service -Name MySQL80
```

### 2. Deploy the Database Schema & Programmability
Execute the master deployment script `setup.sql` or run the numbered SQL scripts:
```powershell
# Master setup:
Get-Content database\mysql\setup.sql -Raw | mysql -u root -p

# Or sequential execution:
Get-Content database\mysql\01_schema.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\04_functions.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\05_procedures.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\06_triggers.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\02_seed.sql -Raw | mysql -u root -p sharesync
Get-Content database\mysql\07_tests.sql -Raw | mysql -u root -p sharesync
```

### 3. Configure MySQL Connection String
Set your credentials in `src/ShareSync.Web/appsettings.json`:
```json
"ConnectionStrings": {
  "MySqlConnection": "Server=localhost;Port=3306;Database=sharesync;User=root;Password=YOUR_PASSWORD;CharSet=utf8mb4;"
}
```
Or export via PowerShell environment variable (avoids committing passwords):
```powershell
$env:ConnectionStrings__MySqlConnection = "Server=localhost;Port=3306;Database=sharesync;User=root;Password=YOUR_PASSWORD;CharSet=utf8mb4;"
```

### 4. Build and Run the Application
```powershell
dotnet build
dotnet run --project src/ShareSync.Web
```

### 5. Open the Application
Navigate to `http://localhost:5000` in your browser.

**Demo Login Credentials:**
- Investor: `investor@sharesync.com` / `Password123#`
- Admin: `admin@sharesync.com` / `Admin123#`
