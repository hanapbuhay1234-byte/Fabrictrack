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
- Kept the Order Preview column sticky while browsing the product catalog from tablet/desktop widths, offset below the sticky top navigation; the cart returns to normal flow on narrow screens where the layout stacks.
- Updated catalog cards to show current order quantity and provide clear add-another/stock-limit feedback; restored the selected customer and cart after checkout validation errors, and added a submitting state to prevent accidental double checkout.
- Added stock concurrency checks so concurrent sales cannot silently oversell inventory; stock conflicts return the cashier to the refreshed order with an explanatory message. Restock and adjustment conflicts also return actionable notifications.
- Added a receipt-style final order review modal before checkout, with an explicit confirmation step; inventory and current prices are still verified server-side when the confirmed sale is recorded.
- Kept checkout idle while the cashier backs out of the final review; only explicit confirmation starts the submitting state, and closing the review restores the checkout controls.
- Standardized completed sales and Sales History receipt links on a polished official receipt screen. Successful checkouts animate a brief print-preparation state before opening the browser print dialog; older receipts remain manual-print only.
- Rebuilt Sales History around server-side search, date and customer filters, stable 20-row paging, and filtered-period completed-sale/revenue summaries; voided records remain visible but are excluded from completed revenue.
- Added CSV export for all transactions matching the active filters, including void status, independently of the current page.
- Added an admin-only, reason-required audited sale-void action. Voids are retained in history, record who/when/why, restore inventory transactionally with compensating stock movements, and are protected against duplicate concurrent voiding. Voided receipts are clearly marked and cannot be printed as valid sales receipts.
- Excluded voided transactions from Dashboard, Sales Analytics, and customer purchase history so void reversals do not inflate sales counts, revenue, or product performance.
- Added migration `20261024082501_AddAuditedSaleVoiding` for the void audit fields. Apply it with `dotnet ef database update --project .\Project\Project.csproj` before using the void feature.

### Customers

- Replaced client-side full-list loading with server-side customer search and paging, matching on name, phone, and email.
- Added completed purchase counts and lifetime spend to customer rows and aggregate summary cards; voided sales are excluded.
- Moved customer creation and admin editing into list-page modals, retaining entered values and reopening the relevant modal when validation fails.
- Added clear, accessible list actions, direct phone/email links, and a cleaner profile view.
- Added completed purchase count and lifetime spend to customer profiles, with server-paged purchase history and direct receipt links.
- Preserved the existing access policy: authenticated staff can add customers, while editing and deleting remain admin-only.

### User management

- Added an admin-only, searchable, paged staff account management module with create, edit, password reset, deactivate, and reactivate actions; staff accounts are never hard-deleted.
- New staff receive a cryptographically generated one-time temporary password, assigned Staff role only, and are required to change that password before accessing the application.
- Added an authenticated password-change page and request gate for temporary-password accounts, plus immediate security-stamp validation so deactivated staff sessions are invalidated.
- Disabled the public registration endpoint and removed account-creation links; the application bootstrap continues to ensure the initial administrator and both roles exist and assigns Staff to existing users without a role.
- Raised password requirements to 10 characters with uppercase, lowercase, number, and symbol; staff password resets also force a password change.
- Added migration `20261024092541_AddStaffAccountLifecycle` for user activation and first-login password-change state.
- Updated `Project\Database\SETUP.md` to remind operators to apply current migrations before running the updated application.
- Preserved optional customer selection and server-side checkout pricing/stock validation.
- Added server-side quantity validation, duplicate-fabric aggregation, and cart restoration when validation fails.
- Files: `Project\Controllers\SalesController.cs`, `Project\Views\Sales\Create.cshtml`, `Project\wwwroot\css\site.css`

### User profiles

- Added a self-service profile page available to both administrators and staff for updating name, sign-in email, phone number, and profile photo.
- Require the current password when changing the sign-in email and keep Identity's normalized email/username values consistent; other profile edits do not require password re-entry.
- Added private profile-photo uploads with a 5 MB limit, JPG/PNG/WEBP extension validation, random filenames, and authenticated delivery outside the public static-files directory, following the existing inventory upload conventions.
- Added profile-photo removal and an initials fallback, with profile photo/name and profile/password links in the top-bar account menu.
- Reused the password-change page for self-service changes and generalized its wording while preserving the mandatory first-login password change.
- Added migration `20261024121958_AddUserProfilePhoto` for the stored profile-photo filename. Apply migrations before running the updated application.
- Files: `Project\Controllers\ProfileController.cs`, `Project\Models\ProfileViewModel.cs`, `Project\Views\Profile\Index.cshtml`, `Project\Views\Shared\_LoginPartial.cshtml`

### Audit logs

- Added an admin-only, searchable, paged audit history with filters for actor, action, record type, and date range, plus filtered CSV export.
- Recorded successful changes to products, categories, suppliers, customers, sales, stock movements, and user accounts, including staff actions as well as administrator actions.
- Captured actor, UTC timestamp, action, record identity, and changed-field summaries; stock events also include quantity change and the entered reason.
- User audit entries omit passwords, password hashes, security stamps, and other credential values. Audit entries are append-only through the application.
- Audit entries are saved atomically with the associated data changes, including Identity operations, by the application database context.
- Audit change detection compares actual before-and-after values, so Identity's full-user updates no longer create false reactivation or duplicate staff-creation entries. Category edits load the existing record so changed fields are captured accurately.
- Added migration `20261024132605_AddAuditLogs` for the centralized audit log table and indexes. Apply migrations before opening the Audit Logs page.
- Files: `Project\Models\AuditLog.cs`, `Project\Models\AuditLogViewModel.cs`, `Project\Controllers\AuditLogsController.cs`, `Project\Views\AuditLogs\Index.cshtml`

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

### Categories

- Added active and archived product counts to category listings, with links to the matching filtered Product Inventory view.
- Added category-level overview cards and improved the empty state, descriptions, and add/edit form guidance.
- Disabled deletion when active or archived products still reference a category, and added a server-side check to prevent linked categories from being deleted.
- Moved category creation and editing into compact list-screen modals, retaining validation feedback and reopening the relevant modal after invalid submissions.

### Suppliers

- Added active and archived product counts to supplier listings, with links to the matching filtered Product Inventory view.
- Added supplier-level overview cards and improved the empty state and contact information layout, including direct email links.
- Moved supplier creation and editing into a wider list-screen modal for supplier and contact fields, retaining validation feedback and reopening the relevant modal after invalid submissions.
- Disabled supplier deletion when active or archived products reference the supplier, with a server-side guard against deleting linked records.

### Fabrics — Product Inventory

- Reframed the Fabrics area as Product Inventory, with searchable product cards, SKU and color-variant details, category/supplier/unit/stock filters, and sort options.
- Added unique SKUs (auto-generated when omitted), color variants as separately stocked products, and Yard, Meter, Roll, and Piece units. Roll and Piece stock, reorder levels, sales, and restocks require whole quantities.
- Added a configurable reorder level per product. The dashboard, POS, inventory filters, and stock-alert page now use that threshold instead of a fixed quantity.
- Removed stock editing from the product details form. Added logged restocks with optional notes and manual stock adjustments with a required reason, plus a recent inventory movement history.
- Replaced permanent deletion with archive/restore so products and sale references remain intact.
- Added sale-item snapshots for SKU, name, color, category, and unit; sales receipts, dashboard product rankings, and analytics use the sale-time values.
- Made product images optional consistently and limited accepted uploads to JPG, PNG, and WEBP up to 3 MB.
- Added a data-preserving migration that backfills SKUs, reorder levels, active status, historical sale-item snapshots, and opening stock balances.
- Refined the Product Inventory UI with full-width responsive filters, readable product cards, clearer on-hand/reorder hierarchy, and consistent stock-status styling across screen sizes.
- Added dedicated Active and Archived inventory tabs with item counts, and rebuilt the add/edit forms and product details page into clear, responsive identity, pricing, stock, and activity sections.
- Added an immediate image preview when selecting a product image on the Add Product form.
- Refined inventory cards with a prominent selling price and unit, supplier/pattern context, clearer stock health colors, and an easy-to-find View Details action.
- Improved Stock Alerts with at-a-glance urgency counts, explicit out-of-stock/reorder priorities, supplier context, per-product replenishment suggestions to reach each reorder point, and guidance for products missing a reorder target. Alerts are ordered by urgency and relative coverage without combining unlike stock units.
- Preserved the Stock Alerts return path when opening product details, so the details back link returns to the alert list instead of the general inventory.
- Centered the Stock Alerts icon action buttons consistently within their button bounds.
- Updated setup instructions to apply the migration after importing the checked-in SQL database.
- Files: `Project\Controllers\FabricsController.cs`, `Project\Controllers\SalesController.cs`, `Project\Controllers\HomeController.cs`, `Project\Controllers\ReportsController.cs`, `Project\Models\Fabric.cs`, `Project\Models\Sale.cs`, `Project\Models\DashboardViewModel.cs`, `Project\Models\SalesReportViewModel.cs`, `Project\Data\ApplicationDbContext.cs`, `Project\Data\Migrations\20261024170000_AddProductInventory.cs`, `Project\Migrations\ApplicationDbContextModelSnapshot.cs`, related inventory/sales/dashboard/report views, `Project\Views\Shared\_Layout.cshtml`, `Project\Views\Shared\_RestockModal.cshtml`, `Project\wwwroot\js\site.js`, `Project\Database\SETUP.md`

### Validation performed

- Sales POS JavaScript syntax check passed.
- Sales Analytics filter JavaScript syntax check passed.
- `git diff --check` passed after the changes.
- .NET build completed successfully with 0 errors during Product Inventory implementation. Prior builds reported existing warnings elsewhere in the project.
