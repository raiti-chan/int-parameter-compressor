#nullable enable
using System.Collections.Generic;
using System.Text;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;

namespace net.raitichan.int_parameter_compressor.NdmfConsole {
    public class MissingCompressedParameterWarning : SimpleError {
        
        private readonly HashSet<string> _missingParameters = new();
        
        public void AddMissingParameter(string parameterName) {
            this._missingParameters.Add(parameterName);
        }
        
        public override Localizer Localizer => Localization.L;
        public override string TitleKey => "error.unused_compressed_parameter";
        public override ErrorSeverity Severity => ErrorSeverity.NonFatal;

        public override string FormatDetails() {
            StringBuilder builder = new();
            builder.AppendLine(this.GetLocalizationString($"{this.TitleKey}:description"));
            foreach (string parameterName in this._missingParameters) {
                builder.AppendLine($"- {parameterName}");
            }
            return builder.ToString();
        }
        
        private string GetLocalizationString(string key) {
            return !this.Localizer.TryGetLocalizedString(key, out string? message) ? $"<{key}>" : message;
        }
    }
}
