-- ============================================================
-- ShareSync - Master MySQL Setup & Deployment Script
-- File: setup.sql
-- Description: Executes full schema creation, programmability, and seed data
-- Usage: mysql -u root -p < database/mysql/setup.sql
--   or within MySQL CLI: source database/mysql/setup.sql;
-- ============================================================

SELECT '>>> Initializing ShareSync Database on MySQL 8.x...' AS status;

-- 1. Create Database and Tables
SOURCE 01_schema.sql;

-- 2. Create User-Defined Functions
SOURCE 04_functions.sql;

-- 3. Create Stored Procedures
SOURCE 05_procedures.sql;

-- 4. Create Audit Triggers
SOURCE 06_triggers.sql;

-- 5. Populate Comprehensive Seed Dataset
SOURCE 02_seed.sql;

SELECT '>>> ShareSync MySQL Setup Completed Successfully!' AS status;

-- 6. Execute Verification & Health Audit
SOURCE 07_tests.sql;
