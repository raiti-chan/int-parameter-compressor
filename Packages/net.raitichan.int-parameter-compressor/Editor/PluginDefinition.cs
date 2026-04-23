using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using net.raitichan.int_parameter_compressor.Context;
using net.raitichan.int_parameter_compressor.Pass;

#nullable enable

[assembly: ExportsPlugin(typeof(net.raitichan.int_parameter_compressor.PluginDefinition))]

namespace net.raitichan.int_parameter_compressor {
    internal class PluginDefinition : Plugin<PluginDefinition> {
        public override string QualifiedName => "net.raitichan.int-parameter-compressor";
        public override string DisplayName => "Int Parameter Compressor";

        protected override void Configure() {
            Sequence seq = this.InPhase(BuildPhase.Optimizing);
            seq.WithRequiredExtension(typeof(CompressedParameterContext), s => {
                s.Run(CompressedParameterPass.Instance);
                s.Run(IntParameterCompressingPass.Instance);
            });
        }
    }
}