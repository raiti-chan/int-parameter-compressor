using UnityEditor;
using UnityEngine.UIElements;

namespace net.raitichan.int_parameter_compressor.Inspector {
    [CustomEditor(typeof(CompressedParameters))]
    public class CompressedParametersEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            VisualElement root = new();
            root.Add(new IMGUIContainer(() => {
                this.serializedObject.Update();
                EditorGUILayout.PropertyField(this.serializedObject.FindProperty("parameters"));
                this.serializedObject.ApplyModifiedProperties();
            }));
            return root;
        }
    }
}