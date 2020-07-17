$request= "https://api.coinbase.com/v2/time"
$dateJson = ((Invoke-WebRequest $request | ConvertFrom-Json ) | select -expand data)
$iso= $dateJson.iso
Write-Host  "Setting date: $iso"
set-date -date $iso