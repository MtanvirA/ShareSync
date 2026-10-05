-- ============================================================================
-- ShareSync - Realistic Demonstration Dataset
-- File: 10_demo_dataset.sql
-- Description: Populates a comprehensive, realistic demonstration dataset for
--              ShareSync. Safe, repeatable, constraint-respecting, and idempotent.
-- ============================================================================

SET DEFINE OFF;
SET SERVEROUTPUT ON;

PROMPT ============================================================================
PROMPT Seeding ShareSync Demo Dataset...
PROMPT ============================================================================

-- ----------------------------------------------------------------------------
-- 1. DEMO USERS (Investor & Admin)
-- ----------------------------------------------------------------------------
DECLARE
    v_investor_hash NVARCHAR2(255) := 'ozOJ8GVWyz3VJnrFM+R/zQ==.Bgq78cPlarDa7KPehH1LMC2Up9VdyNbPbLvzw45g6EA='; -- Password123#
    v_admin_hash    NVARCHAR2(255) := '4ivxEgBzpuLDnOpZvtYPfw==.bPSkAXZlwYp25mGQ8+W9732+X95DUKVmzO0KBTh07ZE='; -- Admin123#
    v_exists NUMBER;
BEGIN
    -- Demo Investor 1: investor@sharesync.com
    SELECT COUNT(*) INTO v_exists FROM app_users WHERE LOWER(email) = 'investor@sharesync.com';
    IF v_exists = 0 THEN
        INSERT INTO app_users (name, email, password_hash, role, is_active, created_at)
        VALUES ('Demo Investor', 'investor@sharesync.com', v_investor_hash, 'INVESTOR', 1, SYSTIMESTAMP - NUMTODSINTERVAL(90, 'DAY'));
        DBMS_OUTPUT.PUT_LINE('Created demo investor: investor@sharesync.com');
    ELSE
        UPDATE app_users
        SET password_hash = v_investor_hash, is_active = 1, role = 'INVESTOR'
        WHERE LOWER(email) = 'investor@sharesync.com';
        DBMS_OUTPUT.PUT_LINE('Updated demo investor: investor@sharesync.com');
    END IF;

    -- Also ensure primary user tanvir@sharesync.com is active with valid hash
    SELECT COUNT(*) INTO v_exists FROM app_users WHERE LOWER(email) = 'tanvir@sharesync.com';
    IF v_exists > 0 THEN
        UPDATE app_users
        SET password_hash = v_investor_hash, is_active = 1, role = 'INVESTOR'
        WHERE LOWER(email) = 'tanvir@sharesync.com';
        DBMS_OUTPUT.PUT_LINE('Synchronized primary investor: tanvir@sharesync.com');
    END IF;

    -- Demo Admin: admin@sharesync.com
    SELECT COUNT(*) INTO v_exists FROM app_users WHERE LOWER(email) = 'admin@sharesync.com';
    IF v_exists = 0 THEN
        INSERT INTO app_users (name, email, password_hash, role, is_active, created_at)
        VALUES ('System Administrator', 'admin@sharesync.com', v_admin_hash, 'ADMIN', 1, SYSTIMESTAMP - NUMTODSINTERVAL(120, 'DAY'));
        DBMS_OUTPUT.PUT_LINE('Created demo admin: admin@sharesync.com');
    ELSE
        UPDATE app_users
        SET password_hash = v_admin_hash, is_active = 1, role = 'ADMIN'
        WHERE LOWER(email) = 'admin@sharesync.com';
        DBMS_OUTPUT.PUT_LINE('Synchronized demo admin: admin@sharesync.com');
    END IF;

    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 2. SECTORS
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE upsert_sector(p_name IN VARCHAR2, p_desc IN VARCHAR2) IS
        v_count NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_count FROM sectors WHERE UPPER(sector_name) = UPPER(p_name);
        IF v_count = 0 THEN
            INSERT INTO sectors (sector_name, description) VALUES (p_name, p_desc);
            DBMS_OUTPUT.PUT_LINE('Added sector: ' || p_name);
        END IF;
    END;
BEGIN
    upsert_sector('Telecommunication', 'Mobile networks, telecommunication infrastructure and digital services');
    upsert_sector('Pharmaceuticals', 'Pharmaceutical manufacturing, clinical research and healthcare products');
    upsert_sector('Consumer Goods', 'Fast-moving consumer products, tobacco, food and household goods');
    upsert_sector('Financial Services', 'Commercial banks, non-banking financial institutions and insurance');
    upsert_sector('Technology', 'Software engineering, digital solutions and IT hardware systems');
    upsert_sector('Energy & Power', 'Power generation, natural gas distribution and clean energy solutions');
    upsert_sector('Engineering & Industrial', 'Heavy manufacturing, electronics assembly and construction materials');
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 3. DEMO COMPANIES
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE upsert_company(
        p_ticker IN VARCHAR2,
        p_name IN VARCHAR2,
        p_sector IN VARCHAR2,
        p_price IN NUMBER,
        p_mcap IN NUMBER
    ) IS
        v_sector_id NUMBER;
        v_count NUMBER;
    BEGIN
        SELECT sector_id INTO v_sector_id FROM sectors WHERE UPPER(sector_name) = UPPER(p_sector);
        SELECT COUNT(*) INTO v_count FROM companies WHERE UPPER(ticker_symbol) = UPPER(p_ticker);
        IF v_count = 0 THEN
            INSERT INTO companies (company_name, ticker_symbol, sector_id, current_price, market_cap, created_at, is_active)
            VALUES (p_name, UPPER(p_ticker), v_sector_id, p_price, p_mcap, SYSTIMESTAMP - NUMTODSINTERVAL(90, 'DAY'), 1);
            DBMS_OUTPUT.PUT_LINE('Added company: ' || p_ticker);
        ELSE
            UPDATE companies
            SET current_price = p_price, market_cap = p_mcap, is_active = 1
            WHERE UPPER(ticker_symbol) = UPPER(p_ticker);
            DBMS_OUTPUT.PUT_LINE('Updated price for company: ' || p_ticker || ' -> ' || p_price);
        END IF;
    END;
BEGIN
    upsert_company('GP', 'Grameenphone PLC', 'Telecommunication', 380.00, 513000000000);
    upsert_company('SQURPHARMA', 'Square Pharmaceuticals PLC', 'Pharmaceuticals', 225.00, 201000000000);
    upsert_company('BATBC', 'British American Tobacco Bangladesh', 'Consumer Goods', 440.00, 237000000000);
    upsert_company('BEXIMCO', 'BEXIMCO Pharmaceuticals PLC', 'Pharmaceuticals', 115.50, 52600000000);
    upsert_company('BRACBANK', 'BRAC Bank PLC', 'Financial Services', 56.50, 91000000000);
    upsert_company('EBL', 'Eastern Bank PLC', 'Financial Services', 32.00, 39000000000);
    upsert_company('WALTONHIL', 'Walton Hi-Tech Industries PLC', 'Consumer Goods', 680.00, 206000000000);
    upsert_company('SUMITPOWER', 'Summit Power Limited', 'Energy & Power', 24.80, 26500000000);
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 4. DEMO PORTFOLIOS (For investor@sharesync.com and tanvir@sharesync.com)
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE ensure_portfolio(p_user_email IN VARCHAR2, p_port_name IN VARCHAR2, p_desc IN VARCHAR2) IS
        v_user_id NUMBER;
        v_count NUMBER;
    BEGIN
        SELECT user_id INTO v_user_id FROM app_users WHERE LOWER(email) = LOWER(p_user_email);
        SELECT COUNT(*) INTO v_count FROM portfolios WHERE user_id = v_user_id AND UPPER(portfolio_name) = UPPER(p_port_name);
        IF v_count = 0 THEN
            INSERT INTO portfolios (user_id, portfolio_name, description, created_at)
            VALUES (v_user_id, p_port_name, p_desc, SYSTIMESTAMP - NUMTODSINTERVAL(60, 'DAY'));
            DBMS_OUTPUT.PUT_LINE('Created portfolio "' || p_port_name || '" for ' || p_user_email);
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    -- Portfolios for Demo Investor (investor@sharesync.com)
    ensure_portfolio('investor@sharesync.com', 'Long Term Growth', 'Core long-term growth portfolio focusing on market leaders and stable blue-chip equities.');
    ensure_portfolio('investor@sharesync.com', 'Dividend Income Portfolio', 'High-yield dividend focus generating stable passive quarterly cash flow.');
    ensure_portfolio('investor@sharesync.com', 'Tech & High-Growth Portfolio', 'High-growth equities in telecommunications, tech hardware, and consumer electronics.');

    -- Also ensure for tanvir@sharesync.com
    ensure_portfolio('tanvir@sharesync.com', 'Long Term Growth', 'Core long-term growth portfolio focusing on market leaders and stable blue-chip equities.');
    ensure_portfolio('tanvir@sharesync.com', 'Dividend Income Portfolio', 'High-yield dividend focus generating stable passive quarterly cash flow.');
    ensure_portfolio('tanvir@sharesync.com', 'Tech & High-Growth Portfolio', 'High-growth equities in telecommunications, tech hardware, and consumer electronics.');

    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 5. DEMO TRANSACTIONS (Realistic BUY & SELL with Integer Shares)
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE seed_tx_for_user(p_user_email IN VARCHAR2) IS
        v_user_id NUMBER;
        v_p_growth NUMBER;
        v_p_div NUMBER;
        v_p_tech NUMBER;
        v_c_gp NUMBER;
        v_c_squr NUMBER;
        v_c_batbc NUMBER;
        v_c_bex NUMBER;
        v_c_brac NUMBER;
        v_c_ebl NUMBER;
        v_c_walton NUMBER;
        v_c_sumit NUMBER;

        PROCEDURE add_tx(
            p_port_id IN NUMBER,
            p_comp_id IN NUMBER,
            p_type IN VARCHAR2,
            p_qty IN NUMBER,
            p_price IN NUMBER,
            p_days_ago IN NUMBER
        ) IS
            v_cnt NUMBER;
        BEGIN
            -- Avoid exact duplicates
            SELECT COUNT(*) INTO v_cnt FROM transactions
            WHERE portfolio_id = p_port_id
              AND company_id = p_comp_id
              AND transaction_type = p_type
              AND quantity = p_qty
              AND price_per_share = p_price;

            IF v_cnt = 0 THEN
                INSERT INTO transactions (portfolio_id, company_id, transaction_type, quantity, price_per_share, transaction_date)
                VALUES (p_port_id, p_comp_id, p_type, p_qty, p_price, SYSTIMESTAMP - NUMTODSINTERVAL(p_days_ago, 'DAY'));
            END IF;
        END;
    BEGIN
        SELECT user_id INTO v_user_id FROM app_users WHERE LOWER(email) = LOWER(p_user_email);

        SELECT portfolio_id INTO v_p_growth FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Long Term Growth';
        SELECT portfolio_id INTO v_p_div FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Dividend Income Portfolio';
        SELECT portfolio_id INTO v_p_tech FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Tech & High-Growth Portfolio';

        SELECT company_id INTO v_c_gp FROM companies WHERE ticker_symbol = 'GP';
        SELECT company_id INTO v_c_squr FROM companies WHERE ticker_symbol = 'SQURPHARMA';
        SELECT company_id INTO v_c_batbc FROM companies WHERE ticker_symbol = 'BATBC';
        SELECT company_id INTO v_c_bex FROM companies WHERE ticker_symbol = 'BEXIMCO';
        SELECT company_id INTO v_c_brac FROM companies WHERE ticker_symbol = 'BRACBANK';
        SELECT company_id INTO v_c_ebl FROM companies WHERE ticker_symbol = 'EBL';
        SELECT company_id INTO v_c_walton FROM companies WHERE ticker_symbol = 'WALTONHIL';
        SELECT company_id INTO v_c_sumit FROM companies WHERE ticker_symbol = 'SUMITPOWER';

        -- Portfolio 1: Long Term Growth
        -- GP: Bought 150 @ 350.00, Sold 50 @ 370.00 -> Net 100 shares held (Current 380.00, Gain +3,000)
        add_tx(v_p_growth, v_c_gp, 'BUY', 150, 350.00, 50);
        add_tx(v_p_growth, v_c_gp, 'SELL', 50, 370.00, 20);

        -- SQURPHARMA: Bought 200 @ 210.00 -> Net 200 shares held (Current 225.00, Gain +3,000)
        add_tx(v_p_growth, v_c_squr, 'BUY', 200, 210.00, 45);

        -- BRACBANK: Bought 600 @ 51.00 -> Net 600 shares held (Current 56.50, Gain +3,300)
        add_tx(v_p_growth, v_c_brac, 'BUY', 600, 51.00, 40);

        -- BATBC: Bought 100 @ 465.00, Sold 20 @ 450.00 -> Net 80 shares held (Current 440.00, Unrealized Loss -2,000)
        add_tx(v_p_growth, v_c_batbc, 'BUY', 100, 465.00, 35);
        add_tx(v_p_growth, v_c_batbc, 'SELL', 20, 450.00, 15);

        -- Portfolio 2: Dividend Income Portfolio
        -- GP: Bought 120 @ 355.00
        add_tx(v_p_div, v_c_gp, 'BUY', 120, 355.00, 55);

        -- BATBC: Bought 150 @ 430.00
        add_tx(v_p_div, v_c_batbc, 'BUY', 150, 430.00, 50);

        -- EBL: Bought 1200 @ 29.50
        add_tx(v_p_div, v_c_ebl, 'BUY', 1200, 29.50, 42);

        -- SUMITPOWER: Bought 2500 @ 23.50, Sold 500 @ 25.00 -> Net 2000 shares held
        add_tx(v_p_div, v_c_sumit, 'BUY', 2500, 23.50, 38);
        add_tx(v_p_div, v_c_sumit, 'SELL', 500, 25.00, 12);

        -- Portfolio 3: Tech & High-Growth Portfolio
        -- WALTONHIL: Bought 60 @ 640.00, Sold 10 @ 690.00 -> Net 50 shares held (Current 680.00)
        add_tx(v_p_tech, v_c_walton, 'BUY', 60, 640.00, 48);
        add_tx(v_p_tech, v_c_walton, 'SELL', 10, 690.00, 18);

        -- GP: Bought 80 @ 365.00
        add_tx(v_p_tech, v_c_gp, 'BUY', 80, 365.00, 30);

        -- BEXIMCO: Bought 300 @ 110.00
        add_tx(v_p_tech, v_c_bex, 'BUY', 300, 110.00, 25);

        DBMS_OUTPUT.PUT_LINE('Seeded transactions for user: ' || p_user_email);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    seed_tx_for_user('investor@sharesync.com');
    seed_tx_for_user('tanvir@sharesync.com');
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 6. DEMO COMPANY PRICE HISTORY (30-day realistic series for Chart.js)
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE seed_history(p_ticker IN VARCHAR2, p_base_price IN NUMBER, p_drift_step IN NUMBER) IS
        v_comp_id NUMBER;
        v_existing NUMBER;
        v_cur_price NUMBER := p_base_price;
        v_open NUMBER;
        v_high NUMBER;
        v_low NUMBER;
        v_vol NUMBER;
    BEGIN
        SELECT company_id INTO v_comp_id FROM companies WHERE ticker_symbol = p_ticker;
        SELECT COUNT(*) INTO v_existing FROM company_price_history WHERE company_id = v_comp_id;

        IF v_existing < 15 THEN
            FOR d IN REVERSE 1..30 LOOP
                -- Plausible price oscillation
                v_cur_price := ROUND(p_base_price + (SIN(d * 0.4) * p_drift_step) + ((30 - d) * (p_drift_step * 0.1)), 2);
                v_open := ROUND(v_cur_price - (p_drift_step * 0.15), 2);
                v_high := ROUND(v_cur_price + (p_drift_step * 0.35), 2);
                v_low := ROUND(v_cur_price - (p_drift_step * 0.30), 2);
                v_vol := ROUND(40000 + (MOD(d * 4791, 35000)));

                INSERT INTO company_price_history (company_id, price, open_price, high_price, low_price, volume, recorded_at, created_at)
                VALUES (v_comp_id, v_cur_price, v_open, v_high, v_low, v_vol, TRUNC(SYSTIMESTAMP - NUMTODSINTERVAL(d, 'DAY')) + INTERVAL '14' HOUR, SYSTIMESTAMP);
            END LOOP;
            DBMS_OUTPUT.PUT_LINE('Generated 30-day price history for: ' || p_ticker);
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    seed_history('GP', 380.00, 12.00);
    seed_history('SQURPHARMA', 225.00, 6.50);
    seed_history('BATBC', 440.00, 15.00);
    seed_history('BEXIMCO', 115.50, 4.00);
    seed_history('BRACBANK', 56.50, 2.20);
    seed_history('EBL', 32.00, 1.10);
    seed_history('WALTONHIL', 680.00, 22.00);
    seed_history('SUMITPOWER', 24.80, 0.90);
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 7. DEMO DIVIDENDS
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE add_dividend(p_ticker IN VARCHAR2, p_dps IN NUMBER, p_decl_days IN NUMBER, p_pay_days IN NUMBER) IS
        v_comp_id NUMBER;
        v_cnt NUMBER;
    BEGIN
        SELECT company_id INTO v_comp_id FROM companies WHERE ticker_symbol = p_ticker;
        SELECT COUNT(*) INTO v_cnt FROM dividends
        WHERE company_id = v_comp_id AND dividend_per_share = p_dps;

        IF v_cnt = 0 THEN
            INSERT INTO dividends (company_id, dividend_per_share, declaration_date, payment_date)
            VALUES (v_comp_id, p_dps, TRUNC(SYSDATE - p_decl_days), TRUNC(SYSDATE - p_pay_days));
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    add_dividend('GP', 12.50, 75, 45);
    add_dividend('GP', 14.00, 160, 130);
    add_dividend('BATBC', 20.00, 90, 60);
    add_dividend('BATBC', 18.50, 180, 150);
    add_dividend('SQURPHARMA', 6.00, 60, 30);
    add_dividend('EBL', 2.50, 80, 50);
    add_dividend('BRACBANK', 1.50, 70, 40);
    add_dividend('SUMITPOWER', 1.80, 85, 55);
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 8. DEMO WATCHLISTS & ITEMS
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE seed_watchlist(p_user_email IN VARCHAR2) IS
        v_user_id NUMBER;
        v_wl_id NUMBER;
        v_cnt NUMBER;
        v_comp_id NUMBER;

        PROCEDURE add_item(p_ticker IN VARCHAR2, p_target IN NUMBER) IS
            v_c_id NUMBER;
            v_i_cnt NUMBER;
        BEGIN
            SELECT company_id INTO v_c_id FROM companies WHERE ticker_symbol = p_ticker;
            SELECT COUNT(*) INTO v_i_cnt FROM watchlist_items WHERE watchlist_id = v_wl_id AND company_id = v_c_id;
            IF v_i_cnt = 0 THEN
                INSERT INTO watchlist_items (watchlist_id, company_id, target_price, added_at)
                VALUES (v_wl_id, v_c_id, p_target, SYSTIMESTAMP - NUMTODSINTERVAL(15, 'DAY'));
            END IF;
        END;
    BEGIN
        SELECT user_id INTO v_user_id FROM app_users WHERE LOWER(email) = LOWER(p_user_email);

        SELECT COUNT(*) INTO v_cnt FROM watchlists WHERE user_id = v_user_id;
        IF v_cnt = 0 THEN
            INSERT INTO watchlists (user_id, watchlist_name, description, created_at)
            VALUES (v_user_id, 'Primary Market Watchlist', 'High-priority growth and dividend stocks to monitor', SYSTIMESTAMP - NUMTODSINTERVAL(30, 'DAY'))
            RETURNING watchlist_id INTO v_wl_id;
        ELSE
            SELECT watchlist_id INTO v_wl_id FROM watchlists WHERE user_id = v_user_id AND ROWNUM = 1;
        END IF;

        add_item('GP', 400.00);
        add_item('SQURPHARMA', 240.00);
        add_item('WALTONHIL', 720.00);
        add_item('BRACBANK', 60.00);
        add_item('BEXIMCO', 130.00);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    seed_watchlist('investor@sharesync.com');
    seed_watchlist('tanvir@sharesync.com');
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 9. DEMO ALERTS
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE seed_alerts(p_user_email IN VARCHAR2) IS
        v_user_id NUMBER;
        v_c_gp NUMBER;
        v_c_batbc NUMBER;
        v_p_growth NUMBER;
        v_cnt NUMBER;
    BEGIN
        SELECT user_id INTO v_user_id FROM app_users WHERE LOWER(email) = LOWER(p_user_email);
        SELECT company_id INTO v_c_gp FROM companies WHERE ticker_symbol = 'GP';
        SELECT company_id INTO v_c_batbc FROM companies WHERE ticker_symbol = 'BATBC';
        SELECT portfolio_id INTO v_p_growth FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Long Term Growth';

        -- 1. Triggered alert: GP exceeded 375.00
        SELECT COUNT(*) INTO v_cnt FROM alerts WHERE user_id = v_user_id AND company_id = v_c_gp AND alert_type = 'PRICE_ABOVE';
        IF v_cnt = 0 THEN
            INSERT INTO alerts (user_id, company_id, portfolio_id, alert_type, threshold_value, is_active, created_at, triggered_at, message)
            VALUES (v_user_id, v_c_gp, NULL, 'PRICE_ABOVE', 375.00, 0, SYSTIMESTAMP - NUMTODSINTERVAL(10, 'DAY'), SYSTIMESTAMP - NUMTODSINTERVAL(1, 'DAY'), 'Grameenphone PLC (GP) crossed above threshold ৳375.00');
        END IF;

        -- 2. Active alert: BATBC drops below 430.00
        SELECT COUNT(*) INTO v_cnt FROM alerts WHERE user_id = v_user_id AND company_id = v_c_batbc AND alert_type = 'PRICE_BELOW';
        IF v_cnt = 0 THEN
            INSERT INTO alerts (user_id, company_id, portfolio_id, alert_type, threshold_value, is_active, created_at, triggered_at, message)
            VALUES (v_user_id, v_c_batbc, NULL, 'PRICE_BELOW', 430.00, 1, SYSTIMESTAMP - NUMTODSINTERVAL(5, 'DAY'), NULL, 'Alert if BATBC drops below ৳430.00');
        END IF;

        -- 3. Active alert: Portfolio value above 150,000
        SELECT COUNT(*) INTO v_cnt FROM alerts WHERE user_id = v_user_id AND portfolio_id = v_p_growth AND alert_type = 'PORTFOLIO_VALUE_ABOVE';
        IF v_cnt = 0 THEN
            INSERT INTO alerts (user_id, company_id, portfolio_id, alert_type, threshold_value, is_active, created_at, triggered_at, message)
            VALUES (v_user_id, NULL, v_p_growth, 'PORTFOLIO_VALUE_ABOVE', 150000.00, 1, SYSTIMESTAMP - NUMTODSINTERVAL(15, 'DAY'), NULL, 'Milestone alert when Long Term Growth reaches ৳150,000');
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    seed_alerts('investor@sharesync.com');
    seed_alerts('tanvir@sharesync.com');
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 10. DEMO PORTFOLIO GOALS
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE seed_goals(p_user_email IN VARCHAR2) IS
        v_user_id NUMBER;
        v_p_growth NUMBER;
        v_p_div NUMBER;
        v_p_tech NUMBER;
        v_cnt NUMBER;
    BEGIN
        SELECT user_id INTO v_user_id FROM app_users WHERE LOWER(email) = LOWER(p_user_email);
        SELECT portfolio_id INTO v_p_growth FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Long Term Growth';
        SELECT portfolio_id INTO v_p_div FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Dividend Income Portfolio';
        SELECT portfolio_id INTO v_p_tech FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Tech & High-Growth Portfolio';

        -- Goal 1: Target Portfolio Value on Long Term Growth
        SELECT COUNT(*) INTO v_cnt FROM portfolio_goals WHERE user_id = v_user_id AND portfolio_id = v_p_growth AND goal_type = 'TARGET_PORTFOLIO_VALUE';
        IF v_cnt = 0 THEN
            INSERT INTO portfolio_goals (user_id, portfolio_id, goal_type, target_value, target_date, title, description, is_active, created_at)
            VALUES (v_user_id, v_p_growth, 'TARGET_PORTFOLIO_VALUE', 250000.00, TRUNC(SYSDATE + 365), 'Retirement Milestone 250k', 'Target portfolio valuation for long-term compounding fund', 1, SYSTIMESTAMP - NUMTODSINTERVAL(30, 'DAY'));
        END IF;

        -- Goal 2: Target Dividend Income on Dividend Portfolio
        SELECT COUNT(*) INTO v_cnt FROM portfolio_goals WHERE user_id = v_user_id AND portfolio_id = v_p_div AND goal_type = 'TARGET_DIVIDEND_INCOME';
        IF v_cnt = 0 THEN
            INSERT INTO portfolio_goals (user_id, portfolio_id, goal_type, target_value, target_date, title, description, is_active, created_at)
            VALUES (v_user_id, v_p_div, 'TARGET_DIVIDEND_INCOME', 25000.00, TRUNC(SYSDATE + 180), 'Annual Dividend Milestone', 'Generate ৳25,000 passive annual cash dividends', 1, SYSTIMESTAMP - NUMTODSINTERVAL(20, 'DAY'));
        END IF;

        -- Goal 3: Target Return on Tech & High-Growth
        SELECT COUNT(*) INTO v_cnt FROM portfolio_goals WHERE user_id = v_user_id AND portfolio_id = v_p_tech AND goal_type = 'TARGET_RETURN';
        IF v_cnt = 0 THEN
            INSERT INTO portfolio_goals (user_id, portfolio_id, goal_type, target_value, target_date, title, description, is_active, created_at)
            VALUES (v_user_id, v_p_tech, 'TARGET_RETURN', 15.00, TRUNC(SYSDATE + 120), '15% Alpha Outperformance', 'Achieve 15% net return in technology sector', 1, SYSTIMESTAMP - NUMTODSINTERVAL(15, 'DAY'));
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    seed_goals('investor@sharesync.com');
    seed_goals('tanvir@sharesync.com');
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 11. DEMO NOTIFICATIONS
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE seed_notifications(p_user_email IN VARCHAR2) IS
        v_user_id NUMBER;
        v_cnt NUMBER;
    BEGIN
        SELECT user_id INTO v_user_id FROM app_users WHERE LOWER(email) = LOWER(p_user_email);

        SELECT COUNT(*) INTO v_cnt FROM notifications WHERE user_id = v_user_id;
        IF v_cnt < 3 THEN
            INSERT INTO notifications (user_id, notification_type, title, message, is_read, created_at, related_entity_type, related_entity_id)
            VALUES (v_user_id, 'SYSTEM', 'Welcome to ShareSync', 'Your portfolio and real-time market tracker is fully configured and ready for trading.', 1, SYSTIMESTAMP - NUMTODSINTERVAL(30, 'DAY'), 'PORTFOLIO', NULL);

            INSERT INTO notifications (user_id, notification_type, title, message, is_read, created_at, related_entity_type, related_entity_id)
            VALUES (v_user_id, 'PRICE_ALERT', 'Price Alert: GP Reached Target', 'Grameenphone PLC (GP) crossed your price threshold of ৳375.00 at ৳380.00.', 0, SYSTIMESTAMP - NUMTODSINTERVAL(1, 'DAY'), 'COMPANY', 1);

            INSERT INTO notifications (user_id, notification_type, title, message, is_read, created_at, related_entity_type, related_entity_id)
            VALUES (v_user_id, 'DIVIDEND', 'Dividend Declared: SQURPHARMA', 'Square Pharmaceuticals PLC announced a dividend of ৳6.00 per share with record date approaching.', 0, SYSTIMESTAMP - NUMTODSINTERVAL(3, 'DAY'), 'COMPANY', 4);

            INSERT INTO notifications (user_id, notification_type, title, message, is_read, created_at, related_entity_type, related_entity_id)
            VALUES (v_user_id, 'GOAL_PROGRESS', 'Goal Milestone: 50% Achieved', 'You have achieved over 50% of your Retirement Milestone 250k goal.', 1, SYSTIMESTAMP - NUMTODSINTERVAL(7, 'DAY'), 'GOAL', 1);
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    seed_notifications('investor@sharesync.com');
    seed_notifications('tanvir@sharesync.com');
    COMMIT;
END;
/

-- ----------------------------------------------------------------------------
-- 12. DEMO PORTFOLIO SNAPSHOTS (Past 30 days daily progression)
-- ----------------------------------------------------------------------------
DECLARE
    PROCEDURE seed_snapshots(p_user_email IN VARCHAR2) IS
        v_user_id NUMBER;
        v_p_growth NUMBER;
        v_p_div NUMBER;
        v_p_tech NUMBER;
        v_cnt NUMBER;
        v_base_growth NUMBER := 135000;
        v_base_div    NUMBER := 160000;
        v_base_tech   NUMBER := 95000;
    BEGIN
        SELECT user_id INTO v_user_id FROM app_users WHERE LOWER(email) = LOWER(p_user_email);
        SELECT portfolio_id INTO v_p_growth FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Long Term Growth';
        SELECT portfolio_id INTO v_p_div FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Dividend Income Portfolio';
        SELECT portfolio_id INTO v_p_tech FROM portfolios WHERE user_id = v_user_id AND portfolio_name = 'Tech & High-Growth Portfolio';

        SELECT COUNT(*) INTO v_cnt FROM portfolio_snapshots WHERE portfolio_id = v_p_growth;
        IF v_cnt < 15 THEN
            FOR d IN REVERSE 1..30 LOOP
                INSERT INTO portfolio_snapshots (portfolio_id, snapshot_date, total_value)
                VALUES (v_p_growth, TRUNC(SYSDATE - d), ROUND(v_base_growth + (SIN(d * 0.3) * 3500) + ((30 - d) * 450), 2));

                INSERT INTO portfolio_snapshots (portfolio_id, snapshot_date, total_value)
                VALUES (v_p_div, TRUNC(SYSDATE - d), ROUND(v_base_div + (COS(d * 0.25) * 2200) + ((30 - d) * 380), 2));

                INSERT INTO portfolio_snapshots (portfolio_id, snapshot_date, total_value)
                VALUES (v_p_tech, TRUNC(SYSDATE - d), ROUND(v_base_tech + (SIN(d * 0.45) * 4200) + ((30 - d) * 520), 2));
            END LOOP;
            DBMS_OUTPUT.PUT_LINE('Seeded 30-day snapshots for portfolios of: ' || p_user_email);
        END IF;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN NULL;
    END;
BEGIN
    seed_snapshots('investor@sharesync.com');
    seed_snapshots('tanvir@sharesync.com');
    COMMIT;
END;
/

PROMPT ============================================================================
PROMPT ShareSync Demo Dataset Seeding Completed Successfully!
PROMPT ============================================================================
EXIT;
