using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// EquipmentSlot has no "Belt" member, so InventoryEquipment's constructor throws when it
// calls Enum.Parse(typeof(EquipmentSlot), "Belt") while building the slot for our custom
// Belt item template. This preload-time patch reroutes those Enum.Parse calls through a
// small injected method that special-cases "Belt" (returning a boxed placeholder value
// that no vanilla EquipmentSlot member uses) and otherwise falls through to the original
// Enum.Parse behavior unchanged.
public static class PackNStrapBeltPrepatch
{
    private const int BeltSlotValue = 15;

    public static IEnumerable<string> TargetDLLs => new[] { "Assembly-CSharp.dll" };

    public static void Patch(AssemblyDefinition assembly)
    {
        var module = assembly.MainModule;
        var inventoryEquipmentType = module.GetType("EFT.InventoryLogic.InventoryEquipment");
        if (inventoryEquipmentType == null)
        {
            Console.WriteLine("[PackNStrap Belt Prepatch] InventoryEquipment type not found; no changes made.");
            return;
        }

        var constructor = inventoryEquipmentType.Methods.FirstOrDefault(m =>
            m.IsConstructor && !m.IsStatic && m.HasBody &&
            m.Parameters.Count == 2 &&
            m.Parameters[0].ParameterType.FullName == "System.String" &&
            m.Body.Instructions.Any(IsEnumParseCall));

        if (constructor == null)
        {
            Console.WriteLine("[PackNStrap Belt Prepatch] InventoryEquipment constructor/Enum.Parse pattern not found; no changes made.");
            return;
        }

        var enumParseMethod = constructor.Body.Instructions
            .Where(i => i.OpCode == OpCodes.Call)
            .Select(i => i.Operand as MethodReference)
            .First(mr => mr != null && mr.DeclaringType.FullName == "System.Enum" && mr.Name == "Parse" && mr.Parameters.Count == 2);

        var allowBeltMethod = inventoryEquipmentType.Methods.FirstOrDefault(m => m.Name == "PNS_ParseEquipmentSlotAllowBelt");
        if (allowBeltMethod == null)
        {
            allowBeltMethod = new MethodDefinition(
                "PNS_ParseEquipmentSlotAllowBelt",
                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
                module.TypeSystem.Object);
            allowBeltMethod.Parameters.Add(new ParameterDefinition("enumType", ParameterAttributes.None, enumParseMethod.Parameters[0].ParameterType));
            allowBeltMethod.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, module.TypeSystem.String));
            inventoryEquipmentType.Methods.Add(allowBeltMethod);

            var stringEquality = new MethodReference("op_Equality", module.TypeSystem.Boolean, module.TypeSystem.String) { HasThis = false };
            stringEquality.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            stringEquality.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));

            var il = allowBeltMethod.Body.GetILProcessor();
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
            il.Append(Instruction.Create(OpCodes.Call, enumParseMethod));
            il.Append(Instruction.Create(OpCodes.Ret));
        }

        var replaced = 0;
        foreach (var instruction in constructor.Body.Instructions)
        {
            if (instruction.OpCode == OpCodes.Call && instruction.Operand is MethodReference mr &&
                mr.DeclaringType.FullName == "System.Enum" && mr.Name == "Parse" && mr.Parameters.Count == 2)
            {
                instruction.Operand = allowBeltMethod;
                replaced++;
            }
        }

        Console.WriteLine($"[PackNStrap Belt Prepatch] Patched InventoryEquipment for custom Belt slot ({replaced} Enum.Parse replacement(s)).");
    }

    private static bool IsEnumParseCall(Instruction instruction)
    {
        return instruction.OpCode == OpCodes.Call &&
               instruction.Operand is MethodReference mr &&
               mr.DeclaringType.FullName == "System.Enum" &&
               mr.Name == "Parse" &&
               mr.Parameters.Count == 2;
    }
}
