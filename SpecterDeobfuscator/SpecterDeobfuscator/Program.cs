using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SpecterDeobfuscator
{
    class Program
    {
        public static ModuleDefMD mod = null;

        /// <summary>
        /// Save deobfuqcated file to disk
        /// </summary>
        static void savefile(ModuleDefMD mod)
        {
            string text2 = Path.GetDirectoryName(mod.Location);

            if (!text2.EndsWith("\\")) text2 += "\\";

            string path = text2 + Path.GetFileNameWithoutExtension(mod.Location) + "_deobfuscated" +
                          Path.GetExtension(mod.Location);
            var opts = new ModuleWriterOptions(mod);
            opts.Logger = DummyLogger.NoThrowInstance;
            mod.Write(path, opts);
            Console.WriteLine($"[!] File saved : {path}");
        }

        static void Main(string[] args)
        {
            if (args.Length < 0)
            {
                Console.WriteLine("Input file missing");
                return;
            }

            mod = ModuleDefMD.Load(args[0]);
            Console.WriteLine($"[!] File loaded : {mod.Location}");
            Console.WriteLine("[+] starting ConstantDemutation...");
            Console.WriteLine("     [+] grabbing Fields value");
            Deobfuscator.GrabFieldValues(mod);
            if(Deobfuscator.proxyInt != null)
            {
                Deobfuscator.ReplaceProxyInt(mod);
            }
            else
            {
                Console.WriteLine("     [-] could not find Fields value ! Aborting");
            }
            Console.WriteLine("[+] starting ConstantDecoding...");
            Console.WriteLine("     [+] Decoding integers using static method");
            Deobfuscator.DecodingInt(mod);
            savefile(mod);
            Console.ReadKey();
        }


    }

    class Deobfuscator
    {

        public static Dictionary<FieldDef, int> proxyInt = new Dictionary<FieldDef, int>();
        
        /// <summary>
        /// Grab proxy Int field value for constant demutation
        /// </summary>
        public static void GrabFieldValues(ModuleDefMD mod)
        {

            MethodDef modulector = mod.GlobalType.FindStaticConstructor();

            var inst = modulector.Body.Instructions;

            for (int i = 0; i < inst.Count; i++)
            {
                if (inst[i].IsLdcI4() && inst[i+1].OpCode == OpCodes.Stsfld)
                {
                    int value = inst[i].GetLdcI4Value();
                    FieldDef field = inst[i + 1].GetOperand() as FieldDef;

                    proxyInt.Add(field, value);
                    Console.WriteLine($"        [+] Grabbed Field {field.Name} with value {value}");

                }
            }
            Console.WriteLine("---");
        }

        /// <summary>
        /// Replace proxy int field with original value
        /// </summary>
        public static void ReplaceProxyInt(ModuleDefMD mod)
        {
            foreach (var t in mod.Types)
            {
                if (t.IsGlobalModuleType) continue;

                foreach (var m in t.Methods)
                {
                    if (!m.HasBody) continue;

                    var inst = m.Body.Instructions;

                    for (int i = 0; i < inst.Count; i++)
                   
                    if (inst[i].OpCode == OpCodes.Ldsfld)
                    {
                        FieldDef field = inst[i].GetOperand() as FieldDef;
                        int value;
                        if (proxyInt.TryGetValue(field, out value))
                        {
                            inst[i] = Instruction.CreateLdcI4(value);
                            Console.WriteLine($"        [+] Removed proxyInt {field.Name} to {value} in method : {m.Name}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decode integer staticaly
        /// </summary>
        public static void DecodingInt(ModuleDefMD mod)
        {

            MethodDef decodeMethod = mod.GlobalType.FindMethod("DecodeNum");

            if (decodeMethod == null) {
                Console.WriteLine($"        [-] Could not find decoding method. Aborting."); return;
            }

            foreach (var t in mod.Types)
            {
                if (t.IsGlobalModuleType) continue;

                foreach (var m in t.Methods)
                {
                    if (!m.HasBody) continue;

                    var inst = m.Body.Instructions;

                    for (int i = 0; i < inst.Count; i++)

                        if (inst[i].OpCode == OpCodes.Call)
                        {
                            if (inst[i].Operand.ToString().Contains(decodeMethod.Name))
                            {
                                int param1 = inst[i - 2].GetLdcI4Value();
                                int param2 = inst[i - 1].GetLdcI4Value();
                                int decodedInt = DecodeNum(param1, param2);

                                Instruction NOP = Instruction.Create(OpCodes.Nop);
                                
                                inst[i - 1] = NOP;
                                inst[i - 2] = NOP;

                                inst[i] = Instruction.CreateLdcI4(decodedInt);
                                Console.WriteLine($"        [+] Decoded Int with value {decodedInt} in method : {m.Name}");
                            }
                        }
                }
            }            
        }

        public static int DecodeNum(int A_0, int A_1)
        {
            int datetime;
            int timespan;

            unsafe
            {
                datetime = sizeof(DateTime);
                timespan = sizeof(TimeSpan);
            }

            int num = datetime - timespan;
            int num2 = A_0 + num;
            return A_0 ^ A_1;
        }
    }
}