using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

// BepInEx preloader patcher contract: TargetDLLs + Patch(AssemblyDefinition) run against the
// raw assembly metadata before Unity loads it, which is the only way to make EquipmentSlot
// string-parsing recognize "Belt" without a real enum member existing for it.
public static class PackNStrapBeltPrepatch
{
    // Ordinal reused for the independent "Belt" slot whenever Assembly-CSharp code parses
    // an EquipmentSlot by name. Chosen to be one past the last real EquipmentSlot value.
    private const int BeltSlotValue = 15;

    private const string HelperNamespace = "PackNStrap.Generated";
    private const string HelperTypeName = "BeltSlotEnumHelper";

    public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

    public static void Patch(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;

        var equipmentSlotType = module.GetTypes()
            .FirstOrDefault(t => t.Name == "EquipmentSlot" && t.IsEnum);

        if (equipmentSlotType == null)
        {
            Console.WriteLine("[PackNStrap Belt Prepatch] EquipmentSlot enum not found; no changes made.");
            return;
        }

        var parseMethod = module.ImportReference(typeof(Enum).GetMethod(
            nameof(Enum.Parse), new[] { typeof(Type), typeof(string) }));

        var tryParseGeneric = new GenericInstanceMethod(module.ImportReference(
            typeof(Enum).GetMethods().First(m => m.Name == nameof(Enum.TryParse) && m.IsGenericMethodDefinition && m.GetParameters().Length == 2)));
        tryParseGeneric.GenericArguments.Add(equipmentSlotType);

        var stringEquality = module.ImportReference(typeof(string).GetMethod(
            "op_Equality", new[] { typeof(string), typeof(string) }));

        var helperType = GetOrCreateHelperType(module);
        var parseHelper = GetOrCreateParseHelper(helperType, module, parseMethod, stringEquality);
        var tryParseHelper = GetOrCreateTryParseHelper(helperType, module, equipmentSlotType, tryParseGeneric, stringEquality);

        int parseReplacements = 0;
        int tryParseReplacements = 0;

        foreach (var type in module.GetTypes())
        {
            if (type == helperType)
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                var instructions = method.Body.Instructions;
                for (int i = 0; i < instructions.Count; i++)
                {
                    var instruction = instructions[i];
                    if (instruction.OpCode != OpCodes.Call)
                    {
                        continue;
                    }

                    if (!(instruction.Operand is MethodReference calledMethod))
                    {
                        continue;
                    }

                    if (calledMethod.DeclaringType.FullName == "System.Enum"
                        && calledMethod.Name == "Parse"
                        && calledMethod.Parameters.Count == 2
                        && PrecedingLoadsType(instructions, i, "EquipmentSlot"))
                    {
                        instruction.Operand = parseHelper;
                        parseReplacements++;
                        continue;
                    }

                    if (calledMethod is GenericInstanceMethod genericMethod
                        && calledMethod.DeclaringType.FullName == "System.Enum"
                        && calledMethod.Name == "TryParse"
                        && genericMethod.GenericArguments.Count == 1
                        && genericMethod.GenericArguments[0].Name == "EquipmentSlot")
                    {
                        instruction.Operand = tryParseHelper;
                        tryParseReplacements++;
                    }
                }
            }
        }

        Console.WriteLine($"[PackNStrap Belt Prepatch] Patched InventoryEquipment for custom Belt slot ({parseReplacements} Enum.Parse replacement(s), {tryParseReplacements} Enum.TryParse replacement(s)).");
    }

    private static bool PrecedingLoadsType(Collection<Instruction> instructions, int callIndex, string typeName)
    {
        int start = Math.Max(0, callIndex - 8);
        for (int i = callIndex - 1; i >= start; i--)
        {
            if (instructions[i].OpCode == OpCodes.Ldtoken && instructions[i].Operand is TypeReference typeRef && typeRef.Name == typeName)
            {
                return true;
            }
        }

        return false;
    }

    private static TypeDefinition GetOrCreateHelperType(ModuleDefinition module)
    {
        var existing = module.GetType(HelperNamespace, HelperTypeName)
            ?? module.Types.FirstOrDefault(t => t.Namespace == HelperNamespace && t.Name == HelperTypeName);

        if (existing != null)
        {
            return existing;
        }

        var helperType = new TypeDefinition(
            HelperNamespace,
            HelperTypeName,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed,
            module.TypeSystem.Object);

        module.Types.Add(helperType);
        return helperType;
    }

    // object ParseEquipmentSlotAllowBelt(Type enumType, string value)
    //     => value == "Belt" ? (object)BeltSlotValue : Enum.Parse(enumType, value);
    private static MethodReference GetOrCreateParseHelper(
        TypeDefinition helperType, ModuleDefinition module, MethodReference parseMethod, MethodReference stringEquality)
    {
        var existing = helperType.Methods.FirstOrDefault(m => m.Name == "ParseEquipmentSlotAllowBelt");
        if (existing != null)
        {
            return existing;
        }

        var method = new MethodDefinition(
            "ParseEquipmentSlotAllowBelt",
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

    // bool TryParseEquipmentSlotAllowBelt(string value, out EquipmentSlot result)
    //     => value == "Belt" ? (result = (EquipmentSlot)BeltSlotValue) == result || true
    //                        : Enum.TryParse<EquipmentSlot>(value, out result);
    private static MethodReference GetOrCreateTryParseHelper(
        TypeDefinition helperType, ModuleDefinition module, TypeReference equipmentSlotType,
        GenericInstanceMethod tryParseGeneric, MethodReference stringEquality)
    {
        var existing = helperType.Methods.FirstOrDefault(m => m.Name == "TryParseEquipmentSlotAllowBelt");
        if (existing != null)
        {
            return existing;
        }

        var method = new MethodDefinition(
            "TryParseEquipmentSlotAllowBelt",
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
