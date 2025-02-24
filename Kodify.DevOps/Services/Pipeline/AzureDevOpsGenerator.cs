using System.Text;
using Kodify.Repository.Services;
using Kodify.DevOps.Models;

namespace Kodify.DevOps.Pipeline;

public class AzureDevOpsGenerator : IPipelineGenerator
{
    public string PlatformName => "Azure DevOps";
    private readonly DevOpsAnalyzer _analyzer;
    private readonly PipelineInfo _info;
    private readonly GitRepositoryService _gitService;
    private readonly string _defaultBranch;

    public AzureDevOpsGenerator()
    {
        _analyzer = new DevOpsAnalyzer();
        _gitService = new GitRepositoryService();
        _info = _analyzer.AnalyzeProjectAsync().Result;
        _defaultBranch = _gitService.GetDefaultBranch() ?? "main";
    }

    public AzureDevOpsGenerator(DevOpsAnalyzer analyzer)
    {
        _analyzer = analyzer;
        _gitService = new GitRepositoryService();
        _info = _analyzer.AnalyzeProjectAsync().Result;
        _defaultBranch = _gitService.GetDefaultBranch() ?? "main";
    }

    public bool SupportsProjectType(string projectType) => true;

    public async Task GenerateAsync()
    {
        var yaml = new StringBuilder();
        yaml.AppendLine("trigger:");
        yaml.AppendLine($"- {_defaultBranch}");
        yaml.AppendLine();
        yaml.AppendLine("pool:");
        yaml.AppendLine("  vmImage: 'ubuntu-latest'");
        yaml.AppendLine();
        yaml.AppendLine("variables:");
        yaml.AppendLine("  buildConfiguration: 'Release'");
        yaml.AppendLine();
        yaml.AppendLine("steps:");
        yaml.AppendLine("- task: UseDotNet@2");
        yaml.AppendLine("  inputs:");
        yaml.AppendLine($"    version: '{_info.TargetFrameworks.First().Replace("net", "")}.x'");
        yaml.AppendLine();
        
        yaml.AppendLine("- task: DotNetCoreCLI@2");
        yaml.AppendLine("  inputs:");
        yaml.AppendLine("    command: 'restore'");
        yaml.AppendLine("    projects: '**/*.csproj'");
        yaml.AppendLine();
        
        yaml.AppendLine("- task: DotNetCoreCLI@2");
        yaml.AppendLine("  inputs:");
        yaml.AppendLine("    command: 'build'");
        yaml.AppendLine("    projects: '**/*.csproj'");
        yaml.AppendLine("    arguments: '--configuration $(buildConfiguration)'");
        yaml.AppendLine();

        if (_info.HasTests)
        {
            yaml.AppendLine("- task: DotNetCoreCLI@2");
            yaml.AppendLine("  inputs:");
            yaml.AppendLine("    command: 'test'");
            yaml.AppendLine("    projects: '**/*Tests/*.csproj'");
            yaml.AppendLine("    arguments: '--configuration $(buildConfiguration)'");
            yaml.AppendLine();
        }

        if (_info.IsPackageProject)
        {
            yaml.AppendLine("- task: DotNetCoreCLI@2");
            yaml.AppendLine("  inputs:");
            yaml.AppendLine("    command: 'pack'");
            yaml.AppendLine("    packagesToPack: '**/*.csproj'");
            yaml.AppendLine("    configuration: '$(buildConfiguration)'");
            yaml.AppendLine("    nobuild: true");
            yaml.AppendLine();

            yaml.AppendLine("- task: NuGetCommand@2");
            yaml.AppendLine("  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))");
            yaml.AppendLine("  inputs:");
            yaml.AppendLine("    command: 'push'");
            yaml.AppendLine("    packagesToPush: '$(Build.ArtifactStagingDirectory)/**/*.nupkg'");
            yaml.AppendLine("    nuGetFeedType: 'external'");
            yaml.AppendLine("    publishFeedCredentials: 'NuGet'");
        }

        var pipelinePath = Path.Combine(_analyzer.ProjectPath, "azure-pipelines.yml");
        await File.WriteAllTextAsync(pipelinePath, yaml.ToString());
    }
} 