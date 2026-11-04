using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.vrchat;
using net.raitichan.int_parameter_compressor.Context;
using net.raitichan.int_parameter_compressor.Enum;
using net.raitichan.int_parameter_compressor.license;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using VRCParameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;
using Object = UnityEngine.Object;

#nullable enable

namespace net.raitichan.int_parameter_compressor.Pass {
    internal class NativeIntParameterCompressingPass : Pass<NativeIntParameterCompressingPass> {
        private const byte _MIN_BIT_WIDTH = VRCParameter.MinBitWidth;
        private const byte _MAX_BIT_WIDTH = VRCParameter.MaxBitWidth;

        // このツールが書き換えるのはbitWidth(圧縮幅)のみ。flippedRepresentation(符号)は
        // 既存の値を読むだけで、絶対に書き換えない。旧同期プロトコルでは負の値は0として同期されて
        // いたため、同期パラメータが実際に負数を使っているケースは存在しない。符号を自動で
        // 書き換えることは、圧縮の範囲を超えてパラメータの挙動そのものを変えてしまうため。
        // ただし、すでにSigned設定済みのパラメータ(-4~+2 等)を正しくbit幅計算するには
        // 最小値も必要になる。

        private NativeIntParameterCompressingPassInternal _internal = null!;

        private Dictionary<string, (int Min, int Max)> _rangeDict = new();
        private HashSet<string> _autoRangeTargets = new();
        private HashSet<string> _unknownRangeTargets = new();

        private void Initialize() {
            this._rangeDict = new Dictionary<string, (int Min, int Max)>();
            this._autoRangeTargets = new HashSet<string>();
            this._unknownRangeTargets = new HashSet<string>();
            this._internal = new NativeIntParameterCompressingPassInternal();
        }

        protected override void Execute(BuildContext context) {
            if (!License.Check()) return;

            this.Initialize();

            IntParameterCompressor[]? intParameterCompressors = context.AvatarRootObject.GetComponentsInChildren<IntParameterCompressor>();
            if (intParameterCompressors == null) return;
            if (intParameterCompressors.Length <= 0) return;

            bool useWhitelist = intParameterCompressors[0].UseWhitelist;

            foreach (IntParameterCompressor intParameterCompressor in intParameterCompressors) {
                Object.DestroyImmediate(intParameterCompressor);
            }

            VRCAvatarDescriptor descriptor = context.VRChatAvatarDescriptor();
            if (descriptor.expressionParameters == null) return;
            VRCExpressionParameters parameters = descriptor.expressionParameters;

            CompressedParameterContext parameterContext = context.Extension<CompressedParameterContext>();
            Dictionary<string, ResolvedCompressedParameter> contextParameters = parameterContext.State.Parameters;

            Dictionary<string, VRCParameter> targets = this.FindTargetParameters(parameters, contextParameters, useWhitelist);
            if (targets.Count <= 0) return;

            // Autoなパラメータだけ、ExpressionsMenu/AnimatorControllerから値の範囲(最小・最大)を検索する
            if (this._autoRangeTargets.Count > 0) {
                if (descriptor.expressionsMenu != null) {
                    this.FindRangeFromMenu(descriptor.expressionsMenu, this._autoRangeTargets);
                }

                foreach (VRCAvatarDescriptor.CustomAnimLayer customAnimLayer in descriptor.baseAnimationLayers) {
                    switch (customAnimLayer.animatorController) {
                        case AnimatorController controller:
                            this.FindRangeFromAnimatorController(controller, this._autoRangeTargets);
                            break;
                        case AnimatorOverrideController overrideController:
                            this.FindRangeFromAnimatorController(overrideController.runtimeAnimatorController as AnimatorController,
                                                                 this._autoRangeTargets);
                            break;
                    }
                }
            }

            foreach ((string name, VRCParameter parameter) in targets) {
                if (!this.TryResolveBitWidth(name, parameter, contextParameters, out byte width)) continue;

                parameter.bitWidth = width;
                Debug.Log($"Native compress target : {name} , bitWidth = {width}");
            }
        }

        /// <summary>
        /// Int型・同期あり・SyncPointerを含まないパラメータの中から、Native圧縮の対象を選定する。
        /// 対象に選ばれ、かつBitCountがAutoのものは <see cref="_autoRangeTargets"/> に登録する。
        /// </summary>
        private Dictionary<string, VRCParameter> FindTargetParameters(VRCExpressionParameters parameters,
                                                                      Dictionary<string, ResolvedCompressedParameter> contextParameters,
                                                                      bool useWhitelist) {
            Dictionary<string, VRCParameter> targets = new();

            foreach (VRCParameter parameter in parameters.parameters) {
                if (parameter.valueType != VRCExpressionParameters.ValueType.Int) continue;
                if (!parameter.networkSynced) continue;
                if (parameter.name.IndexOf("SyncPointer", StringComparison.OrdinalIgnoreCase) != -1) continue;

                bool defined = contextParameters.TryGetValue(parameter.name, out ResolvedCompressedParameter resolved);
                ParameterCompressionMode mode = defined ? resolved.Mode : ParameterCompressionMode.Auto;

                switch (mode) {
                    case ParameterCompressionMode.Exclude:
                        continue;
                    case ParameterCompressionMode.Compress:
                        // 明示的にCompress指定されたものは、ホワイトリスト設定に関わらず常に対象
                        break;
                    case ParameterCompressionMode.Auto when defined:
                        // CompressedParametersで明示的にAutoと定義されているもの。
                        // ホワイトリスト設定に関わらず、既存bitWidthが8の場合のみ対象。
                        if (parameter.bitWidth != 8) continue;
                        break;
                    case ParameterCompressionMode.Auto:
                        // どこにも定義されていない(defaultのAuto)もの。
                        // ホワイトリストの本来の目的は「未定義のパラメータを勝手に対象に含めない」ことなので、
                        // ホワイトリストONの場合はbitWidthに関わらず常に対象外。
                        if (useWhitelist) continue;
                        if (parameter.bitWidth != 8) continue;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                targets[parameter.name] = parameter;

                int defaultValue = (int)parameter.defaultValue;
                this._rangeDict[parameter.name] = (Mathf.Min(0, defaultValue), Mathf.Max(0, defaultValue));

                BitCount bitCount = defined ? resolved.BitCount : BitCount.Auto;
                if (bitCount == BitCount.Auto) {
                    this._autoRangeTargets.Add(parameter.name);
                }
            }

            return targets;
        }

        /// <summary>
        /// 対象パラメータの最終的なbitWidthを確定する。
        /// 書き込むべきでない(範囲不明・表現不能・既存値と同じで無意味)場合はfalseを返す。
        /// flippedRepresentation(符号)は既存の値を読むだけで、このメソッドでは一切変更しない。
        /// </summary>
        private bool TryResolveBitWidth(string name,
                                        VRCParameter parameter,
                                        Dictionary<string, ResolvedCompressedParameter> contextParameters,
                                        out byte width) {
            bool defined = contextParameters.TryGetValue(name, out ResolvedCompressedParameter resolved);
            BitCount bitCount = defined ? resolved.BitCount : BitCount.Auto;

            int requiredWidth;
            if (bitCount != BitCount.Auto) {
                // 明示指定は検出より優先
                requiredWidth = Math.Max((int)bitCount, _MIN_BIT_WIDTH);
            } else {
                if (this._unknownRangeTargets.Contains(name)) {
                    // ParameterDriverのAdd、または範囲変換なしのCopyにより範囲が確定できなかったもの
                    width = 0;
                    return false;
                }

                (int min, int max) = this._rangeDict[name];
                bool isSigned = parameter.flippedRepresentation; // 既存の符号設定を読むだけ

                if (!isSigned && min < 0) {
                    // Unsigned設定のまま負の値が検出された。符号の変更はこのツールの範囲外なので除外する。
                    Debug.LogWarning(
                        $"Native compress: parameter '{name}' takes a negative value but is set to Unsigned. " +
                        "This tool does not change the sign representation, so it is excluded from native compression.");
                    width = 0;
                    return false;
                }

                requiredWidth = this._internal.GetRequiredBitWidth(min, max, isSigned, _MIN_BIT_WIDTH, _MAX_BIT_WIDTH);
                if (requiredWidth > _MAX_BIT_WIDTH) {
                    // 符号付き/符号なしのどちらでも8bitに収まらない(表現不能)ので除外
                    width = 0;
                    return false;
                }
            }

            if (requiredWidth == parameter.bitWidth) {
                // 既存の値と同じで書き換える意味が無いので除外
                width = 0;
                return false;
            }

            width = (byte)requiredWidth;
            return true;
        }

        private void FoldMin(string name, int value) {
            if (!this._rangeDict.TryGetValue(name, out (int Min, int Max) range)) return;
            this._rangeDict[name] = (Math.Min(range.Min, value), range.Max);
        }

        private void FoldMax(string name, int value) {
            if (!this._rangeDict.TryGetValue(name, out (int Min, int Max) range)) return;
            this._rangeDict[name] = (range.Min, Math.Max(range.Max, value));
        }

        private void FoldBoth(string name, int value) {
            this.FoldMin(name, value);
            this.FoldMax(name, value);
        }

        private void FindRangeFromMenu(VRCExpressionsMenu menu, HashSet<string> targetParameters) {
            HashSet<VRCExpressionsMenu> alreadyVisitMenu = new();
            Queue<VRCExpressionsMenu> queue = new();
            queue.Enqueue(menu);

            while (queue.Count > 0) {
                VRCExpressionsMenu target = queue.Dequeue();
                if (!alreadyVisitMenu.Add(target)) continue;

                foreach (VRCExpressionsMenu.Control control in target.controls) {
                    if (targetParameters.Contains(control.parameter.name)) {
                        this.FoldBoth(control.parameter.name, (int)control.value);
                    }

                    if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
                    if (control.subMenu == null) continue;
                    if (alreadyVisitMenu.Contains(control.subMenu)) continue;
                    queue.Enqueue(control.subMenu);
                }
            }
        }

        private void FindRangeFromAnimatorController(AnimatorController? controller, HashSet<string> targetParameters) {
            if (controller == null) return;

            // ParameterDriverから値の範囲を検索
            VRCAvatarParameterDriver[] drivers = controller.GetBehaviours<VRCAvatarParameterDriver>();
            foreach (VRC_AvatarParameterDriver.Parameter parameter in drivers.SelectMany(driver => driver.parameters)
                                                                             .Where(parameter => targetParameters.Contains(parameter.name))) {
                switch (parameter.type) {
                    case VRC_AvatarParameterDriver.ChangeType.Set:
                        this.FoldBoth(parameter.name, (int)parameter.value);
                        break;
                    case VRC_AvatarParameterDriver.ChangeType.Random:
                        this.FoldBoth(parameter.name, (int)parameter.valueMin);
                        this.FoldBoth(parameter.name, (int)parameter.valueMax);
                        break;
                    case VRC_AvatarParameterDriver.ChangeType.Copy:
                        if (parameter.convertRange) {
                            // 範囲が指定されていれば範囲は明確
                            this.FoldBoth(parameter.name, (int)parameter.destMin);
                            this.FoldBoth(parameter.name, (int)parameter.destMax);
                        } else {
                            // 範囲変換無しのCopyは、コピー元パラメータの範囲次第なので不明として扱う
                            this._unknownRangeTargets.Add(parameter.name);
                            Debug.LogWarning(
                                $"Native compress: parameter '{parameter.name}' is copied without range conversion. Its value range is unknown, so it is excluded from native compression.");
                        }

                        break;
                    case VRC_AvatarParameterDriver.ChangeType.Add:
                        // Addの場合、パラメータが取り得る範囲は不明
                        this._unknownRangeTargets.Add(parameter.name);
                        Debug.LogWarning(
                            $"Native compress: parameter '{parameter.name}' is modified by ParameterDriver Add. Its value range is unknown, so it is excluded from native compression.");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Transitionの条件から値の範囲を検索
            foreach (AnimatorTransitionBase transition in this._internal.GetAllTransitions(controller)) {
                foreach (AnimatorCondition condition in transition.conditions.Where(condition => targetParameters.Contains(condition.parameter))) {
                    switch (condition.mode) {
                        case AnimatorConditionMode.If:
                        case AnimatorConditionMode.IfNot:
                            // Bool/Trigger用の条件なのでInt用途では無視する
                            break;
                        case AnimatorConditionMode.Greater:
                            // 'Greater'の場合、正確な最大値は不明だが、安全のため含めておく(最大値側のみ)
                            this.FoldMax(condition.parameter, (int)condition.threshold + 1);
                            break;
                        case AnimatorConditionMode.Less:
                            // 'Less'の場合、正確な最小値は不明だが、安全のため含めておく(最小値側のみ)
                            this.FoldMin(condition.parameter, (int)condition.threshold - 1);
                            break;
                        case AnimatorConditionMode.Equals:
                            // 'Equals'の場合、その値を実際に取ることが確定しているので、そのまま含める
                            this.FoldBoth(condition.parameter, (int)condition.threshold);
                            break;
                        case AnimatorConditionMode.NotEqual:
                            // 'NotEqual'の場合、正確な範囲は不明だが、安全のため閾値をゼロから
                            // 遠ざける方向(正なら+1、負なら-1)に拡張して含めておく
                            this.FoldBoth(condition.parameter, ExpandAwayFromZero((int)condition.threshold));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
            // アニメーションによって制御されるパラメータは同期対象ではないため考慮不要
        }

        /// <summary>
        /// 値をゼロから遠ざける方向に1だけ拡張する(正なら+1、負なら-1、ゼロはそのまま)。
        /// 'NotEqual'条件のように、正確な境界が不明な場合の防御的な安全マージンとして使う。
        /// </summary>
        private static int ExpandAwayFromZero(int value) {
            return value switch {
                > 0 => value + 1,
                < 0 => value - 1,
                _ => value
            };
        }
    }
}