namespace Kodify.DevOps.Models
{
    public class PipelineInfo
    {
        public string ProjectType { get; set; }
        public string Framework { get; set; }
        public bool HasTests { get; set; }
        public bool HasDocker { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public string SourceControlType { get; set; }
        public string BuildConfiguration { get; set; } = "Release";
        public List<string> TargetFrameworks { get; set; } = new();
        public bool IsPackageProject { get; set; }
        public string PackageId { get; set; }
        public string Version { get; set; }
        public bool HasInfrastructureAsCode { get; set; }
        public EnvironmentRequirements Environment { get; set; }
    }

    public class EnvironmentRequirements
    {
        public bool RequiresDatabase { get; set; }
        public bool RequiresDocker { get; set; }
        public bool RequiresAWS { get; set; }
    }
}