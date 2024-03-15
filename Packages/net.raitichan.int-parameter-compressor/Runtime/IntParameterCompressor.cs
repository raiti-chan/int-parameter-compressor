using UnityEngine;
using VRC.SDKBase;

namespace net.raitichan.int_parameter_compressor {
	
	[AddComponentMenu("Raitichan/NDMF/Int Parameter Compressor")]
	public class IntParameterCompressor : MonoBehaviour, IEditorOnly {
		
		[SerializeField]
		private WriteDefault _useWriteDefault;
		public WriteDefault UseWriteDefault => _useWriteDefault;

		public enum WriteDefault {
			Auto,
			Off,
			On,
			
		}
		
	}
}