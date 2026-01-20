param([string]$FunctionName = "OrderProcessor")

$logGroup = "/aws/lambda/$FunctionName"

while ($true) {
    Clear-Host
    Write-Host "╔═══════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║  LOGS: $FunctionName" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════╝" -ForegroundColor Cyan
    
    $streams = docker exec localstack-aws awslocal logs describe-log-streams --log-group-name $logGroup --order-by LastEventTime --descending --max-items 1 --output json 2>$null | ConvertFrom-Json
    
    if ($streams.logStreams) {
        $streamName = $streams.logStreams[0].logStreamName
        
        $events = docker exec localstack-aws bash -c "awslocal logs get-log-events --log-group-name '$logGroup' --log-stream-name '$streamName'" | ConvertFrom-Json
        
        foreach ($event in $events.events) {
            $timestamp = [DateTimeOffset]::FromUnixTimeMilliseconds($event.timestamp).LocalDateTime
            Write-Host "[$timestamp] " -ForegroundColor Yellow -NoNewline
            Write-Host $event.message
        }
    } else {
        Write-Host "No hay logs disponibles aún" -ForegroundColor Yellow
    }
    
    Write-Host "`nActualizando cada 5 segundos... (Ctrl+C para salir)" -ForegroundColor Gray
    Start-Sleep -Seconds 5
}