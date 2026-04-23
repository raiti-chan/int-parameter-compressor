using System.Collections.Generic;
using nadena.dev.ndmf;
using net.raitichan.int_parameter_compressor.Enum;
#nullable enable

namespace net.raitichan.int_parameter_compressor.Context {
    public class CompressedParameterContext : IExtensionContext {
        public CompressedParameterState State { get; private set; } = null!;

        public void OnActivate(BuildContext context) {
            this.State = context.GetState<CompressedParameterState>();
        }

        public void OnDeactivate(BuildContext context) {
        }
    }

    public class CompressedParameterState {
        public readonly Dictionary<string, ResolvedCompressedParameter> Parameters = new();
    }

    public readonly struct ResolvedCompressedParameter {
        public readonly string Name;
        public readonly ParameterCompressionMode Mode;
        public readonly BitCount BitCount;

        public ResolvedCompressedParameter(string name, ParameterCompressionMode mode, BitCount bitCount) {
            this.Name = name;
            this.Mode = mode;
            this.BitCount = bitCount;
        }
    }
}
