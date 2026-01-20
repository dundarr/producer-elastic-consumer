# Script para probar el endpoint UpdateRate corregido
# Uso: .\Test-ProducerRate.ps1 -BaseUrl "http://localhost:5000"

param(
    [string]$BaseUrl = "http://localhost:5000"
)

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Prueba de Producer Rate Endpoint" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Función para hacer requests
function Invoke-ProducerApi {
    param(
        [string]$Method,
        [string]$Endpoint
    )
    
    try {
        $uri = "$BaseUrl$Endpoint"
        $response = Invoke-RestMethod -Method $Method -Uri $uri -ErrorAction Stop
        return $response
    }
    catch {
        Write-Host "? Error: $_" -ForegroundColor Red
        return $null
    }
}

# 1. Verificar rate inicial
Write-Host "1??  Obteniendo rate actual..." -ForegroundColor Yellow
$currentRate = Invoke-ProducerApi -Method "GET" -Endpoint "/producer/rate"
if ($currentRate) {
    Write-Host "   ? Rate actual: $($currentRate.rate)" -ForegroundColor Green
}
Write-Host ""

# 2. Cambiar rate a 10
Write-Host "2??  Cambiando rate a 10..." -ForegroundColor Yellow
$newRate = Invoke-ProducerApi -Method "POST" -Endpoint "/producer/rate/10"
if ($newRate) {
    Write-Host "   ? Rate actualizado: $($newRate.rate)" -ForegroundColor Green
    if ($newRate.rate -eq 10) {
        Write-Host "   ? ÉXITO: El rate se cambió correctamente a 10" -ForegroundColor Green
    } else {
        Write-Host "   ? ERROR: El rate debería ser 10 pero es $($newRate.rate)" -ForegroundColor Red
    }
}
Write-Host ""

# 3. Verificar que el cambio persiste
Write-Host "3??  Verificando que el cambio persiste..." -ForegroundColor Yellow
$verifyRate = Invoke-ProducerApi -Method "GET" -Endpoint "/producer/rate"
if ($verifyRate) {
    Write-Host "   ? Rate verificado: $($verifyRate.rate)" -ForegroundColor Green
    if ($verifyRate.rate -eq 10) {
        Write-Host "   ? ÉXITO: El rate persiste correctamente" -ForegroundColor Green
    } else {
        Write-Host "   ? ERROR: El rate debería persistir como 10 pero es $($verifyRate.rate)" -ForegroundColor Red
    }
}
Write-Host ""

# 4. Probar diferentes valores
Write-Host "4??  Probando diferentes valores..." -ForegroundColor Yellow
$testValues = @(5, 25, 50, 100, 1)

foreach ($value in $testValues) {
    Write-Host "   Estableciendo rate a $value..." -ForegroundColor Cyan
    $result = Invoke-ProducerApi -Method "POST" -Endpoint "/producer/rate/$value"
    if ($result -and $result.rate -eq $value) {
        Write-Host "   ? $value ? Correcto" -ForegroundColor Green
    } else {
        Write-Host "   ? $value ? ERROR (recibido: $($result.rate))" -ForegroundColor Red
    }
}
Write-Host ""

# 5. Verificar estado del producer
Write-Host "5??  Verificando estado del producer..." -ForegroundColor Yellow
$isRunning = Invoke-ProducerApi -Method "GET" -Endpoint "/producer/is-running"
if ($null -ne $isRunning) {
    if ($isRunning) {
        Write-Host "   ? Producer está CORRIENDO" -ForegroundColor Green
    } else {
        Write-Host "   ??  Producer está PAUSADO" -ForegroundColor Yellow
        Write-Host "   ?? Para iniciar: curl -X POST $BaseUrl/producer/start" -ForegroundColor Gray
    }
}
Write-Host ""

# 6. Prueba de valores edge case
Write-Host "6??  Probando valores edge case..." -ForegroundColor Yellow

Write-Host "   Probando valor 0 (debería convertirse a 1)..." -ForegroundColor Cyan
$zeroResult = Invoke-ProducerApi -Method "POST" -Endpoint "/producer/rate/0"
if ($zeroResult -and $zeroResult.rate -eq 1) {
    Write-Host "   ? 0 ? 1 (Correcto - mínimo forzado)" -ForegroundColor Green
} else {
    Write-Host "   ? 0 ? $($zeroResult.rate) (ERROR - debería ser 1)" -ForegroundColor Red
}

Write-Host "   Probando valor negativo (debería convertirse a 1)..." -ForegroundColor Cyan
$negativeResult = Invoke-ProducerApi -Method "POST" -Endpoint "/producer/rate/-5"
if ($negativeResult -and $negativeResult.rate -eq 1) {
    Write-Host "   ? -5 ? 1 (Correcto - mínimo forzado)" -ForegroundColor Green
} else {
    Write-Host "   ? -5 ? $($negativeResult.rate) (ERROR - debería ser 1)" -ForegroundColor Red
}

Write-Host "   Probando valor muy alto (1000)..." -ForegroundColor Cyan
$highResult = Invoke-ProducerApi -Method "POST" -Endpoint "/producer/rate/1000"
if ($highResult -and $highResult.rate -eq 1000) {
    Write-Host "   ? 1000 ? Correcto" -ForegroundColor Green
} else {
    Write-Host "   ? 1000 ? $($highResult.rate) (ERROR)" -ForegroundColor Red
}
Write-Host ""

# Resumen final
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "? Pruebas Completadas" -ForegroundColor Green
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Endpoints disponibles:" -ForegroundColor White
Write-Host "  POST   $BaseUrl/producer/start" -ForegroundColor Gray
Write-Host "  POST   $BaseUrl/producer/stop" -ForegroundColor Gray
Write-Host "  GET    $BaseUrl/producer/is-running" -ForegroundColor Gray
Write-Host "  GET    $BaseUrl/producer/rate" -ForegroundColor Gray
Write-Host "  POST   $BaseUrl/producer/rate/{valor}" -ForegroundColor Yellow
Write-Host ""
Write-Host "Ejemplo de uso:" -ForegroundColor White
Write-Host '  Invoke-RestMethod -Method POST -Uri "$BaseUrl/producer/rate/50"' -ForegroundColor Gray
