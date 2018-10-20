using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.Diagnostics;
using MicroCHAP;

namespace Cake.Unicorn
{
    public static class Unicorn
    {
        [CakeMethodAlias]
        public static void SyncUnicorn(this ICakeContext context, UnicornSettings settings)
        {
            var syncUrl = $"{settings.ControlPanelUrl}?verb={settings.Verb}&configuration={settings.GetParsedConfigurations()}&skipTransparentConfigs={settings.SkipTransparentConfigs}";

            var challenge = GetChallengeAsync(settings.ControlPanelUrl, context.Log).GetAwaiter().GetResult();
            var signature = CreateSignature(challenge, settings.SharedSecret, syncUrl, context.Log);

            ExecuteUnicornAsync(syncUrl, signature, challenge, context.Log).GetAwaiter().GetResult();
        }

        private static async Task<string> GetChallengeAsync(string controlPanelUrl, ICakeLog log)
        {
            log.Write(Verbosity.Normal, LogLevel.Information, "Fetching authentication token...");
            using (var client = new HttpClient())
            {
                var challenge = await client.GetStringAsync($"{controlPanelUrl}?verb=Challenge");
                log.Debug($"Received challenge from remote server: {challenge}");
                return challenge;
            }
        }

        private static string CreateSignature(string challenge, string sharedSecret, string syncUrl, ICakeLog log)
        {
            var service = new SignatureService(sharedSecret);
            var signature = service.CreateSignature(challenge, syncUrl, null);
            log.Debug($"MAC: '{signature.SignatureSource}'");
            log.Debug($"HMAC: '{signature.SignatureHash}'");
            log.Debug("If you get authorization failures compare the values above to the Sitecore logs.");
            return signature.SignatureHash;
        }

        private static async Task<string> ExecuteUnicornAsync(string syncUrl, string signature, string challenge, ICakeLog log)
        {
            log.Write(Verbosity.Normal, LogLevel.Information, "Executing Unicorn...");
            var responseTextBuilder = new StringBuilder();
            using (var client = new HttpClient())
            {
                log.Debug($"Executing Unicorn sync with signature {signature} and challenge {challenge}.");
                client.DefaultRequestHeaders.Add("X-MC-MAC", signature);
                client.DefaultRequestHeaders.Add("X-MC-NONCE", challenge);
                client.Timeout = new TimeSpan(10800000 * TimeSpan.TicksPerMillisecond);

                var responseStream = await client.GetStreamAsync(syncUrl);
                var responseStreamReader = new StreamReader(responseStream);

                while (!responseStreamReader.EndOfStream)
                {
                    var line = await responseStreamReader.ReadLineAsync();

                    var verbosity = Verbosity.Normal;
                    var logLevel = LogLevel.Information;

                    if (line.StartsWith("Error:"))
                    {
                        line = line.Substring(7);
                        verbosity = Verbosity.Quiet;
                        logLevel = LogLevel.Error;
                    }
                    else if(line.StartsWith("Warning:"))
                    {
                        line = line.Substring(9);
                        verbosity = Verbosity.Normal;
                        logLevel = LogLevel.Warning;
                    }
                    else if(line.StartsWith("Debug:"))
                    {
                        line = line.Substring(7);
                        verbosity = Verbosity.Diagnostic;
                        logLevel = LogLevel.Debug;
                    }
                    else if(line.StartsWith("Info:"))
                    {
                        line = line.Substring(6);
                        verbosity = Verbosity.Verbose;
                        logLevel = LogLevel.Information;
                    }

                    log.Write(verbosity, logLevel, HttpUtility.HtmlDecode(line.Replace("{", "{{").Replace("}", "}}")));

                    responseTextBuilder.AppendLine(line);
                }

                return responseTextBuilder.ToString();
            }
        }
    }
}
