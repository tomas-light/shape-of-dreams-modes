using System;
using System.IO;
using EffectBreakdown;
using Mono.CecilX;
using Mono.CecilX.Cil;

internal static class DiscoveryFixture
{
    internal static void Verify()
    {
        var path = Path.Combine(Path.GetTempPath(), "effect-breakdown-" + Guid.NewGuid() + ".dll");
        try
        {
            WriteAssembly(path);
            var effects = EffectDiscovery.Read(path);
            if (!effects.TryGetValue("Ai_Unlisted::magnitude", out var found) || found != EffectKind.Barrier)
                throw new InvalidOperationException("Discovery must follow the amount through a new ability's helper.");
            if (effects.ContainsKey("Ai_Unlisted::duration"))
                throw new InvalidOperationException("Discovery must keep the duration argument separate from the amount.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteAssembly(string path)
    {
        using var module = ModuleDefinition.CreateModule("Fixture", ModuleKind.Dll);
        var actor = AddType(module, "Actor", module.TypeSystem.Object);
        var apply = AddMethod(actor, "GiveShield", module.TypeSystem.Object, module.TypeSystem.Single, module.TypeSystem.Single);
        apply.Body.GetILProcessor().Emit(OpCodes.Ret);

        var ability = AddType(module, "Ai_Unlisted", module.TypeSystem.Object);
        var helper = AddMethod(ability, "ApplyShield", module.TypeSystem.Single, module.TypeSystem.Single);
        var il = helper.Body.GetILProcessor();
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, apply);
        il.Emit(OpCodes.Ret);

        var amount = new FieldDefinition("magnitude", FieldAttributes.Static, module.TypeSystem.Single);
        var duration = new FieldDefinition("duration", FieldAttributes.Static, module.TypeSystem.Single);
        ability.Fields.Add(amount);
        ability.Fields.Add(duration);
        var cast = AddMethod(ability, "Cast");
        il = cast.Body.GetILProcessor();
        il.Emit(OpCodes.Ldsfld, amount);
        il.Emit(OpCodes.Ldsfld, duration);
        il.Emit(OpCodes.Call, helper);
        il.Emit(OpCodes.Ret);
        module.Write(path);
    }

    private static TypeDefinition AddType(ModuleDefinition module, string name, TypeReference baseType)
    {
        var type = new TypeDefinition("", name, TypeAttributes.Class, baseType);
        module.Types.Add(type);
        return type;
    }

    private static MethodDefinition AddMethod(TypeDefinition type, string name, params TypeReference[] parameters)
    {
        var method = new MethodDefinition(name, MethodAttributes.Static, type.Module.TypeSystem.Void);
        foreach (var parameter in parameters)
            method.Parameters.Add(new ParameterDefinition(parameter));
        type.Methods.Add(method);
        return method;
    }
}
