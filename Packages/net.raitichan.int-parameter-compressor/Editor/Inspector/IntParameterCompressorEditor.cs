using UnityEditor;

namespace net.raitichan.int_parameter_compressor.Inspector {
	[CustomEditor(typeof(IntParameterCompressor))]
	public class IntParameterCompressorEditor : Editor {

		public override void OnInspectorGUI() {
			this.serializedObject.Update();

			EditorGUILayout.PropertyField(serializedObject.FindProperty("_useWriteDefault"));

			this.serializedObject.ApplyModifiedProperties();
		}
	}
}