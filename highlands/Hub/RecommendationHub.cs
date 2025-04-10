using Microsoft.AspNetCore.SignalR;

public class RecommendationHub : Hub
{
    public async Task SendNewRecomment(string message)
    {
        await Clients.All.SendAsync("ReceiveNewRecommention", message);
    }
}
