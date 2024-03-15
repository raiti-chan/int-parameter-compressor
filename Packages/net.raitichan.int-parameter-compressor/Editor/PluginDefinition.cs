using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;

#nullable enable

[assembly: ExportsPlugin(typeof(net.raitichan.int_parameter_compressor.PluginDefinition))]

namespace net.raitichan.int_parameter_compressor {
	internal class PluginDefinition : Plugin<PluginDefinition> {
		public override string QualifiedName => "net.raitichan.int-parameter-compressor";
		public override string DisplayName => "Int Parameter Compressor";

		protected override void Configure() {
			Sequence seq = InPhase(BuildPhase.Optimizing);
			seq.Run(IntParameterCompressingPass.Instance);
		}
	}
}