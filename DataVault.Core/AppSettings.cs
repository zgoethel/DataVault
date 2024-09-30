namespace DataVault.Core;

public class RabbitMQSettings
{
    public string HostName { get; set; } = "";
    public int Port { get; set; }
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AppSettings
{
    public RabbitMQSettings RabbitMQ { get; set; } = new();
}
