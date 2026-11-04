using net.raitichan.int_parameter_compressor.license;
using UnityEditor;

#nullable enable

namespace net.raitichan.int_parameter_compressor.Inspector {
    [CustomEditor(typeof(IntParameterCompressor))]
    public class IntParameterCompressorEditor : Editor {
        
        private LicenseGUI _licenseGUI = null!;
        
        private void OnEnable() {
            this._licenseGUI = new LicenseGUI();
        }

        public override void OnInspectorGUI() {
            this.serializedObject.Update();
            
            bool isLicenced = this._licenseGUI.OnGUI();
            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
            if (isLicenced) {
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("useWhitelist"));
            } else {
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("useWriteDefault"));
            }

            this.serializedObject.ApplyModifiedProperties();
        }
    }
}