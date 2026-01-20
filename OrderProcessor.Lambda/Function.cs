using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using System.Text.Json;

// Assembly attribute para especificar el serializer
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace OrderProcessor.Lambda;

/// <summary>
/// Lambda Function que procesa pedidos automáticamente desde order-processing-queue
/// </summary>
public class Function
{
    /// <summary>
    /// Handler principal de la Lambda - Se ejecuta por cada batch de mensajes SQS
    /// </summary>
    /// <param name="sqsEvent">Evento SQS con 1 o más mensajes</param>
    /// <param name="context">Contexto de ejecución Lambda</param>
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        context.Logger.LogLine("===========================================");
        context.Logger.LogLine($"🚀 OrderProcessor Lambda iniciada");
        context.Logger.LogLine($"📦 Procesando {sqsEvent.Records.Count} mensaje(s)");
        context.Logger.LogLine("===========================================");

        foreach (var record in sqsEvent.Records)
        {
            await ProcessOrderMessage(record, context);
        }

        context.Logger.LogLine("✅ Procesamiento completado exitosamente");
    }

    /// <summary>
    /// Procesa un mensaje individual de pedido
    /// </summary>
    private async Task ProcessOrderMessage(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine($"\n--- Procesando mensaje ID: {message.MessageId} ---");

            // 1️⃣ Deserializar el mensaje
            var orderData = JsonSerializer.Deserialize<OrderMessage>(message.Body);

            if (orderData == null)
            {
                context.Logger.LogLine("⚠️  Mensaje vacío o inválido");
                return;
            }

            // 2️⃣ Validar datos del pedido
            var validationResult = ValidateOrder(orderData, context);
            if (!validationResult.IsValid)
            {
                context.Logger.LogLine($"❌ Validación fallida: {validationResult.ErrorMessage}");
                // En producción, aquí enviarías a DLQ o logging centralizado
                return;
            }

            // 3️⃣ Simular procesamiento del pedido
            context.Logger.LogLine("📋 Datos del pedido:");
            context.Logger.LogLine($"   • Order ID: {orderData.OrderId}");
            context.Logger.LogLine($"   • User ID: {orderData.UserId}");
            context.Logger.LogLine($"   • Total: ${orderData.TotalAmount:F2}");
            context.Logger.LogLine($"   • Items: {orderData.Items?.Length ?? 0} producto(s)");
            context.Logger.LogLine($"   • Timestamp: {orderData.Timestamp}");

            // 4️⃣ Simular operaciones de procesamiento
            await SimulateOrderProcessing(orderData, context);

            context.Logger.LogLine("✅ Pedido procesado exitosamente");
        }
        catch (JsonException jsonEx)
        {
            context.Logger.LogLine($"❌ Error deserializando JSON: {jsonEx.Message}");
            throw; // Re-lanzar para que SQS reintente o envíe a DLQ
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"❌ Error procesando pedido: {ex.Message}");
            context.Logger.LogLine($"   Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Valida los datos del pedido
    /// </summary>
    private ValidationResult ValidateOrder(OrderMessage order, ILambdaContext context)
    {
        context.Logger.LogLine("🔍 Validando pedido...");

        if (order.OrderId <= 0)
            return ValidationResult.Fail("OrderId debe ser mayor que 0");

        if (order.UserId <= 0)
            return ValidationResult.Fail("UserId debe ser mayor que 0");

        if (order.TotalAmount <= 0)
            return ValidationResult.Fail("TotalAmount debe ser mayor que 0");

        if (order.Items == null || order.Items.Length == 0)
            return ValidationResult.Fail("Debe tener al menos 1 item");

        context.Logger.LogLine("✅ Validación exitosa");
        return ValidationResult.Success();
    }

    /// <summary>
    /// Simula el procesamiento real del pedido
    /// </summary>
    private async Task SimulateOrderProcessing(OrderMessage order, ILambdaContext context)
    {
        context.Logger.LogLine("⚙️  Procesando pedido...");

        // Simular operaciones que tomarían tiempo en producción:
        // - Verificar inventario
        // - Reservar productos
        // - Calcular envío
        // - Generar factura

        await Task.Delay(100); // Simular latencia

        context.Logger.LogLine("   ✓ Inventario verificado");
        await Task.Delay(50);

        context.Logger.LogLine("   ✓ Productos reservados");
        await Task.Delay(50);

        context.Logger.LogLine("   ✓ Envío calculado");
        await Task.Delay(50);

        context.Logger.LogLine("   ✓ Factura generada");
    }
}

#region Modelos de Datos

/// <summary>
/// Modelo del mensaje de pedido (debe coincidir con el JSON enviado desde Program.cs)
/// </summary>
public class OrderMessage
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderItem[]? Items { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Item dentro de un pedido
/// </summary>
public class OrderItem
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>
/// Resultado de validación
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Fail(string error) => new() { IsValid = false, ErrorMessage = error };
}

#endregion
