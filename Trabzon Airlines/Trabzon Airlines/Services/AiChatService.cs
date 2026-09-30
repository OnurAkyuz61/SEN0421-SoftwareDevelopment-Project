using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Trabzon_Airlines.Services
{
    // Sohbet geçmişindeki tek bir mesaj
    // Single message in chat history
    public class ChatTurn
    {
        public string Role { get; set; }    // "user" | "assistant"
        public string Content { get; set; }
    }

    // Yapay zeka sağlayıcısına istek atan servis
    // Supported providers (Web.config -> AI:Provider): gemini | openai | anthropic
    public static class AiChatService
    {
        // HttpClient tek instance olmalı (socket tükenmesini önler)
        private static readonly HttpClient Http;

        static AiChatService()
        {
            // .NET Framework 4.7.2'de TLS 1.2'yi garanti et
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        }

        private static string Setting(string key, string fallback = null)
        {
            var v = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();
        }

        public static bool IsConfigured
        {
            get
            {
                var key = Setting("AI:ApiKey");
                return !string.IsNullOrEmpty(key) && !key.StartsWith("BURAYA");
            }
        }

        public static async Task<string> GetReplyAsync(string systemPrompt, IList<ChatTurn> history)
        {
            var provider = (Setting("AI:Provider", "gemini")).ToLowerInvariant();
            var apiKey = Setting("AI:ApiKey");

            switch (provider)
            {
                case "openai":
                    return await CallOpenAi(apiKey, Setting("AI:Model", "gpt-4o-mini"), systemPrompt, history);
                case "anthropic":
                case "claude":
                    return await CallAnthropic(apiKey, Setting("AI:Model", "claude-haiku-4-5"), systemPrompt, history);
                default:
                    return await CallGemini(apiKey, Setting("AI:Model", "gemini-2.5-flash"), systemPrompt, history);
            }
        }

        // ---------------- GOOGLE GEMINI ----------------
        private static async Task<string> CallGemini(string apiKey, string model, string system, IList<ChatTurn> history)
        {
            var body = new
            {
                system_instruction = new { parts = new[] { new { text = system } } },
                contents = history.Select(h => new
                {
                    role = h.Role == "assistant" ? "model" : "user",
                    parts = new[] { new { text = h.Content } }
                }).ToArray(),
                generationConfig = new { temperature = 0.6, maxOutputTokens = 2048 }
            };

            var url = "https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent";
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = Json(body) };
            req.Headers.Add("x-goog-api-key", apiKey);

            var json = await Send(req);
            var parts = json.SelectToken("candidates[0].content.parts") as JArray;
            if (parts == null) return null;
            return string.Concat(parts.Select(p => (string)p["text"] ?? ""));
        }

        // ---------------- OPENAI ----------------
        private static async Task<string> CallOpenAi(string apiKey, string model, string system, IList<ChatTurn> history)
        {
            var messages = new List<object> { new { role = "system", content = system } };
            messages.AddRange(history.Select(h => (object)new { role = h.Role, content = h.Content }));

            var body = new { model = model, messages = messages, temperature = 0.6, max_tokens = 800 };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions") { Content = Json(body) };
            req.Headers.Add("Authorization", "Bearer " + apiKey);

            var json = await Send(req);
            return (string)json.SelectToken("choices[0].message.content");
        }

        // ---------------- ANTHROPIC CLAUDE ----------------
        private static async Task<string> CallAnthropic(string apiKey, string model, string system, IList<ChatTurn> history)
        {
            var body = new
            {
                model = model,
                max_tokens = 800,
                system = system,
                messages = history.Select(h => new { role = h.Role, content = h.Content }).ToArray()
            };

            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages") { Content = Json(body) };
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            var json = await Send(req);
            var content = json["content"] as JArray;
            if (content == null) return null;
            return string.Concat(content.Where(c => (string)c["type"] == "text").Select(c => (string)c["text"]));
        }

        // ---------------- YARDIMCILAR ----------------
        private static StringContent Json(object o)
        {
            return new StringContent(JsonConvert.SerializeObject(o), Encoding.UTF8, "application/json");
        }

        private static async Task<JObject> Send(HttpRequestMessage req)
        {
            using (var res = await Http.SendAsync(req).ConfigureAwait(false))
            {
                var text = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!res.IsSuccessStatusCode)
                {
                    // Detaylı hata sadece Output penceresine yazılır, kullanıcıya gösterilmez
                    System.Diagnostics.Debug.WriteLine("[AI] " + (int)res.StatusCode + " " + text);
                    throw new HttpRequestException("AI API error " + (int)res.StatusCode);
                }
                return JObject.Parse(text);
            }
        }
    }
}
