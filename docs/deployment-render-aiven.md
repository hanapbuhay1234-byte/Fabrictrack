# Deploy FabriTrack with Render and Aiven

This guide deploys the ASP.NET Core app to a Render Free web service and uses Aiven's free MySQL service. It is intended for a demonstration, not production. Recheck the providers' current limits and terms before creating services:

- [Render free services](https://render.com/docs/free)
- [Render Docker deploys](https://render.com/docs/docker)
- [Render environment variables and secret files](https://render.com/docs/configure-environment-variables)
- [Aiven for MySQL free tier](https://aiven.io/docs/products/mysql/concepts/mysql-free-tier)
- [ImageKit plans](https://imagekit.io/plans/)
- [ImageKit upload API](https://imagekit.io/docs/api-reference/upload-file/upload-file)

Render Free services sleep when idle and have an ephemeral filesystem. Aiven's free MySQL service is limited to 1 GB of storage and can be powered off after inactivity. Do not use this setup for important or personal data.

Uploaded fabric images and profile photos are stored as private files in ImageKit. FabriTrack uploads files server-side and serves them through authenticated controller actions; the ImageKit private key is never sent to the browser. Review ImageKit's current free-plan storage and bandwidth limits before deployment.

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

## 3. Prepare ImageKit storage

1. In your ImageKit dashboard, locate the **ImageKit ID** and **Private Key** in the developer/API settings.
2. Keep the private key secret. Do not paste it into application code, browser JavaScript, or a committed configuration file.
3. FabriTrack uploads images as private files in the `profiles` or `fabrics` folders. The app signs short-lived retrieval URLs server-side and proxies image content through its authenticated routes.

## 4. Configure password recovery email

Password recovery sends one-time ASP.NET Core Identity reset links through SMTP. Links expire after one hour. Choose an SMTP email provider and obtain its host, port, username, password, and a verified sender email address. Use the provider's SMTP credentials rather than an account login password; store the SMTP password as a Render secret.

For typical SMTP submission, use port `587` with STARTTLS. FabriTrack requires TLS for SMTP connections. The reset URL uses the public HTTPS address of the deployed Render service, so configure that URL exactly as shown in the Render dashboard.

## 5. Configure the Render web service

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
   | `ImageStorage__Provider` | `ImageKit` |
   | `ImageStorage__ImageKitId` | ImageKit ID from your dashboard |
   | `ImageStorage__PrivateKey` | ImageKit Private Key (keep secret) |
   | `App__PublicBaseUrl` | Public HTTPS URL of this Render service, for example `https://fabrictrack.onrender.com` |
   | `Email__Smtp__Host` | SMTP host from your email provider |
   | `Email__Smtp__Port` | SMTP TLS port, usually `587` |
   | `Email__Smtp__Username` | SMTP username |
   | `Email__Smtp__Password` | SMTP password or provider-generated SMTP key (keep secret) |
   | `Email__Smtp__FromEmail` | Verified sender email address |
   | `Email__Smtp__FromName` | `FabriTrack` (optional) |

6. Under **Environment > Secret Files**, add the downloaded Aiven CA certificate with the filename `aiven-ca.pem`. Render makes it available to the container at `/etc/secrets/aiven-ca.pem`.
7. Save and deploy. Do not add these secret values to source control, a Dockerfile, or a committed `render.yaml`.

`ForwardedHeaders__Enabled=true` is for use behind Render's proxy. It makes the app process the proxy's forwarded scheme before HTTPS redirection and authentication cookies are configured. Only enable it when the app is reachable through the trusted hosting proxy.

## 6. Verify the deployment

1. Wait for Render's deploy to complete and open the provided HTTPS URL.
2. Check Render logs for database, migration, certificate, ImageKit configuration, or administrator-seed errors.
3. Sign in using the administrator credentials configured for this deployment and confirm the Admin role is present.
4. Upload a test fabric image and profile photo. Confirm both display, then restart or redeploy the Render service and confirm they still display.
5. On the sign-in page, use **Forgot password?** with a test account and verify that the email arrives and its link allows a password reset.
6. Use fictional data only. Existing image records still refer to local files from before this change; missing local images cannot be restored by deployment. Re-upload images that you still have.

## Important limitations

- A sleeping Render service has a cold start when a visitor returns.
- Aiven Free MySQL is small and may be powered off after inactivity; wake it from the Aiven console if needed.
- Render Free does not provide persistent disk storage. FabriTrack uses ImageKit for newly uploaded profile photos and fabric images when `ImageStorage__Provider=ImageKit` is configured.
- ImageKit files are private. Profile-photo access is protected by authenticated endpoints, and fabric-image endpoints require a signed-in Admin or Staff account.
- If ImageKit configuration is missing or invalid, the app fails startup rather than silently reverting to Render's ephemeral filesystem.
- Password recovery requires valid SMTP settings and `App__PublicBaseUrl`; without them, the reset form reports that recovery is not configured.
- The reset confirmation page intentionally does not reveal whether the submitted email belongs to an account.
- Check ImageKit storage and bandwidth usage and current plan limits regularly.
- Back up any data you need to keep, and test restoring it. Do not use the demonstration database for real customer, employee, or business information.
- Do not rely on the initial administrator credentials as a password-reset mechanism. The seed step only creates the account when it does not already exist; changing the configured password later does not reset an existing user's password.
- This is a demonstration deployment, not a production security or availability configuration.
