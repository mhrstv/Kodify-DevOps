using Kodify.DevOps.Models;
namespace Kodify.DevOps.IaC;
public interface IIaCGenerator
{
    Task GenerateTemplateAsync();
    string PlatformName { get; }
}