# DevOpsExample

## Initial Setup

The following setup is necessary to execute the deployment to DigitalOcean

### Api Token for DigitalOcean

Nuke needs the DigitalOcean Api Token. It must be set as an environment variable:

```$env:DIGITALOCEAN_TOKEN='dop_...'```

### Deploying common infrastructure

The common deployment creates a Docker Registry and a Postgres Cluster. Execute the following command in the solution directory:

```nuke DeployCommon```

If the deployment was successful, you should see the url for the Docker Registry. 

```registry-url : "registry.digitalocean.com/..."```

This Url is necessary for the deployment to latest and stage. It must be set as an environment variable:

```$env:RegistryUrl='registry.digitalocean.com/devops-example'```

## Deploy to latest

After the initial setup, the project can be deployed to the latest stage with the command:

```nuke DeployToLatest```

## Deploy to latest

The current latest version can be deployed to stage with the command:

```nuke DeployLatestToStage```

