namespace VRCDollyManager.Models;

public class DirectorDolly(Dolly dolly)
{
    public Dolly Dolly { get; set; } = dolly;
    public Guid Id { get; set; } = Guid.NewGuid();
}