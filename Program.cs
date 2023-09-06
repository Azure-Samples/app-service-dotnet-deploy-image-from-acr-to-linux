// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Docker.DotNet;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.CosmosDB;
using Azure.ResourceManager.CosmosDB.Models;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.Samples.Common;
using System;
using System.IO;
using Microsoft.Azure.Management.Samples.Common;
using Azure.ResourceManager.ContainerRegistry.Models;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ManageLinuxWebAppWithContainerRegistry
{
    public class Program
    {
        /**
         * Azure App Service sample for deploying from an Azure Container Registry.
         *    - Create an Azure Container Registry to be used for holding the Docker images
         *    - If a local Docker engine cannot be found, create a Linux virtual machine that will host a Docker engine
         *        to be used for this sample
         *    - Use Docker Java to create a Docker client that will push/pull an image to/from Azure Container Registry
         *    - Pull a test image from the public Docker repo (tomcat:8-jre8) to be used as a sample for pushing/pulling
         *        to/from an Azure Container Registry
         *    - Deploys to a new web app from the Tomcat image
         */

        public static async Task RunSample(ArmClient client)
        {
            string rgName = Utilities.CreateRandomName("rgACR");
            string acrName = Utilities.CreateRandomName("acrsample");
            string saName = Utilities.CreateRandomName("sa");
            string appName = Utilities.CreateRandomName("webapp");
            string appUrl = appName + ".azurewebsites.net";
            AzureLocation region = AzureLocation.EastUS;
            string dockerImageName = "tomcat";
            string dockerImageTag = "8-jre8";
            string dockerContainerName = "tomcat-privates";
            var lro =await client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdateAsync(Azure.WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
            var resourceGroup = lro.Value;

            try
            {
                //=============================================================
                // Create an Azure Container Registry to store and manage private Docker container images

                Utilities.Log("Creating an Azure Container Registry");

                var registryContainerCollection = resourceGroup.GetContainerRegistries();
                var registryContainerData = new ContainerRegistryData(region, new ContainerRegistrySku(ContainerRegistrySkuName.Standard));
                {
                };
                var registryContainer_lro =await registryContainerCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, acrName, registryContainerData);
                var registryContainer = registryContainer_lro.Value;

                Utilities.Print(registryContainer);

                var acrCredentials = (registryContainer.GetCredentials()).Value;

                //=============================================================
                // Create a Docker client that will be used to push/pull images to/from the Azure Container Registry

                using (DockerClient dockerClient = DockerUtils.CreateDockerClient(client, rgName, region))
                {
                    var pullImgResult = dockerClient.Images.PullImage(
                        new Docker.DotNet.Models.ImagesPullParameters()
                        {
                            Parent = dockerImageName,
                            Tag = dockerImageTag
                        },
                        new Docker.DotNet.Models.AuthConfig());

                    Utilities.Log("List Docker images for: " + dockerClient.Configuration.EndpointBaseUri.AbsoluteUri);
                    var listImages = dockerClient.Images.ListImages(
                        new Docker.DotNet.Models.ImagesListParameters()
                        {
                            All = true
                        });
                    foreach (var img in listImages)
                    {
                        Utilities.Log("\tFound image " + img.RepoTags[0] + " (id:" + img.ID + ")");
                    }

                    var createContainerResult = dockerClient.Containers.CreateContainer(
                        new Docker.DotNet.Models.CreateContainerParameters()
                        {
                            Name = dockerContainerName,
                            Image = dockerImageName + ":" + dockerImageTag
                        });
                    Utilities.Log("List Docker containers for: " + dockerClient.Configuration.EndpointBaseUri.AbsoluteUri);
                    var listContainers = dockerClient.Containers.ListContainers(
                        new Docker.DotNet.Models.ContainersListParameters()
                        {
                            All = true
                        });
                    foreach (var container in listContainers)
                    {
                        Utilities.Log("\tFound container " + container.Names[0] + " (id:" + container.ID + ")");
                    }

                    //=============================================================
                    // Commit the new container

                    string privateRepoUrl = registryContainer.Data.LoginServer + "/samples/" + dockerContainerName;
                    Utilities.Log("Commiting image at: " + privateRepoUrl);

                    var commitContainerResult = dockerClient.Miscellaneous.CommitContainerChanges(
                        new Docker.DotNet.Models.CommitContainerChangesParameters()
                        {
                            ContainerID = dockerContainerName,
                            RepositoryName = privateRepoUrl,
                            Tag = "latest"
                        });

                    //=============================================================
                    // Push the new Docker image to the Azure Container Registry

                    var password = acrCredentials.Passwords[0];
                    var pushImageResult = dockerClient.Images.PushImage(privateRepoUrl,
                        new Docker.DotNet.Models.ImagePushParameters()
                        {
                            ImageID = privateRepoUrl,
                            Tag = "latest"
                        },
                        new Docker.DotNet.Models.AuthConfig()
                        {
                            Username = acrCredentials.Username,
                            Password = password.Value,
                            ServerAddress = registryContainer.Data.LoginServer
                        });

                    //============================================================
                    // Create a web app with a new app service plan

                    Utilities.Log("Creating web app " + appName + " in resource group " + rgName + "...");


                    var webSiteCollection = resourceGroup.GetWebSites();
                    var webSiteData = new WebSiteData(region)
                    {
                        SiteConfig = new Azure.ResourceManager.AppService.Models.SiteConfigProperties()
                        {
                            WindowsFxVersion = "PricingTier.StandardS1",
                            NetFrameworkVersion = "NetFrameworkVersion.V4_6",
                            AppSettings =
                            {
                                new Azure.ResourceManager.AppService.Models.AppServiceNameValuePair()
                                {
                                    Name = "PORT",
                                    Value = "8080"
                                }
                            }
                        }
                    };
                    var webSite_lro =await webSiteCollection.CreateOrUpdateAsync(Azure.WaitUntil.Completed, appName, webSiteData);
                    var webSite = webSite_lro.Value;

                    Utilities.Log("Created web app " + webSite.Data.Name);
                    Utilities.Print(webSite);

                    // warm up
                    Utilities.Log("Warming up " + appUrl + "...");
                    Utilities.CheckAddress("http://" + appUrl);
                    Thread.Sleep(5000);
                    Utilities.Log("CURLing " + appUrl + "...");
                    Utilities.Log(Utilities.CheckAddress("http://" + appUrl));

                }
            }
            finally
            {
                try
                {
                    Utilities.Log("Deleting Resource Group: " + rgName);
                    resourceGroup.Delete(Azure.WaitUntil.Completed);
                    Utilities.Log("Deleted Resource Group: " + rgName);
                }
                catch (Exception)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                // Print selected subscription
                Utilities.Log("Selected subscription: " + client.GetSubscriptions().Id);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e);
            }
        }
    }
}