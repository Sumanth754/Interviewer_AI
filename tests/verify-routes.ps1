<#
.SYNOPSIS
    Smoke-tests a deployed AI Interview Platform host (app + API on one origin).

.DESCRIPTION
    Checks the routes that broke in earlier deployments: the SPA must be served
    from "/", client-side routes must fall back to index.html, the API must
    answer, and protected endpoints must still reject anonymous callers.

.EXAMPLE
    .\tests\verify-routes.ps1
    .\tests\verify-routes.ps1 -BaseUrl "https://interviewer-ai-ppmz.onrender.com"
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5055"
)

$ErrorActionPreference = "Continue"
$script:Failures = 0

function Test-Route {
    param(
        [string]$Path,
        [int]$ExpectedStatus,
        [string]$ExpectContentTypeLike = "",
        [string]$Note = ""
    )

    $url = "$BaseUrl$Path"
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60
        $status = [int]$response.StatusCode
        $contentType = [string]$response.Headers['Content-Type']
    }
    catch {
        $status = 0
        $contentType = ""
        $raw = $_.Exception.Response
        if ($raw) {
            try { $status = [int]$raw.StatusCode } catch { $status = 0 }
        }
    }

    $ok = $status -eq $ExpectedStatus
    if ($ok -and $ExpectContentTypeLike) {
        $ok = $contentType -like "*$ExpectContentTypeLike*"
    }

    $mark = if ($ok) { "PASS" } else { "FAIL" }
    if (-not $ok) { $script:Failures++ }

    $detail = if ($Note) { "  ($Note)" } else { "" }
    "{0}  {1,-24} expected {2,-3} got {3,-3} {4}{5}" -f $mark, $Path, $ExpectedStatus, $status, $contentType, $detail
}

Write-Host "Verifying $BaseUrl" -ForegroundColor Cyan
Write-Host ""

Write-Host "-- App (React SPA) --" -ForegroundColor Cyan
Test-Route -Path "/"            -ExpectedStatus 200 -ExpectContentTypeLike "text/html" -Note "app root"
Test-Route -Path "/dashboard"   -ExpectedStatus 200 -ExpectContentTypeLike "text/html" -Note "SPA fallback"
Test-Route -Path "/admin"       -ExpectedStatus 200 -ExpectContentTypeLike "text/html" -Note "SPA fallback"
Test-Route -Path "/interview/x" -ExpectedStatus 200 -ExpectContentTypeLike "text/html" -Note "SPA fallback"
Test-Route -Path "/favicon.svg" -ExpectedStatus 200

Write-Host ""
Write-Host "-- API --" -ForegroundColor Cyan
Test-Route -Path "/api/health"  -ExpectedStatus 200 -ExpectContentTypeLike "application/json"
Test-Route -Path "/api/banks"   -ExpectedStatus 401 -Note "auth still enforced"
Test-Route -Path "/api/nope"    -ExpectedStatus 404 -Note "unknown API route stays 404"
Test-Route -Path "/api/missing.js" -ExpectedStatus 404 -Note "missing asset must not return HTML"

Write-Host ""
Write-Host "-- Docs --" -ForegroundColor Cyan
Test-Route -Path "/swagger/index.html" -ExpectedStatus 200 -Note "set Swagger__Enabled=false to hide"

Write-Host ""
if ($script:Failures -eq 0) {
    Write-Host "All checks passed." -ForegroundColor Green
    exit 0
}

Write-Host "$script:Failures check(s) failed." -ForegroundColor Red
exit 1
