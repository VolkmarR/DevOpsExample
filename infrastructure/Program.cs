using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Hosting;
using Pulumi;
using DigitalOcean = Pulumi.DigitalOcean;

return await Deployment.RunAsync(() =>
{
    var outputs = new Dictionary<string, object?>();

    if ("registry".Equals(Deployment.Instance.StackName, StringComparison.OrdinalIgnoreCase))
    {

        var containerRegistry = new DigitalOcean.ContainerRegistry("registry", new DigitalOcean.ContainerRegistryArgs
        {
            SubscriptionTierSlug = "starter",
            Name = "devops-example",
            Region = "fra1",
        });
        outputs["registry-url"] = containerRegistry.Endpoint;
    }
    else
    {
        var reg = new StackReference($"{Deployment.Instance.OrganizationName}/{Deployment.Instance.ProjectName}/registry");

        var dbCluster = new DigitalOcean.DatabaseCluster("devops-example-db-cluster", new()
        {
            Engine = "pg",
            Name = "devops-example-db-cluster",
            NodeCount = 1,
            Region = "fra1",
            Size = "db-s-1vcpu-1gb",
            Version = "15",
        });

        var db = new DigitalOcean.DatabaseDb("devops-example-db", new()
        {
            ClusterId = dbCluster.Id,
        });

        var app = new DigitalOcean.App("devops-example-app", new()
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
                Name = "devops-example",
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
                                Value = dbCluster.Host,
                            },
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__Port",
                                Value = dbCluster.Port.Apply(v => v.ToString()),
                            },
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__Database",
                                Value = db.Name,
                            },
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__UserName",
                                Value = dbCluster.User,
                            },
                            new DigitalOcean.Inputs.AppSpecServiceEnvArgs
                            {
                                Key = "DB__Password",
                                Value = dbCluster.Password,
                            },
                        },
                        HttpPort = 8080,
                        Image = new DigitalOcean.Inputs.AppSpecServiceImageArgs
                        {
                            DeployOnPushes = new[]
                            {
                                new DigitalOcean.Inputs.AppSpecServiceImageDeployOnPushArgs
                                {
                                    Enabled = true,
                                },
                            },
                            RegistryType = "DOCR",
                            Repository = "rigo-questions-app",
                            Tag = "latest",
                        },
                        InstanceSizeSlug = "basic-xxs",
                        Name = "rigo-questions-app",
                    },
                },
            },
        });

        outputs["LiveUrl"] = app.LiveUrl;
    }

    // Export outputs here
    return outputs;
});

