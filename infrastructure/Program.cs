using System;
using System.Collections.Generic;
using Pulumi;
using Pulumi.DigitalOcean;
using DigitalOcean = Pulumi.DigitalOcean;

return await Deployment.RunAsync(() =>
{
    var outputs = new Dictionary<string, object?>();

    if ("common".Equals(Deployment.Instance.StackName, StringComparison.OrdinalIgnoreCase))
    {
        outputs["registry-url"] = SetupRegistry().Endpoint;
        outputs["db-cluster-id"] = SetupDatabaseCluster().Id;
    }
    else
    {
        var cfg = new Pulumi.Config();
        var dockerTag = cfg.Require("dockerTag");

        var cluster = GetCluster();

        var db = SetupDatabase(cluster);

        var app = SetupApp(cluster, db, dockerTag);

        outputs["LiveUrl"] = app.LiveUrl;
        outputs["dockerTag"] = dockerTag;
    }

    // Export outputs here
    return outputs;
});


static ContainerRegistry SetupRegistry()
    => new ContainerRegistry("registry", new ContainerRegistryArgs
    {
        SubscriptionTierSlug = "starter",
        Name = "devops-example",
        Region = "fra1",
    });

static DatabaseCluster SetupDatabaseCluster()
    => new("devops-example-db-cluster", new()
    {
        Engine = "pg",
        Name = "devops-example-db-cluster",
        NodeCount = 1,
        Region = "fra1",
        Size = "db-s-1vcpu-1gb",
        Version = "17",
    });

static Output<GetDatabaseClusterResult> GetCluster()
    => GetDatabaseCluster.Invoke(new() { Name = "devops-example-db-cluster" });

static DatabaseDb SetupDatabase(Output<GetDatabaseClusterResult> cluster)
    => new($"devops-example-db-{Deployment.Instance.StackName}", new()
    {
        ClusterId = cluster.Apply(v => v.Id),
    });

static App SetupApp(Output<GetDatabaseClusterResult> dbCluster, DatabaseDb db, string dockerTag)
    => new("devops-example-app", new()
    {
        Spec = new DigitalOcean.Inputs.AppSpecArgs
        {
            Alerts = new[]
                {
                    new DigitalOcean.Inputs.AppSpecAlertArgs
                    {
                        Rule = "DEPLOYMENT_FAILED",
                    },
                    new DigitalOcean.Inputs.AppSpecAlertArgs
                    {
                        Rule = "DOMAIN_FAILED",
                    },
                },
            Name = $"devops-example-{Deployment.Instance.StackName}",
            Region = "fra",
            Services = new[]
                {
                    new DigitalOcean.Inputs.AppSpecServiceArgs
                    {
                        Envs = new[]
                        {
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__Host",
                                Value = dbCluster.Apply(v => v.Host),
                            },
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__Port",
                                Value = dbCluster.Apply(v => v.Port.ToString()),
                            },
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__Database",
                                Value = db.Name,
                            },
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__UserName",
                                Value = dbCluster.Apply(v => v.User),
                            },
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__Password",
                                Value = dbCluster.Apply(v => v.Password),
                            },
                        },
                        HttpPort = 8080,
                        Image = new DigitalOcean.Inputs.AppSpecServiceImageArgs
                        {
                            DeployOnPushes = new[]
                            {
                                new DigitalOcean.Inputs.AppSpecServiceImageDeployOnPushArgs
                                {
                                    Enabled = false,
                                },
                            },
                            RegistryType = "DOCR",
                            Repository = "rigo-questions-app",
                            Tag = dockerTag,
                            RegistryCredentials = "",
                        },
                        InstanceSizeSlug = "basic-xxs",
                        Name = "rigo-questions-app",
                    },
                },
        },
    });
