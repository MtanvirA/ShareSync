# Testing

ShareSync incorporates a robust suite of **350 automated tests**.

## Test Coverage
- **Unit Tests:** Validates core domain logic and mathematical calculations (e.g. CAGR/Volatility).
- **Integration Tests:** Verifies API endpoint behaviors and DTO serialization.
- **Database Tests:** Simulates EF Core behaviors using InMemory databases with explicit client-side LINQ evaluations for Oracle-specific grouping constraints.

## Running Tests
Execute the following command from the root directory:
`dotnet test ShareSync.sln`
