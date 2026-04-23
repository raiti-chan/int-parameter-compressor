using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using nadena.dev.ndmf;
using nadena.dev.ndmf.localization;
using UnityEngine;

#nullable enable

namespace net.raitichan.int_parameter_compressor.NdmfConsole {
    public class UnknownMaxParameterWarning : SimpleError {
        private readonly HashSet<(string parameterName, ReasonType reasonType)> _unknownMaxWarnings = new();

        public void AddUnknownMaxParameter(string parameterName, ReasonType reasonType) {
            if (this._unknownMaxWarnings.Add((parameterName, reasonType))) {
                Debug.LogWarning($"Unknown maximum value taken by parameter '{parameterName}' in case of '{reasonType}'");
            }
        }

        public override Localizer Localizer => Localization.L;
        public override string TitleKey => "error.unknown_max_parameter";
        public override string[] DetailsSubst => new[] { this.BuildDetails() };
        public override ErrorSeverity Severity => ErrorSeverity.NonFatal;

        public override string FormatDetails() {
            StringBuilder builder = new();
            builder.AppendLine(this.GetLocalizationString($"{this.TitleKey}:description"));
            string[] copyWarnings = this._unknownMaxWarnings
                .Where(warning => warning.reasonType == ReasonType.ParameterDriverCopy)
                .Select(warning => warning.parameterName)
                .ToArray();
            if (copyWarnings.Length != 0) {
                builder.AppendLine(this.GetLocalizationString($"{this.TitleKey}:description-copy"));
                foreach (string parameterName in copyWarnings) {
                    builder.AppendLine($"- {parameterName}");
                }
            }

            string[] addWarnings = this._unknownMaxWarnings
                .Where(warning => warning.reasonType == ReasonType.ParameterDriverAdd)
                .Select(warning => warning.parameterName)
                .ToArray();
            if (addWarnings.Length == 0) return builder.ToString();
            builder.AppendLine(this.GetLocalizationString($"{this.TitleKey}:description-add"));
            foreach (string parameterName in addWarnings) {
                builder.AppendLine($"- {parameterName}");
            }

            return builder.ToString();
        }

        private string GetLocalizationString(string key) {
            return !this.Localizer.TryGetLocalizedString(key, out string? message) ? $"<{key}>" : message;
        }

        private string BuildDetails() {
            return string.Join(
                Environment.NewLine,
                this._unknownMaxWarnings
                    .OrderBy(warning => warning.parameterName)
                    .ThenBy(warning => warning.reasonType)
                    .Select(warning => $"- {warning.parameterName}: {GetReasonText(warning.reasonType)}")
            );
        }

        private static string GetReasonText(ReasonType reasonType) {
            return reasonType switch {
                ReasonType.ParameterDriverCopy => "copied without range conversion",
                ReasonType.ParameterDriverAdd => "modified by ParameterDriver Add",
                _ => throw new ArgumentOutOfRangeException(nameof(reasonType), reasonType, null)
            };
        }

        public enum ReasonType {
            ParameterDriverCopy,
            ParameterDriverAdd
        }
    }
}