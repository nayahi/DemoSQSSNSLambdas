using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using System.Text;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace EmailNotifier.Lambda;

/// <summary>
/// Lambda Function que envía emails de notificación desde email-notifications-queue
/// Triggered por mensajes SNS → SQS (fan-out pattern)
/// </summary>
public class Function
{
    private const string EmailFrom = "noreply@ecommerce.local";
    private const string EmailSubject = "Confirmación de Pedido";

    /// <summary>
    /// Handler principal de la Lambda
    /// </summary>
    public async Task FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        context.Logger.LogLine("===========================================");
        context.Logger.LogLine($"📧 EmailNotifier Lambda iniciada");
        context.Logger.LogLine($"📬 Procesando {sqsEvent.Records.Count} notificación(es)");
        context.Logger.LogLine("===========================================");

        foreach (var record in sqsEvent.Records)
        {
            await ProcessEmailNotification(record, context);
        }

        context.Logger.LogLine("✅ Notificaciones enviadas exitosamente");
    }

    /// <summary>
    /// Procesa un mensaje individual de notificación
    /// </summary>
    private async Task ProcessEmailNotification(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine($"\n--- Procesando notificación ID: {message.MessageId} ---");

            // 1️⃣ El mensaje viene de SNS, así que tiene una estructura envuelta
            var snsMessage = ExtractSnsMessage(message.Body, context);

            if (snsMessage == null)
            {
                context.Logger.LogLine("⚠️  No se pudo extraer mensaje SNS");
                return;
            }

            // 2️⃣ Deserializar el evento de pedido
            var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(snsMessage);

            if (orderEvent == null)
            {
                context.Logger.LogLine("⚠️  Evento de pedido inválido");
                return;
            }

            // 3️⃣ Generar contenido del email
            var emailContent = GenerateEmailContent(orderEvent, context);

            // 4️⃣ Simular envío de email
            await SendEmail(emailContent, context);

            context.Logger.LogLine("✅ Email enviado exitosamente");
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"❌ Error procesando notificación: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Extrae el mensaje original del wrapper SNS
    /// </summary>
    private string? ExtractSnsMessage(string sqsBody, ILambdaContext context)
    {
        try
        {
            // Los mensajes de SNS vienen envueltos en una estructura JSON
            using var document = JsonDocument.Parse(sqsBody);
            var root = document.RootElement;

            // Verificar si tiene la propiedad "Message" (típico de SNS)
            if (root.TryGetProperty("Message", out var messageElement))
            {
                context.Logger.LogLine("📨 Mensaje SNS detectado, extrayendo contenido...");
                return messageElement.GetString();
            }

            // Si no tiene wrapper SNS, es un mensaje directo
            context.Logger.LogLine("📨 Mensaje directo detectado");
            return sqsBody;
        }
        catch (JsonException)
        {
            context.Logger.LogLine("⚠️  Error parseando wrapper SNS, usando mensaje directo");
            return sqsBody;
        }
    }

    /// <summary>
    /// Genera el contenido HTML del email de confirmación
    /// </summary>
    private EmailContent GenerateEmailContent(OrderCreatedEvent orderEvent, ILambdaContext context)
    {
        context.Logger.LogLine("📝 Generando contenido del email...");

        var htmlBody = new StringBuilder();
        htmlBody.AppendLine("<!DOCTYPE html>");
        htmlBody.AppendLine("<html>");
        htmlBody.AppendLine("<head><meta charset='UTF-8'></head>");
        htmlBody.AppendLine("<body style='font-family: Arial, sans-serif;'>");
        htmlBody.AppendLine($"  <h2>¡Gracias por tu pedido #{orderEvent.OrderId}!</h2>");
        htmlBody.AppendLine($"  <p>Hola, hemos recibido tu pedido correctamente.</p>");
        htmlBody.AppendLine("  <hr>");
        htmlBody.AppendLine("  <h3>Detalles del Pedido:</h3>");
        htmlBody.AppendLine("  <table border='1' cellpadding='8' style='border-collapse: collapse;'>");
        htmlBody.AppendLine("    <tr><th>Campo</th><th>Valor</th></tr>");
        htmlBody.AppendLine($"    <tr><td>ID Pedido</td><td>{orderEvent.OrderId}</td></tr>");
        htmlBody.AppendLine($"    <tr><td>ID Usuario</td><td>{orderEvent.UserId}</td></tr>");
        htmlBody.AppendLine($"    <tr><td>Total</td><td>${orderEvent.TotalAmount:F2}</td></tr>");
        htmlBody.AppendLine($"    <tr><td>Fecha</td><td>{orderEvent.Timestamp:yyyy-MM-dd HH:mm:ss} UTC</td></tr>");
        htmlBody.AppendLine("  </table>");

        if (orderEvent.Items != null && orderEvent.Items.Length > 0)
        {
            htmlBody.AppendLine("  <h3>Productos:</h3>");
            htmlBody.AppendLine("  <ul>");
            foreach (var item in orderEvent.Items)
            {
                htmlBody.AppendLine($"    <li>{item.ProductName} (x{item.Quantity})</li>");
            }
            htmlBody.AppendLine("  </ul>");
        }

        htmlBody.AppendLine("  <hr>");
        htmlBody.AppendLine("  <p style='color: #666; font-size: 12px;'>Este es un email automático, por favor no responder.</p>");
        htmlBody.AppendLine("</body>");
        htmlBody.AppendLine("</html>");

        var content = new EmailContent
        {
            To = $"user{orderEvent.UserId}@example.com", // Email simulado
            From = EmailFrom,
            Subject = $"{EmailSubject} #{orderEvent.OrderId}",
            HtmlBody = htmlBody.ToString()
        };

        context.Logger.LogLine($"   • Para: {content.To}");
        context.Logger.LogLine($"   • Asunto: {content.Subject}");

        return content;
    }

    /// <summary>
    /// Simula el envío de email vía SMTP
    /// </summary>
    private async Task SendEmail(EmailContent email, ILambdaContext context)
    {
        context.Logger.LogLine("📤 Enviando email...");

        // En producción, aquí usarías:
        // - Amazon SES
        // - SendGrid
        // - SMTP server
        // Para el demo, solo simulamos el envío

        await Task.Delay(100); // Simular latencia de red

        context.Logger.LogLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        context.Logger.LogLine("📧 EMAIL SIMULADO ENVIADO:");
        context.Logger.LogLine($"   De:      {email.From}");
        context.Logger.LogLine($"   Para:    {email.To}");
        context.Logger.LogLine($"   Asunto:  {email.Subject}");
        context.Logger.LogLine($"   Tamaño:  {email.HtmlBody.Length} caracteres");
        context.Logger.LogLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // Log de confirmación
        context.Logger.LogLine("✅ Email enviado (simulado) exitosamente");
    }
}

#region Modelos de Datos

/// <summary>
/// Evento de pedido creado (publicado por SNS)
/// </summary>
public class OrderCreatedEvent
{
    public int OrderId { get; set; }
    public int UserId { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderItemEvent[]? Items { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Item de pedido en el evento
/// </summary>
public class OrderItemEvent
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>
/// Contenido del email a enviar
/// </summary>
public class EmailContent
{
    public string To { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
}
#endregion
