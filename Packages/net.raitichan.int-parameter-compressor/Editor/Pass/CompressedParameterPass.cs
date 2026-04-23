using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using net.raitichan.int_parameter_compressor.Context;
using net.raitichan.int_parameter_compressor.Enum;
using net.raitichan.int_parameter_compressor.Serializable;
using UnityEngine;

#nullable enable

namespace net.raitichan.int_parameter_compressor.Pass {
    internal class CompressedParameterPass : Pass<CompressedParameterPass> {
        protected override void Execute(BuildContext context) {
            CompressedParameterContext parameterContext = context.Extension<CompressedParameterContext>();
            Transform root = context.AvatarRootObject.transform;
            parameterContext.State.Parameters.Clear();

            Dictionary<string, ResolvedCompressedParameter>? flattenParameters = this.FlattenParameters(root);
            if (flattenParameters != null) {
                foreach (KeyValuePair<string, ResolvedCompressedParameter> resolvedCompressedParameter in flattenParameters) {
                    parameterContext.State.Parameters[resolvedCompressedParameter.Key] = resolvedCompressedParameter.Value;
                }
            }

            foreach (KeyValuePair<string, ResolvedCompressedParameter> resolvedCompressedParameter in parameterContext.State.Parameters) {
                Debug.Log(
                    $"Resolved parameter : {resolvedCompressedParameter.Key} , mode = {resolvedCompressedParameter.Value.Mode}, bitCount = {resolvedCompressedParameter.Value.BitCount}");
            }
        }

        private Dictionary<string, ResolvedCompressedParameter>? FlattenParameters(Transform target) {
            List<Dictionary<string, ResolvedCompressedParameter>>? childParameters = null;
            if (target.childCount > 0) {
                for (int i = 0; i < target.childCount; i++) {
                    Dictionary<string, ResolvedCompressedParameter>? childParameter = this.FlattenParameters(target.GetChild(i));
                    if (childParameter == null) continue;
                    childParameters ??= new List<Dictionary<string, ResolvedCompressedParameter>>();
                    childParameters.Add(childParameter);
                }
            }

            bool hasCompressedParameters = target.TryGetComponent(out CompressedParameters compressedParameters);
            switch (hasCompressedParameters) {
                case false when childParameters is not { Count: > 0 }:
                    return null;
                case false when childParameters is { Count: 1 }:
                    return childParameters[0];
            }

            Dictionary<string, ResolvedCompressedParameter> parameterDict = new();

            if (childParameters is { Count: > 0 }) {
                foreach (KeyValuePair<string, ResolvedCompressedParameter> parameter in childParameters.SelectMany(childParameter => childParameter)) {
                    if (!parameterDict.TryGetValue(parameter.Key, out ResolvedCompressedParameter value)) {
                        parameterDict[parameter.Key] = parameter.Value;
                        continue;
                    }

                    parameterDict[parameter.Key] = MergeParameters(value, parameter.Value);
                }
            }

            if (!hasCompressedParameters) return parameterDict;
            foreach (CompressedParameter parameter in compressedParameters.parameters
                         .Where(parameter => !string.IsNullOrWhiteSpace(parameter.name))) {
                if (!parameterDict.TryGetValue(parameter.name, out ResolvedCompressedParameter value)) {
                    parameterDict[parameter.name] = new ResolvedCompressedParameter(parameter.name, parameter.mode, parameter.bitCount);
                    continue;
                }

                parameterDict[parameter.name] = OverrideParameter(value, parameter);
            }

            return parameterDict;
        }

        private static ResolvedCompressedParameter OverrideParameter(
            ResolvedCompressedParameter baseParameter, CompressedParameter overrideParameter) {
            ParameterCompressionMode mode = overrideParameter.mode switch {
                ParameterCompressionMode.Auto => baseParameter.Mode,
                _ => overrideParameter.mode
            };

            BitCount bitCount = overrideParameter.bitCount switch {
                BitCount.Auto => baseParameter.BitCount,
                _ => overrideParameter.bitCount
            };

            return new ResolvedCompressedParameter(overrideParameter.name, mode, bitCount);
        }

        private static ResolvedCompressedParameter MergeParameters(ResolvedCompressedParameter parameter1, ResolvedCompressedParameter parameter2) {
            ParameterCompressionMode mode = GetModePriority(parameter1.Mode) > GetModePriority(parameter2.Mode) ? parameter1.Mode : parameter2.Mode;
            BitCount bitCount = GetBitCountPriority(parameter1.BitCount) > GetBitCountPriority(parameter2.BitCount)
                ? parameter1.BitCount
                : parameter2.BitCount;

            return new ResolvedCompressedParameter(parameter1.Name, mode, bitCount);
        }

        private static int GetModePriority(ParameterCompressionMode mode) {
            return mode switch {
                ParameterCompressionMode.Exclude => 3,
                ParameterCompressionMode.ForceCompress => 2,
                ParameterCompressionMode.Auto => 1,
                _ => 0
            };
        }

        private static int GetBitCountPriority(BitCount bitCount) {
            return (int)bitCount;
        }
    }
}