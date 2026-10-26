# Deploy FabriTrack with Render and Aiven

This guide deploys the ASP.NET Core app to a Render Free web service and uses Aiven's free MySQL service. It is intended for a demonstration, not production. Recheck the providers' current limits and terms before creating services:

- [Render free services](https://render.com/docs/free)
- [Render Docker deploys](https://render.com/docs/docker)
- [Render environment variables and secret files](https://render.com/docs/configure-environment-variables)
- [Aiven for MySQL free tier](https://aiven.io/docs/products/mysql/concepts/mysql-free-tier)

Render Free services sleep when idle and have an ephemeral filesystem. Aiven's free MySQL service is limited to 1 GB of storage and can be powered off after inactivity. Do not use this setup for important or personal data.

## 1. Create a clean demo database

1. Create a free MySQL service in Aiven and wait until it is running.
2. Record the service host, port, database name, username, and password from the Aiven connection information. Keep the password private.
3. Download the Aiven CA certificate for TLS verification.
4. Do not import `Project/Database/ken_backup.sql` unless you have inspected its contents and confirmed that its data is safe to publish. For a clean demonstration database, apply the project's migrations in the next section.

The app uses MySQL through Pomelo and MySqlConnector. The connection string should enable certificate and hostname verification:

```text
Server=<AIVEN_HOST>;Port=<AIVEN_PORT>;Database=<AIVEN_DATABASE>;User ID=<AIVEN_USER>;Password=<AIVEN_PASSWORD>;SslMode=VerifyFull;SslCa=<CA_CERTIFICATE_PATH>;
```

Configure MySQL Workbench to use the downloaded CA certificate when connecting to Aiven. In Render, use `/etc/secrets/aiven-ca.pem` as the certificate path.

## 2. Apply the schema migrations

Run the migrations once, before the first web-service deployment. This keeps schema changes out of concurrent web-service startup and avoids relying on a public request to initialize the database.

From the repository root in PowerShell, create an `artifacts` folder and install the EF tool if it is not already available:

```powershell
New-Item -ItemType Directory -Force .\artifacts | Out-Null
dotnet tool install --global dotnet-ef --version 8.0.0
```

If that tool version is already installed, skip the install command. Generate a SQL script for all migrations:

```powershell
dotnet ef migrations script --configuration Release --project .\Project\Project.csproj --output .\artifacts\demo-migrations.sql
```

The design-time context factory uses the checked-in MySQL 8 configuration to generate the script without connecting to a database or starting application seeding. Open `artifacts/demo-migrations.sql` in MySQL Workbench, connect to Aiven using TLS and the downloaded CA certificate, select the target database, and execute the script. Confirm it completes successfully before deploying the app.

The project uses EF Core 8, so use the matching `dotnet-ef` 8.x tool. The migration script applies the schema and category seed data. On first app startup, FabriTrack creates the Admin and Staff roles and the configured administrator account.

## 3. Create the Render web service

1. Push the project to a Git repository accessible to Render.
2. In Render, choose **New > Web Service**, connect the repository, and select the branch to deploy.
3. Select **Docker** as the runtime. Use the repository root as the Docker build context and `Dockerfile` as the Dockerfile path. Leave the Docker command empty.
4. Select the **Free** instance type for a demonstration. Render's public port is supplied through `PORT`; the Docker entrypoint binds Kestrel to that port.
5. Add the following environment variables in the service's **Environment** settings:

   | Key | Value |
   |---|---|
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__DefaultConnection` | Aiven connection string with `SslMode=VerifyFull;SslCa=/etc/secrets/aiven-ca.pem;` |
   | `Seed__AdminEmail` | A new demo administrator email address |
   | `Seed__AdminPassword` | A unique, strong password that meets the app's Identity password rules |
   | `ForwardedHeaders__Enabled` | `true` |

6. Under **Environment > Secret Files**, add the downloaded Aiven CA certificate with the filename `aiven-ca.pem`. Render makes it available to the container at `/etc/secrets/aiven-ca.pem`.
7. Save and deploy. Do not add these secret values to source control, a Dockerfile, or a committed `render.yaml`.

`ForwardedHeaders__Enabled=true` is for use behind Render's proxy. It makes the app process the proxy's forwarded scheme before HTTPS redirection and authentication cookies are configured. Only enable it when the app is reachable through the trusted hosting proxy.

## 4. Verify the deployment

1. Wait for Render's deploy to complete and open the provided HTTPS URL.
2. Check Render logs for database, migration, certificate, or administrator-seed errors.
3. Sign in using the administrator credentials configured for this deployment and confirm the Admin role is present.
4. Test the main demo workflows and check that the database records persist after an app redeploy.
5. Avoid uploading profile photos on Render Free: the app stores them on local disk, and Render's free filesystem is ephemeral. Uploaded photos can disappear after a restart, spin-down, or redeploy. Use fictional data only.

## Important limitations

- A sleeping Render service has a cold start when a visitor returns.
- Aiven Free MySQL is small and may be powered off after inactivity; wake it from the Aiven console if needed.
- Render Free does not provide persistent disk storage for uploads. The app's `App_Data/ProfilePhotos` and any local uploads are not durable there.
- Back up any data you need to keep, and test restoring it. Do not use the demonstration database for real customer, employee, or business information.
- Do not rely on the initial administrator credentials as a password-reset mechanism. The seed step only creates the account when it does not already exist; changing the configured password later does not reset an existing user's password.
- This is a demonstration deployment, not a production security or availability configuration.
