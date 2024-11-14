using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WandererKey
{
    public static class Patcher
    {
        public static ManualLogSource Log = Logger.CreateLogSource("WandererKey");

        //--AssemblyVariables
        public static AssemblyDefinition PublicAssembly;

        public static AssemblyDefinition HooksAssembly;

        public static AssemblyDefinition ImprovedInput;

        public static AssemblyDefinition Drought;

        public static AssemblyDefinition BepInExAssembly;

        //II
        public static TypeDefinition PlayerKeybing;
        public static TypeReference PlayerKeybind;

        public static MethodDefinition Register;
        public static MethodReference RegisterRef;

        public static MethodDefinition IsPressed;
        public static MethodReference IsPressedRef;

        //Drought
        public static TypeDefinition DroughtMod;
        public static TypeReference DroughtModRef;

        public static ILProcessor Drought_OnEnable;

        public static MethodDefinition Drought_DoAbilities;
        public static ILProcessor IL_DoAbilities;

        public static VariableDefinition Drought_scug;

        public static FieldDefinition Pulse;


        //RainWorld
        public static TypeDefinition RainWorld;

        public static TypeDefinition OnRainWorld;

        public static TypeDefinition orig_PostModsInit;


        //awa
        public static MethodDefinition Drought_PostModsInit;

        //TargetDLLs is not really used, as i can't return the assemblies path there. method Patch is also useless
        public static IEnumerable<string> TargetDLLs { get; } = GetDLLs();

        public static IEnumerable<string> GetDLLs()
        {
            string[] modList = File.ReadAllLines(".\\RainWorld_Data\\StreamingAssets\\enabledMods.txt");

            if (modList.Any(i => i.Contains("2944727862")) && modList.Any(i => i.Contains("3324777051")))
            {

                string ImprovedInputPath = modList.FirstOrDefault(i => i.Contains("2944727862")).Remove(0, 10) + "\\plugins\\ImprovedInput.dll";
                string DroughtPath = modList.FirstOrDefault(i => i.Contains("3324777051")).Remove(0, 10) + "\\plugins\\Rain World Drought.dll";

                try
                {
                    
                    //set assemblies
                    HooksAssembly = AssemblyDefinition.ReadAssembly(".\\BepInEx\\plugins\\HOOKS-Assembly-CSharp.dll");
                    PublicAssembly = AssemblyDefinition.ReadAssembly(".\\BepInEx\\utils\\PUBLIC-Assembly-CSharp.dll");
                    ImprovedInput = AssemblyDefinition.ReadAssembly(ImprovedInputPath);
                    Drought = AssemblyDefinition.ReadAssembly(DroughtPath, new ReaderParameters { ReadWrite = true, InMemory = true });
                    BepInExAssembly = AssemblyDefinition.ReadAssembly(".\\BepInEx\\core\\BepInEx.dll");


                    Log.LogInfo(HooksAssembly.FullName);
                    Log.LogInfo(PublicAssembly.FullName);
                    Log.LogInfo(ImprovedInput.FullName);
                    Log.LogInfo(Drought.FullName);


                    
                    //set II vars
                    PlayerKeybing = ImprovedInput.MainModule.Types.FirstOrDefault(i => i.Name == "PlayerKeybind");
                    PlayerKeybind = Drought.MainModule.ImportReference(PlayerKeybing);

                    Pulse = new FieldDefinition("WandererPulse", FieldAttributes.Public | FieldAttributes.Static, PlayerKeybind);

                    Register = PlayerKeybing.Methods.FirstOrDefault(i => i.Name == "Register");

                    IsPressed = ImprovedInput.MainModule.Types.FirstOrDefault(i => i.Name == "CustomInputExt").Methods.FirstOrDefault(i => i.Name == "IsPressed");


                    
                    //set Drought Vars
                    DroughtMod = Drought.MainModule.Types.FirstOrDefault(i => i.Name == "DroughtMod");
                    Drought_OnEnable = DroughtMod.Methods.FirstOrDefault(i => i.Name == "OnEnable").Body.GetILProcessor();

                    Drought_DoAbilities = Drought.MainModule.Types.FirstOrDefault(i => i.Name == "PlayerHK").Methods.FirstOrDefault(i => i.Name == "DoAbilities");
                    IL_DoAbilities = Drought_DoAbilities.Body.GetILProcessor();


                    
                    //set RainWorld Vars
                    OnRainWorld = HooksAssembly.MainModule.Types.FirstOrDefault(i => i.Name == "RainWorld" && i.Name != "RainWorldGame");

                    orig_PostModsInit = OnRainWorld.NestedTypes.FirstOrDefault(i=>i.Name== "orig_PostModsInit");

                    RainWorld = PublicAssembly.MainModule.Types.FirstOrDefault(i => i.Name == "RainWorld" && i.Name != "RainWorldGame");


                    
                    //patch
                    if (!DroughtMod.Fields.Any(i => i.Name == "WandererPulse"))
                    {
                        
                        //create post init method
                        Drought_PostModsInit = new MethodDefinition("RainWorld_PostModsInit", MethodAttributes.Private, Drought.MainModule.TypeSystem.Void);
                        Drought_PostModsInit.Parameters.Add(new ParameterDefinition("orig", ParameterAttributes.None, Drought.MainModule.Import(orig_PostModsInit)));
                        Drought_PostModsInit.Parameters.Add(new ParameterDefinition("self", ParameterAttributes.None, Drought.MainModule.Import(RainWorld)));
                        

                        DroughtMod.Methods.Add(Drought_PostModsInit);
                        
                        ILProcessor postmods = Drought_PostModsInit.Body.GetILProcessor();
                        
                        postmods.Emit(OpCodes.Nop);
                        postmods.Emit(OpCodes.Ldarg_1);
                        postmods.Emit(OpCodes.Ldarg_2);
                        postmods.Emit(OpCodes.Callvirt, Drought.MainModule.ImportReference(orig_PostModsInit.Methods.FirstOrDefault(i=>i.Name=="Invoke")));
                        
                        postmods.Emit(OpCodes.Nop);
                        postmods.Emit(OpCodes.Ldsfld, Pulse);
                        postmods.Emit(OpCodes.Ldc_I4, 0);
                        postmods.Emit(OpCodes.Callvirt, Drought.MainModule.ImportReference(PlayerKeybing.Methods.FirstOrDefault(i => i.Name == "set_HideConfig")));
                        
                        postmods.Emit(OpCodes.Nop);
                        postmods.Emit(OpCodes.Ldsfld, DroughtMod.Fields.FirstOrDefault(i => i.Name == "Logger"));
                        
                        postmods.Emit(OpCodes.Ldstr,"PostInitDone");
                        postmods.Emit(OpCodes.Call, Drought.MainModule.ImportReference(BepInExAssembly.MainModule.Types.FirstOrDefault(i => i.Name== "ManualLogSource").Methods.FirstOrDefault(i => i.Name == "LogInfo")));
                        postmods.Emit(OpCodes.Nop);
                        postmods.Emit(OpCodes.Ret);
                        

                        postmods.Body.OptimizeMacros();
                        //Drought.MainModule.ImportReference((TypeReference)PlayerKeybind).Resolve();

                        DroughtMod.Fields.Add(Pulse);
                        
                        DroughtMod.Methods.FirstOrDefault(i => i.Name == "OnEnable").Body.Instructions.Remove(DroughtMod.Methods.FirstOrDefault(i => i.Name == "OnEnable").Body.Instructions.Last(i => i.OpCode == OpCodes.Ret));

                        MethodDefinition DOnEnable = DroughtMod.Methods.FirstOrDefault(i => i.Name == "OnEnable");

                        Instruction instruc = DOnEnable.Body.Instructions.First(i => i.OpCode == OpCodes.Stsfld);

                        Drought_OnEnable.InsertAfter(instruc, Instruction.Create(OpCodes.Ldarg_0));
                        Drought_OnEnable.InsertAfter(instruc.Next, Instruction.Create(OpCodes.Ldftn, Drought_PostModsInit));
                        Drought_OnEnable.InsertAfter(instruc.Next.Next, Instruction.Create(OpCodes.Newobj, Drought.MainModule.ImportReference(OnRainWorld.NestedTypes.FirstOrDefault(i => i.Name == "hook_PostModsInit").Methods.FirstOrDefault(i=>i.Name==".ctor"))));
                        Drought_OnEnable.InsertAfter(instruc.Next.Next.Next, Instruction.Create(OpCodes.Call, Drought.MainModule.ImportReference(OnRainWorld.Methods.FirstOrDefault(i=>i.Name=="add_PostModsInit"))));
                        Drought_OnEnable.InsertAfter(instruc.Next.Next.Next.Next, Instruction.Create(OpCodes.Nop));

                        Drought_OnEnable.Emit(OpCodes.Ldarg_0);
                        Drought_OnEnable.Emit(OpCodes.Ldstr, "drought:wandererpulse");
                        Drought_OnEnable.Emit(OpCodes.Ldstr, "Rain World Wanderer");
                        Drought_OnEnable.Emit(OpCodes.Ldstr, "Pulse Jump");
                        Drought_OnEnable.Emit(OpCodes.Ldc_I4, 99);
                        Drought_OnEnable.Emit(OpCodes.Ldc_I4, 0);
                        Drought_OnEnable.Emit(OpCodes.Call, Drought.MainModule.ImportReference(Register));
                        Drought_OnEnable.Emit(OpCodes.Stsfld, Pulse);
                        Drought_OnEnable.Emit(OpCodes.Ret);

                        Drought_OnEnable.Body.OptimizeMacros();

                        Drought_scug = Drought_DoAbilities.Body.Variables.FirstOrDefault(i => i.ToString() == "V_5");

                        IL_DoAbilities.Replace(Drought_DoAbilities.Body.Instructions.Where(i => i.Operand != null).Where(i => i.Operand.ToString() == "Player/InputPackage[] Player::get_input()").ToList()[1], Instruction.Create(OpCodes.Ldsfld, Pulse));
                        IL_DoAbilities.InsertAfter(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse"), Instruction.Create(OpCodes.Call, Drought.MainModule.ImportReference(IsPressed)));
                        IL_DoAbilities.InsertAfter(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next, Instruction.Create(OpCodes.Stloc_S, Drought_scug));
                        IL_DoAbilities.InsertAfter(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next, Instruction.Create(OpCodes.Ldloc_S, Drought_scug));
                        IL_DoAbilities.InsertAfter(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next, Instruction.Create(OpCodes.Ldc_I4,1));
                        IL_DoAbilities.Remove(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next);
                        IL_DoAbilities.Remove(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next);
                        IL_DoAbilities.Remove(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next);
                        IL_DoAbilities.Remove(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next);
                        IL_DoAbilities.Remove(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next);
                        IL_DoAbilities.Remove(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next);
                        IL_DoAbilities.Remove(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next);
                        IL_DoAbilities.Remove(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next);
                        IL_DoAbilities.Replace(Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next, Instruction.Create(OpCodes.Br_S, Drought_DoAbilities.Body.Instructions.FirstOrDefault(i => i.Operand != null && i.Operand.ToString() == "ImprovedInput.PlayerKeybind Rain_World_Drought.DroughtMod::WandererPulse").Next.Next.Next.Next.Next.Next.Next.Next.Next));
                        IL_DoAbilities.Emit(OpCodes.Ceq);


                        IL_DoAbilities.Body.OptimizeMacros();



                        Drought.Write(DroughtPath);

                        Log.LogInfo("Drought Patched");
                    }
                    else
                    {
                        Log.LogInfo("Drought already Patched");
                    }

                }
                catch (Exception e)
                {
                    Log.LogError(e);
                }

            }
            else
            {
                Log.LogError("Drought or ImprovedInput missing");
            }
            yield return "";
        }
public static void Patch(AssemblyDefinition assembly)
        {
            Log.LogDebug(assembly.FullName);
        }

        public static void Initialize()
        {
            Log.LogMessage("DPatcher Initializing");
        }
        public static void Finish()
        {
            Log.LogMessage("DPatcher Finished");


            PublicAssembly.Dispose();

            HooksAssembly.Dispose();

            ImprovedInput.Dispose();

            Drought.Dispose();

            BepInExAssembly.Dispose();
        }

        

    }
}