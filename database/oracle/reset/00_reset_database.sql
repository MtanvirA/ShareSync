-- ============================================================
-- ShareSync - Oracle Development Schema Reset Script
-- File: 00_reset_database.sql
-- WARNING: Run ONLY against isolated test/dev schemas!
-- ============================================================

-- 1. Drop Views, Triggers, Procedures, Functions
BEGIN
    EXECUTE IMMEDIATE 'DROP VIEW VW_PORTFOLIO_HOLDINGS';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP TRIGGER trg_transactions_audit';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP PROCEDURE sp_record_transaction';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP PROCEDURE sp_generate_portfolio_snapshot';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP FUNCTION fn_calculate_unrealized_pl';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

BEGIN
    EXECUTE IMMEDIATE 'DROP FUNCTION fn_get_weighted_avg_price';
EXCEPTION WHEN OTHERS THEN NULL;
END;
/

-- 2. Drop Tables in reverse dependency order
DECLARE
    TYPE t_table_names IS TABLE OF VARCHAR2(50);
    v_tables t_table_names := t_table_names(
        'TRANSACTION_AUDIT',
        'PORTFOLIO_SNAPSHOTS',
        'TRANSACTIONS',
        'WATCHLIST_ITEMS',
        'DIVIDENDS',
        'COMPANIES',
        'SECTORS',
        'WATCHLISTS',
        'PORTFOLIOS',
        'APP_USERS'
    );
BEGIN
    FOR i IN 1..v_tables.COUNT LOOP
        BEGIN
            EXECUTE IMMEDIATE 'DROP TABLE ' || v_tables(i) || ' CASCADE CONSTRAINTS';
        EXCEPTION
            WHEN OTHERS THEN NULL;
        END;
    END LOOP;
END;
/
