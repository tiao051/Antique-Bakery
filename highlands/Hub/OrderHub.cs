using Microsoft.AspNetCore.SignalR;

public class OrderHub : Hub
{
    public async Task SendNewOrder(string message)
    {
        await Clients.All.SendAsync("ReceiveNewOrder", message);
    }
}
