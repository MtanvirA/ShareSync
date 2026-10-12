-- ============================================================
-- ShareSync - Oracle Function: FN_CALCULATE_REAL_RETURN
-- File: 05_fn_calculate_real_return.sql
-- Description: LF-28: Calculates inflation-adjusted (real) return percentage
-- Formula: (((1 + Nominal Return / 100) / (1 + Inflation Rate / 100)) - 1) * 100
-- ============================================================

CREATE OR REPLACE FUNCTION fn_calculate_real_return (
    p_nominal_return IN NUMBER,
    p_inflation_rate IN NUMBER
) RETURN NUMBER
DETERMINISTIC
IS
BEGIN
    IF p_inflation_rate IS NULL OR p_nominal_return IS NULL THEN
        RETURN NULL;
    END IF;

    IF (1.0000 + (p_inflation_rate / 100.0000)) = 0 THEN
        RETURN 0.0000;
    END IF;

    RETURN ROUND((((1.0000 + (p_nominal_return / 100.0000)) / (1.0000 + (p_inflation_rate / 100.0000))) - 1.0000) * 100.0000, 4);
END fn_calculate_real_return;
/
