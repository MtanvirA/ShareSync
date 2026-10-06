# SHARESYNC RELEASE CANDIDATE (RC-1)

## 1. Build Status
- **Backend (.NET 8):** PASS 
- **Frontend:** PASS (Vanilla JavaScript, No build step required)
- **Data Importer (.NET 10):** PASS

## 2. Test Status
- **Unit/Integration Tests:** 350 / 350 PASSED
- **Database Connectivity:** PASSED
- **Investment Intelligence Mathematics:** PASSED

## 3. Database Status
- **Platform:** Oracle 26ai (Free/XE)
- **Schema:** Intact, with complete referential integrity (PK/FK/Constraints).
- **Data Integrity:** Invalid OHLC constraint violations and Duplicate Company/Date observations have been cleanly purged.
- **Data Provenance:** Updated missing metadata sources to `Harvard Dataverse`.

## 4. Frontend Status
- **Architecture:** Pure HTML5 / CSS3 / Bootstrap / Vanilla JS / Chart.js
- **Console Errors:** None detected. Graceful error handling implemented.
- **Responsiveness:** Validated on Desktop and Mobile viewport sizes.

## 5. API Status
- **Routing:** Complete standard RESTful patterns.
- **Security:** JWT Authentication and BCrypt password hashing active. Exception details are obscured from the frontend.
- **CORS:** Secure origin policies maintained where appropriate.

## 6. Environment Requirements
- **Database:** Oracle Database Express Edition or 26ai running locally on port 1521, service `FREEPDB1` or `XE`.
- **Runtime:** .NET 8 SDK or .NET 10 SDK (with Roll-Forward configured).

## 7. Known Limitations
- The Investment Intelligence scores evaluate strictly historical mathematically derived metrics. They do *not* integrate real-time or forecasted ML modeling (per design scope constraints).
- Large time-series data visualization (e.g. 1000+ points on mobile) may feel heavy, but the query executes quickly due to `COMPANY_PRICE_HISTORY` bulk-load indexing strategies.

## 8. Startup Instructions
1. `cd src/ShareSync.Web`
2. `dotnet run`
3. Navigate to `http://localhost:5000`.

## 9. Demo Instructions
Please refer to `docs/DEMO_GUIDE.md` for a comprehensive step-by-step Viva script and expected queries.

**STATUS: PROJECT FROZEN. READY FOR UNIVERSITY SUBMISSION.**
