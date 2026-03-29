
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TalentTrack.Worker;

public class ServiceBusWorker : BackgroundService
{
    private readonly ILogger<ServiceBusWorker> _logger;
    private readonly IConfiguration _config;

    public ServiceBusWorker(ILogger<ServiceBusWorker> logger, IConfiguration config)
    {
        this._logger = logger;
        this._config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string connectionString = _config["ServiceBus:ConnectionString"] ?? "";
        string queueName = _config["ServiceBus:TalentTracNewUserQueue"] ?? "";

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ServiceBus:ConnectionString missing");

        var client = new ServiceBusClient(connectionString);

        var processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 1
        });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                string body = args.Message.Body.ToString();

                _logger.LogInformation("📥 SB [{queue}] {msg}", queueName, body);

                await Task.Delay(1000, stoppingToken);

                // ✅ mark as processed
                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed processing");

                // 🔁 retry later
                await args.AbandonMessageAsync(args.Message, cancellationToken: stoppingToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception,
                "❌ SB Error Source: {source}, Entity: {entity}",
                args.ErrorSource,
                args.EntityPath);

            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation("☁️ Service Bus listener started");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // expected
        }

        _logger.LogInformation("🛑 Service Bus worker stopping...");

        await processor.StopProcessingAsync(stoppingToken);
        await processor.DisposeAsync();
        await client.DisposeAsync();
    }
}
