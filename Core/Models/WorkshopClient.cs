using Client_v.Core;

namespace CoreAPI.Core.Models;

public class WorkshopClient
{
    public Workshop workshop { get; set; }
    public Client client { get; set; }

    public WorkshopClient(Workshop workshop, Client client)
    {
        this.workshop = workshop;
        this.client = client;
    }
}
