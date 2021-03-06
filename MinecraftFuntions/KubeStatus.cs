﻿
using System.IO;
using k8s;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace MinecraftFuntions
{
    public static class KubeStatus
    {
        static string configPath = "config";

        [FunctionName(@"kubestatus")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "kube/status")]HttpRequest req, 
            TraceWriter log, ExecutionContext context)
        {
            string configFile = Path.Combine(context.FunctionAppDirectory, configPath);

            FileInfo file = new FileInfo(configFile);

            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(file);
            var client = new Kubernetes(config);

            if (client != null)
                return new OkObjectResult("Kube online");

            return new OkObjectResult("Kube offline");
        }
    }
}