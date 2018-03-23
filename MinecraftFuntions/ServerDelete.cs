
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using k8s;
using System;

namespace MinecraftFuntions
{
    public static class ServerDelete
    {
        static string configPath = "config";

        static Kubernetes client = null;

        [FunctionName(@"serversdelete")]
        public static async System.Threading.Tasks.Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "servers/delete")]HttpRequest req,
            TraceWriter log, ExecutionContext context)
        {
            string configFile = Path.Combine(context.FunctionAppDirectory, configPath);
            FileInfo file = new FileInfo(configFile);
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(file);
            client = new Kubernetes(config);

            string name = req.Query["name"];

            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            if (!string.IsNullOrEmpty(name))
            {
                var result = DeleteServer(name);
                if (result)
                    return (ActionResult)new OkObjectResult($"Server {name} delete succeded");
                else
                    return (ActionResult)new OkObjectResult($"Server {name} delete failed");
            }

            return new BadRequestObjectResult("Please pass a name of server the query string or in the request body");
        }

        public static bool DeleteServer(string name)
        {
            try
            {
                DeleteDeployment(name);
                DeleteService(name);
                
                DeleteClaim(name + "-storage");

                return true;
            }
            catch (Exception ex) {
                return false;
            }
            
        }

        static void DeleteService(string serviceName, string namespacename = "default")
        {
            client.DeleteNamespacedService(serviceName, namespacename);
        }

        static void DeleteDeployment(string instanceName, string namespaceParameter = "default")
        {
            client.DeleteNamespacedDeployment2(new k8s.Models.V1DeleteOptions()
            {
                //ApiVersion = "v1",
                //Kind = "deployments",
            }, instanceName, namespaceParameter);
        }

        static void DeleteClaim(string claimName, string namespacename = "default")
        {
            client.DeleteNamespacedPersistentVolumeClaim(new k8s.Models.V1DeleteOptions
            {
                //ApiVersion = "v1",
                //Kind = "PersistentVolumeClaim",
            }, claimName, namespacename);
        }
    }
}