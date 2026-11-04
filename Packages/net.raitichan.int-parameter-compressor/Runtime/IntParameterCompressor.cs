using UnityEngine;
using VRC.SDKBase;

namespace net.raitichan.int_parameter_compressor {
	
	[AddComponentMenu("Raitichan/NDMF/Int Parameter Compressor")]
	public class IntParameterCompressor : MonoBehaviour, IEditorOnly {
		
		[SerializeField]
		private WriteDefault useWriteDefault;
		public WriteDefault UseWriteDefault => this.useWriteDefault;

		[SerializeField]
		private bool useWhitelist;
		public bool UseWhitelist => this.useWhitelist;

		public enum WriteDefault {
			Auto,
			Off,
			On,
			
		}
		
	}
}