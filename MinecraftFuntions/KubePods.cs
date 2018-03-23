
using System.Collections.Generic;
using System.IO;
using k8s;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace MinecraftFuntions
{
    public static class KubePods
    {
        static string configPath = "config";

        [FunctionName(@"kubepods")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "kube/pods")]HttpRequest req, 
            TraceWriter log, ExecutionContext context)
        {
            string configFile = Path.Combine(context.FunctionAppDirectory, configPath);

            FileInfo file = new FileInfo(configFile);

            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(file);
            var client = new Kubernetes(config);

            var list = client.ListNamespacedPod("default");
            var result = new List<string>();
            foreach (var item in list.Items)
            {
                result.Add(item.Metadata.Name);
            }
            if (list.Items.Count == 0)
            {
                result.Add("Empty!");
            }

            return new OkObjectResult(result);
        }
    }
}