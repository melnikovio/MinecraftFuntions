
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace MinecraftFuntions
{
    public static class ServerChange
    {
        static string configPath = "config";

        [FunctionName("ServerChange")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "servers")] HttpRequest req,
            TraceWriter log, ExecutionContext context)
        {
            try
            {
                log.Info("Came into.");
                string name = req.Query["name"];
                string replicas = req.Query["replicas"];
                string requestBody = new StreamReader(req.Body).ReadToEnd();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                name = name ?? data?.name;
                replicas = replicas ?? data?.replicas;
                var configFile = Path.Combine(context.FunctionAppDirectory, configPath);
                var file = new FileInfo(configFile);
                var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(file);
                var client = new Kubernetes(config);

                var deployment = SpecsFactory.CreateDeployment(name, int.Parse(replicas));
                await client.ReplaceNamespacedDeployment1WithHttpMessagesAsync(deployment, name, "default");

                return new OkResult();
            }
            catch (Exception e)
            {
               log.Error("Error",e);
                return new BadRequestResult();
            }
        }
    }
}
