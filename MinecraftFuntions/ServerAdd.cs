
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

        [FunctionName(@"serveradd")]
        public static async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "post", Route = "servers")]HttpRequest req,
            TraceWriter log, ExecutionContext context)
        {
            log.Info("Came into.");
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
                var result = await AddServer(log, name, int.Parse(replicas));
                if (result)
                    return new OkObjectResult($"Server publish succeded: {name}; Replicas count: {replicas}");
                return new OkObjectResult($"Server publish failed: {name}; Replicas count: {replicas}");
            }

            return new BadRequestObjectResult("Please pass a name of server the query string or in the request body");
        }

        public static async Task<bool> AddServer(TraceWriter log,string name, int replicasCount)
        {
            try {
                await CreateLabeledClaim(name);
                await CreateDeployment(name, replicasCount);
                await CreateService(name);
                
                return true;
            }
            catch (Exception ex) {
                log.Error("Error",ex);
                return false;
            }
           
        }

        static async Task CreateLabeledClaim(string serverName)
        {
            var claim = SpecsFactory.CreateVolumeClaim(serverName);
            await client.CreateNamespacedPersistentVolumeClaimWithHttpMessagesAsync(claim,"default");         
        }

        public static async Task CreateDeployment(string instanceName,  int replicasCount = 1)
        {
            var deployment = SpecsFactory.CreateDeployment(instanceName, replicasCount);
            await client.CreateNamespacedDeployment1WithHttpMessagesAsync(deployment, "default");            
        }

        static async Task CreateService(string name)
        {
            //if (client == null)
            // throw new ArgumentNullException(nameof(client));

            var service = SpecsFactory.CreateService(name);
           await client.CreateNamespacedServiceWithHttpMessagesAsync(service, "default");
        }


    }
}