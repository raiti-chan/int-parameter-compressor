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
using Object = UnityEngine.Object;

#nullable enable

namespace net.raitichan.int_parameter_compressor {
	internal class IntParameterCompressingPass : Pass<IntParameterCompressingPass> {
		private Dictionary<string, int>? _maxValueDict;

		private IntParameterCompressor.WriteDefault _useWriteDefault;
		private bool _writeDefault;
		private bool _isAllWriteDefault = true;

		protected override void Execute(BuildContext context) {

			IntParameterCompressor[]? intParameterCompressors = context.AvatarRootObject.GetComponentsInChildren<IntParameterCompressor>();
			if (intParameterCompressors == null) return;
			if (intParameterCompressors.Length <= 0) return;

			{
				IntParameterCompressor intParameterCompressor = intParameterCompressors[0];
				this._useWriteDefault = intParameterCompressor.UseWriteDefault;
			}
			
			foreach (IntParameterCompressor intParameterCompressor in intParameterCompressors) {
				Object.DestroyImmediate(intParameterCompressor);
			}
			
			
			if (context.AvatarDescriptor.expressionParameters == null) return;
			this.FindTargetParameter(context.AvatarDescriptor);

			if (this._maxValueDict == null) return;
			if (this._maxValueDict.Count <= 0) return;

			this._writeDefault = this._useWriteDefault switch {
				IntParameterCompressor.WriteDefault.Auto => this._isAllWriteDefault,
				IntParameterCompressor.WriteDefault.Off => false,
				IntParameterCompressor.WriteDefault.On => true,
				_ => throw new ArgumentOutOfRangeException()
			};

			List<VRCExpressionParameters.Parameter> appendVRCParameters = new();
			List<AnimatorControllerParameter> appendParameters = new();

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
						type = AnimatorControllerParameterType.Bool
					});
				}
			}

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
					// Add Encode & Decode Layer
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

						AnimatorStateMachine edStateMachine = new() {
							name = $"{parameterName}_encode_decode",
							entryPosition = Vector3.zero,
							exitPosition = Vector3.down * 60,
							anyStatePosition = Vector3.up * 60
						};

						AnimatorState entryState = new() { name = "entry", writeDefaultValues = this._writeDefault };
						AssetDatabase.AddObjectToAsset(entryState, context.AssetContainer);
						AnimatorState remoteState = new() { name = "remote", writeDefaultValues = this._writeDefault };
						AssetDatabase.AddObjectToAsset(remoteState, context.AssetContainer);
						AnimatorState localState = new() { name = "local", writeDefaultValues = this._writeDefault };
						AssetDatabase.AddObjectToAsset(localState, context.AssetContainer);

						edStateMachine.AddState(entryState, Vector3.right * 230);
						edStateMachine.AddState(remoteState, new Vector3(230, -60));
						edStateMachine.AddState(localState, new Vector3(230, 60));

						edStateMachine.defaultState = entryState;

						AnimatorStateTransition entryToRemote = entryState.AddTransition(remoteState);
						entryToRemote.AddCondition(AnimatorConditionMode.IfNot, 1, "IsLocal");
						entryToRemote.duration = 0;

						AnimatorStateTransition entryToLocal = entryState.AddTransition(localState);
						entryToLocal.AddCondition(AnimatorConditionMode.If, 1, "IsLocal");
						entryToLocal.duration = 0;

						AssetDatabase.AddObjectToAsset(edStateMachine, context.AssetContainer);

						AnimatorControllerLayer encodeLayer = new() {
							name = edStateMachine.name,
							stateMachine = edStateMachine,
							defaultWeight = 0
						};

						Stack<List<AnimatorState>> encodeStatesStack = new();
						Stack<List<AnimatorState>> decodeStatesStack = new();

						List<AnimatorState> encodeLeafStates = new();
						List<AnimatorState> decodeLeafStates = new();

						for (int i = 0; i <= maxValue; i++) {
							AnimatorState encodeState = new() { name = $"encode_{i}", writeDefaultValues = this._writeDefault };
							encodeLeafStates.Add(encodeState);
							AssetDatabase.AddObjectToAsset(encodeState, context.AssetContainer);
							VRCAvatarParameterDriver encodeDriver = encodeState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
							for (int bit = 0; bit < useBitSize; bit++) {
								encodeDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter {
									type = VRC_AvatarParameterDriver.ChangeType.Set,
									name = $"{parameterName}.bit_{bit}",
									value = (i >> bit) & 1
								});
							}

							AnimatorState decodeState = new() { name = $"decode_{i}", writeDefaultValues = this._writeDefault };
							decodeLeafStates.Add(decodeState);
							AssetDatabase.AddObjectToAsset(decodeState, context.AssetContainer);
							VRCAvatarParameterDriver decodeDriver = decodeState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
							decodeDriver.parameters.Add(new VRC_AvatarParameterDriver.Parameter {
								type = VRC_AvatarParameterDriver.ChangeType.Set,
								name = parameterName,
								value = i
							});
						}

						encodeStatesStack.Push(encodeLeafStates);
						decodeStatesStack.Push(decodeLeafStates);

						int stateCunt = (maxValue + 1);
						int layerMul = 1;
						while (stateCunt > 4) {
							stateCunt = (int)Math.Ceiling(stateCunt / 4.0f);
							layerMul *= 4;

							List<AnimatorState> encodeStates = new();
							List<AnimatorState> decodeStates = new();
							for (int i = 0; i < stateCunt; i++) {
								AnimatorState encodeState = new() { name = $"encode-node-{i * layerMul}-{(i + 1) * layerMul - 1}", writeDefaultValues = this._writeDefault };
								encodeStates.Add(encodeState);
								AssetDatabase.AddObjectToAsset(encodeState, context.AssetContainer);

								AnimatorState decodeState = new() { name = $"decode-node-{i * layerMul}-{(i + 1) * layerMul - 1}", writeDefaultValues = this._writeDefault };
								decodeStates.Add(decodeState);
								AssetDatabase.AddObjectToAsset(decodeState, context.AssetContainer);
							}

							encodeStatesStack.Push(encodeStates);
							decodeStatesStack.Push(decodeStates);
						}

						{
							List<AnimatorState> topEncodeStates = encodeStatesStack.Peek();
							List<AnimatorState> topDecodeStates = decodeStatesStack.Peek();

							int layerCount = decodeStatesStack.Count;
							int mul = (int)Math.Pow(4, encodeStatesStack.Count) / 4;
							for (int stateIndex = 0; stateIndex < topEncodeStates.Count; stateIndex++) {
								{
									AnimatorStateTransition transition = localState.AddTransition(topEncodeStates[stateIndex]);
									transition.duration = 0;
									if (mul == 1) {
										transition.AddCondition(AnimatorConditionMode.Equals, stateIndex, parameterName);
									} else {
										transition.AddCondition(AnimatorConditionMode.Greater, stateIndex * mul - 1, parameterName);
										transition.AddCondition(AnimatorConditionMode.Less, (stateIndex + 1) * mul, parameterName);
									}
								}
								{
									AnimatorStateTransition transition = remoteState.AddTransition(topDecodeStates[stateIndex]);
									transition.duration = 0;
									int currentBit = (layerCount - 1) * 2;
									if ((useBitSize & 1) == 0) {
										transition.AddCondition((stateIndex & 1) == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 1, $"{parameterName}.bit_{currentBit}");
										transition.AddCondition((stateIndex & 2) == 2 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 1, $"{parameterName}.bit_{currentBit + 1}");
									} else {
										transition.AddCondition((stateIndex & 1) == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 1, $"{parameterName}.bit_{currentBit}");
									}
								}
							}
						}

						int row = 2;
						while (encodeStatesStack.Count > 0) {
							List<AnimatorState> currentEncodeStates = encodeStatesStack.Pop();
							List<AnimatorState> currentDecodeStates = decodeStatesStack.Pop();

							int column = 1;
							int columnIncrement = (int)Math.Pow(4, encodeStatesStack.Count);
							for (int stateIndex = 0; stateIndex < currentEncodeStates.Count; stateIndex++) {
								edStateMachine.AddState(currentEncodeStates[stateIndex], new Vector3(230 * column, 60 * row));
								edStateMachine.AddState(currentDecodeStates[stateIndex], new Vector3(230 * column, -60 * row));
								column += columnIncrement;
							}

							row++;

							if (encodeStatesStack.TryPeek(out List<AnimatorState> childEncodeStates) && decodeStatesStack.TryPeek(out List<AnimatorState> childDecodeStates)) {
								
								int mul = columnIncrement / 4;
								for (int stateIndex = 0; stateIndex < childEncodeStates.Count; stateIndex++) {
									int parentStateIndex = stateIndex / 4;
									{
										AnimatorStateTransition transition = currentEncodeStates[parentStateIndex].AddTransition(childEncodeStates[stateIndex]);
										transition.duration = 0;

										if (mul == 1) {
											transition.AddCondition(AnimatorConditionMode.Equals, stateIndex, parameterName);
										} else {
											transition.AddCondition(AnimatorConditionMode.Greater, stateIndex * mul - 1, parameterName);
											transition.AddCondition(AnimatorConditionMode.Less, (stateIndex + 1) * mul, parameterName);
										}
									}

									{
										AnimatorStateTransition transition = currentDecodeStates[parentStateIndex].AddTransition(childDecodeStates[stateIndex]);
										transition.duration = 0;
										int currentBit = (decodeStatesStack.Count - 1) * 2;
										transition.AddCondition((stateIndex & 1) == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 1, $"{parameterName}.bit_{currentBit}");
										transition.AddCondition((stateIndex & 2) == 2 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 1, $"{parameterName}.bit_{currentBit + 1}");
									}
								}
							} else {
								for (int stateIndex = 0; stateIndex < currentEncodeStates.Count; stateIndex++) {
									{
										AnimatorStateTransition transition = currentEncodeStates[stateIndex].AddTransition(localState);
										transition.duration = 0;
										transition.AddCondition(AnimatorConditionMode.NotEqual, stateIndex, parameterName);
									}

									for (int bitCount = 0; bitCount < useBitSize; bitCount++) {
										AnimatorStateTransition transition = currentDecodeStates[stateIndex].AddTransition(remoteState);
										transition.duration = 0;
										bool flag = ((stateIndex >> bitCount) & 1) == 1;
										transition.AddCondition(flag ? AnimatorConditionMode.IfNot : AnimatorConditionMode.If, 1, $"{parameterName}.bit_{bitCount}");
									}
								}
							}
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
				         .SelectMany(layer => this.GetAllTransitions(layer.stateMachine))) {
				foreach (AnimatorCondition condition in transition.conditions
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

		private IEnumerable<AnimatorTransitionBase> GetAllTransitions(AnimatorStateMachine stateMachine) {
			foreach (AnimatorTransition transition in stateMachine.entryTransitions) {
				yield return  transition;
			}

			foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions) {
				yield return transition;
			}

			foreach (ChildAnimatorState childAnimatorState in stateMachine.states) {
				if (!childAnimatorState.state.writeDefaultValues) {
					this._isAllWriteDefault = false;
				}
				foreach (AnimatorStateTransition transition in childAnimatorState.state.transitions) {
					yield return transition;
				}
			}

			foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines) {
				foreach (AnimatorTransition transition in stateMachine.GetStateMachineTransitions(childStateMachine.stateMachine)) {
					yield return transition;
				}

				foreach (AnimatorTransitionBase transition in this.GetAllTransitions(childStateMachine.stateMachine)) {
					yield return transition;
				}
			}
		}
	}
}