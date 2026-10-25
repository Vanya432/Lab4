
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

    try {
    $response = Invoke-WebRequest `
        -Uri "$baseUrl/api/incidents/$IncidentId" `
        -Method Get `
        -WebSession $session `
        -UseBasicParsing

    $statusCode = [int]$response.StatusCode
}
catch [System.Net.WebException] {
    if ($null -eq $_.Exception.Response) {
        throw
    }

    $statusCode = [int]$_.Exception.Response.StatusCode
}

Write-Host "$UserName reads incident $IncidentId : $statusCode"
}

Test-IncidentAccess -UserName "alice" -IncidentId $aliceIncidentId
Test-IncidentAccess -UserName "bob" -IncidentId $aliceIncidentId
Test-IncidentAccess -UserName "morgan" -IncidentId $aliceIncidentId

$bobIncidentId = "20000000-0000-0000-0000-000000000002"

Test-IncidentAccess -UserName "alice" -IncidentId $bobIncidentId