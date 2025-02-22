using Kodify.DevOps.Models;
namespace Kodify.DevOps.IaC;
public interface IIaCGenerator
{
    Task GenerateInfrastructureTemplateAsync();
    string PlatformName { get; }
}