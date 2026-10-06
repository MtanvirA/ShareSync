# Troubleshooting

- **Oracle Connection Refused:** Verify the Oracle Listener is running on Port 1521 and `FREEPDB1` is mounted.
- **JWT Token Invalid:** Ensure your `appsettings.json` has a secret key at least 32 characters long.
- **EF Core Translation Errors:** Some `GroupBy` queries in Oracle EF Core fail in older versions. The application evaluates these client-side where necessary.
- **Data Importer Hangs:** Large datasets require `OracleBulkCopy`. Ensure the target table is not locked by a pending transaction in SQL*Plus.
