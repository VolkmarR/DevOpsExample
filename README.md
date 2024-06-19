set# DevOpsExample

## Initial Setup

The following setup is necessary to execute the deployment to DigitalOcean

### Api-Tokens 

Nuke needs a DigitalOcean Api-Token and a Pulumi API-Token. Both must be set as environment variables:

```
$env:DIGITALOCEAN_TOKEN='dop_...'
$env:PULUMI_ACCESS_TOKEN='pul_...'
```

### Nuke and Pulumi

Nuke (https://nuke.build/docs/getting-started/installation/) and Pulumi (https://www.pulumi.com/docs/install/) must be installed.

### Deploying common infrastructure

The common deployment creates a Docker Registry and a Postgres Cluster on DigitalOcean. Execute the following command in the solution directory:

```nuke DeployCommon```

If the deployment was successful, you should see the url for the Docker Registry. 

```registry-url : "registry.digitalocean.com/..."```

This Url is necessary for the deployment to latest and stage. It must be set as an environment variable:

```$env:RegistryUrl='registry.digitalocean.com/devops-example'```

### Local developement 

To setup the local password for the postgres database and the user secret for the web app, you need to run the following nuke command:

```nuke InitLocalDB```

## Deployment with github actions

### Setup

GitHub Actions triggers nuke and pulumi. Therefore it is neccessary to define the environment variable as Repository secrets.

* DIGITALOCEAN_TOKEN
* PULUMI_ACCESS_TOKEN
* REGISTRYURL

### Deploy to latest

To deploy to the *latest* environment, you have to create a pull-request. The pull-request triggers the GitHub action **PR-Test.yml**. After completing the PR, the GitHub action **PR-DeployToLatest.yml** is triggered, that creates the docker image and updates the *latest* environment. Alternatively you can trigger the PR-DeployToLatest Workflow manually.

### Deploy latest to stage

To deploy the current docker image from the *latest* environment to the *stage* environment, you have to execute the GitHub action **MAN-DeployLatestToStage.yml**.

## Local testing

Docker Compose is uses for local testing. To start the PostgreSQL Database, execute the following command in the root directory of the solution:

```docker compose up```

Then you can load and start the solution in visual studio.