using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class OrderHub : Hub
{
    public async Task SendNewOrder(string message)
    {
        await Clients.All.SendAsync("ReceiveNewOrder", message);
    }
}
