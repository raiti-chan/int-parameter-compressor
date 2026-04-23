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
                "If you encounter issues when compressing these parameters, add them to the exclusion list or specify their ranges explicitly."
            },
            {
                "error.unused_compressed_parameter",
                "An asynchronous, non-integer, or undefined parameter has been defined."
            },
            {
                "error.unused_compressed_parameter:description",
                "The following parameters are ignored in the compression/exclusion settings."
            },
            {
                "error.unused_compressed_parameter:hint",
                "Please remove the specification of the above parameters."
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
                "これらのパラメータの圧縮で問題が起きる場合は、除外リストに追加、または明示的に範囲を指定してください。"
            },
            {
                "error.unused_compressed_parameter",
                "非同期、非整数、または定義されていないパラメータが定義されています。"
            },
            {
                "error.unused_compressed_parameter:description",
                "以下のパラメータは圧縮/除外設定から無視されています。"
            },
            {
                "error.unused_compressed_parameter:hint",
                "上記のパラメータの指定を外してください。"
            }
        };
    }
}
