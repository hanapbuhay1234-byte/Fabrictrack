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

1. Go to **File → Open SQL Script** and choose `Database/fabritrack.sql`.
2. If the script does **not** contain a `CREATE DATABASE` line, run this first:
   ```sql
   CREATE DATABASE fabritrack CHARACTER SET utf8mb4;
   USE fabritrack;
   ```
3. Click the **lightning bolt** (Execute) and wait until it finishes with no red errors.

### Option B: Data Import

1. Go to **Server → Data Import → Import from Self-Contained File** and select the `.sql` file.
2. Under *Default Target Schema*, pick or create `fabritrack`.
3. Click **Start Import**.

After importing, right-click **Schemas → Refresh All**. You should see the tables (Fabrics, Categories, Suppliers, Customers, Sales, etc.).

---

## 4. Set your connection string

Open `appsettings.json` (or `appsettings.Development.json`) and use **your own** MySQL password:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Port=3306;Database=fabritrack;User=root;Password=YOUR_PASSWORD;"
}
```

- The `Database=` name must match the database you created in Step 3.
- Each member uses their own password. **Do not commit your password.**

---

## 5. Run the system

```bash
dotnet restore
dotnet build
dotnet run
```

Or open the `.sln` file in Visual Studio and press **F5**. Open the URL shown in the console (for example `https://localhost:xxxx`).

> **Do NOT run `dotnet ef database update`** if you already imported the SQL file. The tables already exist, so it will fail with "table already exists." Only use it on an empty database, without importing the file.

---

## 6. Log in

Use the default account that came with the data:

- **Email:** `______________`
- **Password:** `______________`

---

## Troubleshooting

| Problem | Fix |
|---|---|
| `Access denied for user 'root'` | The password in your connection string is wrong. |
| `Unable to connect to any of the specified MySQL hosts` | MySQL is not running. Open **Services** (Windows), start **MySQL80**, and check that the port is 3306. |
| `Unknown database 'fabritrack'` | The database name does not match, or Step 3 was not done. |
| `Table already exists` during import | Run `DROP DATABASE fabritrack;`, recreate it, and import again. |
| `Unknown collation: utf8mb4_0900_ai_ci` | You have an older MySQL or MariaDB. Install MySQL 8.0. |
| Login fails or pages show no data | The import did not finish. Check the Workbench output for errors and re-import. |

---

## When the database changes later

1. The person who changed the database re-exports the SQL file (**Server → Data Export**, choose *Dump Structure and Data*, export as a self-contained file).
2. Replace `Database/fabritrack.sql`, then commit and push.
3. Everyone else runs `git pull`, drops their local `fabritrack` database, and imports the new file again.

> Re-importing wipes your local data, so don't use it if you have test data you want to keep.
