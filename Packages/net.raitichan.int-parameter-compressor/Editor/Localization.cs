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
            },
            {
                "ui.parameter_compression_mode.label",
                "Parameter compression mode"
            },
            {
                "ui.parameter_compression_mode.auto",
                "Auto"
            },
            {
                "ui.parameter_compression_mode.exclude",
                "Exclude"
            },
            {
                "ui.parameter_compression_mode.compress",
                "Compress"
            },
            {
                "ui.bit_count.label",
                "Maximum value"
            },
            {
                "ui.bit_count.auto",
                "Auto"
            },
            {
                "ui.bit_count.one",
                "1bit (0~1)"
            },
            {
                "ui.bit_count.two",
                "2bit (0~3)"
            },
            {
                "ui.bit_count.three",
                "3bit (0~7)"
            },
            {
                "ui.bit_count.four",
                "4bit (0~15)"
            },
            {
                "ui.bit_count.five",
                "5bit (0~31)"
            },
            {
                "ui.bit_count.six",
                "6bit (0~63)"
            },
            {
                "ui.bit_count.seven",
                "7bit (0~127)"
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
            },
            {
                "ui.parameter_compression_mode.label",
                "パラメータの圧縮モード"
            },
            {
                "ui.parameter_compression_mode.auto",
                "自動"
            },
            {
                "ui.parameter_compression_mode.exclude",
                "圧縮しない"
            },
            {
                "ui.parameter_compression_mode.compress",
                "圧縮する"
            },
            {
                "ui.bit_count.label",
                "最大値"
            },
            {
                "ui.bit_count.auto",
                "自動"
            },
            {
                "ui.bit_count.one",
                "1bit (0~1)"
            },
            {
                "ui.bit_count.two",
                "2bit (0~3)"
            },
            {
                "ui.bit_count.three",
                "3bit (0~7)"
            },
            {
                "ui.bit_count.four",
                "4bit (0~15)"
            },
            {
                "ui.bit_count.five",
                "5bit (0~31)"
            },
            {
                "ui.bit_count.six",
                "6bit (0~63)"
            },
            {
                "ui.bit_count.seven",
                "7bit (0~127)"
            }
        };
    }
}
