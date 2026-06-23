# Vault - scaffold the .NET solution, then drop the seeded source over it.
# Run from the repo root in PowerShell.
$ErrorActionPreference = "Stop"

# 1. Solution + projects (templates create default Program.cs / Class1.cs)
dotnet new sln -n Vault

dotnet new webapi   -n Vault.Api            -o src/Vault.Api            --use-minimal-apis
dotnet new classlib -n Vault.Domain         -o src/Vault.Domain
dotnet new classlib -n Vault.Contracts      -o src/Vault.Contracts
dotnet new classlib -n Vault.Infrastructure -o src/Vault.Infrastructure
dotnet new classlib -n Vault.Client         -o src/Vault.Client
dotnet new xunit    -n Vault.Tests          -o tests/Vault.Tests

dotnet sln add src/Vault.Api src/Vault.Domain src/Vault.Contracts src/Vault.Infrastructure src/Vault.Client tests/Vault.Tests

# 2. References
dotnet add src/Vault.Api            reference src/Vault.Domain src/Vault.Contracts src/Vault.Infrastructure
dotnet add src/Vault.Infrastructure reference src/Vault.Domain
dotnet add src/Vault.Client         reference src/Vault.Contracts
dotnet add tests/Vault.Tests        reference src/Vault.Domain src/Vault.Infrastructure

# 3. Packages
dotnet add src/Vault.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/Vault.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/Vault.Infrastructure package Microsoft.Extensions.Options
dotnet add src/Vault.Infrastructure package Microsoft.Extensions.Logging.Abstractions
dotnet add src/Vault.Client         package System.Net.Http.Json
dotnet add src/Vault.Api            package Swashbuckle.AspNetCore

# 4. Drop the seeded source over the templates (overwrites Program.cs, replaces Class1.cs)
Copy-Item -Recurse -Force seed/* .
Remove-Item -ErrorAction SilentlyContinue `
    src/Vault.Domain/Class1.cs, src/Vault.Contracts/Class1.cs, `
    src/Vault.Infrastructure/Class1.cs, src/Vault.Client/Class1.cs

# 5. Build + test the seeded domain logic
dotnet build
dotnet test
Write-Host "Bootstrap complete. Seeded domain logic is built and tested."
