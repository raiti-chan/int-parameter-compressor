#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using net.raitichan.int_parameter_compressor.Enum;
using net.raitichan.int_parameter_compressor.Serializable;
using UnityEditor;
using UnityEngine.UIElements;

namespace net.raitichan.int_parameter_compressor.Inspector {
    [CustomPropertyDrawer(typeof(CompressedParameter))]
    public class CompressedParameterDrawer : PropertyDrawer {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) {
            VisualElement root = new();

            TextField parameterName = new() {
                name = "ParameterName",
                bindingPath = nameof(CompressedParameter.name),
                style = { flexGrow = 1 },
                label = ""
            };
            root.Add(parameterName);


            SerializedProperty modeProperty = property.FindPropertyRelative("mode");
            ParameterCompressionMode compressionMode = (ParameterCompressionMode)modeProperty.enumValueIndex;
            DropdownField modeDoropDown = CreateEnumDropDown(compressionMode,
                mode => mode switch {
                    ParameterCompressionMode.Auto => "ui.parameter_compression_mode.auto",
                    ParameterCompressionMode.Exclude => "ui.parameter_compression_mode.exclude",
                    ParameterCompressionMode.Compress => "ui.parameter_compression_mode.compress",
                    _ => mode.ToString()
                },
                mode => {
                    modeProperty.serializedObject.Update();
                    modeProperty.enumValueIndex = (int)mode;
                    modeProperty.serializedObject.ApplyModifiedProperties();
                });
            modeDoropDown.label = GetLocalizationString("ui.parameter_compression_mode.label");
            root.Add(modeDoropDown);

            SerializedProperty bitCountProperty = property.FindPropertyRelative("bitCount");
            BitCount bitCount = (BitCount)bitCountProperty.enumValueIndex;
            DropdownField bitCountDoropDown = CreateEnumDropDown(bitCount,
                count => count switch {
                    BitCount.Auto => "ui.bit_count.auto",
                    BitCount.One => "ui.bit_count.one",
                    BitCount.Two => "ui.bit_count.two",
                    BitCount.Three => "ui.bit_count.three",
                    BitCount.Four => "ui.bit_count.four",
                    BitCount.Five => "ui.bit_count.five",
                    BitCount.Six => "ui.bit_count.six",
                    BitCount.Seven => "ui.bit_count.seven",
                    _ => throw new ArgumentOutOfRangeException(nameof(count), count, null)
                },
                count => {
                    bitCountProperty.serializedObject.Update();
                    bitCountProperty.enumValueIndex = (int)count;
                    bitCountProperty.serializedObject.ApplyModifiedProperties();
                });
            bitCountDoropDown.label = GetLocalizationString("ui.bit_count.label");
            root.Add(bitCountDoropDown);

            return root;
        }

        private static DropdownField CreateEnumDropDown<T>(T currentValue, Func<T, string> toLabel, Action<T> onChanged) where T : System.Enum {
            T[] valuse = System.Enum.GetValues(typeof(T)).Cast<T>().ToArray();
            string[] labels = valuse.Select(toLabel).Select(GetLocalizationString).ToArray();
            Dictionary<string, T> labelToValue = valuse.ToDictionary(v => GetLocalizationString(toLabel(v)), v => v);

            string defaultValue = GetLocalizationString(toLabel(currentValue));

            DropdownField dropdown = new(choices: labels.ToList(), defaultValue: defaultValue) {
                style = {
                    flexGrow = 1
                }
            };
            dropdown.RegisterValueChangedCallback(evt => {
                int index = Array.IndexOf(valuse, labelToValue[evt.newValue]);
                if (index >= 0) {
                    onChanged(valuse[index]);
                }
            });
            return dropdown;
        }

        private static string GetLocalizationString(string key) {
            return !Localization.L.TryGetLocalizedString(key, out string? message) ? $"<{key}>" : message;
        }
    }
}