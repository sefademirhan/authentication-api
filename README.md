# Authentication API

[![.NET build](https://github.com/sefademirhan/authentication-api/actions/workflows/dotnet.yml/badge.svg)](https://github.com/sefademirhan/authentication-api/actions/workflows/dotnet.yml)

An educational **ASP.NET Core 8 authentication REST API** with **JWT**, **BCrypt password hashes**, **Entity Framework Core**, and **SQL Server**.

## Features
- Register a user
- Sign in and receive a 30-minute JWT access token
- Retrieve the authenticated user's basic profile
- Hash passwords with BCrypt; never store plaintext passwords
- Prevent duplicate email accounts

## Requirements
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server Express LocalDB or another SQL Server instance
- Windows PowerShell

## 1. Configure JWT secrets locally
Extract the ZIP and open PowerShell in the folder containing this README. **Do not put private keys in GitHub.** The project already includes a `UserSecretsId`.

The following commands work in Windows PowerShell, including 5.1:

```powershell
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $key --project src/AuthenticationApi/AuthenticationApi.csproj
```

The issuer and audience are already specified in `appsettings.json`. You can override them via User Secrets. If the JWT key is missing or too short, the application intentionally refuses to start.

## 2. Create database and run
```powershell
dotnet restore src/AuthenticationApi/AuthenticationApi.csproj
dotnet tool install --global dotnet-ef --version 8.0.11
dotnet ef migrations add InitialCreate --project src/AuthenticationApi/AuthenticationApi.csproj
dotnet ef database update --project src/AuthenticationApi/AuthenticationApi.csproj
dotnet run --project src/AuthenticationApi/AuthenticationApi.csproj --launch-profile http
```

Open **http://localhost:5072/swagger**. If `dotnet-ef` already exists, update it with `dotnet tool update --global dotnet-ef --version 8.0.11`.

If your SQL Server is not LocalDB, update `AuthDatabase` in `src/AuthenticationApi/appsettings.json`.

## Endpoints
| Method | URL | Description |
| --- | --- | --- |
| POST | `/api/auth/register` | Create account |
| POST | `/api/auth/login` | Receive a JWT token |
| GET | `/api/auth/me` | Authenticated profile |

**Registration**
```json
{
  "email": "demo@example.com",
  "password": "change-this-password-123"
}
```

Use the same email and password to log in. For `/api/auth/me`, include the returned token in the `Authorization: Bearer <token>` header. For this local learning project, the launch profile uses HTTP; use HTTPS for real network deployments and never send real credentials over untrusted HTTP connections.

## Security roadmap
- [x] Password hashing
- [x] Signed and validated JWT tokens
- [x] Protected user endpoint
- [x] GitHub Actions build checks
- [ ] Rate limiting and account lockout
- [ ] Email verification, password reset and refresh tokens
- [ ] Security and integration tests

> **Not production-ready.** This starter intentionally omits several protections needed for real user accounts.
