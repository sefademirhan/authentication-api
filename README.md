# Authentication API

[![.NET build and tests](https://github.com/sefademirhan/authentication-api/actions/workflows/dotnet.yml/badge.svg)](https://github.com/sefademirhan/authentication-api/actions/workflows/dotnet.yml)

A learning-focused **ASP.NET Core 8** authentication REST API with SQL Server, BCrypt password hashing, **JWT access tokens**, **rotating refresh tokens**, and IP-based rate limiting.

## Features

- Register a user with an email and 12–128 character password
- Store only BCrypt password hashes
- Sign in with email/password; receive a 30-minute JWT access token and 7-day refresh token
- Renew the access token with `POST /api/auth/refresh`; the previous refresh token immediately becomes unusable
- Revoke a refresh token with `POST /api/auth/logout`
- View a protected profile via `GET /api/auth/me`
- Validate JWT signing key, issuer, audience and expiration
- Limit registration, login and refresh requests to **6 attempts per minute per client IP** (in memory)
- Store SHA-256 hashes of refresh tokens, not raw credentials
- Swagger Bearer authorization control on the protected endpoint
- Automated API integration tests against SQLite in-memory databases

## Windows setup

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), SQL Server Express LocalDB.

Extract the ZIP and open PowerShell in the directory with `README.md`.

### 1. Configure a JWT signing key (once per computer)

```powershell
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $key --project src/AuthenticationApi/AuthenticationApi.csproj
```

The value is stored outside the source repository using .NET User Secrets. **Never commit keys, access tokens or real passwords.** `Jwt:Issuer` and `Jwt:Audience` are configured in `appsettings.json`.

### 2. Point to your SQL Server LocalDB instance

The default SQL connection is `(localdb)\\MSSQLLocalDB`. If you are using the working `StockDemo` instance, use:

```powershell
$env:ConnectionStrings__AuthDatabase = 'Server=(localdb)\StockDemo;Database=AuthenticationApiDb;Integrated Security=True;TrustServerCertificate=True;'
```

Keep the **same** PowerShell window open for the following commands.

### 3. Apply database migrations and start the API

```powershell
dotnet restore src/AuthenticationApi/AuthenticationApi.csproj
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/AuthenticationApi/AuthenticationApi.csproj
dotnet run --project src/AuthenticationApi/AuthenticationApi.csproj --launch-profile http
```

The migrations `InitialCreate` and `AddRefreshTokens` are **already committed**. Do **not** generate `InitialCreate` again. `database update` only applies pending changes, keeping your existing accounts.

Open **http://localhost:5072/swagger** while the API is running.

## Demo workflow

1. `POST /api/auth/register` with a test email and password.
2. `POST /api/auth/login` with the same credentials. Copy the `accessToken` for protected API requests.
3. In Swagger, click **Authorize**, paste the access token, and use `GET /api/auth/me`.
4. `POST /api/auth/refresh` with the `refreshToken` to issue new tokens.
5. `POST /api/auth/logout` with the **latest** refresh token to revoke further renewals.

Example request body for registration:

```json
{
  "email": "demo@example.com",
  "password": "ExamplePassword12345!"
}
```

Refresh and logout both accept:

```json
{ "refreshToken": "paste-the-current-refresh-token-here" }
```

### Endpoints

| Method | Path | Description |
| --- | --- | --- |
| POST | `/api/auth/register` | Create a user |
| POST | `/api/auth/login` | Receive access and refresh tokens |
| POST | `/api/auth/refresh` | Rotate refresh token and renew JWT |
| POST | `/api/auth/logout` | Revoke a refresh token |
| GET | `/api/auth/me` | Return authenticated profile (Bearer JWT) |

## Automated tests

```powershell
dotnet test tests/AuthenticationApi.Tests/AuthenticationApi.Tests.csproj
```

Integration tests cover registration, login, protected routes, refresh rotation, logout and invalid signup. Tests use a disposable SQLite database in memory (your SQL Server data is untouched).

## Security notes and remaining production requirements

This is an **educational API, not a ready-to-deploy identity provider**. Rate limits are kept in process memory, so they reset on application restart and are not distributed. Access JWTs are valid for up to 30 minutes even after refresh-token logout; JWT revocation would require additional infrastructure or shorter access token lifetimes. Email verification, password reset via email, multi-factor authentication, security logs, centralized rate limits, device/session management, backup procedures and production monitoring require additional services and operational setup.

The local demo uses HTTP on localhost; **production deployments require HTTPS** and careful management of keys and tokens.
