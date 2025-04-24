using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class DeepSeekClient
{
    private readonly string _apiKey = "sk-fe4288c8ecd04b9fac67fea2e319cb50";
    private readonly HttpClient _httpClient;
    private const string ApiUrl = "https://api.deepseek.com/v1/chat/completions"; // Example endpoint

    public DeepSeekClient()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    /// <summary>
    /// Sends a prompt to DeepSeek API and returns the response
    /// </summary>
    /// <param name="prompt">The input text/prompt to send</param>
    /// <returns>The API response as string</returns>
    public async Task<string> GetDeepSeekResponseAsync(string prompt)
    {
        try
        {
            // Create request payload
            var requestData = new
            {
                model = "deepseek-chat", // Example model name
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                temperature = 0.7,
                max_tokens = 2000
            };

            // Serialize to JSON
            var jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Send request
            var response = await _httpClient.PostAsync(ApiUrl, content);

            // Check for success
            response.EnsureSuccessStatusCode();

            // Read and parse response
            var responseJson = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(responseJson);
            
            // Extract the response content (adjust based on actual API response structure)
            var choices = jsonDoc.RootElement.GetProperty("choices");
            var firstChoice = choices[0];
            var message = firstChoice.GetProperty("message");
            var responseText = message.GetProperty("content").GetString();

            return responseText ?? "No response content found";
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"API request failed: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new Exception($"Failed to parse API response: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Unexpected error: {ex.Message}", ex);
        }
    }
}
