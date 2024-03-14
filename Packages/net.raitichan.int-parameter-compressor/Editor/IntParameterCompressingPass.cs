using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

#nullable enable

namespace net.raitichan.int_parameter_compressor {
	internal class IntParameterCompressingPass : Pass<IntParameterCompressingPass> {
		private Dictionary<string, int>? _maxValueDict;

		protected override void Execute(BuildContext context) {
			if (context.AvatarDescriptor.expressionParameters == null) return;
			this.FindTargetParameter(context.AvatarDescriptor);

			if (this._maxValueDict == null) return;
			if (this._maxValueDict.Count <= 0) return;

			List<VRCExpressionParameters.Parameter> appendVRCParameters = new();
			List<AnimatorControllerParameter> appendParameters = new();
			List<BlendTree> appendBlendTrees = new();


			foreach ((string targetParameter, int value) in this._maxValueDict) {
				int useBitSize = value switch {
					< 2 => 1,
					< 4 => 2,
					< 8 => 3,
					< 16 => 4,
					< 32 => 5,
					< 64 => 6,
					_ => 7
				};
				Debug.Log($"Compress target : {targetParameter} , max = {value}, bitSize = {useBitSize}");

				// Do not synchronize target parameters
				foreach (VRCExpressionParameters.Parameter parameter in context.AvatarDescriptor.expressionParameters.parameters) {
					if (parameter.name == targetParameter) {
						parameter.networkSynced = false;
					}
				}

				// Add Parameter
				for (int i = 0; i < useBitSize; i++) {
					string paramName = $"{targetParameter}.bit_{i}";
					appendVRCParameters.Add(new VRCExpressionParameters.Parameter {
						name = paramName,
						defaultValue = 0,
						saved = false,
						valueType = VRCExpressionParameters.ValueType.Bool,
						networkSynced = true
					});

					appendParameters.Add(new AnimatorControllerParameter {
						name = paramName,
						type = AnimatorControllerParameterType.Float
					});
				}

				appendParameters.Add(new AnimatorControllerParameter {
					name = $"{targetParameter}.float",
					type = AnimatorControllerParameterType.Float
				});

				// Generate Assets
				ChildMotion[] childMotions = new ChildMotion [useBitSize];
				for (int i = 0; i < useBitSize; i++) {
					ChildMotion c = childMotions[i];
					c.directBlendParameter = $"{targetParameter}.bit_{i}";

					AnimationClip decodeClip = new() {
						name = c.directBlendParameter
					};

					AnimationCurve curve = new();
					curve.AddKey(0, 1 << i);
					decodeClip.SetCurve("", typeof(Animator), $"{targetParameter}.float", curve);

					AssetDatabase.AddObjectToAsset(decodeClip, context.AssetContainer);
					c.motion = decodeClip;
					childMotions[i] = c;
				}

				BlendTree blendTree = new() {
					blendType = BlendTreeType.Direct,
					name = $"{targetParameter}_decoder",
					children = childMotions
				};

				AssetDatabase.AddObjectToAsset(blendTree, context.AssetContainer);
				appendBlendTrees.Add(blendTree);
			}

			appendParameters.Add(new AnimatorControllerParameter {
				name = "__int-parameter-compressor_dummy_parameter",
				type = AnimatorControllerParameterType.Float,
				defaultFloat = 1.0f
			});


			// Add BlendTree
			ChildMotion[] decodeMotions = appendBlendTrees
				.Select(tree => new ChildMotion {
					motion = tree,
					directBlendParameter = "__int-parameter-compressor_dummy_parameter"
				})
				.ToArray();
			BlendTree decodeBlendTree = new() {
				blendType = BlendTreeType.Direct,
				name = "IntParameterDecoder",
				children = decodeMotions
			};

			AssetDatabase.AddObjectToAsset(decodeBlendTree, context.AssetContainer);

			// Add parameter
			context.AvatarDescriptor.expressionParameters.parameters = context.AvatarDescriptor.expressionParameters.parameters.Concat(appendVRCParameters).ToArray();

			{
				// Process FX Layer
				RuntimeAnimatorController? runtimeController = context.AvatarDescriptor.baseAnimationLayers
					.FirstOrDefault(layer => layer.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
				AnimatorController? controller = runtimeController switch {
					AnimatorOverrideController overrideController => overrideController.runtimeAnimatorController as AnimatorController,
					AnimatorController animatorController => animatorController,
					_ => null
				};
				// Avatars with no FX layer present do not run.
				if (controller == null) return;

				// Add Parameter to controller
				foreach (AnimatorControllerParameter parameter in appendParameters) {
					controller.AddParameter(parameter);
				}

				{
					// Add decode layer
					AnimatorStateMachine decodeStateMachine = new() {
						name = "IntParameterDecoder",
						entryPosition = Vector3.zero,
						exitPosition = Vector3.down * 60,
						anyStatePosition = Vector3.up * 60
					};

					AnimatorState decodeState = new() {
						name = decodeStateMachine.name,
						motion = decodeBlendTree,
						writeDefaultValues = true,
					};
					AssetDatabase.AddObjectToAsset(decodeState, context.AssetContainer);

					decodeStateMachine.AddState(decodeState, Vector3.right * 230);
					decodeStateMachine.defaultState = decodeState;
					AssetDatabase.AddObjectToAsset(decodeStateMachine, context.AssetContainer);

					AnimatorControllerLayer decodeLayer = new() {
						name = decodeStateMachine.name,
						stateMachine = decodeStateMachine,
						defaultWeight = 1.0f
					};

					controller.AddLayer(decodeLayer);
				}

				{
					// Add ParameterValue provide layer
					AnimatorStateMachine provideStateMachine = new() {
						name = "IntValueProvider",
						entryPosition = Vector3.zero,
						exitPosition = Vector3.down * 60,
						anyStatePosition = Vector3.up * 60
					};

					AnimationClip waitClip = new() { name = "wait" };
					AnimationCurve curve = new();
					curve.AddKey(0f, 0);
					curve.AddKey(0.2f, 0);
					waitClip.SetCurve("dummy", typeof(GameObject), "dummy", curve);
					AssetDatabase.AddObjectToAsset(waitClip, context.AssetContainer);

					AnimatorState entryState = new() { name = "entry", writeDefaultValues = false };
					AssetDatabase.AddObjectToAsset(entryState, context.AssetContainer);
					AnimatorState remoteState_0 = new() { name = "remote_0", motion = waitClip, writeDefaultValues = false };
					AssetDatabase.AddObjectToAsset(remoteState_0, context.AssetContainer);
					AnimatorState remoteState_1 = new() { name = "remote_1", motion = waitClip, writeDefaultValues = false };
					AssetDatabase.AddObjectToAsset(remoteState_1, context.AssetContainer);
					AnimatorState localState = new() { name = "local", motion = waitClip, writeDefaultValues = false };
					AssetDatabase.AddObjectToAsset(localState, context.AssetContainer);

					provideStateMachine.AddState(entryState, Vector3.right * 230);
					provideStateMachine.AddState(remoteState_0, new Vector3(230, -60));
					provideStateMachine.AddState(remoteState_1, new Vector3(230, -120));
					provideStateMachine.AddState(localState, new Vector3(230, 60));

					provideStateMachine.defaultState = entryState;

					AnimatorStateTransition entryToRemote_0 = entryState.AddTransition(remoteState_0);
					entryToRemote_0.AddCondition(AnimatorConditionMode.IfNot, 1, "IsLocal");
					entryToRemote_0.duration = 0;

					AnimatorStateTransition entryToLocal = entryState.AddTransition(localState);
					entryToLocal.AddCondition(AnimatorConditionMode.If, 1, "IsLocal");
					entryToLocal.duration = 0;

					AnimatorStateTransition remote_0ToRemote_1 = remoteState_0.AddTransition(remoteState_1);
					remote_0ToRemote_1.hasExitTime = true;
					remote_0ToRemote_1.exitTime = 1;
					remote_0ToRemote_1.duration = 0;

					AnimatorStateTransition remote_1ToRemote_0 = remoteState_1.AddTransition(remoteState_0);
					remote_1ToRemote_0.hasExitTime = true;
					remote_1ToRemote_0.exitTime = 1;
					remote_1ToRemote_0.duration = 0;

					VRCAvatarParameterDriver remoteState_0Driver = remoteState_0.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
					VRCAvatarParameterDriver remoteState_1Driver = remoteState_1.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

					foreach (string key in this._maxValueDict.Keys) {
						remoteState_0Driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter {
							type = VRC_AvatarParameterDriver.ChangeType.Copy,
							source = $"{key}.float",
							name = $"{key}"
						});

						remoteState_1Driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter {
							type = VRC_AvatarParameterDriver.ChangeType.Copy,
							source = $"{key}.float",
							name = $"{key}"
						});
					}

					AssetDatabase.AddObjectToAsset(provideStateMachine, context.AssetContainer);

					AnimatorControllerLayer provideLayer = new() {
						name = provideStateMachine.name,
						stateMachine = provideStateMachine,
						defaultWeight = 0
					};

					controller.AddLayer(provideLayer);
				}

				{
					// Add Encode Layer
					foreach ((string parameterName, int maxValue) in this._maxValueDict) {
						if (controller.parameters.All(parameter => parameter.name != parameterName)) {
							controller.AddParameter(parameterName, AnimatorControllerParameterType.Int);
						}
						
						int useBitSize = maxValue switch {
							< 2 => 1,
							< 4 => 2,
							< 8 => 3,
							< 16 => 4,
							< 32 => 5,
							< 64 => 6,
							_ => 7
						};

						AnimatorStateMachine encodeStateMachine = new() {
							name = $"{parameterName}_encode",
							entryPosition = Vector3.zero,
							exitPosition = Vector3.down * 60,
							anyStatePosition = Vector3.up * 60
						};

						AnimatorState entryState = new() { name = "entry", writeDefaultValues = false };
						AssetDatabase.AddObjectToAsset(entryState, context.AssetContainer);
						AnimatorState remoteState = new() { name = "remote", writeDefaultValues = false };
						AssetDatabase.AddObjectToAsset(remoteState, context.AssetContainer);
						AnimatorState localState = new() { name = "local", writeDefaultValues = false };
						AssetDatabase.AddObjectToAsset(localState, context.AssetContainer);

						encodeStateMachine.AddState(entryState, Vector3.right * 230);
						encodeStateMachine.AddState(remoteState, new Vector3(230, -60));
						encodeStateMachine.AddState(localState, new Vector3(230, 60));

						encodeStateMachine.defaultState = entryState;

						AnimatorStateTransition entryToRemote = entryState.AddTransition(remoteState);
						entryToRemote.AddCondition(AnimatorConditionMode.IfNot, 1, "IsLocal");
						entryToRemote.duration = 0;

						AnimatorStateTransition entryToLocal = entryState.AddTransition(localState);
						entryToLocal.AddCondition(AnimatorConditionMode.If, 1, "IsLocal");
						entryToLocal.duration = 0;

						AssetDatabase.AddObjectToAsset(encodeStateMachine, context.AssetContainer);

						AnimatorControllerLayer encodeLayer = new() {
							name = encodeStateMachine.name,
							stateMachine = encodeStateMachine,
							defaultWeight = 0
						};

						for (int i = 0; i <= maxValue; i++) {
							AnimatorState encodeState = new() { name = $"encode_{i}", writeDefaultValues = false };
							AssetDatabase.AddObjectToAsset(encodeState, context.AssetContainer);
							encodeStateMachine.AddState(encodeState, new Vector3(230 * (i + 1), 180));

							VRCAvatarParameterDriver driver = encodeState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
							for (int bit = 0; bit < useBitSize; bit++) {
								driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter {
									type = VRC_AvatarParameterDriver.ChangeType.Set,
									name = $"{parameterName}.bit_{bit}",
									value = (i >> bit) & 1
								});
							}

							AnimatorStateTransition localToEncode = localState.AddTransition(encodeState);
							AnimatorStateTransition encodeToLocal = encodeState.AddTransition(localState);
							
							// TODO: High speed with tree structure
							localToEncode.AddCondition(AnimatorConditionMode.Equals, i, parameterName);
							localToEncode.duration = 0;
							encodeToLocal.AddCondition(AnimatorConditionMode.NotEqual, i, parameterName);
							encodeToLocal.duration = 0;
						}

						controller.AddLayer(encodeLayer);
					}
				}
			}
		}

		private void FindTargetParameter(VRCAvatarDescriptor avatar) {
			if (avatar.expressionParameters == null) return;
			VRCExpressionParameters parameters = avatar.expressionParameters;

			this._maxValueDict = parameters.parameters
				.Where(parameter => parameter.valueType == VRCExpressionParameters.ValueType.Int && parameter.networkSynced)
				.ToDictionary(parameter => parameter.name, parameter => (int)parameter.defaultValue);

			// Find the maximum value of the parameter from the Menu
			this.FindMaxValueFromMenu(avatar.expressionsMenu);

			foreach (string key in this._maxValueDict.Where(pair => pair.Value > 127)
				         .Select(pair => pair.Key)
				         .ToArray()) {
				this._maxValueDict.Remove(key);
			}

			if (this._maxValueDict.Count <= 0) return;

			// Find the maximum value of the parameter from the Animator Controller
			foreach (VRCAvatarDescriptor.CustomAnimLayer customAnimLayer in avatar.baseAnimationLayers) {
				switch (customAnimLayer.animatorController) {
					case AnimatorController controller:
						this.FindMaxValueFromAnimatorController(controller);
						break;
					case AnimatorOverrideController overrideController:
						this.FindMaxValueFromAnimatorController(overrideController.runtimeAnimatorController as AnimatorController);
						break;
				}
			}

			foreach (string key in this._maxValueDict.Where(pair => pair.Value > 127)
				         .Select(pair => pair.Key)
				         .ToArray()) {
				this._maxValueDict.Remove(key);
			}
		}

		private void FindMaxValueFromMenu(VRCExpressionsMenu menu) {
			if (this._maxValueDict == null) return;
			HashSet<VRCExpressionsMenu> alreadyVisitMenu = new();
			Queue<VRCExpressionsMenu> queue = new();
			queue.Enqueue(menu);

			while (queue.Count > 0) {
				VRCExpressionsMenu target = queue.Dequeue();
				if (!alreadyVisitMenu.Add(target)) continue;

				foreach (VRCExpressionsMenu.Control control in target.controls) {
					if (this._maxValueDict.Keys.Any(name => name == control.parameter.name)) {
						int val = this._maxValueDict[control.parameter.name];
						val = (int)Mathf.Max(val, control.value);
						this._maxValueDict[control.parameter.name] = val;
					}

					if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu) continue;
					if (control.subMenu == null) continue;
					if (alreadyVisitMenu.Contains(control.subMenu)) continue;
					queue.Enqueue(control.subMenu);
				}
			}
		}

		private void FindMaxValueFromAnimatorController(AnimatorController? controller) {
			if (controller == null) return;
			if (this._maxValueDict == null) return;
			// Find the maximum value from Parameter driver
			VRCAvatarParameterDriver[] drivers = controller.GetBehaviours<VRCAvatarParameterDriver>();
			foreach (VRC_AvatarParameterDriver.Parameter parameter in drivers.SelectMany(driver => driver.parameters)
				         .Where(parameter => this._maxValueDict.Keys.Any(name => parameter.name == name))) {
				int val = this._maxValueDict[parameter.name];
				switch (parameter.type) {
					case VRC_AvatarParameterDriver.ChangeType.Set:
						val = (int)Mathf.Max(val, parameter.value);
						break;
					case VRC_AvatarParameterDriver.ChangeType.Add:
						// Unknown maximum value taken by parameter in case of Add
						break;
					case VRC_AvatarParameterDriver.ChangeType.Random:
						val = (int)Mathf.Max(val, parameter.valueMax);
						val = (int)Mathf.Max(val, parameter.valueMin);
						break;
					case VRC_AvatarParameterDriver.ChangeType.Copy:
						if (parameter.convertRange) {
							// If a range is specified, the maximum value is clear
							val = (int)Mathf.Max(val, parameter.destMin);
							val = (int)Mathf.Max(val, parameter.destMax);
						}

						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				this._maxValueDict[parameter.name] = val;
			}

			// Find the maximum value from transitions
			foreach (var transition in controller.layers
				         .Where(layer => layer.syncedLayerIndex < 0)
				         .SelectMany(layer => GetAllTransitions(layer.stateMachine))) {
				foreach (AnimatorCondition condition in transition.AnimatorTransition.conditions
					         .Where(condition => this._maxValueDict.Keys.Any(name => name == condition.parameter))) {
					int val = this._maxValueDict[condition.parameter];
					switch (condition.mode) {
						case AnimatorConditionMode.If:
						case AnimatorConditionMode.IfNot:
							break;
						case AnimatorConditionMode.Greater:
							// In the case of 'Greater', the exact maximum is not known, but it is included anyway
							val = (int)Mathf.Max(val, condition.threshold);
							break;
						case AnimatorConditionMode.Less:
							// In the case of 'Less', the exact maximum is not known, but it is included anyway
							val = (int)Mathf.Max(val, condition.threshold);
							break;
						case AnimatorConditionMode.Equals:
							val = (int)Mathf.Max(val, condition.threshold);
							break;
						case AnimatorConditionMode.NotEqual:
							// In the case of 'NotEqual', the exact maximum is not known, but it is included anyway
							val = (int)Mathf.Max(val, condition.threshold);
							break;
						default:
							throw new ArgumentOutOfRangeException();
					}

					this._maxValueDict[condition.parameter] = val;
				}
			}

			// Parameters controlled by animation are not synchronized and need not be considered
		}

		private static IEnumerable<Transition> GetAllTransitions(AnimatorStateMachine stateMachine) {
			foreach (AnimatorTransition transition in stateMachine.entryTransitions) {
				yield return new Transition {
					AnimatorTransition = transition,
					TransitionType = TransitionType.EntryTo,
					StateMachine = stateMachine
				};
			}

			foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions) {
				yield return new Transition {
					AnimatorTransition = transition,
					TransitionType = TransitionType.AnyTo,
					StateMachine = stateMachine
				};
			}

			foreach (ChildAnimatorState childAnimatorState in stateMachine.states) {
				foreach (AnimatorStateTransition transition in childAnimatorState.state.transitions) {
					yield return new Transition {
						AnimatorTransition = transition,
						TransitionType = TransitionType.StateTo,
						StateMachine = stateMachine,
						SrcState = childAnimatorState.state
					};
				}
			}

			foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines) {
				foreach (AnimatorTransition transition in stateMachine.GetStateMachineTransitions(childStateMachine.stateMachine)) {
					yield return new Transition {
						AnimatorTransition = transition,
						TransitionType = TransitionType.StateMachineTo,
						StateMachine = stateMachine,
						SrcStateMachine = childStateMachine.stateMachine
					};
				}

				foreach (Transition transition in GetAllTransitions(childStateMachine.stateMachine)) {
					yield return transition;
				}
			}
		}

		private struct Transition {
			public AnimatorTransitionBase AnimatorTransition { get; set; }
			public AnimatorStateMachine StateMachine { get; set; }

			public TransitionType TransitionType { get; set; }

			public AnimatorStateMachine? SrcStateMachine { get; set; }
			public AnimatorState? SrcState { get; set; }
		}

		private enum TransitionType {
			StateTo,
			AnyTo,
			EntryTo,
			StateMachineTo,
		}
	}
}