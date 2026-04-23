using System.Collections.Generic;
using net.raitichan.int_parameter_compressor.Serializable;
using UnityEngine;
using VRC.SDKBase;

namespace net.raitichan.int_parameter_compressor {
    [DisallowMultipleComponent]
    [AddComponentMenu("Raitichan/NDMF/Compressed Parameters")]
    public class CompressedParameters : MonoBehaviour, IEditorOnly {
        [SerializeField]
        public List<CompressedParameter> parameters = new();
    }
}