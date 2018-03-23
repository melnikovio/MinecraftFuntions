
using System;
using System.IO;
using System.Threading.Tasks;
using k8s;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace MinecraftFuntions
{
    public static class ServerDelete
    {
        private static string configPath = "config";

        private static Kubernetes client;

        [FunctionName(@"serversdelete")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "servers/delete")]HttpRequest req,
            TraceWriter log, ExecutionContext context)
        {
            string configFile = Path.Combine(context.FunctionAppDirectory, configPath);
            var file = new FileInfo(configFile);
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(file);
            client = new Kubernetes(config);

            string name = req.Query["name"];

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            log.Verbose($"Request\n{requestBody}");
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
            log.Verbose($"name: {name}");
            if (!string.IsNullOrEmpty(name))
            {
                var result = await DeleteServer(log,name);
                if (result)
                    return new OkObjectResult($"Server {name} delete succeded");
                return new OkObjectResult($"Server {name} delete failed");
            }

            return new BadRequestObjectResult("Please pass a name of server the query string or in the request body");
        }

        public static async Task<bool> DeleteServer(TraceWriter log, string name)
        {
            try
            {
                await DeleteService(name);
                await DeleteDeployment(name);                              
                await DeleteClaim(name + "-storage");
                return true;
            }
            catch (Exception ex) {
                log.Error("Error",ex);
                return false;
            }
            
        }

        private static async Task DeleteService(string serviceName, string namespacename = "default")
        {
            await client.DeleteNamespacedServiceWithHttpMessagesAsync(serviceName, namespacename);
        }

        private static async Task DeleteDeployment(string instanceName, string namespaceParameter = "default")
        {
            await client.DeleteNamespacedDeployment1WithHttpMessagesAsync(null, instanceName, namespaceParameter);

        }

        private static async Task DeleteClaim(string claimName, string namespacename = "default")
        {
            await client.DeleteNamespacedPersistentVolumeClaimWithHttpMessagesAsync(null, claimName, namespacename);            
        }
    }
}