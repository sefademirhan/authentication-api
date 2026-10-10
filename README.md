# Authentication API

A learning-focused authentication REST API built with C#, ASP.NET Core 8, Entity Framework Core, SQL Server, BCrypt password hashing, and JWT bearer tokens.

## Features
- User registration and login
- Passwords stored as BCrypt hashes, never plain text
- JWT access tokens with issuer, audience, signature, and lifetime validation
- Protected `GET /api/auth/me` endpoint
- Duplicate email prevention

## Requirements
- .NET 8 SDK
- SQL Server LocalDB or another SQL Server instance

## Configure local JWT secrets
Do not commit signing keys or credentials. From the repository root, set a development key using .NET User Secrets:

```powershell
dotnet user-secrets init --project src/AuthenticationApi/AuthenticationApi.csproj
$bytes = New-Object byte[] 48
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $key --project src/AuthenticationApi/AuthenticationApi.csproj
dotnet user-secrets set "Jwt:Issuer" "AuthenticationApi" --project src/AuthenticationApi/AuthenticationApi.csproj
dotnet user-secrets set "Jwt:Audience" "AuthenticationApiClient" --project src/AuthenticationApi/AuthenticationApi.csproj
```

## Create database and run
```powershell
dotnet restore src/AuthenticationApi/AuthenticationApi.csproj
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/AuthenticationApi/AuthenticationApi.csproj
dotnet ef database update --project src/AuthenticationApi/AuthenticationApi.csproj
dotnet run --project src/AuthenticationApi/AuthenticationApi.csproj
```

Open the Swagger URL printed in the terminal. Update the `AuthDatabase` connection string if using another SQL Server instance.

## Endpoints
- `POST /api/auth/register` — JSON: `{ "email": "person@example.com", "password": "a-long-password-here" }`
- `POST /api/auth/login` — returns a JWT access token
- `GET /api/auth/me` — requires `Authorization: Bearer <token>`

## Security notes
This is a portfolio starter, not production-ready authentication. Before production use, add rate limiting, email verification, password reset, refresh-token rotation/revocation, audit logging, and security tests. Keep JWT keys and database credentials out of source control.
