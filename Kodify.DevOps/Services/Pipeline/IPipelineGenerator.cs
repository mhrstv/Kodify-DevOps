namespace Kodify.DevOps.Pipeline
{
    public interface IPipelineGenerator
    {
        Task GenerateAsync();
        bool SupportsProjectType(string projectType);
        string PlatformName { get; }
    }
}