
$ErrorActionPreference = "Stop"

$baseUrl = "http://localhost:5080"
$aliceIncidentId = "20000000-0000-0000-0000-000000000001"

if ([string]::IsNullOrWhiteSpace($env:SeedUsers__Password)) {
    throw "SeedUsers__Password is not set."
}

function Test-IncidentAccess {
    param(
        [string]$UserName,
        [string]$IncidentId
    )

    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

    $loginBody = @{
        userName = $UserName
        password = $env:SeedUsers__Password
    } | ConvertTo-Json

    $login = Invoke-WebRequest `
        -Uri "$baseUrl/api/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $loginBody `
        -WebSession $session `
        -UseBasicParsing

    Write-Host "$UserName login: $($login.StatusCode)"

    if ($login.StatusCode -ne 204) {
        throw "Login failed for $UserName"
    }

    $response = Invoke-WebRequest `
        -Uri "$baseUrl/api/incidents/$IncidentId" `
        -Method Get `
        -WebSession $session `
        -UseBasicParsing

    Write-Host "$UserName reads Alice incident: $($response.StatusCode)"
}

Test-IncidentAccess -UserName "alice" -IncidentId $aliceIncidentId
Test-IncidentAccess -UserName "bob" -IncidentId $aliceIncidentId
Test-IncidentAccess -UserName "morgan" -IncidentId $aliceIncidentId
