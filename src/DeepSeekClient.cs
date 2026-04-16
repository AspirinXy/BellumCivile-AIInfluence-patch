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
        /// Synchronous expand for settings test button only.
        /// Returns the expanded description (test button doesn't need title).
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
                var result = Task.Run(() => CallApiAsync(briefDescription, title, kingdomContext,
                    useChinese, apiKey, apiUrl, model)).GetAwaiter().GetResult();
                return result.description ?? briefDescription;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[BC-AI Bridge] DeepSeek expansion failed, using original text: " + ex.Message,
                    Colors.Yellow));
                return briefDescription;
            }
        }

        /// <summary>
        /// Fire-and-forget: writes the event immediately with original text,
        /// then expands via DeepSeek in background and updates the event file.
        /// The game thread is never blocked.
        /// </summary>
        public static void ExpandAndUpdateEventAsync(string eventId, string briefDescription,
            string title, string kingdomContext, bool useChinese)
        {
            var settings = Settings.BellumCivileAIInfluencePatchSettings.Instance;
            string apiKey = settings?.DeepSeekApiKey;

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "sk-xxx")
                return;

            string apiUrl = string.IsNullOrWhiteSpace(settings?.DeepSeekApiUrl)
                ? DEFAULT_API_URL
                : settings.DeepSeekApiUrl;

            string model = string.IsNullOrWhiteSpace(settings?.DeepSeekModel)
                ? DEFAULT_MODEL
                : settings.DeepSeekModel;

            Task.Run(async () =>
            {
                try
                {
                    var result = await CallApiAsync(briefDescription, title, kingdomContext,
                        useChinese, apiKey, apiUrl, model);

                    // Update both title (if provided) and description in our local store
                    AIInfluenceWriter.UpdateEventTitleAndDescription(
                        eventId, result.title, result.description);

                    string newTitleDisplay = string.IsNullOrWhiteSpace(result.title) ? title : result.title;
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[BC-AI Bridge] DeepSeek expansion complete: " + newTitleDisplay,
                        Colors.Cyan));
                }
                catch (Exception ex)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "[BC-AI Bridge] DeepSeek background expansion failed: " + ex.Message,
                        Colors.Yellow));
                }
            });
        }

        private static async Task<(string title, string description)> CallApiAsync(
            string briefDescription, string title, string kingdomContext,
            bool useChinese, string apiKey, string apiUrl, string model)
        {
            string systemPrompt = useChinese
                ? @"你是一位中世纪奇幻世界的史官，负责把简短的政治/军事事件摘要改写成沉浸式编年史叙述，并起一个史诗风格的专属标题。

输出要求（必须返回严格的 JSON 对象，无任何额外文字）：
{
  ""title"": ""四字或六字史诗标题，如'王冠蒙尘'、'旌旗易主'、'金流暗渡'；避免'阴谋败露'这类通用词"",
  ""description"": ""扩写后的事件叙述，150-250字""
}

写作约束（必须严格遵守）：
- 保留输入中的所有人名、地名、数字、日期
- 如果输入以【日期】或 [日期] 标记开头，description 必须以同样日期标记开头
- 用古典史书笔法：加入环境渲染、人物反应、政治影响分析
- **禁止使用百分比（%）、具体数值比率等现代量化语言**，改用叙事描述（例：『军心大振』而非『凝聚力+40』、『朝堂威望扫地』而非『影响力下降 30%』）
- **禁止现代词汇**：如『政治影响力/凝聚力/忠诚度/满意度/效率』等管理学术语；改用『威名/军心/民心/士气/朝望』等古意词
- **王国统治者必须是叙事主体**。地方领主、氏族、大臣、祭司只能作为背景压力来源（如『某氏族向国王施压』），不得成为外交决策主体或直接对他国表态
- 不得编造新的人名或事实
- 标题务必独特：使用比喻、象征、典故；禁用平铺直叙式命名"
                : @"You are a chronicler in a medieval fantasy world. Your task is to expand brief political/military summaries into immersive chronicle prose AND craft an epic, unique title.

Output requirements (return STRICT JSON only, no extra text):
{
  ""title"": ""A 2-4 word epic title, e.g. 'The Crown Beset', 'Banners Bought', 'Gold in Shadow'; avoid generic words like 'Plot Exposed'"",
  ""description"": ""Expanded narrative, 100-200 words""
}

Writing constraints (strict):
- Preserve all names, places, numbers, and dates from the input
- If the input starts with a [date] or 【date】 marker, the description must begin with the same marker
- Chronicle prose: atmosphere, character reactions, political ramifications
- **Forbidden: percentages (%), numeric ratios, or any modern quantitative language.** Use narrative instead ('the host's spirit swelled' not 'cohesion +40'; 'the crown's name lay in ash' not 'influence fell by 30%')
- **Forbidden modern vocabulary**: 'political influence / cohesion / loyalty rating / satisfaction / efficiency' and similar management-speak. Use classical registers: 'renown, resolve, the favor of the court, the hearts of the common folk'
- **Kingdom rulers must be the narrative subjects.** Local lords, clans, ministers, and priests may appear only as background pressure ('a great house pressed its suit upon the king'); they must never act as diplomatic principals or speak for the realm
- Do not invent new names or facts
- The title must be distinctive: use metaphor, symbolism, or allusion; no plain descriptive labels";

            string userPrompt = useChinese
                ? $"原标题（仅供参考，请生成新的专属标题）：{title}\n王国背景：{kingdomContext}\n\n请扩写以下事件摘要：\n{briefDescription}"
                : $"Original title (for reference, generate a new unique one): {title}\nKingdom context: {kingdomContext}\n\nExpand this event summary:\n{briefDescription}";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 600,
                temperature = 0.85,
                response_format = new { type = "json_object" }
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("Authorization", "Bearer " + apiKey);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            string responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"API returned {(int)response.StatusCode}: {responseBody}");

            var json = JObject.Parse(responseBody);
            string content_ = json["choices"]?[0]?["message"]?["content"]?.ToString();

            if (string.IsNullOrWhiteSpace(content_))
                throw new Exception("Empty response from API");

            // Parse the inner JSON object (model returns JSON-as-string in content)
            string newTitle = null;
            string newDescription = null;
            try
            {
                var inner = JObject.Parse(content_);
                newTitle = inner["title"]?.ToString()?.Trim();
                newDescription = inner["description"]?.ToString()?.Trim();
            }
            catch
            {
                // Graceful degradation: if model didn't return valid JSON,
                // treat the whole response as description and keep template title.
                newDescription = content_.Trim();
            }

            // If description came back empty for any reason, fall back to original
            if (string.IsNullOrWhiteSpace(newDescription))
                newDescription = briefDescription;

            return (newTitle, newDescription);
        }
    }
}
