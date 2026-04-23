using System;
using net.raitichan.int_parameter_compressor.Enum;
using UnityEngine;

namespace net.raitichan.int_parameter_compressor.Serializable {
    [Serializable]
    public class CompressedParameter {
        [SerializeField]
        public string name;

        [SerializeField]
        public ParameterCompressionMode mode;

        [SerializeField]
        public BitCount bitCount;
    }
}