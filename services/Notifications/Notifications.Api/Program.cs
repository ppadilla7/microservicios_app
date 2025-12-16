using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Net.Mail;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.Configure<MailtrapOptions>(builder.Configuration.GetSection("Mailtrap"));
builder.Services.AddSingleton<IRabbitSubscriber, RabbitSubscriber>();
builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddHostedService<EnrollmentNotificationsWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

// Options
public class RabbitOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public class MailtrapOptions
{
    public string Host { get; set; } = "sandbox.smtp.mailtrap.io";
    public int Port { get; set; } = 2525;
    public string User { get; set; } = "c936a3a4cd3519";
    public string Password { get; set; } = "e150ef49d01272";
    public string FromEmail { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "University Notifications";
}

// RabbitMQ subscriber abstraction
public interface IRabbitSubscriber
{
    void Subscribe(string exchange, string queue, string routingKey, Func<ReadOnlyMemory<byte>, Task> handler);
}

public sealed class RabbitSubscriber : IRabbitSubscriber, IDisposable
{
    private IConnection? _connection;

    public RabbitSubscriber(IOptions<RabbitOptions> opts)
    {
        var cfg = opts.Value;
        var factory = new ConnectionFactory
        {
            HostName = cfg.HostName,
            Port = cfg.Port,
            UserName = cfg.UserName,
            Password = cfg.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };

        // Retry connection to RabbitMQ on startup to avoid crash if broker is not ready yet
        var maxAttempts = 15; // ~30s total with 2s backoff
        var attempt = 0;
        while (attempt < maxAttempts && _connection == null)
        {
            try
            {
                _connection = factory.CreateConnection();
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException)
            {
                attempt++;
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
        }

        if (_connection == null)
        {
            // Let the host start; the background worker will attempt subscription and log errors if connection is still unavailable
            _connection = null;
        }
    }

    public void Subscribe(string exchange, string queue, string routingKey, Func<ReadOnlyMemory<byte>, Task> handler)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("RabbitMQ connection is not established.");
        }
        var channel = _connection.CreateModel();
        channel.ExchangeDeclare(exchange: exchange, type: ExchangeType.Topic, durable: true);
        channel.QueueDeclare(queue: queue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(queue: queue, exchange: exchange, routingKey: routingKey);
        channel.BasicQos(0, prefetchCount: 1, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                await handler(ea.Body);
                channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch
            {
                channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };
        channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
    }

    public void Dispose()
    {
        try { _connection?.Dispose(); } catch { }
    }
}

// Email service using Mailtrap SMTP
public interface IEmailService
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}

public sealed class EmailService : IEmailService
{
    private readonly MailtrapOptions _opts;
    public EmailService(IOptions<MailtrapOptions> opts) { _opts = opts.Value; }

    public async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        using var client = new SmtpClient(_opts.Host, _opts.Port)
        {
            EnableSsl = true,
            Credentials = new System.Net.NetworkCredential(_opts.User, _opts.Password)
        };
        client.Timeout = 5000; // ms
        using var msg = new MailMessage()
        {
            From = new MailAddress(_opts.FromEmail, _opts.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(toEmail);
        await client.SendMailAsync(msg);
    }
}

// Background worker: handle enrollment.created
public sealed class EnrollmentNotificationsWorker : BackgroundService
{
    private readonly IRabbitSubscriber _subscriber;
    private readonly IEmailService _emails;
    private readonly ILogger<EnrollmentNotificationsWorker> _logger;

    public EnrollmentNotificationsWorker(IRabbitSubscriber subscriber, IEmailService emails, ILogger<EnrollmentNotificationsWorker> logger)
    {
        _subscriber = subscriber;
        _emails = emails;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando suscripción de Notifications a university.events -> notifications.enrollment [enrollment.created]");

        // Retry subscription until RabbitMQ connection becomes available
        var attempts = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _subscriber.Subscribe(
                    exchange: "university.events",
                    queue: "notifications.enrollment",
                    routingKey: "enrollment.created",
                    handler: async body =>
                    {
                        var json = Encoding.UTF8.GetString(body.Span);
                        var evt = JsonSerializer.Deserialize<EnrollmentCreatedEvent>(json);
                        if (evt == null) return;

                        _logger.LogInformation("Evento recibido: enrollment.created Id={Id} StudentId={StudentId} CourseId={CourseId}", evt.Id, evt.StudentId, evt.CourseId);

                        var subject = $"Matrícula creada: Curso {evt.CourseId}";
                        var html = $"<p>Tu matrícula fue registrada.</p><ul><li>Estudiante: {evt.StudentId}</li><li>Curso: {evt.CourseId}</li><li>Fecha: {evt.EnrolledAt:u}</li></ul>";

                        try
                        {
                            var sendTask = _emails.SendAsync(toEmail: "inbox@mailtrap.io", subject, html);
                            var completed = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(5), stoppingToken));
                            if (completed != sendTask)
                            {
                                _logger.LogWarning("Timeout enviando notificación para {EnrollmentId}", evt.Id);
                            }
                            else
                            {
                                await sendTask; // propagate exceptions if any
                                _logger.LogInformation("Notificación de matrícula enviada para {EnrollmentId}", evt.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error enviando notificación para {EnrollmentId}", evt.Id);
                        }
                    });

                _logger.LogInformation("Suscripción a RabbitMQ establecida.");
                break; // subscription ok
            }
            catch (InvalidOperationException)
            {
                attempts++;
                _logger.LogWarning("RabbitMQ aún no disponible. Reintentando suscripción en 2s (intento {Attempt})", attempts);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                attempts++;
                _logger.LogError(ex, "Error inicializando suscripción. Reintento en 2s (intento {Attempt})", attempts);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }

        // Keep the background service alive
        await Task.CompletedTask;
    }
}

public class EnrollmentCreatedEvent
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public DateTime EnrolledAt { get; set; }
}