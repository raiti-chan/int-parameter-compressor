using System;
using System.Collections.Generic;
using nadena.dev.ndmf.localization;

namespace net.raitichan.int_parameter_compressor {
    internal static class Localization {
        public static readonly Localizer L = new(
            "en-US",
            () => new List<(string, Func<string, string>)> {
                ("en-US", key => En.GetValueOrDefault(key)),
                ("ja-JP", key => Ja.GetValueOrDefault(key))
            }
        );

        private static readonly Dictionary<string, string> En = new() {
            {
                "error.unknown_max_parameter",
                "A parameter with an unknown maximum value was found."
            },
            {
                "error.unknown_max_parameter:description",
                "The following parameters will be compressed to the extent possible. \nCompression may cause issues."
            },
            {
                "error.unknown_max_parameter:description-copy",
                "It is copied by ParameterDriver."
            },
            {
                "error.unknown_max_parameter:description-add",
                "It is added by the ParameterDriver."
            },
            {
                "error.unknown_max_parameter:hint",
                "If compression breaks one of these parameters, add it to the exclude list."
            }
        };

        private static readonly Dictionary<string, string> Ja = new() {
            {
                "error.unknown_max_parameter",
                "最大値が不明なパラメータが見つかりました。"
            },
            {
                "error.unknown_max_parameter:description",
                "以下のパラメータは予測できる範囲で圧縮されます。\n圧縮時に問題が起きる可能性があります。"
            },
            {
                "error.unknown_max_parameter:description-copy",
                "ParameterDriverによってコピーされます。"
            },
            {
                "error.unknown_max_parameter:description-add",
                "ParameterDriverによって加算されます。"
            },
            {
                "error.unknown_max_parameter:hint",
                "これらのパラメータの圧縮で問題が起きる場合は、除外リストに追加してください。"
            }
        };
    }
}
