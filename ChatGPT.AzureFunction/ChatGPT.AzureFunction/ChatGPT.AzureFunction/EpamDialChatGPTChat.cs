using System;
using System.Dynamic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace ChatGPT.AzureFunction;

public static class EpamDialChatGPTChat
{
    [FunctionName("epam-dial-chat-completion")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
    {
        var environmentValue = Environment.GetEnvironmentVariable("dial-chatGPTToken");

        if (string.IsNullOrWhiteSpace(environmentValue))
        {
            return new BadRequestObjectResult("chatGPT token is not provided");
        }

        if (!req.Query.TryGetValue("prompt", out var prompts) || prompts.Count == 0)
        {
            log.LogError("C# HTTP trigger function stops processing the call because prompt is corrupted or not provided");
            return new BadRequestObjectResult("prompt is not provided");
        }
        
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("Api-key", environmentValue);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        dynamic content = new ExpandoObject();
        content.max_tokens = 3000;
        content.temperature = 0.7;
        content.messages = new object[]
        {
            new { role = "system", content = prompts[0] }
        };
        
        var stringContent = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://ai-proxy.lab.epam.com/openai/deployments/gpt-35-turbo/chat/completions?api-version=2023-06-01-preview", stringContent);

        var responseString = await response.Content.ReadAsStringAsync();

        try
        {
            var dynData = JsonConvert.DeserializeObject<dynamic>(responseString);
            var answer = (string)dynData.choices[0].message.content;
            return new OkObjectResult(answer.Replace("\n", ""));
        }
        catch (Exception e)
        {
            log.LogInformation("Could not deserialized Json, error: {0}", e.Message);
            return new BadRequestObjectResult("answer could not be deserialized");
        }
    }
}