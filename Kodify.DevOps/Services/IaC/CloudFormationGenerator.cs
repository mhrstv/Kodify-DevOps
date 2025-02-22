using System.Text;
using Kodify.DevOps.Models;
using Kodify.AutoDoc.Repository;

namespace Kodify.DevOps.IaC;

public class CloudFormationGenerator : IIaCGenerator
{
    public string PlatformName => "AWS CloudFormation";
    private readonly DevOpsAnalyzer _analyzer;
    private readonly PipelineInfo _info;
    private readonly string _projectName;
    private readonly Dictionary<string, string> _detectedServices;

    public CloudFormationGenerator(DevOpsAnalyzer analyzer = null)
    {
        _analyzer = analyzer ?? new DevOpsAnalyzer();
        _info = _analyzer.AnalyzeProjectAsync().Result;
        _projectName = _info.PackageId ?? Path.GetFileName(_analyzer.ProjectPath);
        _detectedServices = DetectRequiredServices();
    }

    private Dictionary<string, string> DetectRequiredServices()
    {
        var services = new Dictionary<string, string>();
        
        if (_info.Environment.RequiresDatabase)
        {
            if (_info.Dependencies.Any(d => d.Contains("Npgsql")))
                services["database"] = "postgres";
            else if (_info.Dependencies.Any(d => d.Contains("SqlClient")))
                services["database"] = "sqlserver";
            else if (_info.Dependencies.Any(d => d.Contains("MySql")))
                services["database"] = "mysql";
        }

        if (_info.Dependencies.Any(d => d.Contains("StackExchange.Redis")))
            services["cache"] = "redis";

        if (_info.Dependencies.Any(d => d.Contains("AWSSQS")))
            services["queue"] = "sqs";

        return services;
    }

    public async Task GenerateTemplateAsync()
    {
        var template = new StringBuilder();
        
        // Add CloudFormation template header
        template.AppendLine("AWSTemplateFormatVersion: '2010-09-09'");
        template.AppendLine("Description: 'Infrastructure for .NET application'");
        template.AppendLine();

        // Add parameters
        template.AppendLine("Parameters:");
        template.AppendLine("  Environment:");
        template.AppendLine("    Type: String");
        template.AppendLine("    AllowedValues: [dev, staging, prod]");
        template.AppendLine("    Description: Environment name");
        template.AppendLine();
        
        if (_detectedServices.ContainsKey("database"))
        {
            template.AppendLine("  DBInstanceClass:");
            template.AppendLine("    Type: String");
            template.AppendLine("    Default: db.t3.micro");
            template.AppendLine("    Description: Database instance class");
        }

        // Add conditions
        template.AppendLine("Conditions:");
        template.AppendLine("  IsProd: !Equals [!Ref Environment, prod]");
        template.AppendLine();

        // Add resources
        template.AppendLine("Resources:");
        
        // VPC and networking
        template.AppendLine("  VPC:");
        template.AppendLine("    Type: AWS::EC2::VPC");
        template.AppendLine("    Properties:");
        template.AppendLine("      CidrBlock: 10.0.0.0/16");
        template.AppendLine("      EnableDnsHostnames: true");
        template.AppendLine("      EnableDnsSupport: true");
        template.AppendLine("      Tags:");
        template.AppendLine("        - Key: Name");
        template.AppendLine($"          Value: !Sub ${_projectName}-${{Environment}}-vpc");
        template.AppendLine();

        // Add subnets
        template.AppendLine("  PrivateSubnet1:");
        template.AppendLine("    Type: AWS::EC2::Subnet");
        template.AppendLine("    Properties:");
        template.AppendLine("      VpcId: !Ref VPC");
        template.AppendLine("      CidrBlock: 10.0.1.0/24");
        template.AppendLine("      AvailabilityZone: !Select [0, !GetAZs '']");
        template.AppendLine();

        template.AppendLine("  PrivateSubnet2:");
        template.AppendLine("    Type: AWS::EC2::Subnet");
        template.AppendLine("    Properties:");
        template.AppendLine("      VpcId: !Ref VPC");
        template.AppendLine("      CidrBlock: 10.0.2.0/24");
        template.AppendLine("      AvailabilityZone: !Select [1, !GetAZs '']");
        template.AppendLine();

        // Add database if required
        if (_detectedServices.TryGetValue("database", out var dbType))
        {
            template.AppendLine("  DBSubnetGroup:");
            template.AppendLine("    Type: AWS::RDS::DBSubnetGroup");
            template.AppendLine("    Properties:");
            template.AppendLine("      DBSubnetGroupDescription: Subnet group for RDS");
            template.AppendLine("      SubnetIds:");
            template.AppendLine("        - !Ref PrivateSubnet1");
            template.AppendLine("        - !Ref PrivateSubnet2");
            template.AppendLine();

            template.AppendLine("  DBSecurityGroup:");
            template.AppendLine("    Type: AWS::EC2::SecurityGroup");
            template.AppendLine("    Properties:");
            template.AppendLine("      GroupDescription: Security group for RDS");
            template.AppendLine("      VpcId: !Ref VPC");
            template.AppendLine("      SecurityGroupIngress:");
            template.AppendLine("        - IpProtocol: tcp");
            template.AppendLine($"          FromPort: {GetDefaultDbPort(dbType)}");
            template.AppendLine($"          ToPort: {GetDefaultDbPort(dbType)}");
            template.AppendLine("          CidrIp: 10.0.0.0/16");
            template.AppendLine();

            template.AppendLine("  Database:");
            template.AppendLine("    Type: AWS::RDS::DBInstance");
            template.AppendLine("    Properties:");
            template.AppendLine($"      Engine: {GetDbEngine(dbType)}");
            template.AppendLine($"      EngineVersion: {GetLatestEngineVersion(dbType)}");
            template.AppendLine("      DBInstanceClass: !Ref DBInstanceClass");
            template.AppendLine("      AllocatedStorage: 20");
            template.AppendLine("      DBSubnetGroupName: !Ref DBSubnetGroup");
            template.AppendLine("      VPCSecurityGroups:");
            template.AppendLine("        - !Ref DBSecurityGroup");
            template.AppendLine("      MultiAZ: !If [IsProd, true, false]");
            template.AppendLine("      BackupRetentionPeriod: !If [IsProd, 30, 7]");
            template.AppendLine("      DeletionProtection: !If [IsProd, true, false]");
        }

        // Add ECS if Docker is required
        if (_info.Environment.RequiresDocker)
        {
            template.AppendLine("  ECSCluster:");
            template.AppendLine("    Type: AWS::ECS::Cluster");
            template.AppendLine("    Properties:");
            template.AppendLine($"      ClusterName: !Sub ${_projectName}-${{Environment}}");
            template.AppendLine();

            template.AppendLine("  ECSTaskExecutionRole:");
            template.AppendLine("    Type: AWS::IAM::Role");
            template.AppendLine("    Properties:");
            template.AppendLine("      AssumeRolePolicyDocument:");
            template.AppendLine("        Version: '2012-10-17'");
            template.AppendLine("        Statement:");
            template.AppendLine("          - Effect: Allow");
            template.AppendLine("            Principal:");
            template.AppendLine("              Service: ecs-tasks.amazonaws.com");
            template.AppendLine("            Action: sts:AssumeRole");
            template.AppendLine("      ManagedPolicyArns:");
            template.AppendLine("        - arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy");
        }

        // Add outputs
        template.AppendLine("Outputs:");
        template.AppendLine("  VpcId:");
        template.AppendLine("    Description: VPC ID");
        template.AppendLine("    Value: !Ref VPC");
        template.AppendLine();

        if (_detectedServices.ContainsKey("database"))
        {
            template.AppendLine("  DatabaseEndpoint:");
            template.AppendLine("    Description: Database endpoint");
            template.AppendLine("    Value: !GetAtt Database.Endpoint.Address");
        }

        if (_info.Environment.RequiresDocker)
        {
            template.AppendLine("  ECSClusterArn:");
            template.AppendLine("    Description: ECS Cluster ARN");
            template.AppendLine("    Value: !GetAtt ECSCluster.Arn");
        }

        // Create infrastructure directory and files
        var infraPath = Path.Combine(_analyzer.ProjectPath, "iac");
        Directory.CreateDirectory(infraPath);

        // Write main template
        await File.WriteAllTextAsync(
            Path.Combine(infraPath, "template.yaml"),
            template.ToString()
        );

        // Create README
        await GenerateReadme(infraPath);
    }

    private string GetDbEngine(string dbType) => dbType switch
    {
        "postgres" => "postgres",
        "mysql" => "mysql",
        "sqlserver" => "sqlserver-ex",
        _ => "postgres"
    };

    private string GetLatestEngineVersion(string dbType) => dbType switch
    {
        "postgres" => "14.6",
        "mysql" => "8.0.28",
        "sqlserver" => "15.00",
        _ => "14.6"
    };

    private int GetDefaultDbPort(string dbType) => dbType switch
    {
        "postgres" => 5432,
        "mysql" => 3306,
        "sqlserver" => 1433,
        _ => 5432
    };

    private async Task GenerateReadme(string infraPath)
    {
        var readme = new StringBuilder();
        readme.AppendLine("# AWS CloudFormation Infrastructure");
        readme.AppendLine("\n## Project Infrastructure");
        readme.AppendLine($"This infrastructure is automatically generated for {_projectName} based on project analysis.");
        readme.AppendLine("\nDetected requirements:");
        
        foreach (var service in _detectedServices)
        {
            readme.AppendLine($"- {service.Key}: {service.Value}");
        }

        readme.AppendLine("\n## Prerequisites");
        readme.AppendLine("- AWS CLI configured");
        readme.AppendLine("- Appropriate AWS permissions");

        readme.AppendLine("\n## Deployment Instructions");
        readme.AppendLine("1. Create an S3 bucket for templates (if not exists):");
        readme.AppendLine("   ```bash");
        readme.AppendLine($"   aws s3 mb s3://{_projectName}-cfn-templates");
        readme.AppendLine("   ```");

        readme.AppendLine("\n2. Package the template:");
        readme.AppendLine("   ```bash");
        readme.AppendLine($"   aws cloudformation package \\");
        readme.AppendLine("     --template-file template.yaml \\");
        readme.AppendLine($"     --s3-bucket {_projectName}-cfn-templates \\");
        readme.AppendLine("     --output-template-file packaged.yaml");
        readme.AppendLine("   ```");

        readme.AppendLine("\n3. Deploy the stack:");
        readme.AppendLine("   ```bash");
        readme.AppendLine($"   aws cloudformation deploy \\");
        readme.AppendLine("     --template-file packaged.yaml \\");
        readme.AppendLine($"     --stack-name {_projectName}-${{ENVIRONMENT}} \\");
        readme.AppendLine("     --parameter-overrides \\");
        readme.AppendLine("       Environment=dev \\");
        readme.AppendLine("     --capabilities CAPABILITY_IAM");
        readme.AppendLine("   ```");

        readme.AppendLine("\n## Best Practices");
        readme.AppendLine("- Use different stacks for each environment");
        readme.AppendLine("- Enable multi-AZ for production databases");
        readme.AppendLine("- Use appropriate instance sizes per environment");
        readme.AppendLine("- Enable backup retention based on environment");
        readme.AppendLine("- Use proper IAM roles and policies");

        await File.WriteAllTextAsync(
            Path.Combine(infraPath, "README.md"),
            readme.ToString()
        );
    }
} 