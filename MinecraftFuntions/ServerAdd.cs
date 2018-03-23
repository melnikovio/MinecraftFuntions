
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace MinecraftFuntions
{
    public static class ServerAdd
    {
        static string configPath = "config";

        static Kubernetes client;

        [FunctionName(@"serversadd")]
        public static async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "servers/add")]HttpRequest req,
            TraceWriter log, ExecutionContext context)
        {
            var configFile = Path.Combine(context.FunctionAppDirectory, configPath);
            var file = new FileInfo(configFile);
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(file);
            client = new Kubernetes(config);
            
            string name = req.Query["name"];
            string replicas = req.Query["replicas"];
            
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
            replicas = replicas ?? data?.replicas;
            log.Verbose($"name: {name}, replicas{replicas}");
            if (!string.IsNullOrEmpty(name)) {
                var result = await AddServer(log,"minecraft-server-"+name, int.Parse(replicas));
                if (result)
                    return new OkObjectResult($"Server publish succeded: {name}; Replicas count: {replicas}");
                return new OkObjectResult($"Server publish failed: {name}; Replicas count: {replicas}");
            }

            return new BadRequestObjectResult("Please pass a name of server the query string or in the request body");
        }

        public static async Task<bool> AddServer(TraceWriter log,string name, int replicasCount)
        {
            try {
                await CreateLabeledClaim("default", name + "-storage");
                await CreateDeployment(name, name + "-storage", replicasCount);
                await CreateService(name, name);

                return true;
            }
            catch (Exception ex) {
                log.Error("Error",ex);
                return false;
            }
           
        }

        static async Task CreateLabeledClaim(string namespacename, string label, string storageClassName = "azurefile", double sizegb = 30d)
        {
            var claim = new V1PersistentVolumeClaim
            {
                ApiVersion = "v1",
                Kind = "PersistentVolumeClaim",
                Metadata = new V1ObjectMeta {Name = label},
                Spec = new V1PersistentVolumeClaimSpec
                {
                    AccessModes = new List<string>
                    {
                        "ReadWriteMany"
                    },
                    StorageClassName = storageClassName,
                    Resources = new V1ResourceRequirements
                    {
                        Requests = new Dictionary<string, ResourceQuantity>
                        {
                            {"storage", new ResourceQuantity(sizegb + "Gi")}
                        }
                    }
                }
            };
            await client.CreateNamespacedPersistentVolumeClaimWithHttpMessagesAsync(claim, namespacename);         
        }

        public static async Task CreateDeployment(string instanceName, string storageName, int replicasCount = 1, string image = "openhack/minecraft-server:2.0-alpine", string namespaceParameter = "default")
        {
            var deployment = new Appsv1beta1Deployment
            {
                ApiVersion = "apps/v1beta1",
                Kind = "Deployment",
                Metadata = new V1ObjectMeta
                {
                    Name = instanceName
                },
                Spec = new Appsv1beta1DeploymentSpec
                {
                    Replicas = replicasCount,
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Labels = new Dictionary<string, string> { { "app", instanceName } }
                        },
                        Spec = new V1PodSpec
                        {
                            Containers = new List<V1Container>
                            {
                                new V1Container
                                {
                                    Name = instanceName,
                                    Image = image,
                                    Env = new List<V1EnvVar>
                                    {
                                        new V1EnvVar
                                        {
                                            Name = "EULA",
                                            Value = "true"
                                        }
                                    },
                                    Ports = new List<V1ContainerPort>
                                    {
                                        new V1ContainerPort
                                        {
                                            ContainerPort = 25565,
                                            Name = "first-port"
                                        },
                                        new V1ContainerPort
                                        {
                                            ContainerPort = 25575,
                                            Name = "second-port"
                                        }
                                    },
                                    VolumeMounts = new List<V1VolumeMount>
                                    {
                                        new V1VolumeMount
                                        {
                                            Name = "azure",
                                            MountPath = "/data"
                                        }
                                    }
                                }

                            },
                            Volumes = new List<V1Volume>
                            {
                                new V1Volume("azure",
                                    persistentVolumeClaim: new V1PersistentVolumeClaimVolumeSource
                                    {
                                        ClaimName = storageName
                                    })
                            }
                        }

                    }
                }
            };
            await client.CreateNamespacedDeployment1WithHttpMessagesAsync(deployment, namespaceParameter);            
        }

        static async Task CreateService(string name, string appname /*= "minecraft-server2"*/,
          int port1 = 25565, int port2 = 25575,
          string nameport1 = "first-port", string nameport2 = "second-port",
          string targetport1 = "first-port", string targetport2 = "second-port", string namespacename = "default")
        {
            //if (client == null)
            // throw new ArgumentNullException(nameof(client));

            var service = new V1Service
            {
                ApiVersion = "v1",
                Kind = "Service",
                Metadata = new V1ObjectMeta
                {
                    Name = name
                },
                Spec = new V1ServiceSpec
                {
                    Type = "LoadBalancer",
                    Ports = new List<V1ServicePort>
                    {
                        new V1ServicePort
                        {
                            Port = port1,
                            Name = nameport1,
                            TargetPort = targetport1
                        },
                        new V1ServicePort
                        {
                            Port = port2,
                            Name = nameport2,
                            TargetPort = targetport2
                        }
                    },
                    Selector = new Dictionary<string, string>
                    {
                        {"app", appname}
                    }
                }
            };

           await client.CreateNamespacedServiceWithHttpMessagesAsync(service, namespacename);
        }


    }
}