using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class OrderHub : Hub
{
    public async Task SendOrderUpdate(string orderId)
    {
        await Clients.All.SendAsync("ReceiveOrderUpdate", orderId);
    }
}
