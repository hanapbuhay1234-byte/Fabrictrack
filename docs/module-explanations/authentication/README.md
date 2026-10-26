# Authentication and password-change modules

This folder explains the backend authentication flow from signing in through changing a password. Each module page contains a complete, annotated teaching copy of the relevant backend C# file, followed by an explanation of how the file participates in the flow.

> The code blocks are documentation copies with explanatory comments added. They are not compiled source files. The live implementation remains under `Project/Areas/Identity`, `Project/Models`, `Project/Services`, and `Project/Program.cs`. If behavior changes, update these explanations to match the live code.

## Module guides

1. [Login](login.md) — validates the request, checks whether the account is active, delegates password verification and lockout handling to ASP.NET Identity, and records activity.
2. [Authenticator sign-in](two-factor-sign-in.md) — completes a pending two-factor sign-in and records the final success or failure.
3. [Logout](logout.md) — records an explicit sign-out and clears the Identity authentication session.
4. [Change password](change-password.md) — validates and updates the signed-in user's password, clears the first-login requirement, and refreshes the session.
5. [Supporting models and services](supporting-components.md) — explains user fields, the forced-password-change claim, and Login Activity persistence.

## Request flow at a glance

```text
Login form
  -> LoginModel.OnPostAsync
      -> reject inactive user before password verification
      -> PasswordSignInAsync (Identity checks password and lockout)
      -> if two-factor is required: LoginWith2faModel
      -> if sign-in succeeds and password change is required: ChangePasswordModel
  -> ChangePasswordModel.OnPostAsync
      -> change password and clear RequirePasswordChange in a database transaction
      -> refresh authentication cookie
      -> redirect to the dashboard

Authenticated profile menu
  -> LogoutModel.OnPost
      -> record explicit sign-out
      -> clear the authentication cookie
```

## Important behavior

- Login, two-factor, logout, and password change use ASP.NET Core Identity's `SignInManager` and `UserManager`; the application does not implement password hashing itself.
- The password policy is configured in [`Program.cs`](../../../Project/Program.cs). Current code requires at least 10 characters, uppercase and lowercase letters, a digit, and a non-alphanumeric character.
- Password lockout is also managed by Identity. The login page enables lockout on failed password attempts; Identity's configured/default lockout options determine the threshold and duration.
- Inactive accounts are rejected before password verification and receive the same generic login error as an invalid password.
- Accounts marked `RequirePasswordChange` are redirected to the change-password page by both the login flow and an application-wide request gate.
- Login Activity is separate from business Audit Logs. It must never contain passwords or authenticator codes.
