# FabriTrack – Setup Guide

Follow these steps to open the system and the database on your own PC.

---

## 1. Install the tools

- **Git**
- **.NET SDK** (same version as the project, .NET 10). Check with `dotnet --version`
- **Visual Studio 2022 or later** (with the *ASP.NET and web development* workload) or VS Code
- **MySQL Server 8.0** and **MySQL Workbench**

> Use MySQL **8.0**, not 5.7. The SQL file may use `utf8mb4_0900_ai_ci`, which 5.7 does not support.

Remember the **root password** you set when installing MySQL.

---

## 2. Clone the project

```bash
git clone <repo-url>
cd <project-folder>
```

---

## 3. Create the database

Open **MySQL Workbench** and connect to `localhost` (user `root`). Then use one of the two options below.

### Option A: Run the script

1. Go to **File → Open SQL Script** and choose `Project\Database\ken_backup.sql`.
2. The provided script creates and selects the `ken` database. If you are using a different SQL file without a `CREATE DATABASE` line, run this first:
   ```sql
   CREATE DATABASE ken CHARACTER SET utf8mb4;
   USE ken;
   ```
3. Click the **lightning bolt** (Execute) and wait until it finishes with no red errors.

### Option B: Data Import

1. Go to **Server → Data Import → Import from Self-Contained File** and select the `.sql` file.
2. The provided script creates the `ken` schema. For a different SQL file, select or create the database specified by that file.
3. Click **Start Import**.

After importing, right-click **Schemas → Refresh All**. You should see the tables (Fabrics, Categories, Suppliers, Customers, Sales, etc.).

---

## 4. Set your connection string

Open `Project\appsettings.json` and set **your own** MySQL password:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=ken;User=root;Password=YOUR_PASSWORD;"
}
```

- The checked-in SQL dump and connection string use the `ken` database. The `Database=` name must match the database you imported in Step 3.
- Each member uses their own password. **Do not commit your password.**

---

## 5. Apply application updates to the imported database

The checked-in SQL dump contains the existing schema and migration history. After importing it, back up the database, then apply any newer application migrations. The current application includes migrations for Product Inventory, audited sale voiding, staff account activation and first-login password changes, user profile photos, centralized audit logs, saved sale-processor names, and Login Activity. Applying migrations is required before starting the updated application:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.0
dotnet ef database update --project .\Project\Project.csproj
```

If `dotnet-ef` is already installed, skip the install command. These updates preserve existing records and do not drop or recreate the imported tables. Login Activity keeps the most recent 90 days of recorded events. Staff accounts are now created by an administrator; public self-registration is disabled. Profile photos are stored under `Project\App_Data\ProfilePhotos`, outside the public web folder. Ensure the application service account can write to that folder and include it in your database/file backups if you want to preserve uploaded photos.

Do not run the migration command before importing the provided SQL dump. If you are working with a database that contains valuable data, always take a backup before applying schema changes.

---

## 6. Run the system

```bash
dotnet restore
dotnet build
dotnet run --project .\Project\Project.csproj
```

Restore and build work from the repository root because it contains `Project.slnx`. `dotnet run` needs the app's project path, so use the command above. Alternatively, change directory to `Project` and run `dotnet run` there.

Or open `Project.slnx` in Visual Studio and press **F5**. Open the URL shown in the console (for example `https://localhost:xxxx`).

---

## 7. Log in

Use the default account that came with the data:

- **Email:** `______________`
- **Password:** `______________`

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `Access denied for user 'root'` | The password in your connection string is wrong. |
| `Unable to connect to any of the specified MySQL hosts` | MySQL is not running. Open **Services** (Windows), start **MySQL80**, and check that the port is 3306. |
| `Unknown database 'ken'` | The database name does not match, or Step 3 was not done. |
| `Table already exists` during import | If you are okay losing its data, drop and recreate the `ken` database, then import again. |
| `Unknown collation: utf8mb4_0900_ai_ci` | You have an older MySQL or MariaDB. Install MySQL 8.0. |
| Login fails or pages show no data | The import did not finish. Check the Workbench output for errors and re-import. |

---

## When the database changes later

1. The person who changed the database re-exports the SQL file (**Server → Data Export**, choose *Dump Structure and Data*, export as a self-contained file).
2. Replace `Project\Database\ken_backup.sql`, then commit and push.
3. Everyone else runs `git pull`, backs up or recreates their local database as needed, and imports the updated SQL file.

> Re-importing wipes your local data, so don't use it if you have test data you want to keep.
