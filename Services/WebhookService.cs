using BepInEx;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DailyQuest.Services;

internal static class WebhookService
{
    private static readonly string CONFIG_DIR = Path.Combine(BepInEx.Paths.ConfigPath, MyPluginInfo.PLUGIN_NAME);
    private static readonly string CONFIG_FILE = Path.Combine(CONFIG_DIR, "webhook_config.json");
    private const int TimeoutSeconds = 10;

    private static bool _loaded;
    private static bool _enabled = false;
    private static string _webhookUrl = "";

    private static readonly object _lock = new();
    private static readonly HttpClient _http = new();

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true
    };

    public static bool IsEnabled
    {
        get
        {
            EnsureLoaded();
            lock (_lock)
                return _enabled;
        }
    }

    public static bool IsConfigured()
    {
        EnsureLoaded();
        lock (_lock)
            return !string.IsNullOrWhiteSpace(_webhookUrl);
    }

    public static void EnsureFilesExist()
    {
        try
        {
            Directory.CreateDirectory(CONFIG_DIR);

            if (!File.Exists(CONFIG_FILE))
            {
                File.WriteAllText(CONFIG_FILE, JsonSerializer.Serialize(CreateDefaultConfig(), JsonWriteOptions));
                Core.Log.LogInfo($"[Webhook] Created config: {CONFIG_FILE}");
            }
        }
        catch (Exception e)
        {
            Core.LogException(e);
        }
    }

    public static bool Reload(out string error)
    {
        try
        {
            EnsureFilesExist();

            var json = File.ReadAllText(CONFIG_FILE);
            var cfg = JsonSerializer.Deserialize<WebhookConfig>(json, JsonReadOptions) ?? CreateDefaultConfig();
            ApplyConfig(cfg);

            error = null;
            Core.Log.LogInfo("[Webhook] webhook_config.json reloaded");
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            Core.LogException(e);
            return false;
        }
    }

    public static bool SetEnabled(bool enabled, out string error)
    {
        try
        {
            EnsureFilesExist();

            WebhookConfig cfg;
            if (File.Exists(CONFIG_FILE))
            {
                var json = File.ReadAllText(CONFIG_FILE);
                cfg = JsonSerializer.Deserialize<WebhookConfig>(json, JsonReadOptions) ?? CreateDefaultConfig();
            }
            else
            {
                cfg = CreateDefaultConfig();
            }

            cfg.Enabled = enabled;

            File.WriteAllText(CONFIG_FILE, JsonSerializer.Serialize(cfg, JsonWriteOptions));
            ApplyConfig(cfg);

            error = null;
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            Core.LogException(e);
            return false;
        }
    }

    public static async Task<(bool ok, string error)> SendAsync(string message, CancellationToken ct = default)
    {
        try
        {
            EnsureLoaded();

            string url;
            bool enabled;

            lock (_lock)
            {
                enabled = _enabled;
                url = _webhookUrl;
            }

            if (!enabled)
                return (false, "Webhook is disabled.");

            if (string.IsNullOrWhiteSpace(url))
                return (false, "Webhook URL is empty.");

            message = (message ?? "").Trim();
            if (message.Length == 0)
                return (false, "Message is empty.");

            if (message.Length > 1990)
                message = message[..1990] + "...";

            var payload = new
            {
                content = message,
                allowed_mentions = new { parse = Array.Empty<string>() }
            };

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            var body = JsonSerializer.Serialize(payload, JsonWriteOptions);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(url, content, linkedCts.Token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return (false, $"Discord returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseText}");
            }

            return (true, null);
        }
        catch (Exception e)
        {
            Core.LogException(e);
            return (false, e.Message);
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
            return;

        lock (_lock)
        {
            if (_loaded)
                return;

            Reload(out _);
        }
    }

    private static WebhookConfig CreateDefaultConfig()
    {
        return new WebhookConfig
        {
            Enabled = false,
            WebhookUrl = ""
        };
    }

    private static void ApplyConfig(WebhookConfig cfg)
    {
        cfg ??= CreateDefaultConfig();

        lock (_lock)
        {
            _enabled = cfg.Enabled;
            _webhookUrl = (cfg.WebhookUrl ?? "").Trim();
            _loaded = true;
        }
    }

    private sealed class WebhookConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("webhookUrl")]
        public string WebhookUrl { get; set; } = "";
    }
}