using System.Text.Json;
using Contracts;
using RabbitMQ.Client;

namespace RegistrationApi.Messaging;

public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConnectionFactory _factory;
    private readonly string _exchange;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly object _lock = new();

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqEventPublisher(IConfiguration config, ILogger<RabbitMqEventPublisher> logger)
    {
        _logger = logger;
        var section = config.GetSection("RabbitMq");
        _exchange = section["Exchange"] ?? "registration.events";

        _factory = new ConnectionFactory
        {
            HostName = section["Host"] ?? "localhost",
            Port = int.Parse(section["Port"] ?? "5672"),
            UserName = section["User"] ?? "guest",
            Password = section["Password"] ?? "guest",
            AutomaticRecoveryEnabled = true
        };
    }

    // Kết nối lười: API vẫn khởi động được khi RabbitMQ chưa bật
    private void EnsureConnected()
    {
        if (_channel is { IsOpen: true }) return;

        _channel?.Dispose();
        _connection?.Dispose();

        _connection = _factory.CreateConnection("registration-api");
        _channel = _connection.CreateModel();

        // Khai báo exchange (idempotent: có rồi thì giữ nguyên)
        _channel.ExchangeDeclare(_exchange, ExchangeType.Fanout, durable: true);
        // Bật publisher confirms: broker xác nhận đã nhận tin
        _channel.ConfirmSelect();

        _logger.LogInformation("Đã kết nối RabbitMQ, exchange {Exchange}", _exchange);
    }

    public void Publish(RegistrationEvent evt)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(evt, JsonOptions);

        // IModel (channel) không an toàn khi dùng đa luồng, nên phải khóa
        lock (_lock)
        {
            EnsureConnected();

            var props = _channel!.CreateBasicProperties();
            props.Persistent = true;                       // ghi xuống đĩa
            props.ContentType = "application/json";
            props.MessageId = evt.EventId.ToString();
            props.Type = evt.EventType;
            props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(_exchange, routingKey: "", basicProperties: props, body: body);
            _channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
        }

        _logger.LogInformation("Đã phát {Type} cho đăng ký {Id}", evt.EventType, evt.RegistrationId);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
