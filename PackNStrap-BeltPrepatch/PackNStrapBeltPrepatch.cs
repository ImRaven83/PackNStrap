using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;

// EquipmentSlot has no "Belt" member, so any code that resolves a Slot's string
// ID to an EquipmentSlot value throws for our custom Belt slot. Two different
// call patterns do this in Assembly-CSharp.dll: InventoryEquipment's constructor
// uses Enum.Parse(typeof(EquipmentSlot), slotId), while EquipItemWindow.Show
// (invoked when a slot header is clicked) uses the generic
// Enum.TryParse<EquipmentSlot>(slotId, out result). This preload-time patch scans
// the whole assembly for both patterns and reroutes every call site through
// injected helper methods that special-case "Belt" (mapping it to a boxed
// placeholder value no vanilla EquipmentSlot member uses) and otherwise fall
// through to the original Enum.Parse/TryParse behavior unchanged.
public static class PackNStrapBeltPrepatch
{
    private const int BeltSlotValue = 15;
    private const string HelperNamespace = "PackNStrap.Generated";
    private const string HelperTypeName = "BeltSlotEnumHelper";

    public static IEnumerable<string> TargetDLLs => new[] { "Assembly-CSharp.dll" };

    public static void Patch(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;

        var equipmentSlotType = module.GetTypes().FirstOrDefault(t => t.Name == "EquipmentSlot" && t.IsEnum);
        if (equipmentSlotType == null)
        {
            Console.WriteLine("[PackNStrap Belt Prepatch] EquipmentSlot enum not found; no changes made.");
            return;
        }

        var parseMethod = module.ImportReference(
            typeof(Enum).GetMethod(nameof(Enum.Parse), new[] { typeof(Type), typeof(string) }));
        var tryParseOpenGeneric = module.ImportReference(
            typeof(Enum).GetMethods().First(m => m.Name == nameof(Enum.TryParse) && m.IsGenericMethodDefinition && m.GetParameters().Length == 2));
        var tryParseGeneric = new GenericInstanceMethod(tryParseOpenGeneric);
        tryParseGeneric.GenericArguments.Add(equipmentSlotType);

        var stringEquality = module.ImportReference(
            typeof(string).GetMethod("op_Equality", new[] { typeof(string), typeof(string) }));

        var helperType = GetOrCreateHelperType(module);
        var parseHelper = GetOrCreateParseHelper(helperType, module, parseMethod, stringEquality);
        var tryParseHelper = GetOrCreateTryParseHelper(helperType, module, equipmentSlotType, tryParseGeneric, stringEquality);

        var parseReplaced = 0;
        var tryParseReplaced = 0;
        foreach (var type in module.GetTypes())
        {
            if (type == helperType) continue;

            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                var instructions = method.Body.Instructions;
                for (var i = 0; i < instructions.Count; i++)
                {
                    var instruction = instructions[i];
                    if (instruction.OpCode != OpCodes.Call || instruction.Operand is not MethodReference mr)
                    {
                        continue;
                    }

                    if (mr.DeclaringType.FullName == "System.Enum" && mr.Name == "Parse" && mr.Parameters.Count == 2
                        && PrecedingLoadsType(instructions, i, "EquipmentSlot"))
                    {
                        instruction.Operand = parseHelper;
                        parseReplaced++;
                    }
                    else if (mr is GenericInstanceMethod gim && mr.DeclaringType.FullName == "System.Enum"
                             && mr.Name == "TryParse" && gim.GenericArguments.Count == 1
                             && gim.GenericArguments[0].Name == "EquipmentSlot")
                    {
                        instruction.Operand = tryParseHelper;
                        tryParseReplaced++;
                    }
                }
            }
        }

        Console.WriteLine($"[PackNStrap Belt Prepatch] Patched InventoryEquipment for custom Belt slot ({parseReplaced} Enum.Parse replacement(s), {tryParseReplaced} Enum.TryParse replacement(s)).");
    }

    private static bool PrecedingLoadsType(Collection<Instruction> instructions, int callIndex, string typeName)
    {
        var lookback = Math.Max(0, callIndex - 8);
        for (var j = callIndex - 1; j >= lookback; j--)
        {
            if (instructions[j].OpCode == OpCodes.Ldtoken && instructions[j].Operand is TypeReference typeRef && typeRef.Name == typeName)
            {
                return true;
            }
        }
        return false;
    }

    private static TypeDefinition GetOrCreateHelperType(ModuleDefinition module)
    {
        var existing = module.GetType(HelperNamespace, HelperTypeName) ?? module.Types.FirstOrDefault(t => t.Namespace == HelperNamespace && t.Name == HelperTypeName);
        if (existing != null)
        {
            return existing;
        }

        var helperType = new TypeDefinition(
            HelperNamespace,
            HelperTypeName,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class,
            module.TypeSystem.Object);
        module.Types.Add(helperType);
        return helperType;
    }

    private static MethodReference GetOrCreateParseHelper(TypeDefinition helperType, ModuleDefinition module, MethodReference parseMethod, MethodReference stringEquality)
    {
        const string methodName = "ParseEquipmentSlotAllowBelt";
        var existing = helperType.Methods.FirstOrDefault(m => m.Name == methodName);
        if (existing != null)
        {
            return existing;
        }

        var method = new MethodDefinition(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            module.TypeSystem.Object);
        method.Parameters.Add(new ParameterDefinition("enumType", ParameterAttributes.None, module.ImportReference(typeof(Type))));
        method.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.String));
        helperType.Methods.Add(method);

        var il = method.Body.GetILProcessor();
        var fallThrough = Instruction.Create(OpCodes.Ldarg_0);
        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        il.Append(Instruction.Create(OpCodes.Ldstr, "Belt"));
        il.Append(Instruction.Create(OpCodes.Call, stringEquality));
        il.Append(Instruction.Create(OpCodes.Brfalse_S, fallThrough));
        il.Append(Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)BeltSlotValue));
        il.Append(Instruction.Create(OpCodes.Box, module.TypeSystem.Int32));
        il.Append(Instruction.Create(OpCodes.Ret));
        il.Append(fallThrough);
        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        il.Append(Instruction.Create(OpCodes.Call, parseMethod));
        il.Append(Instruction.Create(OpCodes.Ret));

        return method;
    }

    private static MethodReference GetOrCreateTryParseHelper(TypeDefinition helperType, ModuleDefinition module, TypeReference equipmentSlotType, GenericInstanceMethod tryParseGeneric, MethodReference stringEquality)
    {
        const string methodName = "TryParseEquipmentSlotAllowBelt";
        var existing = helperType.Methods.FirstOrDefault(m => m.Name == methodName);
        if (existing != null)
        {
            return existing;
        }

        var method = new MethodDefinition(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig,
            module.TypeSystem.Boolean);
        method.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.String));
        method.Parameters.Add(new ParameterDefinition("result", ParameterAttributes.Out, new ByReferenceType(equipmentSlotType)));
        helperType.Methods.Add(method);

        var il = method.Body.GetILProcessor();
        var fallThrough = Instruction.Create(OpCodes.Ldarg_0);
        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Ldstr, "Belt"));
        il.Append(Instruction.Create(OpCodes.Call, stringEquality));
        il.Append(Instruction.Create(OpCodes.Brfalse_S, fallThrough));
        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        il.Append(Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)BeltSlotValue));
        il.Append(Instruction.Create(OpCodes.Stind_I4));
        il.Append(Instruction.Create(OpCodes.Ldc_I4_1));
        il.Append(Instruction.Create(OpCodes.Ret));
        il.Append(fallThrough);
        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        il.Append(Instruction.Create(OpCodes.Call, tryParseGeneric));
        il.Append(Instruction.Create(OpCodes.Ret));

        return method;
    }
}
