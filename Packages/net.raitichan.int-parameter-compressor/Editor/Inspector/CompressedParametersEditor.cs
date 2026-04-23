using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#nullable enable

namespace net.raitichan.int_parameter_compressor.Inspector {
    [CustomEditor(typeof(CompressedParameters))]
    public class CompressedParametersEditor : Editor {
        private const string _PARAMETER_NAME = nameof(CompressedParameters.parameters);

        private ListView _parameterListView = null!;

        public override VisualElement CreateInspectorGUI() {
            VisualElement root = new();

            ListView listView = new() {
                name = "CompressedParameters",
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                showFoldoutHeader = false,
                showAddRemoveFooter = true,
                showBorder = true,
                bindingPath = _PARAMETER_NAME,
                style = { flexGrow = 1 },
                showBoundCollectionSize = false,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.Multiple,
            };
            this._parameterListView = listView;

            listView.RegisterCallback<KeyDownEvent>(this.KeydownCallback);
            root.Add(listView);
            root.Bind(this.serializedObject);
            return root;
        }

        private void KeydownCallback(KeyDownEvent evt) {
            if (evt is not { keyCode: KeyCode.Delete, modifiers: EventModifiers.FunctionKey }) return;
            this.serializedObject.Update();
            SerializedProperty prop = this.serializedObject.FindProperty(_PARAMETER_NAME);
            List<int> indices = this._parameterListView.selectedIndices.ToList();
            foreach (int index in indices.OrderByDescending(i => i)) {
                prop.DeleteArrayElementAtIndex(index);
            }

            this.serializedObject.ApplyModifiedProperties();
            if (indices.Count == 0) {
                EditorApplication.delayCall += () => { this._parameterListView.SetSelectionWithoutNotify(indices); };
            }

            evt.StopPropagation();
        }
    }
}