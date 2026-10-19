# Creates the portal_app role and databases, stores the connection strings in
# .NET user-secrets (outside the repository), then applies the schema and seed data.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File database\setup-database.ps1

param(
    [string]$PgBin = "C:\Program Files\PostgreSQL\18\bin",
    [string]$DbHost = "localhost",
    [int]$Port = 5432,
    [string]$SuperUser = "postgres"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $root "backend\StudentPortal.Api"

$psql = Join-Path $PgBin "psql.exe"
if (-not (Test-Path $psql)) { $psql = "psql" }

$secure = Read-Host "Password for PostgreSQL user '$SuperUser'" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
$appPassword = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object { [char]$_ })

try {
    $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    $env:PORTAL_APP_PASSWORD = $appPassword
    & $psql -h $DbHost -p $Port -U $SuperUser -d postgres -f (Join-Path $PSScriptRoot "00-create-role-and-databases.sql")
    if ($LASTEXITCODE -ne 0) { throw "psql failed. Check the superuser password and that PostgreSQL is running." }
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:PORTAL_APP_PASSWORD -ErrorAction SilentlyContinue
}

$base = "Host=$DbHost;Port=$Port;Username=portal_app;Password=$appPassword;GSS Encryption Mode=Disable"
dotnet user-secrets set "ConnectionStrings:Portal" "$base;Database=student_portal" --project $apiProject | Out-Null
dotnet user-secrets set "ConnectionStrings:PortalTest" "$base;Database=student_portal_test" --project $apiProject | Out-Null

dotnet run --project $apiProject -- migrate
if ($LASTEXITCODE -ne 0) { throw "Migration failed." }

Write-Host ""
Write-Host "Database ready: student_portal (app) and student_portal_test (automated tests)."
Write-Host "Role: portal_app. Its password is stored only in .NET user-secrets for StudentPortal.Api."
