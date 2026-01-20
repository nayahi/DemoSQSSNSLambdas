#Requires -Version 7

<#
.SYNOPSIS
    Despliega las Lambda Functions en LocalStack
.DESCRIPTION
    Script para crear, actualizar y configurar event source mappings de las Lambdas
.NOTES
    Asegúrate de que LocalStack esté ejecutándose
#>

param(
    [switch]$Clean,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║        DEPLOYMENT DE LAMBDAS EN LOCALSTACK               ║" -ForegroundColor Cyan
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Cyan

# Configuración
$LocalStackEndpoint = "http://localhost:4566"
$Region = "us-east-1"
$RoleArn = "arn:aws:iam::000000000000:role/lambda-execution-role"

# Verificar que LocalStack esté corriendo
Write-Host "`n🔍 Verificando LocalStack..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$LocalStackEndpoint/_localstack/health" -UseBasicParsing
    Write-Host "✅ LocalStack está disponible" -ForegroundColor Green
} catch {
    Write-Host "❌ LocalStack no está disponible. Inicia LocalStack primero." -ForegroundColor Red
    exit 1
}

# Función para crear/actualizar Lambda
function Deploy-Lambda {
    param(
        [string]$FunctionName,
        [string]$ProjectPath,
        [string]$Handler,
        [string]$QueueName,
        [string]$Description
    )

    Write-Host "`n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
    Write-Host "📦 Desplegando: $FunctionName" -ForegroundColor Cyan
    Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

    Push-Location $ProjectPath

    try {
        # Compilar
        Write-Host "🔨 Compilando proyecto..." -ForegroundColor Yellow
        dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
        
        if ($LASTEXITCODE -ne 0) {
            throw "Error compilando proyecto"
        }
        Write-Host "✅ Compilación exitosa" -ForegroundColor Green

        # Crear ZIP con RUTA ABSOLUTA
        Write-Host "📦 Creando paquete ZIP..." -ForegroundColor Yellow
        $zipFileName = "$FunctionName.zip"
        $zipPath = Join-Path (Get-Location) $zipFileName
        
        # Eliminar ZIP anterior si existe
        if (Test-Path $zipPath) {
            Remove-Item $zipPath -Force
        }

        # Crear ZIP
        Compress-Archive -Path ./publish/* -DestinationPath $zipPath -Force
        Write-Host "✅ Paquete creado: $zipPath" -ForegroundColor Green

        # Verificar que el ZIP existe
        if (-not (Test-Path $zipPath)) {
            throw "ZIP no creado correctamente"
        }

        # Copiar ZIP al contenedor con VERIFICACIÓN
        Write-Host "📤 Copiando ZIP al contenedor..." -ForegroundColor Yellow
        docker cp $zipPath localstack-aws:/tmp/$zipFileName
        
        if ($LASTEXITCODE -ne 0) {
            throw "Error copiando ZIP al contenedor"
        }

        # Verificar que el archivo llegó al contenedor
        $fileCheck = docker exec localstack-aws ls -la /tmp/$zipFileName 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "ZIP no encontrado en contenedor: $fileCheck"
        }
        Write-Host "✅ ZIP copiado al contenedor" -ForegroundColor Green

        # Verificar si Lambda ya existe - VERSIÓN CORREGIDA
Write-Host "🔍 Verificando si Lambda existe..." -ForegroundColor Yellow

$checkResult = docker exec localstack-aws awslocal lambda get-function --function-name $FunctionName 2>&1
$lambdaExists = $LASTEXITCODE -eq 0

if ($lambdaExists) {
    Write-Host "🔄 Lambda existente, actualizando código..." -ForegroundColor Yellow
    
    docker exec localstack-aws awslocal lambda update-function-code `
        --function-name $FunctionName `
        --zip-file fileb:///tmp/$zipFileName

    Write-Host "✅ Código actualizado" -ForegroundColor Green
} else {
    Write-Host "🆕 Creando nueva Lambda..." -ForegroundColor Yellow
    
    docker exec localstack-aws awslocal lambda create-function `
        --function-name $FunctionName `
        --runtime dotnet8 `
        --handler $Handler `
        --role arn:aws:iam::000000000000:role/lambda-execution-role `
        --zip-file fileb:///tmp/$zipFileName `
        --timeout 30 `
        --memory-size 512 `
        --description "$Description"

    Write-Host "✅ Lambda creada" -ForegroundColor Green

    # Esperar a que esté activa
    Start-Sleep -Seconds 3

    # Configurar event source mapping
    Write-Host "🔗 Configurando trigger SQS..." -ForegroundColor Yellow
    
    $queueUrl = "http://localhost:4566/000000000000/$QueueName"
    $queueArn = docker exec localstack-aws awslocal sqs get-queue-attributes `
        --queue-url $queueUrl `
        --attribute-names QueueArn `
        --query 'Attributes.QueueArn' `
        --output text

    docker exec localstack-aws awslocal lambda create-event-source-mapping `
        --function-name $FunctionName `
        --event-source-arn $queueArn `
        --batch-size 10 `
        --enabled

    Write-Host "✅ Trigger configurado" -ForegroundColor Green
}

        # Limpiar ZIP local
        Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

    } finally {
        Pop-Location
    }
}

# Desplegar Lambda 1: OrderProcessor
Deploy-Lambda `
    -FunctionName "OrderProcessor" `
    -ProjectPath "OrderProcessor.Lambda" `
    -Handler "OrderProcessor.Lambda::OrderProcessor.Lambda.Function::FunctionHandler" `
    -QueueName "order-processing-queue" `
    -Description "Procesa pedidos automáticamente desde order-processing-queue"

# Desplegar Lambda 2: EmailNotifier
Deploy-Lambda `
    -FunctionName "EmailNotifier" `
    -ProjectPath "EmailNotifier.Lambda" `
    -Handler "EmailNotifier.Lambda::EmailNotifier.Lambda.Function::FunctionHandler" `
    -QueueName "email-notifications-queue" `
    -Description "Envía emails de confirmación desde email-notifications-queue"

Write-Host "`n╔═══════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║           ✅ DEPLOYMENT COMPLETADO EXITOSAMENTE           ║" -ForegroundColor Green
Write-Host "╚═══════════════════════════════════════════════════════════╝" -ForegroundColor Green

Write-Host "`n📋 Verificación de Lambdas:" -ForegroundColor Yellow
docker exec localstack-aws awslocal lambda list-functions --query 'Functions[].FunctionName' --output table

Write-Host "`n📋 Event Source Mappings:" -ForegroundColor Yellow
docker exec localstack-aws awslocal lambda list-event-source-mappings --query 'EventSourceMappings[].[FunctionArn, EventSourceArn, State]' --output table

Write-Host "`n🎉 Las Lambdas están listas para procesar eventos!" -ForegroundColor Green
Write-Host "💡 Usa el demo SQS/SNS (Program.cs) para enviar mensajes y verificar la ejecución" -ForegroundColor Cyan