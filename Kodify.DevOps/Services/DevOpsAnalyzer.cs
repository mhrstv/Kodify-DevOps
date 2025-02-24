using Kodify.DevOps.Models;
using System.Text.RegularExpressions;
using Kodify.Repository.Services;

namespace Kodify.DevOps
{
    public class DevOpsAnalyzer
    {
        private readonly ProjectAnalyzer _projectAnalyzer;
        private readonly GitRepositoryService _gitService;
        public string ProjectPath { get; private set; }

        public DevOpsAnalyzer()
        {
            _projectAnalyzer = new ProjectAnalyzer();
            _gitService = new GitRepositoryService();
        }

        public async Task<PipelineInfo> AnalyzeProjectAsync(string projectPath = null)
        {
            ProjectPath = projectPath ?? _gitService.DetectProjectRoot();
            var projectInfo = _projectAnalyzer.Analyze(ProjectPath);

            var pipelineInfo = new PipelineInfo
            {
                HasTests = projectInfo.Structure.Directories
                    .Any(d => d.Contains("test", StringComparison.OrdinalIgnoreCase)),
                HasDocker = File.Exists(Path.Combine(ProjectPath, "Dockerfile")),
                IsPackageProject = true,
                HasInfrastructureAsCode = DetectInfrastructureAsCode(ProjectPath),
                Dependencies = await AnalyzeDependenciesAsync(ProjectPath),
                Environment = DetectEnvironmentRequirements(ProjectPath)
            };

            // Analyze .csproj file for more details
            var csprojFiles = Directory.GetFiles(ProjectPath, "*.csproj", SearchOption.AllDirectories);
            if (csprojFiles.Any())
            {
                var csprojContent = File.ReadAllText(csprojFiles.First());
                pipelineInfo.TargetFrameworks = ExtractTargetFrameworks(csprojContent);
                pipelineInfo.PackageId = ExtractPackageId(csprojContent);
                pipelineInfo.Version = ExtractVersion(csprojContent);
            }

            // Determine source control type
            var (hasGit, repoUrl) = _gitService.CheckForGitRepository(ProjectPath);
            if (hasGit)
            {
                pipelineInfo.SourceControlType = repoUrl?.Contains("github.com") == true 
                    ? "GitHub" 
                    : repoUrl?.Contains("dev.azure.com") == true 
                        ? "Azure DevOps" 
                        : "Git";
            }

            return pipelineInfo;
        }

        private List<string> ExtractTargetFrameworks(string csprojContent)
        {
            // Simple XML parsing
            var frameworks = new List<string>();
            if (csprojContent.Contains("<TargetFramework>"))
            {
                var match = Regex.Match(csprojContent, "<TargetFramework>(.*?)</TargetFramework>");
                if (match.Success)
                {
                    frameworks.Add(match.Groups[1].Value);
                }
            }
            return frameworks;
        }

        private string ExtractPackageId(string csprojContent)
        {
            var match = Regex.Match(csprojContent, "<PackageId>(.*?)</PackageId>");
            return match.Success ? match.Groups[1].Value : null;
        }

        private string ExtractVersion(string csprojContent)
        {
            var match = Regex.Match(csprojContent, "<Version>(.*?)</Version>");
            return match.Success ? match.Groups[1].Value : "1.0.0";
        }

        private bool DetectInfrastructureAsCode(string path)
        {
            return Directory.Exists(Path.Combine(path, "infrastructure")) ||
                   Directory.GetFiles(path, "*.tf", SearchOption.AllDirectories).Any() ||
                   Directory.GetFiles(path, "*.yaml", SearchOption.AllDirectories)
                       .Any(f => File.ReadAllText(f).Contains("AWSTemplateFormatVersion"));
        }

        private async Task<List<string>> AnalyzeDependenciesAsync(string path)
        {
            var dependencies = new List<string>();
            var csprojFiles = Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories);
            
            foreach (var file in csprojFiles)
            {
                var content = await File.ReadAllTextAsync(file);
                // Extract PackageReference elements
                var matches = Regex.Matches(content, "<PackageReference Include=\"(.*?)\"");
                dependencies.AddRange(matches.Select(m => m.Groups[1].Value));
            }
            
            return dependencies;
        }

        private EnvironmentRequirements DetectEnvironmentRequirements(string path)
        {
            return new EnvironmentRequirements
            {
                RequiresDatabase = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                    .Any(f => File.ReadAllText(f).Contains("DbContext")),
                RequiresDocker = File.Exists(Path.Combine(path, "Dockerfile")),
                RequiresAWS = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                    .Any(f => File.ReadAllText(f).Contains("Amazon.") || File.ReadAllText(f).Contains("AWS."))
            };
        }
    }
}