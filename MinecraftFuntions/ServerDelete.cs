
using System;
using System.IO;
using System.Linq;
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
    public static class ServerDelete
    {
        private static string configPath = "config";

        private static Kubernetes client;

        [FunctionName(@"serversdelete")]
        public static async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "servers")]HttpRequest req,
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
                var result = await DeleteServer(log, name);
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
                
                await DeleteService(log,name);
                await DeleteDeployment(log,name);                              
                await DeleteClaim(log,name);
                return true;
            }
            catch (Exception ex) {
                log.Error("Error",ex);
                return false;
            }
            
        }

        private static async Task DeleteService(TraceWriter log, string serviceName, string namespacename = "default")
        {
            log.Verbose($"deleting service: {serviceName}");
            await client.DeleteNamespacedServiceWithHttpMessagesAsync(serviceName, namespacename);
        }

        private static async Task DeleteDeployment(TraceWriter log, string instanceName, string namespaceParameter = "default")
        {
            var depName = "minecraft-server-" + instanceName;
            log.Verbose($"deleting deploy: {depName}");

            var dep = SpecsFactory.CreateDeployment(instanceName, 0);
            await client.ReplaceNamespacedDeployment1WithHttpMessagesAsync(dep, depName, "default");

            var res = await client.DeleteNamespacedDeployment3WithHttpMessagesAsync(new V1DeleteOptions() ,
               "minecraft-server-" + instanceName, namespaceParameter,propagationPolicy:"Background");

            var depls =  await client.ReadNamespacedDeployment3WithHttpMessagesAsync(depName,namespaceParameter);
            if (depls.Response?.IsSuccessStatusCode??true)
            {
                var sets =
                    await client.ListReplicaSetForAllNamespaces2WithHttpMessagesAsync(labelSelector: "app="+depName);
                foreach (var st in sets.Body.Items)
                {
                    log.Verbose($"deleting {st.Metadata.Name}");
                    st.Spec.Replicas = 0;
                    await client.ReplaceNamespacedReplicaSet2WithHttpMessagesAsync(st, st.Metadata.Name,
                        "default");
                    await client.DeleteNamespacedReplicaSet2WithHttpMessagesAsync(new V1DeleteOptions(), st.Metadata.Name,
                        "default");
                }
            }
            else
            {
                log.Verbose($"deleted it says");
            }
           log.Verbose($"{res.Response?.StatusCode} {res.Response?.ReasonPhrase} {res.Response?.Content}"); 
        }

        private static async Task DeleteClaim(TraceWriter log, string claimName, string namespacename = "default")
        {
            log.Verbose($"deleting claim: {claimName + "-storage"}");
            await client.DeleteNamespacedPersistentVolumeClaimWithHttpMessagesAsync(new V1DeleteOptions(), claimName + "-storage", namespacename);            
        }
    }
}