using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class RabbitMQWorker : BackgroundService
{
    private readonly ILogger<RabbitMQWorker> _logger;
    private readonly IConfiguration _config;

    public RabbitMQWorker(ILogger<RabbitMQWorker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string rabbitMQURIString = _config["RabbitMQ:Uri"] ?? "";

        if (string.IsNullOrWhiteSpace(rabbitMQURIString))
            throw new InvalidOperationException("RabbitMQ:UriString missing");

        var factory = new ConnectionFactory()
        {
            Uri = new Uri(rabbitMQURIString)
        };

        var connection = await factory.CreateConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        string queueName = _config["RabbitMQ:TalentTracNewCandidateQueue"] ?? "";

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        // 🔥 Prevent message flooding (VERY useful)
        await channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());

                _logger.LogInformation("📥 {msg}", json);

                await Task.Delay(1000, stoppingToken);

                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed processing");

                await channel.BasicNackAsync(ea.DeliveryTag, false, true, stoppingToken);
            }
        };

        _logger.LogInformation("🐰 RabbitMQ listener started");

        await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // expected during shutdown
        }

        // graceful shutdown
        _logger.LogInformation("🛑 RabbitMQ worker stopping...");

        await channel.CloseAsync(stoppingToken);
        await connection.CloseAsync(stoppingToken);
    }
}