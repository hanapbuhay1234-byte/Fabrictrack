# FabriTrack Project Updates

This file tracks the features and project changes implemented during the current improvement work. It is updated as additional modules are reviewed and changed.

## 2026-10-24

### Setup guide

- Corrected the SQL dump path to `Project\Database\ken_backup.sql`.
- Aligned the documented database name with the checked-in dump and application configuration (`ken`).
- Corrected the repository-root run command to `dotnet run --project .\Project\Project.csproj`.
- Documented why `dotnet restore` and `dotnet build` work from the solution root while `dotnet run` needs the project path.
- Updated the documented project and SQL file paths.
- File: `Project\Database\SETUP.md`

### Sales — POS order entry

- Replaced the fabric dropdown-and-table order workflow with a product catalog and cart.
- Added searchable fabric cards with image fallback, color/pattern, category, price, and available stock.
- Added category filtering and visible low-stock/out-of-stock indicators.
- Added cart controls for decimal quantities, using 0.5-unit increments for add/remove actions.
- Repeated additions of a fabric combine into the existing cart line.
- Added per-item totals, available-stock context, item count, and estimated order total.
- Made the layout responsive, with the cart beside the catalog on desktop and below it on narrow screens.
- Preserved optional customer selection and server-side checkout pricing/stock validation.
- Added server-side quantity validation, duplicate-fabric aggregation, and cart restoration when validation fails.
- Files: `Project\Controllers\SalesController.cs`, `Project\Views\Sales\Create.cshtml`, `Project\wwwroot\css\site.css`

### Dashboard — owner overview

- Replaced lifetime counts and the single-day sales figure with selectable 7-, 30-, and 90-day sales performance.
- Added sales revenue, order count, and average order value, each with comparison against the previous equal-length period.
- Added a daily revenue trend and a top-selling fabrics list for the selected period.
- Added inventory attention counts and links to affected fabrics.
- Defined dashboard stock alerts as out of stock at zero units and low stock at positive quantities of 5 units or fewer.
- Added a recent sales list with links to individual sale details.
- Added responsive dashboard styling and empty states.
- Files: `Project\Controllers\HomeController.cs`, `Project\Models\DashboardViewModel.cs`, `Project\Views\Home\Index.cshtml`, `Project\wwwroot\css\site.css`

### Validation summary display

- Hid empty ASP.NET validation summaries so they no longer leave blank warning/error bars on pages.
- Actual validation messages remain visible.
- This applies to the Reports/Sales Analytics and New Sale validation summaries.
- File: `Project\wwwroot\css\site.css`

### Reports — Sales Analytics

- Replaced the generic Reports label with Sales Analytics in the page title and navigation.
- Added comparisons for sales revenue, order count, and average order value against the immediately preceding period of equal length.
- Added a revenue trend chart, grouped by day, week, or month based on the selected period length.
- Added sales breakdowns by category and fabric.
- Kept fabric quantities grouped by fabric and unit so yards and meters are not combined or directly ranked as equivalent.
- Replaced the transaction table in analytics with a link to Sales History for individual transaction review.
- Expanded CSV export to include period metrics, trend data, category performance, and fabric results.
- Retained guarded date filters, including Monday–Sunday weeks and validation for invalid or incomplete custom ranges.
- Files: `Project\Controllers\ReportsController.cs`, `Project\Models\SalesReportViewModel.cs`, `Project\Views\Reports\Index.cshtml`, `Project\Views\Shared\_Layout.cshtml`, `Project\wwwroot\css\site.css`

### Validation performed

- Sales POS JavaScript syntax check passed.
- Sales Analytics filter JavaScript syntax check passed.
- `git diff --check` passed after the changes.
- .NET builds completed successfully with 0 errors. The latest builds reported 20 warnings elsewhere in the project.
