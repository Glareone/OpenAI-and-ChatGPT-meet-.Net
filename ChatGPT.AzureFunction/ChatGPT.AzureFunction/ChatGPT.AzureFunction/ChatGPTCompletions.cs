namespace ChatGPT.AzureFunction;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Dynamic;

public static class ChatGptCompletions
{
    private const string DefaultChatGptModel = "text-davinci-003";
    private const decimal DefaultChatGptTemperature = 0.5m;

    [FunctionName("completion")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
    {
        var environmentValue = Environment.GetEnvironmentVariable("chatGPTToken");

        if (string.IsNullOrWhiteSpace(environmentValue))
        {
            return new BadRequestObjectResult("chatGPT token is not provided");
        }

        if (!req.Query.TryGetValue("prompt", out var prompts) || prompts.Count == 0)
        {
            log.LogError("C# HTTP trigger function stops processing the call because prompt is corrupted or not provided");
            return new BadRequestObjectResult("prompt is not provided");
        }

        var isTemperatureDeclared = req.Query.TryGetValue("temperature", out var temperatures);

        HttpClient client = new();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {environmentValue}");

        dynamic content = new ExpandoObject();
        content.model = DefaultChatGptModel;
        content.prompt = prompts[0];
        content.temperature = isTemperatureDeclared && Decimal.TryParse(temperatures[0], out var temperature) ? temperature : DefaultChatGptTemperature;
        content.max_tokens = 3000;

        var stringContent = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://api.openai.com/v1/completions", stringContent);

        var responseString = await response.Content.ReadAsStringAsync();

        try
        {
            var dynData = JsonConvert.DeserializeObject<dynamic>(responseString);
            var answer = (string)dynData.choices[0].text;
            return new OkObjectResult(answer.Replace("\n", ""));
        }
        catch (Exception e)
        {
            log.LogInformation("Could not deserialized Json, error: {0}", e.Message);
            return new BadRequestObjectResult("answer could not be deserialized");
        }
    }
}