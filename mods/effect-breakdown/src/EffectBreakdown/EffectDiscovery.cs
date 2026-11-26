using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.CecilX;
using Mono.CecilX.Cil;

namespace EffectBreakdown
{
    // A tooltip field is classified by the argument it contributes to in game code.
    // Analysis reads IL metadata and never invokes abilities or combat processors.
    internal static class EffectDiscovery
    {
        internal static Dictionary<string, EffectKind> Read(string assemblyPath, Action<string> reportSkipped = null)
        {
            using var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
            string corePath = Path.Combine(Path.GetDirectoryName(assemblyPath) ?? "", "Dew.Core.dll");
            using var core = File.Exists(corePath) ? AssemblyDefinition.ReadAssembly(corePath) : null;
            var types = EnumerateTypes(assembly.MainModule.Types.Where(IsEffectType)).ToArray();
            var result = new Dictionary<string, EffectKind>(StringComparer.Ordinal);
            var fieldInputs = new Dictionary<string, Origin>(StringComparer.Ordinal);
            var callers = new Dictionary<string, HashSet<MethodDefinition>>(StringComparer.Ordinal);
            var summaries = new Dictionary<string, Dictionary<int, EffectKind>>(StringComparer.Ordinal);
            var pending = new Queue<MethodDefinition>();
            var scanTypes = types.AsEnumerable();
            if (core != null)
                scanTypes = scanTypes.Concat(core.MainModule.Types.Where(type => type.Name == "DamageInstance"));
            foreach (var type in scanTypes)
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody)
                        continue;

                    try
                    {
                        bool hasSink = false;
                        bool readsMagnitude = false;
                        bool writesMagnitude = false;
                        foreach (var instruction in method.Body.Instructions)
                        {
                            if (instruction.Operand is FieldReference field && IsValue(field.FieldType))
                            {
                                var code = instruction.OpCode.Code;
                                readsMagnitude |= code == Code.Ldfld || code == Code.Ldsfld ||
                                    code == Code.Ldflda || code == Code.Ldsflda;
                                writesMagnitude |= code == Code.Stfld || code == Code.Stsfld;
                            }
                            if (!(instruction.Operand is MethodReference target))
                                continue;

                            hasSink |= Sink(target).Kind != EffectKind.None;
                            if (target.DeclaringType.Scope != assembly.MainModule)
                                continue;

                            if (!callers.TryGetValue(target.FullName, out var directCallers))
                                callers.Add(target.FullName, directCallers = new HashSet<MethodDefinition>());
                            directCallers.Add(method);
                        }
                        if (hasSink || (readsMagnitude && (writesMagnitude ||
                            method.IsGetter && IsValue(method.ReturnType))))
                            pending.Enqueue(method);
                    }
                    catch (NullReferenceException error) when (error.TargetSite?.DeclaringType?.FullName ==
                        "Mono.CecilX.Cil.CodeReader" && error.TargetSite.Name == "ReadExceptionHandlers")
                    {
                        reportSkipped?.Invoke("Malformed exception handlers in " + method.FullName);
                    }
                }
            }

            while (pending.Count != 0)
            {
                var method = pending.Dequeue();
                var inputs = new Dictionary<int, EffectKind>();
                Scan(method, result, inputs, summaries, fieldInputs);
                if (inputs.Count == 0)
                    continue;

                if (!summaries.TryGetValue(method.FullName, out var previous))
                    summaries.Add(method.FullName, previous = new Dictionary<int, EffectKind>());

                bool changed = false;
                foreach (var entry in inputs)
                {
                    previous.TryGetValue(entry.Key, out var kind);
                    var combined = kind | entry.Value;
                    previous[entry.Key] = combined;
                    changed |= combined != kind;
                }
                if (changed && callers.TryGetValue(method.FullName, out var directCallers))
                    foreach (var caller in directCallers)
                        pending.Enqueue(caller);
            }

            PropagateFieldTypes(result, fieldInputs);
            AddInheritedNames(result, types, core?.MainModule);
            return result;
        }

        private static void PropagateFieldTypes(Dictionary<string, EffectKind> kinds, Dictionary<string, Origin> fieldInputs)
        {
            var pending = new Queue<string>(kinds.Keys);
            while (pending.Count != 0)
            {
                string field = pending.Dequeue();
                if (!fieldInputs.TryGetValue(field, out var inputs))
                    continue;

                foreach (var input in inputs.Fields)
                {
                    if (input[0] == '@')
                        continue;
                    kinds.TryGetValue(input, out var previous);
                    var combined = previous | kinds[field];
                    if (combined == previous)
                        continue;
                    kinds[input] = combined;
                    pending.Enqueue(input);
                }
            }
        }

        private static void AddInheritedNames(Dictionary<string, EffectKind> kinds, TypeDefinition[] types,
            ModuleDefinition core)
        {
            foreach (var type in types)
            {
                var seen = new HashSet<string>(type.Fields.Select(field => field.Name), StringComparer.Ordinal);
                var baseType = type.BaseType;
                while (baseType != null)
                {
                    var parent = type.Module.GetType(baseType.FullName) ?? core?.GetType(baseType.FullName);
                    if (parent == null)
                        break;

                    foreach (var field in parent.Fields)
                        if (seen.Add(field.Name) && kinds.TryGetValue(FieldKey(field), out var kind))
                            kinds[type.Name + "::" + field.Name] = kind;

                    baseType = parent.BaseType;
                }
            }
        }

        private static bool IsEffectType(TypeDefinition type) =>
            type.Name.StartsWith("St_", StringComparison.Ordinal) ||
            type.Name.StartsWith("Ai_", StringComparison.Ordinal) ||
            type.Name.StartsWith("At_", StringComparison.Ordinal) ||
            type.Name.StartsWith("Se_", StringComparison.Ordinal) ||
            type.Name.StartsWith("Gem_", StringComparison.Ordinal) ||
            type.Name.StartsWith("Summon_", StringComparison.Ordinal);

        private static IEnumerable<TypeDefinition> EnumerateTypes(IEnumerable<TypeDefinition> types)
        {
            foreach (var type in types)
            {
                yield return type;
                foreach (var nested in EnumerateTypes(type.NestedTypes))
                    yield return nested;
            }
        }

        private static (EffectKind Kind, int Parameter) Sink(MethodReference method)
        {
            switch (method.DeclaringType.Name)
            {
                case "HealData" when method.Name == ".ctor":
                case "Actor" when method.Name == "Heal":
                case "SkillTrigger" when method.Name == "Heal":
                case "AbilityInstance" when method.Name == "Heal":
                case "Summon" when method.Name == "Heal":
                    return (EffectKind.Heal, 0);
                case "Actor" when method.Name == "GiveShield" || method.Name == "ProcessShieldAmount":
                    return (EffectKind.Barrier, method.Name == "GiveShield" ? 1 : 0);
                case "Actor" when method.Parameters.Count > 1 && method.Parameters[1].ParameterType.MetadataType == MetadataType.Single &&
                    (method.Name == "ApplyCooldownReductionByRatio" || method.Name == "ApplyCooldownReduction"):
                    return (method.Name == "ApplyCooldownReductionByRatio" ? EffectKind.CooldownRatio : EffectKind.CooldownSeconds, 1);
                case "Actor" when CooldownSettingsKind(method) != EffectKind.None:
                    return (CooldownSettingsKind(method), 1);
                case "SkillBonus" when method.Name == "set_cooldownMultiplier":
                    return (EffectKind.CooldownMultiplier, 0);
                case "Gem" when method.Name == "ApplyCooldownReduction":
                    return (EffectKind.GemCooldownSeconds, 0);
                case "Se_GenericShield_OneShot" when method.Name == "SetAmount" || method.Name == "AddAmount":
                    return (EffectKind.Barrier, 0);
                case "DamageData" when method.Name == ".ctor" && method.Parameters.Count > 1 &&
                    (method.Parameters[1].ParameterType.MetadataType == MetadataType.Single ||
                     method.Parameters[1].ParameterType.Name == "ScalingValue"):
                    return (EffectKind.Damage, 1);
                case "Actor":
                case "SkillTrigger":
                case "AbilityInstance":
                case "Summon":
                    if (method.Name == "CreateDamage")
                        return (EffectKind.Damage, 1);
                    if (method.Name == "Damage" || method.Name == "DefaultDamage" || method.Name == "PhysicalDamage" ||
                        method.Name == "MagicDamage" || method.Name == "PhysicalMagicDamage" || method.Name == "PureDamage")
                        return (EffectKind.Damage, 0);
                    break;
            }

            return (EffectKind.None, 0);
        }

        private static EffectKind CooldownSettingsKind(MethodReference target)
        {
            if (target.DeclaringType.Name != "Actor" || target.Parameters.Count != 2)
                return EffectKind.None;

            if (target.Name == "ApplyCooldownReduction" &&
                target.Parameters[1].ParameterType.Name == "CooldownReductionSettings")
                return EffectKind.CooldownSeconds;
            if (target.Name == "ApplyCooldownReductionByRatio" &&
                target.Parameters[1].ParameterType.Name == "CooldownReductionByRatioSettings")
                return EffectKind.CooldownRatio;
            return EffectKind.None;
        }

        private static void Scan(MethodDefinition method, Dictionary<string, EffectKind> result,
            Dictionary<int, EffectKind> inputs, Dictionary<string, Dictionary<int, EffectKind>> summaries,
            Dictionary<string, Origin> fieldInputs)
        {
            // Some barrier implementations feed HealData through the shield processors directly.
            var healDataKind = HealDataKind(method);
            var first = method.Body.Instructions[0];
            var containingType = method.DeclaringType;
            while (containingType.DeclaringType != null)
                containingType = containingType.DeclaringType;
            var states = new Dictionary<Instruction, State>
            {
                [first] = new State(method.Body.Variables.Count, method.Parameters.Count, method.HasThis)
            };
            var pending = new Queue<Instruction>();
            pending.Enqueue(first);

            while (pending.Count != 0)
            {
                var instruction = pending.Dequeue();
                var state = new State(states[instruction]);
                if (instruction.OpCode.Code == Code.Ret && method.IsGetter && IsValue(method.ReturnType) &&
                    state.Stack.Count != 0)
                    RecordFieldInput(fieldInputs, PropertyKey(method), state.Stack[state.Stack.Count - 1]);
                Apply(instruction, state, result, healDataKind, inputs, summaries, fieldInputs, containingType.Name);
                foreach (var next in Successors(instruction))
                {
                    if (!states.TryGetValue(next, out var previous))
                    {
                        states.Add(next, new State(state));
                        pending.Enqueue(next);
                    }
                    else if (previous.Merge(state))
                    {
                        pending.Enqueue(next);
                    }
                }
            }
        }

        private static EffectKind HealDataKind(MethodDefinition method)
        {
            bool barrier = method.Body.Instructions.Any(instruction =>
                instruction.Operand is FieldReference field && field.Name == "takenShieldProcessor");
            bool heal = method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference target &&
                (target.DeclaringType.Name == "HealData" && target.Name == "Dispatch" ||
                 target.DeclaringType.Name == "Actor" && target.Name == "DoHeal"));
            if (barrier)
                return heal ? EffectKind.Heal | EffectKind.Barrier : EffectKind.Barrier;
            return heal ? EffectKind.Heal : EffectKind.None;
        }

        private static void Apply(Instruction instruction, State state, Dictionary<string, EffectKind> result,
            EffectKind healDataKind, Dictionary<int, EffectKind> inputs,
            Dictionary<string, Dictionary<int, EffectKind>> summaries, Dictionary<string, Origin> fieldInputs,
            string containingType)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Ldc_I4_M1:
                case Code.Ldc_I4_1:
                case Code.Ldc_I4_2:
                case Code.Ldc_I4_3:
                case Code.Ldc_I4_4:
                case Code.Ldc_I4_5:
                case Code.Ldc_I4_6:
                case Code.Ldc_I4_7:
                case Code.Ldc_I4_8:
                case Code.Ldc_I4 when (int)instruction.Operand != 0:
                case Code.Ldc_I4_S when (sbyte)instruction.Operand != 0:
                case Code.Ldc_I8 when (long)instruction.Operand != 0:
                case Code.Ldc_R4 when (float)instruction.Operand != 0f:
                case Code.Ldc_R8 when (double)instruction.Operand != 0d:
                    state.Stack.Add(Origin.NonzeroConstant);
                    return;
                case Code.Ldarg_0: state.LoadArgumentSlot(0); return;
                case Code.Ldarg_1: state.LoadArgumentSlot(1); return;
                case Code.Ldarg_2: state.LoadArgumentSlot(2); return;
                case Code.Ldarg_3: state.LoadArgumentSlot(3); return;
                case Code.Ldarg:
                case Code.Ldarg_S:
                case Code.Ldarga:
                case Code.Ldarga_S:
                    state.LoadParameter(((ParameterDefinition)instruction.Operand).Index);
                    return;
                case Code.Starg:
                case Code.Starg_S:
                    state.StoreParameter(((ParameterDefinition)instruction.Operand).Index);
                    return;
                case Code.Ldloc_0: state.LoadLocal(0); return;
                case Code.Ldloc_1: state.LoadLocal(1); return;
                case Code.Ldloc_2: state.LoadLocal(2); return;
                case Code.Ldloc_3: state.LoadLocal(3); return;
                case Code.Stloc_0: state.StoreLocal(0); return;
                case Code.Stloc_1: state.StoreLocal(1); return;
                case Code.Stloc_2: state.StoreLocal(2); return;
                case Code.Stloc_3: state.StoreLocal(3); return;
                case Code.Ldloc:
                case Code.Ldloc_S:
                    state.LoadLocal(((VariableDefinition)instruction.Operand).Index);
                    return;
                case Code.Ldloca:
                case Code.Ldloca_S:
                    state.LoadLocalAddress(((VariableDefinition)instruction.Operand).Index);
                    return;
                case Code.Initobj:
                    state.WriteLocalAddress(state.Pop(), Origin.Empty);
                    return;
                case Code.Stloc:
                case Code.Stloc_S:
                    state.StoreLocal(((VariableDefinition)instruction.Operand).Index);
                    return;
                case Code.Ldfld:
                case Code.Ldflda:
                case Code.Ldsfld:
                case Code.Ldsflda:
                    if (instruction.OpCode.Code == Code.Ldfld || instruction.OpCode.Code == Code.Ldflda)
                        state.Pop();
                    var field = (FieldReference)instruction.Operand;
                    state.Stack.Add(state.LoadField(field).Union(FieldValue(field)));
                    return;
                case Code.Stfld:
                case Code.Stsfld:
                    var storedField = (FieldReference)instruction.Operand;
                    var storedValue = state.Pop();
                    state.StoreField(storedField, storedValue);
                    if (IsValue(storedField.FieldType))
                        RecordFieldInput(fieldInputs, FieldKey(storedField), storedValue);
                    if (instruction.OpCode.Code == Code.Stfld)
                    {
                        var destination = state.Pop();
                        if (storedField.DeclaringType.Name == "CooldownReductionSettings" && storedField.Name == "amount" ||
                            storedField.DeclaringType.Name == "CooldownReductionByRatioSettings" && storedField.Name == "ratio")
                            state.WriteLocalAddress(destination, storedValue);
                    }
                    return;
                case Code.Call:
                case Code.Callvirt:
                case Code.Newobj:
                    ApplyCall((MethodReference)instruction.Operand, instruction.OpCode.Code == Code.Newobj, state, result,
                        healDataKind, inputs, summaries, containingType);
                    return;
                case Code.Dup:
                    state.Stack.Add(state.Stack[state.Stack.Count - 1]);
                    return;
                case Code.Div:
                case Code.Div_Un:
                    // A divisor can describe a tick count or interval. In reciprocal formulas the
                    // denominator is the only source, such as skill haste in 1 / (1 + haste).
                    var denominator = state.Pop();
                    var numerator = state.Pop();
                    state.Stack.Add(numerator == Origin.NonzeroConstant ? denominator.WithoutConstant() : numerator.WithoutConstant());
                    return;
                case Code.Leave:
                case Code.Leave_S:
                    state.Stack.Clear();
                    return;
            }

            var operands = Origin.Empty;
            int popCount = Count(instruction.OpCode.StackBehaviourPop);
            for (int i = 0; i < popCount; i++)
                operands = operands.Union(state.Pop());
            if (popCount != 1)
                operands = operands.WithoutConstant();

            int pushCount = Count(instruction.OpCode.StackBehaviourPush);
            for (int i = 0; i < pushCount; i++)
                state.Stack.Add(operands);
        }

        private static void ApplyCall(MethodReference method, bool constructor, State state,
            Dictionary<string, EffectKind> result, EffectKind healDataKind, Dictionary<int, EffectKind> inputs,
            Dictionary<string, Dictionary<int, EffectKind>> summaries, string containingType)
        {
            var arguments = new Origin[method.Parameters.Count];
            for (int i = arguments.Length - 1; i >= 0; i--)
                arguments[i] = state.Pop();

            var receiver = method.HasThis && !constructor ? state.Pop() : Origin.Empty;
            var sink = Sink(method);
            if (method.DeclaringType.Name == "HealData" && method.Name == ".ctor")
                sink.Kind = healDataKind;
            if (sink.Kind != EffectKind.None && sink.Parameter < arguments.Length)
                Record(arguments[sink.Parameter], sink.Kind, result, inputs);
            if (summaries.TryGetValue(method.FullName, out var summary))
                foreach (var entry in summary)
                    Record(arguments[entry.Key], entry.Value, result, inputs);

            if (method.DeclaringType.Name == "DamageData" && method.Name == "Dispatch" &&
                arguments.Length > 0 && arguments[0].Fields.Contains("@caster"))
                RecordSelfDamage(receiver, containingType, result, inputs);
            else if (method.DeclaringType.Name == "Actor" && method.Name == "DealDamage" &&
                arguments.Length > 1 && arguments[1].Fields.Contains("@caster"))
                RecordSelfDamage(arguments[0], containingType, result, inputs);

            if (constructor || method.ReturnType.MetadataType != MetadataType.Void)
            {
                var value = Origin.Empty;
                if (IsValue(method.ReturnType) || method.ReturnType.Name == "DamageData" ||
                    constructor && method.DeclaringType.Name == "DamageData")
                {
                    value = receiver;
                    if (method.Name.StartsWith("get_", StringComparison.Ordinal) && arguments.Length == 0)
                        value = value.Union(new Origin(new[] { PropertyKey(method) }));
                    // Normalized interpolation inputs determine applicability, rather than the maximum effect.
                    bool normalizes = method.DeclaringType.Name == "Mathf" &&
                        (method.Name == "Clamp01" || method.Name == "InverseLerp");
                    if (!normalizes)
                        foreach (var argument in arguments)
                            value = value.Union(argument);
                }
                state.Stack.Add(value);
            }
        }

        private static void RecordSelfDamage(Origin value, string containingType,
            Dictionary<string, EffectKind> result, Dictionary<int, EffectKind> inputs)
        {
            // A shared base field may be used by unrelated offensive skills. Only classify
            // the originating member on this ability as the cost when the caster is the target.
            string prefix = containingType + "::";
            var declaredInputs = value.Fields.Where(field => field.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Record(new Origin(declaredInputs), EffectKind.SelfDamage, result, inputs);
        }

        private static void RecordFieldInput(Dictionary<string, Origin> fieldInputs, string key, Origin value)
        {
            if (value.Fields.Length == 0)
                return;
            fieldInputs.TryGetValue(key, out var previous);
            fieldInputs[key] = (previous ?? Origin.Empty).Union(value);
        }

        private static string PropertyKey(MethodReference method) =>
            method.DeclaringType.Name + "::" + method.Name.Substring(4);

        private static void Record(Origin value, EffectKind kind, Dictionary<string, EffectKind> result,
            Dictionary<int, EffectKind> inputs)
        {
            foreach (var field in value.Fields)
            {
                if (field[0] == '@' && int.TryParse(field.Substring(1), out var parameter))
                {
                    inputs.TryGetValue(parameter, out var existing);
                    inputs[parameter] = existing | kind;
                }
                else if (field[0] != '@')
                {
                    result.TryGetValue(field, out var existing);
                    result[field] = existing | kind;
                }
            }
        }

        private static Origin FieldValue(FieldReference field)
        {
            if (field.DeclaringType.Name == "CastInfo" && field.Name == "caster")
                return Origin.Caster;
            return IsValue(field.FieldType) && field.FieldType.MetadataType != MetadataType.Int32 &&
                !field.DeclaringType.Name.StartsWith("<", StringComparison.Ordinal)
                ? new Origin(new[] { FieldKey(field) })
                : Origin.Empty;
        }

        private static string FieldKey(FieldReference field) => field.DeclaringType.Name + "::" + field.Name;

        private static bool IsValue(TypeReference type) =>
            type.MetadataType == MetadataType.Single || type.MetadataType == MetadataType.Double ||
            type.MetadataType == MetadataType.Int32 || type.Name == "ScalingValue" || type.Name == "StarScalingValue";

        private static int Count(StackBehaviour behaviour)
        {
            string name = behaviour.ToString();
            if (name == "Pop0" || name == "Push0" || name.StartsWith("Var", StringComparison.Ordinal) || name == "PopAll")
                return 0;
            int count = 1;
            foreach (char character in name)
                if (character == '_')
                    count++;
            return count;
        }

        private static IEnumerable<Instruction> Successors(Instruction instruction)
        {
            switch (instruction.OpCode.FlowControl)
            {
                case FlowControl.Branch:
                    if (instruction.Operand is Instruction target)
                        yield return target;
                    yield break;
                case FlowControl.Cond_Branch:
                    if (instruction.Operand is Instruction branch)
                        yield return branch;
                    if (instruction.Operand is Instruction[] choices)
                        foreach (var choice in choices)
                            yield return choice;
                    break;
                case FlowControl.Return:
                case FlowControl.Throw:
                    yield break;
            }

            if (instruction.Next != null)
                yield return instruction.Next;
        }

        private sealed class State
        {
            internal readonly List<Origin> Stack;
            private readonly Origin[] _locals;
            private readonly Origin[] _parameters;
            private readonly int _receiverSlots;
            private readonly Dictionary<string, Origin> _fields;

            internal State(int localCount, int parameterCount, bool hasReceiver)
            {
                Stack = new List<Origin>();
                _locals = Enumerable.Repeat(Origin.Empty, localCount).ToArray();
                _parameters = Enumerable.Range(0, parameterCount).Select(index => new Origin(new[] { "@" + index })).ToArray();
                _receiverSlots = hasReceiver ? 1 : 0;
                _fields = new Dictionary<string, Origin>(StringComparer.Ordinal);
            }

            internal State(State state)
            {
                Stack = new List<Origin>(state.Stack);
                _locals = (Origin[])state._locals.Clone();
                _parameters = (Origin[])state._parameters.Clone();
                _receiverSlots = state._receiverSlots;
                _fields = new Dictionary<string, Origin>(state._fields, StringComparer.Ordinal);
            }

            internal Origin Pop()
            {
                int index = Stack.Count - 1;
                var value = Stack[index];
                Stack.RemoveAt(index);
                return value;
            }

            internal void LoadLocal(int index) => Stack.Add(_locals[index]);
            internal void LoadLocalAddress(int index) => Stack.Add(_locals[index].Union(
                new Origin(new[] { "@local:" + index })));
            internal void WriteLocalAddress(Origin address, Origin value)
            {
                foreach (var field in address.Fields)
                    if (field.StartsWith("@local:", StringComparison.Ordinal) && int.TryParse(field.Substring(7), out int index))
                        _locals[index] = value;
            }
            internal void StoreLocal(int index) => _locals[index] = Pop();
            internal void LoadArgumentSlot(int slot) => LoadParameter(slot - _receiverSlots);
            internal void LoadParameter(int index) => Stack.Add(index < 0 ? Origin.Empty : _parameters[index]);
            internal void StoreParameter(int index)
            {
                var value = Pop();
                if (index >= 0)
                    _parameters[index] = value;
            }
            internal Origin LoadField(FieldReference field) => LoadField(field.FullName);
            internal void StoreField(FieldReference field, Origin value)
            {
                if (IsValue(field.FieldType))
                    _fields[field.FullName] = value;
            }

            private Origin LoadField(string field) => _fields.TryGetValue(field, out var value) ? value : Origin.Empty;

            internal bool Merge(State other)
            {
                if (Stack.Count != other.Stack.Count)
                    return false;

                bool changed = false;
                for (int i = 0; i < Stack.Count; i++)
                {
                    var merged = Stack[i].MergeAlternative(other.Stack[i]);
                    changed |= !ReferenceEquals(merged, Stack[i]);
                    Stack[i] = merged;
                }
                for (int i = 0; i < _locals.Length; i++)
                {
                    var merged = _locals[i].MergeAlternative(other._locals[i]);
                    changed |= !ReferenceEquals(merged, _locals[i]);
                    _locals[i] = merged;
                }
                for (int i = 0; i < _parameters.Length; i++)
                {
                    var merged = _parameters[i].MergeAlternative(other._parameters[i]);
                    changed |= !ReferenceEquals(merged, _parameters[i]);
                    _parameters[i] = merged;
                }
                foreach (var pair in other._fields)
                {
                    var previous = LoadField(pair.Key);
                    var merged = previous.MergeAlternative(pair.Value);
                    if (!ReferenceEquals(previous, merged))
                    {
                        _fields[pair.Key] = merged;
                        changed = true;
                    }
                }
                return changed;
            }
        }

        private sealed class Origin
        {
            internal static readonly Origin Empty = new Origin(Array.Empty<string>());
            internal static readonly Origin NonzeroConstant = new Origin(new[] { "@constant" });
            internal static readonly Origin Caster = new Origin(new[] { "@caster" });
            internal readonly string[] Fields;

            internal Origin(string[] fields) => Fields = fields;

            internal Origin WithoutConstant()
            {
                if (this == NonzeroConstant)
                    return Empty;
                if (!Fields.Contains("@constant"))
                    return this;
                return new Origin(Fields.Where(field => field != "@constant").ToArray());
            }

            internal Origin MergeAlternative(Origin other) => this == other
                ? this
                : WithoutConstant().Union(other.WithoutConstant());

            internal Origin Union(Origin other)
            {
                if (this == other || other.Fields.Length == 0 || other.Fields.All(Fields.Contains))
                    return this;
                return Fields.Length == 0 ? other : new Origin(Fields.Union(other.Fields).ToArray());
            }
        }
    }
}
