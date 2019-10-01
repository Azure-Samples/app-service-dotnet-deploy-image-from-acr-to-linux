---
page_type: sample
languages:
- csharp
products:
- azure
extensions:
- services: App-Service
- platforms: dotnet
description: "Azure App Service sample for deploying from an Azure Container Registry."
---

# Deploy a container image from Azure Container Registry to Linux containers in App Service using C#

 Azure App Service sample for deploying from an Azure Container Registry.
    - Create an Azure Container Registry to be used for holding the Docker images
    - If a local Docker engine cannot be found, create a Linux virtual machine that will host a Docker engine
        to be used for this sample
    - Use Docker Java to create a Docker client that will push/pull an image to/from Azure Container Registry
    - Pull a test image from the public Docker repo (tomcat:8-jre8) to be used as a sample for pushing/pulling
        to/from an Azure Container Registry
    - Deploys to a new web app from the Tomcat image


## Running this Sample ##

To run this sample:

Set the environment variable `AZURE_AUTH_LOCATION` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-libraries-for-net/blob/master/AUTH.md).

```bash
git clone https://github.com/Azure-Samples/app-service-dotnet-deploy-image-from-acr-to-linux.git
cd app-service-dotnet-deploy-image-from-acr-to-linux
dotnet build
bin\Debug\net452\ManageLinuxWebAppWithContainerRegistry.exe
```

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/tree/Fluent)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
