using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;
using System.Linq;

namespace UrlShortener
{
    public static class Function1
    {
        [FunctionName("Set")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            [Table("urls", "1", "KEY", Take = 1)]UrlKey urlKey,
            [Table("urls")]CloudTable tableOut,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string href = req.Query["href"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            href = href ?? data?.href;

            if (urlKey == null)
            {
                urlKey = new UrlKey
                {
                    PartitionKey = "1",
                    RowKey = "KEY",
                    Id = 1024
                };

                var operation = TableOperation.Insert(urlKey);
                await tableOut.ExecuteAsync(operation);
            }

            int id = urlKey.Id;
            string Alpha = "ABCDEFJHIJKLMNOPQRSTUVWXYZ";
            string s = "";

            while (id > 0)
            {
                s += Alpha[id % Alpha.Length];
                id /= Alpha.Length;
            }

            string code = string.Join("", s.Reverse());
            var url = new UrlData
            {
                PartitionKey = code[0].ToString(),
                RowKey = code,
                Count = 1,
                Url = href
            };

            urlKey.Id++;

            var oper = TableOperation.Replace(urlKey);
            await tableOut.ExecuteAsync(oper);
            oper = TableOperation.Insert(url);

            await tableOut.ExecuteAsync(oper);

            return href != null
                ? (ActionResult)new OkObjectResult($"You have entered: {href} generated short url: {url.RowKey}")
                : new BadRequestObjectResult("Please pass a href as a param on the query string or in the request body");
        }

        [FunctionName("Locate")]
        public static async Task<IActionResult> Locate(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "Go/{shortUrl}")]HttpRequest req,
            [Table("urls")]CloudTable inputTable,
            string shortUrl,
            [Queue("counts")]IAsyncCollector<string> queue
        )
        {
            if (string.IsNullOrWhiteSpace(shortUrl))
                return new BadRequestResult();

            shortUrl = shortUrl.ToUpper();

            var operation = TableOperation.Retrieve<UrlData>(shortUrl[0].ToString(), shortUrl);
            var result = await inputTable.ExecuteAsync(operation);

            string url = "https://google.com";

            if (result != null && result.Result is UrlData data)
            {
                url = data.Url;
                await queue.AddAsync(data.RowKey);
            }

            return new RedirectResult(url);
        }

        [FunctionName("ProcessQueue")]
        public static async Task ProcessQueue(
            [QueueTrigger("counts")]string shortUrl,
            [Table("urls")]CloudTable inputTable
        )
        {
            var operation = TableOperation.Retrieve<UrlData>(shortUrl[0].ToString(), shortUrl);
            var result = await inputTable.ExecuteAsync(operation);

            if (result.Result != null && result.Result is UrlData data)
            {
                data.Count++;
                operation = TableOperation.Replace(data);
                await inputTable.ExecuteAsync(operation);
            }
        }
    }
}
