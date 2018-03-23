
    using System.IO;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.WebJobs.Host;
    using Newtonsoft.Json;

    namespace MinecraftFuntions
    {
        public static class Kube
        {
            [FunctionName("kube")]
            public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "kube")]HttpRequest req, TraceWriter log)
            {


            return (ActionResult)new OkObjectResult($"This is my cube");

                //log.Info("C# HTTP trigger function processed a request.");

                //string name = req.Query["name"];

                //string requestBody = new StreamReader(req.Body).ReadToEnd();
                //dynamic data = JsonConvert.DeserializeObject(requestBody);
                //name = name ?? data?.name;

                //return name != null
                //    ? (ActionResult)new OkObjectResult($"Hello, {name}")
                //    : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
            }

       
        }
    }
