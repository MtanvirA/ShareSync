# Historical Dataset

## Source Details
- **Dataset Name:** Dhaka Stock Exchange Historical Data (1999-2025)
- **Source:** Harvard Dataverse
- **DOI:** `10.7910/DVN/XIFYT1`

## Cleaning & Scope
- **Imported Scale:** ~46,000+ valid records for active demo companies.
- **Data Cleaning:** Invalid OHLC records (e.g. High < Low) were explicitly filtered out. Duplicate observations for the same company and date were purged.
- **Github Policy:** Due to repository limits, the raw 1.5M row dataset `.csv`/`.zip` is ignored in Git. Download it directly from Harvard Dataverse if you wish to run the `ShareSync.DataImporter` utility.
