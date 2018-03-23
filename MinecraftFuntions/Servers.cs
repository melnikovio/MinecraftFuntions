
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using k8s;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;

namespace MinecraftFuntions
{
    public class Players
    {
        public int max { get; set; }
        public int now { get; set; }
    }


    public class MonitoringData
    {
        public string Name { get; set; }
        public string Ip { get; set; }
        public List<string> Ports { get; set; }
        public string status { get; set; }
        public bool online { get; set; }
        public string motd { get; set; }
        public string error { get; set; }
        public Players players { get; set; }
        //public Server server { get; set; }
        public string last_online { get; set; }
        public string last_updated { get; set; }
        public int duration { get; set; }
    }

        public static class Server
    {

        public static string _urlPrefix = @"https://mcapi.us/server/status?ip=";

        static string configPath = "config";

        [FunctionName(@"servers")]
        public static async System.Threading.Tasks.Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "servers")]HttpRequest req,
            TraceWriter log, ExecutionContext context)
        {
            string configFile = Path.Combine(context.FunctionAppDirectory, configPath);

            FileInfo file = new FileInfo(configFile);

            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(file);
            var client = new Kubernetes(config);

            var result = new List<MonitoringData>();

            var servers = client.ListNamespacedService("default");
          
            foreach (var server in servers.Items)
            {
                if (server.Metadata.Name.Contains("minecraft-server") && server.Status.LoadBalancer.Ingress != null)
                {
                    var serverData = await GetMinecraftServerDataAsync(server.Status.LoadBalancer.Ingress?[0]?.Ip ?? "");

                    serverData.Name = server.Metadata.Name;
                    serverData.Ip = server.Status.LoadBalancer.Ingress?[0]?.Ip ?? "";
                    serverData.Ports = server.Spec.Ports.Select(x => x.Port.ToString()).ToList();

                    result.Add(serverData);
                }
            }

            return (ActionResult)new OkObjectResult(result);
        }

        public static async System.Threading.Tasks.Task<MonitoringData> GetMinecraftServerDataAsync(string ip)
        {
            MonitoringData monitoringData = new MonitoringData();
            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Add(HttpRequestHeader.UserAgent.ToString(), "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2");
                var stream = client.GetStreamAsync(_urlPrefix + ip);
                var serializer = new DataContractJsonSerializer(typeof(MonitoringData));
                monitoringData = serializer.ReadObject(await stream) as MonitoringData;
                //monitoringData.Ip = endpoint.Ip;
                //monitoringData.Port = endpoint.Port.ToString();
            }
            return monitoringData;
        }
    }
}