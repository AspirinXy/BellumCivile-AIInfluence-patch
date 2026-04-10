using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.Library;

namespace BellumCivileAIInfluencePatch
{
    public static class DeepSeekClient
    {
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private const string DEFAULT_API_URL = "https://api.deepseek.com/v1/chat/completions";
        private const string DEFAULT_MODEL = "deepseek-chat";

        /// <summary>
        /// Expands a brief event description into immersive narrative text using DeepSeek.
        /// Returns the original text if API call fails or is not configured.
        /// </summary>
        public static string ExpandDescription(string briefDescription, string title,
            string kingdomContext, bool useChinese)
        {
            var settings = Settings.BellumCivileAIInfluencePatchSettings.Instance;
            string apiKey = settings?.DeepSeekApiKey;

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "sk-xxx")
                return briefDescription;

            string apiUrl = string.IsNullOrWhiteSpace(settings?.DeepSeekApiUrl)
                ? DEFAULT_API_URL
                : settings.DeepSeekApiUrl;

            string model = string.IsNullOrWhiteSpace(settings?.DeepSeekModel)
                ? DEFAULT_MODEL
                : settings.DeepSeekModel;

            try
            {
                string result = ExpandAsync(briefDescription, title, kingdomContext,
                    useChinese, apiKey, apiUrl, model).GetAwaiter().GetResult();
                return result;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[BC-AI Bridge] DeepSeek expansion failed, using original text: " + ex.Message,
                    Colors.Yellow));
                return briefDescription;
            }
        }

        private static async Task<string> ExpandAsync(string briefDescription, string title,
            string kingdomContext, bool useChinese, string apiKey, string apiUrl, string model)
        {
            string language = useChinese ? "中文" : "English";
            string systemPrompt = useChinese
                ? @"你是一个中世纪奇幻世界的历史编年史家。你的任务是将简短的政治/军事事件摘要扩写为沉浸式的叙事文本。

要求：
- 保持所有人名、地名、数字等关键事实不变
- 用古典史书的笔法，加入环境描写、人物反应、政治分析
- 字数控制在150-250字之间
- 不要添加虚构的人名或事实
- 直接输出扩写后的文本，不要加任何前缀或解释"
                : @"You are a chronicler in a medieval fantasy world. Your task is to expand brief political/military event summaries into immersive narrative text.

Requirements:
- Keep all names, places, and numbers unchanged
- Write in the style of medieval chronicles with atmosphere, reactions, and political analysis
- Keep the text between 100-200 words
- Do not invent new names or facts
- Output only the expanded text, no prefixes or explanations";

            string userPrompt = useChinese
                ? $"事件标题：{title}\n王国背景：{kingdomContext}\n\n请扩写以下事件摘要：\n{briefDescription}"
                : $"Event title: {title}\nKingdom context: {kingdomContext}\n\nExpand this event summary:\n{briefDescription}";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 500,
                temperature = 0.8
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("Authorization", "Bearer " + apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API returned {(int)response.StatusCode}: {responseBody}");
            }

            var json = JObject.Parse(responseBody);
            string expanded = json["choices"]?[0]?["message"]?["content"]?.ToString();

            if (string.IsNullOrWhiteSpace(expanded))
            {
                throw new Exception("Empty response from API");
            }

            return expanded.Trim();
        }
    }
}
