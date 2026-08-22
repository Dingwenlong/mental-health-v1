using Microsoft.AspNetCore.SignalR;

namespace MentalHealth.Api.Hubs;

public sealed class DevelopmentProbeHub : Hub
{
    public string Echo(string value) => value;
}
