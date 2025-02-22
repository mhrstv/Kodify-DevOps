using Kodify.DevOps.Models;
using System.Text;
using System.IO;
namespace Kodify.DevOps.Pipeline
{
    public class GithubActionsGenerator : IPipelineGenerator
    {
        public string PlatformName => "GitHub Actions";

        public bool SupportsProjectType(string projectType) => true;
        private DevOpsAnalyzer _analyzer;
        private PipelineInfo _info;
        public GithubActionsGenerator()
        {
            _analyzer = new DevOpsAnalyzer();
            _info = _analyzer.AnalyzeProjectAsync().Result;
        }
        public GithubActionsGenerator(DevOpsAnalyzer analyzer)
        {
            _analyzer = analyzer;
            _info = _analyzer.AnalyzeProjectAsync().Result;
        }

        public async Task GenerateAsync()
        {
            var yaml = new StringBuilder();
            yaml.AppendLine("name: .NET Package CI/CD");
            yaml.AppendLine();
            yaml.AppendLine("on:");
            yaml.AppendLine("  push:");
            yaml.AppendLine("    branches: [ main ]");
            yaml.AppendLine("  pull_request:");
            yaml.AppendLine("    branches: [ main ]");
            yaml.AppendLine();
            
            yaml.AppendLine("jobs:");
            yaml.AppendLine("  build:");
            yaml.AppendLine("    runs-on: ubuntu-latest");
            yaml.AppendLine("    steps:");
            yaml.AppendLine("    - uses: actions/checkout@v3");
            yaml.AppendLine("    - name: Setup .NET");
            yaml.AppendLine("      uses: actions/setup-dotnet@v3");
            yaml.AppendLine($"      with:");
            yaml.AppendLine($"        dotnet-version: {_info.TargetFrameworks.First().Replace("net", "")}");
            
            yaml.AppendLine("    - name: Restore dependencies");
            yaml.AppendLine("      run: dotnet restore");
            
            yaml.AppendLine("    - name: Build");
            yaml.AppendLine($"      run: dotnet build --configuration {_info.BuildConfiguration} --no-restore");

            if (_info.HasTests)
            {
                yaml.AppendLine("    - name: Test");
                yaml.AppendLine("      run: dotnet test --no-build --verbosity normal");
            }

            if (_info.IsPackageProject)
            {
                yaml.AppendLine("    - name: Pack");
                yaml.AppendLine($"      run: dotnet pack --configuration {_info.BuildConfiguration} --no-build");
                
                yaml.AppendLine("    - name: Publish to NuGet");
                yaml.AppendLine("      if: github.event_name == 'push' && github.ref == 'refs/heads/main'");
                yaml.AppendLine("      run: dotnet nuget push \"**/*.nupkg\" --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }} --skip-duplicate");
            }

            // Create .github/workflows directory if it doesn't exist
            var workflowPath = Path.Combine(_analyzer.ProjectPath, ".github", "workflows");
            Directory.CreateDirectory(workflowPath);

            await File.WriteAllTextAsync(
                Path.Combine(workflowPath, "ci-cd.yml"),
                yaml.ToString()
            );
        }
    }
}