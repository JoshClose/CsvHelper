# Benchmark data

- **`PackageAssets.csv`:** `NCsvPerf/TestData/PackageAssets.csv` from [joelverhagen/NCsvPerf](https://github.com/joelverhagen/NCsvPerf), commit `c7bff62e601eb1e2ce97c95a5b54ef94eff4b6ab`, unchanged (SHA-256 `5344e99ab70d3d68edcf41f3f787e4ef330eedae5a84cdb65144dba17485503d` with LF line ends, as committed there): 1,695 records of 25 fields, no header, no quoted fields. MIT License, copyright 2020 Joel Verhagen; its text is in `LICENSE-NCsvPerf.txt` here, as the license asks of copies.

`ExcelDataGenerator` repeats it to a million rows, as NCsvPerf does, for `ExcelPackageAssetsRead`, and lays the million rows out as a report for `ExcelReportRead`. The corpus tool (`tools/CsvHelper.Excel.Corpus`) makes `excel/packageassets-1695.xlsx` and `excel/packageassets-report-250.xlsx` from it in Excel, and CsvHelper.Excel tests check the generator against those files.
