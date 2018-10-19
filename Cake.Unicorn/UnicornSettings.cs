using System;
using System.Collections.Generic;
using System.Text;
using Cake.Core.Tooling;

namespace Cake.Unicorn
{
    public class UnicornSettings : ToolSettings
    {
        public string ControlPanelUrl { get; set; }
        public string SharedSecret { get; set; }
        public IEnumerable<string> Configurations { get; set; }
        public bool SkipTransparentConfigs { get; set; } = false;
        public string Verb { get; set; } = "Sync";
        public string GetParsedConfigurations() => Configurations != null ? string.Join("^", Configurations) : string.Empty;
    }
}
