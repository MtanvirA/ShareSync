-- ============================================================
-- ShareSync - Reporting and Analytical Indexes
-- File: 11_reporting_indexes.sql
-- ============================================================

-- 1. TRANSACTIONS (portfolio_id, transaction_date)
-- QUERY:
--   SELECT ... FROM transactions WHERE portfolio_id = :pId AND transaction_date <= :asOfDate
-- REASON:
--   Holdings calculation, portfolio history, and snapshot generation all filter by
--   portfolio_id and date.
-- EXPECTED BENEFIT:
--   Replaces full table scan with an efficient Index Range Scan.
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX idx_tx_portfolio_date ON transactions (portfolio_id, transaction_date)';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN RAISE; END IF; -- ORA-00955: name is already used by an existing object
END;
/

-- 2. TRANSACTIONS (company_id)
-- QUERY:
--   SELECT ... FROM transactions WHERE company_id = :cId
-- REASON:
--   Calculating user's held shares for dividends and sector aggregations.
-- EXPECTED BENEFIT:
--   Faster index lookup and merge join performance with companies table.
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX idx_tx_company ON transactions (company_id)';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN RAISE; END IF;
END;
/

-- 3. DIVIDENDS (company_id, payment_date)
-- QUERY:
--   SELECT ... FROM dividends WHERE company_id = :cId AND payment_date BETWEEN :start AND :end
-- REASON:
--   Dividend income reports filter by company and date range.
-- EXPECTED BENEFIT:
--   Index range scan on payment dates without evaluating unneeded rows.
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX idx_dividends_comp_paydate ON dividends (company_id, payment_date)';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN RAISE; END IF;
END;
/

-- 4. COMPANIES (sector_id)
-- QUERY:
--   SELECT ... FROM companies c JOIN sectors s ON c.sector_id = s.sector_id
-- REASON:
--   Company and sector investment report groups companies by sector.
-- EXPECTED BENEFIT:
--   Optimizes foreign key joins between sectors and companies.
BEGIN
    EXECUTE IMMEDIATE 'CREATE INDEX idx_companies_sector ON companies (sector_id)';
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE != -955 THEN RAISE; END IF;
END;
/
