using System;
using System.Collections.Generic;
using System.Text;
using k8s.Models;

namespace MinecraftFuntions
{
    internal static class SpecsFactory
    {
        public static V1PersistentVolumeClaim CreateVolumeClaim(string serverName)
        {
            string storageClassName = "azurefile";
            var label = serverName + "-storage";
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
                            {"storage", new ResourceQuantity(30 + "Gi")}
                        }
                    }
                }
            };
            return claim;

        }

        public static V1Service CreateService(string serviceName)
        {
            int port1 = 25565;
            int port2 = 25575;
            string nameport1 = "first-port";
            string nameport2 = "second-port";
            string targetport1 = "first-port"; string targetport2 = "second-port";
            var appname = "minecraft-server-" + serviceName;
            var service = new V1Service
            {
                ApiVersion = "v1",
                Kind = "Service",
                Metadata = new V1ObjectMeta
                {
                    Name = serviceName
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
            return service;
        }

        public static Appsv1beta1Deployment CreateDeployment(string serverName, int replicasCount)
        {
            var image = "openhack/minecraft-server:2.0-alpine";
            var instanceName = "minecraft-server-" + serverName;
            var storageName = serverName + "-storage";
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
                            Labels = new Dictionary<string, string> {{"app", instanceName}}
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
            return deployment;

        }
    }
}
