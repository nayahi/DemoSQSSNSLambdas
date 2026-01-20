# Obtener último stream y ver logs (TODO AUTOMÁTICO):
$logGroup = "/aws/lambda/OrderProcessor"
$streams = docker exec localstack-aws awslocal logs describe-log-streams --log-group-name /aws/lambda/OrderProcessor --order-by LastEventTime --descending --output json | ConvertFrom-Json

$latestStreamName = $streams.logStreams[0].logStreamName

Write-Host "Ver logs de: $latestStreamName" -ForegroundColor Cyan

docker exec localstack-aws bash -c "awslocal logs get-log-events --log-group-name $logGroup --log-stream-name '$latestStreamName' --start-from-head"