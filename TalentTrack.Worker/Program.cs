using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using TalentTrack.Worker;
using YGLogProvider;


// !!! Important !!! in order to be able to run this program, please be sure to sign up with RabbitMQ and Service Bus
// Login to Azure to do that

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<RabbitMQWorker>();
builder.Services.AddHostedService<ServiceBusWorker>();
LogProvider logP = new LogProvider(LogLevel.Information, Path.Combine(AppContext.BaseDirectory, "Log"), "log.txt", 10, 50000);
builder.Logging.ClearProviders(); 
builder.Logging.AddProvider(logP);

var host = builder.Build();
host.Run();
