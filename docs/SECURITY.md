# Security

## Implemented Measures
- **Authentication:** Token-based JWT (`Bearer`).
- **Password Storage:** Strong cryptographic hashing using `BCrypt.Net-Next`.
- **SQL Injection Prevention:** Enforced via EF Core Parameterized Queries.
- **API Exception Obfuscation:** Internal stack traces are suppressed in production mode.

## Local Configuration
Secrets must NOT be stored in source code. `appsettings.json` is excluded via `.gitignore`. Always refer to `appsettings.example.json`.
