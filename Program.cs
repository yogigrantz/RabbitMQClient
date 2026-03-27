
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text;
using System.Threading.Tasks;

var factory = new ConnectionFactory()
{
    Uri = new Uri("amqps://****:********@toucan.lmq.cloudamqp.com/****")
};

using var connection = await factory.CreateConnectionAsync();
using var channel = await connection.CreateChannelAsync();

var queueName = "Candidate.Created";

// Make sure queue exists (same as producer)
await channel.QueueDeclareAsync(
    queue: queueName,
    durable: false,
    exclusive: false,
    autoDelete: false,
    arguments: null);

Console.WriteLine("🚀 Waiting for messages...");

var consumer = new AsyncEventingBasicConsumer(channel);

consumer.ReceivedAsync += async (sender, ea) =>
{
    var body = ea.Body.ToArray();
    var json = Encoding.UTF8.GetString(body);

    Console.WriteLine($"📥 Received: {json}");

    // simulate processing
    await Task.Delay(1000);

    Console.WriteLine("✅ Processed\n");
};

await channel.BasicConsumeAsync(
    queue: queueName,
    autoAck: true,   // keep simple for now
    consumer: consumer);

Console.ReadLine(); // keep app running
