-- ============================================================
-- ShareSync - Seed Data: Benchmark Price History
-- File: 11_insert_benchmark_price_history.sql
-- Description: Benchmark index history (DSEX, DS30, DSES).
-- ============================================================

-- DSEX (DSE Broad Index) - Progression aligned with demo portfolio snapshots
INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', TRUNC(SYSDATE - 30), 6245.00, 6220.00, 6260.00, 6210.00, 65000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', TRUNC(SYSDATE - 25), 6320.00, 6250.00, 6335.00, 6240.00, 71000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', TRUNC(SYSDATE - 20), 6410.00, 6325.00, 6425.00, 6315.00, 83000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', TRUNC(SYSDATE - 15), 6520.00, 6415.00, 6535.00, 6405.00, 78000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', TRUNC(SYSDATE - 10), 6650.00, 6530.00, 6665.00, 6520.00, 92000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', TRUNC(SYSDATE - 5), 6780.00, 6660.00, 6795.00, 6650.00, 88000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', TRUNC(SYSDATE - 1), 6895.50, 6785.00, 6910.00, 6775.00, 95000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', DATE '2022-12-07', 6355.49, 6366.96, 6385.92, 6345.98, 67949350, 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', DATE '2022-12-06', 6431.46, 6480.30, 6481.38, 6421.59, 63640700, 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', DATE '2022-12-05', 6565.47, 6591.99, 6606.19, 6554.09, 82336110, 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSEX', 'DSE Broad Index (DSEX)', DATE '2022-12-01', 6996.06, 7049.15, 7092.31, 6988.66, 166417080, 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1');

-- DS30 (DSE 30 Blue Chip Index)
INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', TRUNC(SYSDATE - 30), 2180.00, 2170.00, 2190.00, 2165.00, 28000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', TRUNC(SYSDATE - 25), 2210.00, 2185.00, 2215.00, 2180.00, 31000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', TRUNC(SYSDATE - 20), 2240.00, 2215.00, 2248.00, 2210.00, 35000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', TRUNC(SYSDATE - 15), 2280.00, 2245.00, 2285.00, 2240.00, 33000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', TRUNC(SYSDATE - 10), 2310.00, 2285.00, 2320.00, 2280.00, 39000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', TRUNC(SYSDATE - 5), 2350.00, 2315.00, 2360.00, 2310.00, 42000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', TRUNC(SYSDATE - 1), 2385.20, 2355.00, 2390.00, 2350.00, 45000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', DATE '2022-12-07', 2284.14, 2293.77, 2300.25, 2282.18, 67949350, 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DS30', 'DSE 30 Index (DS30)', DATE '2022-12-01', 2606.79, 2626.66, 2640.80, 2602.91, 166417080, 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1');

-- DSES (DSE Shariah Index)
INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', TRUNC(SYSDATE - 30), 1390.00, 1380.00, 1395.00, 1375.00, 18000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', TRUNC(SYSDATE - 25), 1410.00, 1395.00, 1415.00, 1390.00, 21000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', TRUNC(SYSDATE - 20), 1430.00, 1415.00, 1435.00, 1410.00, 24000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', TRUNC(SYSDATE - 15), 1450.00, 1435.00, 1455.00, 1430.00, 22000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', TRUNC(SYSDATE - 10), 1475.00, 1455.00, 1480.00, 1450.00, 26000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', TRUNC(SYSDATE - 5), 1490.00, 1478.00, 1495.00, 1475.00, 29000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', TRUNC(SYSDATE - 1), 1510.80, 1492.00, 1515.00, 1490.00, 31000000, 'Dhaka Stock Exchange', 'DSE Daily Price Bulletin', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', DATE '2022-12-07', 1385.63, 1387.89, 1391.93, 1384.37, 67949350, 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1');

INSERT INTO benchmark_price_history (benchmark_code, benchmark_name, trading_date, close_value, open_value, high_value, low_value, volume, source, source_dataset, source_doi)
VALUES ('DSES', 'DSE Shariah Index (DSES)', DATE '2022-12-01', 1494.78, 1495.87, 1506.78, 1492.63, 166417080, 'Harvard Dataverse', 'Dhaka Stock Exchange Historical Data', '10.7910/DVN/XIFYT1');

COMMIT;
