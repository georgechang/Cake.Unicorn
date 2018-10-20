﻿using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
            var response = ExecuteUnicornAsync(syncUrl, signature, challenge, context.Log).GetAwaiter().GetResult();
            context.Log.Write(Verbosity.Normal, LogLevel.Information, response);
        }

        public static async Task<string> GetChallengeAsync(string controlPanelUrl, ICakeLog log)
        {
            using (var client = new HttpClient())
            {
                var challenge = await client.GetStringAsync($"{controlPanelUrl}?verb=Challenge");
                log.Debug($"Received challenge from remote server: {challenge}");
                return challenge;
            }
        }

        public static string CreateSignature(string challenge, string sharedSecret, string syncUrl, ICakeLog log)
        {
            var service = new SignatureService(sharedSecret);
            var signature = service.CreateSignature(challenge, syncUrl, null);
            log.Debug($"MAC: '{signature.SignatureSource}'");
            log.Debug($"HMAC: '{signature.SignatureHash}'");
            log.Debug("If you get authorization failures compare the values above to the Sitecore logs.");
            return signature.SignatureHash;
        }

        public static async Task<string> ExecuteUnicornAsync(string syncUrl, string signature, string challenge, ICakeLog log)
        {
            log.Write(Verbosity.Normal, LogLevel.Information, "Executing Unicorn...");
            var responseStringBuilder = new StringBuilder();
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-MC-MAC", signature);
                client.DefaultRequestHeaders.Add("X-MC-NONCE", challenge);
                client.Timeout = new TimeSpan(10800000);

                var response = await client.GetAsync(syncUrl);
                log.Verbose(response);

                return response.Content.ToString();
            }
        }
    }
}
