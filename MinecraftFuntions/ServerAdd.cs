
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using k8s;
using System.Collections.Generic;
using k8s.Models;
using System;

namespace MinecraftFuntions
{
    public static class ServerAdd
    {
        static string configPath = "config";

        static Kubernetes client = null;

        [FunctionName(@"serversadd")]
        public static async System.Threading.Tasks.Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "servers/add")]HttpRequest req,
            TraceWriter log, ExecutionContext context)
        {
            string configFile = Path.Combine(context.FunctionAppDirectory, configPath);
            FileInfo file = new FileInfo(configFile);
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(file);
            client = new Kubernetes(config);
            
            string name = req.Query["name"];
            string replicas = req.Query["replicas"];

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
            replicas = replicas ?? data?.replicas;

            if (!string.IsNullOrEmpty(name)) {
                var result = AddServer("minecraft-server-"+name, int.Parse(replicas));
                if (result)
                    return (ActionResult)new OkObjectResult($"Server publish succeded: {name}; Replicas count: {replicas}");
                else
                    return (ActionResult)new OkObjectResult($"Server publish failed: {name}; Replicas count: {replicas}");
            }

            return new BadRequestObjectResult("Please pass a name of server the query string or in the request body");
        }

        public static bool AddServer(string name, int replicasCount)
        {
            try {
                CreateLabeledClaim("default", name + "-storage");
                CreateDeployment(name, name + "-storage", replicasCount);
                CreateService(name, name);

                return true;
            }
            catch (Exception ex) {
                return false;
            }
           
        }

        static void CreateLabeledClaim(string namespacename, string label, string storageClassName = "azurefile", double sizegb = 30d)
        {
           
            //if (client == null)
            //    throw new ArgumentNullException(nameof(client));

            var claim = new k8s.Models.V1PersistentVolumeClaim();
            claim.ApiVersion = "v1";
            claim.Kind = "PersistentVolumeClaim";
            claim.Metadata = new k8s.Models.V1ObjectMeta { Name = label };
            claim.Spec = new k8s.Models.V1PersistentVolumeClaimSpec
            {
                AccessModes = new List<string> {
                    "ReadWriteMany"
                },
                StorageClassName = storageClassName,
                Resources = new k8s.Models.V1ResourceRequirements
                {
                    Requests = new Dictionary<string, k8s.Models.ResourceQuantity>
                    {
                        { "storage", new k8s.Models.ResourceQuantity(sizegb.ToString() + "Gi") }
                    }
                }
            };
            client.CreateNamespacedPersistentVolumeClaim(claim, namespacename);
        }

        public static void CreateDeployment(string instanceName, string storageName, int replicasCount = 1, string image = "openhack/minecraft-server:2.0-alpine", string namespaceParameter = "default")
        {
            var deployment = new Appsv1beta1Deployment()
            {
                ApiVersion = "apps/v1beta1",
                Kind = "Deployment",
                Metadata = new V1ObjectMeta()
                {
                    Name = instanceName
                },
                Spec = new Appsv1beta1DeploymentSpec()
                {
                    Replicas = replicasCount,
                    Template = new V1PodTemplateSpec()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Labels = new Dictionary<string, string> { { "app", instanceName } }
                        },
                        Spec = new V1PodSpec()
                        {
                            Containers = new List<V1Container>()
                            {
                                new V1Container()
                                {
                                    Name = instanceName,
                                    Image = image,
                                    Env = new List<V1EnvVar>()
                                    {
                                        new V1EnvVar()
                                        {
                                            Name = "EULA",
                                            Value = "true"
                                        }
                                    },
                                    Ports = new List<V1ContainerPort>()
                                    {
                                        new V1ContainerPort()
                                        {
                                            ContainerPort = 25565,
                                            Name = "first-port"
                                        },
                                        new V1ContainerPort()
                                        {
                                            ContainerPort = 25575,
                                            Name = "second-port"
                                        }
                                    },
                                    VolumeMounts = new List<V1VolumeMount>()
                                    {
                                        new V1VolumeMount()
                                        {
                                            Name = "azure",
                                            MountPath = "/data"
                                        }
                                    }
                                },

                            },
                            Volumes = new List<V1Volume>()
                            {
                                new V1Volume("azure",
                                    persistentVolumeClaim: new V1PersistentVolumeClaimVolumeSource()
                                    {
                                        ClaimName = storageName
                                    })
                            }
                        }

                    },
                }
            };
            client.CreateNamespacedDeployment1(deployment, namespaceParameter);
        }

        static void CreateService(string name, string appname /*= "minecraft-server2"*/,
          int port1 = 25565, int port2 = 25575,
          string nameport1 = "first-port", string nameport2 = "second-port",
          string targetport1 = "first-port", string targetport2 = "second-port", string namespacename = "default")
        {
            //if (client == null)
            // throw new ArgumentNullException(nameof(client));

            var service = new k8s.Models.V1Service();
            service.ApiVersion = "v1";
            service.Kind = "Service";
            service.Metadata = new k8s.Models.V1ObjectMeta
            {
                Name = name
            };
            service.Spec = new k8s.Models.V1ServiceSpec
            {
                Type = "LoadBalancer",
                Ports = new List<k8s.Models.V1ServicePort>
                {
                    new k8s.Models.V1ServicePort
                    {
                        Port = port1,
                        Name = nameport1,
                        TargetPort = targetport1
                    },
                    new k8s.Models.V1ServicePort
                    {
                        Port = port2,
                        Name = nameport2,
                        TargetPort = targetport2
                    }
                },
                Selector = new Dictionary<string, string>
                {
                    { "app",  appname}
                }
            };

            client.CreateNamespacedService(service, namespacename);
        }


    }
}