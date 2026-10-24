# FabriTrack

FabriTrack is a fabric inventory and sales management web application built with ASP.NET Core and MySQL. It supports day-to-day product and stock management, point-of-sale checkout, customer records, staff accounts, and operational activity tracking.

## Features

- Product inventory with categories, suppliers, stock alerts, restocking, and stock adjustments.
- Point-of-sale order entry, printable receipts, sales history, and CSV exports.
- Staff accounts with administrator-managed access, temporary passwords, password-change requirements, and account deactivation/reactivation.
- Role-based workspaces: administrators manage the system; staff access day-to-day sales and their own activity.
- Audit Logs for changes to business records and accounts.
- Login Activity for successful and failed sign-ins, lockouts, blocked inactive-account sign-ins, and explicit sign-outs.
- User profile details and privately stored profile photos.

## Technology

- .NET 10 / ASP.NET Core MVC and Razor Pages
- ASP.NET Core Identity
- Entity Framework Core 8 and Pomelo MySQL provider
- MySQL 8.0

## Requirements

- .NET 10 SDK
- MySQL Server 8.0
- Visual Studio 2022 or later with the ASP.NET and web development workload, or VS Code
- MySQL Workbench (recommended for importing the sample database)

## Setup and run

See the detailed [database and setup guide](Project/Database/SETUP.md) for preparing MySQL, importing the supplied database, configuring the connection string, applying migrations, and troubleshooting.

From the repository root, restore, build, and run the application:

```powershell
dotnet restore
dotnet build
dotnet run --project .\Project\Project.csproj
```

Open the HTTPS URL printed in the console. You can also open `Project.slnx` in Visual Studio and start the application with **F5**.

### Configuration and credentials

Configure `ConnectionStrings:DefaultConnection` for your local MySQL server. Keep database passwords and other secrets out of source control; use .NET User Secrets or environment variables for local configuration. For example, PowerShell supports:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost;Port=3306;Database=ken;User=root;Password=YOUR_PASSWORD;"
```

Set the initial administrator credentials using secure configuration (`Seed:AdminEmail` and `Seed:AdminPassword`) before the first application start. Do not use sample or development credentials in a deployed environment. Public self-registration is disabled; administrators create staff accounts from User Management.

## User roles

- **Admin:** manages inventory, categories, suppliers, staff accounts, reports, and system-wide audit and login activity; can view all sales.
- **Staff:** processes sales, accesses permitted inventory and customer features, and views their own sales and activity.

Access restrictions are enforced by the application, not only by hiding navigation links.

## Database migrations

Back up any database with data you want to keep before applying migrations. From the repository root, install the matching EF command-line tool if needed and apply pending migrations:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.0
dotnet ef database update --project .\Project\Project.csproj
```

If `dotnet-ef` version 8.0.0 is already installed, skip the install command. Do not import the supplied SQL dump over a database containing data you need; importing can replace existing database contents.

## Useful links

- [Detailed setup guide](Project/Database/SETUP.md)
- [Project updates and implemented modules](PROJECT-UPDATES.md)
