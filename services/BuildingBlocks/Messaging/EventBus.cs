using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace BuildingBlocks.Messaging;

public interface IEventBus
{
    Task PublishAsync<T>(string exchange, string routingKey, T payload, CancellationToken ct = default);
    Task WriteAuditAsync(string topic, object payload, CancellationToken ct = default);
}

public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
}

internal sealed class EventBus : IEventBus, IDisposable
{
    private readonly ILogger<EventBus> _logger;
    private readonly IConnection _rabbitConnection;
    private readonly IProducer<string, string> _kafkaProducer;

    public EventBus(ILogger<EventBus> logger, RabbitMqOptions rabbitOptions, KafkaOptions kafkaOptions)
    {
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = rabbitOptions.HostName,
            Port = rabbitOptions.Port,
            UserName = rabbitOptions.UserName,
            Password = rabbitOptions.Password
        };
        _rabbitConnection = factory.CreateConnection();

        var config = new ProducerConfig
        {
            BootstrapServers = kafkaOptions.BootstrapServers,
            Acks = Acks.All
        };
        _kafkaProducer = new ProducerBuilder<string, string>(config).Build();
    }

    public Task PublishAsync<T>(string exchange, string routingKey, T payload, CancellationToken ct = default)
    {
        using var channel = _rabbitConnection.CreateModel();
        channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Topic, durable: true);
        var json = JsonSerializer.Serialize(payload);
        var body = Encoding.UTF8.GetBytes(json);
        var props = channel.CreateBasicProperties();
        props.DeliveryMode = 2; // persistent
        channel.BasicPublish(exchange: exchange, routingKey: routingKey, basicProperties: props, body: body);
        _logger.LogInformation("RabbitMQ published: exchange={Exchange} routingKey={RoutingKey} size={Size}", exchange, routingKey, body.Length);
        return Task.CompletedTask;
    }

    public async Task WriteAuditAsync(string topic, object payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload);
        var msg = new Message<string, string> { Key = Guid.NewGuid().ToString(), Value = json };
        var dr = await _kafkaProducer.ProduceAsync(topic, msg, ct);
        _logger.LogInformation("Kafka audit written: topic={Topic} partition={Partition} offset={Offset}", dr.Topic, dr.Partition, dr.Offset);
    }

    public void Dispose()
    {
        try { _kafkaProducer?.Flush(TimeSpan.FromSeconds(2)); } catch { }
        try { _kafkaProducer?.Dispose(); } catch { }
        try { _rabbitConnection?.Dispose(); } catch { }
    }
}

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddEventBus(this IServiceCollection services, IConfiguration config)
    {
        var rabbit = new RabbitMqOptions();
        config.GetSection("RabbitMQ").Bind(rabbit);
        var kafka = new KafkaOptions();
        config.GetSection("Kafka").Bind(kafka);

        services.AddSingleton(rabbit);
        services.AddSingleton(kafka);
        services.AddSingleton<IEventBus, EventBus>();
        return services;
    }
}