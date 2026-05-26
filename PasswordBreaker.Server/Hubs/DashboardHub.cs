using Microsoft.AspNetCore.SignalR;

namespace PasswordBreaker.Server.Hubs;

public class DashboardHub : Hub
{
    public async Task NotifyWorkerCount(int count)
    {
        await Clients.All.SendAsync("WorkerCountUpdated", count);
    }
}
