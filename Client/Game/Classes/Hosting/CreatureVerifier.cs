//------------------------------------------------------------------------------
//      Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Terrarium.Hosting
{
    /// <summary>
    ///  Checks a creature assembly before it is loaded.  Modern .NET has no
    ///  code access security, so this is what keeps creatures in their box:
    ///  a creature may only use OrganismBase and an allow-list of harmless
    ///  framework types (no files, network, reflection, threads or native
    ///  code), and may not share state between organisms through statics.
    /// </summary>
    /// <remarks>
    ///  Every framework type an assembly uses, in a signature, a base type,
    ///  a call or an attribute, appears in its TypeRef table, so checking
    ///  those references covers everything the creature can touch.  The
    ///  rules follow the checks of the original native AsmCheck tool.
    /// </remarks>
    internal static class CreatureVerifier
    {
        private const string OrganismBaseAssembly = "OrganismBase";

        // Whole namespaces whose every type is harmless
        private static readonly HashSet<string> AllowedNamespaces = new HashSet<string>(StringComparer.Ordinal)
            {
                "System.Collections",
                "System.Collections.Generic",
                "System.Collections.ObjectModel",
                "System.Collections.Specialized",
                "System.Linq",
            };

        // Single types in otherwise off-limits namespaces
        private static readonly HashSet<string> AllowedTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                // Basic types
                "System.Object", "System.ValueType", "System.Enum", "System.String", "System.Boolean",
                "System.Char", "System.Byte", "System.SByte", "System.Int16", "System.UInt16", "System.Int32",
                "System.UInt32", "System.Int64", "System.UInt64", "System.Single", "System.Double", "System.Decimal",
                "System.DateTime", "System.TimeSpan", "System.Guid", "System.Math", "System.MathF", "System.Random",
                "System.Array", "System.Nullable`1", "System.Span`1", "System.ReadOnlySpan`1", "System.Index",
                "System.Range", "System.Tuple", "System.Convert", "System.BitConverter", "System.StringComparison",
                "System.StringComparer", "System.StringSplitOptions", "System.MidpointRounding",
                "System.IComparable", "System.IComparable`1", "System.IEquatable`1", "System.IDisposable",
                "System.ICloneable", "System.IFormattable", "System.IFormatProvider",
                "System.Text.StringBuilder", "System.Globalization.CultureInfo", "System.Globalization.NumberStyles",

                // Delegates and events, including those a creature declares itself
                "System.Delegate", "System.MulticastDelegate", "System.IAsyncResult", "System.AsyncCallback",
                "System.EventArgs", "System.EventHandler", "System.EventHandler`1", "System.Comparison`1",
                "System.Predicate`1", "System.Converter`2",

                // Array initializers load their data with a field handle
                "System.RuntimeFieldHandle",

                // Exceptions a creature might throw or catch
                "System.Exception", "System.SystemException", "System.ArgumentException",
                "System.ArgumentNullException", "System.ArgumentOutOfRangeException",
                "System.InvalidOperationException", "System.NotSupportedException", "System.NotImplementedException",
                "System.IndexOutOfRangeException", "System.NullReferenceException", "System.DivideByZeroException",
                "System.OverflowException", "System.ArithmeticException", "System.FormatException",
                "System.InvalidCastException", "System.Collections.Generic.KeyNotFoundException",

                // Positions, sizes and colors
                "System.Drawing.Point", "System.Drawing.PointF", "System.Drawing.Size", "System.Drawing.SizeF",
                "System.Drawing.Rectangle", "System.Drawing.RectangleF", "System.Drawing.Color",
                "System.Drawing.KnownColor",

                // In-memory streams, which creatures use to save their state
                "System.IO.Stream", "System.IO.MemoryStream", "System.IO.BinaryReader", "System.IO.BinaryWriter",
                "System.IO.SeekOrigin", "System.IO.IOException", "System.IO.EndOfStreamException",

                // Attributes the compiler and the project templates add
                "System.Attribute", "System.AttributeUsageAttribute", "System.AttributeTargets",
                "System.CLSCompliantAttribute", "System.FlagsAttribute", "System.ObsoleteAttribute",
                "System.ParamArrayAttribute", "System.SerializableAttribute", "System.NonSerializedAttribute",
                "System.Diagnostics.DebuggableAttribute", "System.Diagnostics.DebuggableAttribute/DebuggingModes",
                "System.Diagnostics.DebuggerBrowsableAttribute", "System.Diagnostics.DebuggerBrowsableState",
                "System.Diagnostics.DebuggerHiddenAttribute", "System.Diagnostics.DebuggerNonUserCodeAttribute",
                "System.Diagnostics.DebuggerStepThroughAttribute", "System.Diagnostics.DebuggerDisplayAttribute",
                "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute",
                "System.Reflection.AssemblyTitleAttribute", "System.Reflection.AssemblyDescriptionAttribute",
                "System.Reflection.AssemblyConfigurationAttribute", "System.Reflection.AssemblyCompanyAttribute",
                "System.Reflection.AssemblyProductAttribute", "System.Reflection.AssemblyCopyrightAttribute",
                "System.Reflection.AssemblyTrademarkAttribute", "System.Reflection.AssemblyCultureAttribute",
                "System.Reflection.AssemblyVersionAttribute", "System.Reflection.AssemblyFileVersionAttribute",
                "System.Reflection.AssemblyInformationalVersionAttribute",
                "System.Reflection.AssemblyMetadataAttribute", "System.Reflection.DefaultMemberAttribute",
                "System.Runtime.InteropServices.ComVisibleAttribute", "System.Runtime.InteropServices.GuidAttribute",
                "System.Runtime.Versioning.TargetFrameworkAttribute",
            };

        // Compiler support types (array initializers, string interpolation, iterators...),
        // except Unsafe, which allows raw memory access
        private const string CompilerServicesNamespace = "System.Runtime.CompilerServices";

        private static readonly HashSet<string> DeniedCompilerServicesTypes =
            new HashSet<string>(StringComparer.Ordinal) {"Unsafe"};

        // RuntimeHelpers is needed for array initializers, so only some of its members are allowed
        private const string RuntimeHelpersType = "System.Runtime.CompilerServices.RuntimeHelpers";

        private static readonly HashSet<string> AllowedRuntimeHelpersMembers =
            new HashSet<string>(StringComparer.Ordinal) {"InitializeArray", "CreateSpan", "IsReferenceOrContainsReferences"};

        /// <summary>
        ///  Checks an assembly and returns the problems found, or an empty list
        ///  when it is safe to load.
        /// </summary>
        public static IList<string> Verify(byte[] assembly)
        {
            var problems = new List<string>();
            try
            {
                using (var pe = new PEReader(new MemoryStream(assembly, false)))
                {
                    if (!pe.HasMetadata)
                    {
                        problems.Add("The file isn't a .NET assembly.");
                        return problems;
                    }

                    var reader = pe.GetMetadataReader();
                    if (!reader.IsAssembly)
                    {
                        problems.Add("The file is a .NET module, not an assembly.");
                        return problems;
                    }

                    CheckReferences(reader, problems);
                    CheckNativeCode(reader, problems);
                    CheckTypes(reader, problems);
                    CheckMethodBodies(pe, reader, problems);
                }
            }
            catch (BadImageFormatException e)
            {
                problems.Add("The file isn't a valid .NET assembly: " + e.Message);
            }

            return problems;
        }

        private static void CheckReferences(MetadataReader reader, List<string> problems)
        {
            foreach (var handle in reader.TypeReferences)
            {
                var reference = reader.GetTypeReference(handle);
                var name = FullName(reader, reference);
                var assembly = ResolutionAssembly(reader, reference);

                if (assembly == OrganismBaseAssembly || IsAllowed(name))
                {
                    continue;
                }

                if (assembly != null && IsCoreLibrary(assembly))
                {
                    problems.Add("Uses '" + name + "', which creatures aren't allowed to use.");
                }
                else
                {
                    problems.Add("Uses '" + name + "' from '" + (assembly ?? "an unknown assembly") +
                                 "'; creatures can only use OrganismBase and parts of the .NET base library.");
                }
            }

            // RuntimeHelpers is only allowed for its array initialization helpers
            foreach (var handle in reader.MemberReferences)
            {
                var member = reader.GetMemberReference(handle);
                if (member.Parent.Kind != HandleKind.TypeReference) continue;

                var parent = reader.GetTypeReference((TypeReferenceHandle) member.Parent);
                if (FullName(reader, parent) != RuntimeHelpersType) continue;

                var memberName = reader.GetString(member.Name);
                if (!AllowedRuntimeHelpersMembers.Contains(memberName))
                {
                    problems.Add("Uses 'RuntimeHelpers." + memberName + "', which creatures aren't allowed to use.");
                }
            }
        }

        private static void CheckNativeCode(MetadataReader reader, List<string> problems)
        {
            if (reader.GetTableRowCount(TableIndex.ModuleRef) > 0)
            {
                problems.Add("Calls native code (has module references).");
            }

            foreach (var handle in reader.MethodDefinitions)
            {
                var method = reader.GetMethodDefinition(handle);
                if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0 ||
                    (method.ImplAttributes & MethodImplAttributes.CodeTypeMask) != MethodImplAttributes.IL)
                {
                    problems.Add("Method '" + MethodName(reader, method) + "' calls native code.");
                }
            }
        }

        private static void CheckTypes(MetadataReader reader, List<string> problems)
        {
            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                var typeName = FullName(reader, type);
                var compilerGenerated = IsCompilerGenerated(reader, type);

                // Organisms must be public so the game can create them
                if (DerivesFromOrganism(reader, type) && !IsPublic(type))
                {
                    problems.Add("Organism class '" + typeName + "' must be public.");
                }

                if (compilerGenerated)
                {
                    // Lambda caches and array initializer data; not shared state
                    continue;
                }

                foreach (var fieldHandle in type.GetFields())
                {
                    var field = reader.GetFieldDefinition(fieldHandle);
                    if ((field.Attributes & FieldAttributes.Static) != 0 &&
                        (field.Attributes & FieldAttributes.Literal) == 0)
                    {
                        problems.Add("Static field '" + typeName + "." + reader.GetString(field.Name) +
                                     "' isn't allowed; organisms can't share state.  Use const or an instance field.");
                    }
                }
            }
        }

        private static void CheckMethodBodies(PEReader pe, MetadataReader reader, List<string> problems)
        {
            foreach (var handle in reader.MethodDefinitions)
            {
                var method = reader.GetMethodDefinition(handle);
                if (method.RelativeVirtualAddress == 0) continue;

                var declaringType = reader.GetTypeDefinition(method.GetDeclaringType());
                var compilerGenerated = IsCompilerGenerated(reader, declaringType);
                var body = pe.GetMethodBody(method.RelativeVirtualAddress);
                var il = body.GetILReader();
                var name = MethodName(reader, method);

                if (reader.GetString(method.Name) == ".cctor" && !compilerGenerated && !IsTrivialBody(body))
                {
                    problems.Add("Static constructor in '" + FullName(reader, declaringType) +
                                 "' isn't allowed; organisms can't share state.");
                }

                while (il.RemainingBytes > 0)
                {
                    var opCode = ReadOpCode(ref il);
                    switch (opCode)
                    {
                        case ILOpCode.Stsfld:
                        {
                            var token = il.ReadInt32();
                            if (!compilerGenerated && !IsCompilerGeneratedField(reader, token))
                            {
                                problems.Add("Method '" + name + "' writes a static field; organisms can't share state.");
                            }
                            break;
                        }
                        case ILOpCode.Calli:
                        case ILOpCode.Localloc:
                        case ILOpCode.Cpblk:
                        case ILOpCode.Initblk:
                        case ILOpCode.Jmp:
                        case ILOpCode.Arglist:
                        case ILOpCode.Mkrefany:
                        case ILOpCode.Refanyval:
                        case ILOpCode.Refanytype:
                            problems.Add("Method '" + name + "' uses the unsafe instruction '" + opCode + "'.");
                            SkipOperand(ref il, opCode);
                            break;
                        default:
                            SkipOperand(ref il, opCode);
                            break;
                    }
                }
            }
        }

        #region IL decoding

        private static ILOpCode ReadOpCode(ref BlobReader il)
        {
            int value = il.ReadByte();
            if (value == 0xFE)
            {
                value = 0xFE00 | il.ReadByte();
            }
            return (ILOpCode) value;
        }

        private static void SkipOperand(ref BlobReader il, ILOpCode opCode)
        {
            switch (opCode)
            {
                case ILOpCode.Switch:
                    var count = il.ReadInt32();
                    il.Offset += count * 4;
                    return;
            }

            il.Offset += OperandSize(opCode);
        }

        private static int OperandSize(ILOpCode opCode)
        {
            switch (opCode)
            {
                // 1-byte operands
                case ILOpCode.Ldarg_s: case ILOpCode.Ldarga_s: case ILOpCode.Starg_s: case ILOpCode.Ldloc_s:
                case ILOpCode.Ldloca_s: case ILOpCode.Stloc_s: case ILOpCode.Ldc_i4_s: case ILOpCode.Br_s:
                case ILOpCode.Brfalse_s: case ILOpCode.Brtrue_s: case ILOpCode.Beq_s: case ILOpCode.Bge_s:
                case ILOpCode.Bgt_s: case ILOpCode.Ble_s: case ILOpCode.Blt_s: case ILOpCode.Bne_un_s:
                case ILOpCode.Bge_un_s: case ILOpCode.Bgt_un_s: case ILOpCode.Ble_un_s: case ILOpCode.Blt_un_s:
                case ILOpCode.Leave_s: case ILOpCode.Unaligned:
                    return 1;

                // 2-byte operands
                case ILOpCode.Ldarg: case ILOpCode.Ldarga: case ILOpCode.Starg: case ILOpCode.Ldloc:
                case ILOpCode.Ldloca: case ILOpCode.Stloc:
                    return 2;

                // 8-byte operands
                case ILOpCode.Ldc_i8: case ILOpCode.Ldc_r8:
                    return 8;

                // 4-byte operands: tokens, 32-bit branches, ldc.i4 and ldc.r4
                case ILOpCode.Ldc_i4: case ILOpCode.Ldc_r4: case ILOpCode.Jmp: case ILOpCode.Call:
                case ILOpCode.Calli: case ILOpCode.Br: case ILOpCode.Brfalse: case ILOpCode.Brtrue:
                case ILOpCode.Beq: case ILOpCode.Bge: case ILOpCode.Bgt: case ILOpCode.Ble: case ILOpCode.Blt:
                case ILOpCode.Bne_un: case ILOpCode.Bge_un: case ILOpCode.Bgt_un: case ILOpCode.Ble_un:
                case ILOpCode.Blt_un: case ILOpCode.Callvirt: case ILOpCode.Cpobj: case ILOpCode.Ldobj:
                case ILOpCode.Ldstr: case ILOpCode.Newobj: case ILOpCode.Castclass: case ILOpCode.Isinst:
                case ILOpCode.Unbox: case ILOpCode.Ldfld: case ILOpCode.Ldflda: case ILOpCode.Stfld:
                case ILOpCode.Ldsfld: case ILOpCode.Ldsflda: case ILOpCode.Stsfld: case ILOpCode.Stobj:
                case ILOpCode.Box: case ILOpCode.Newarr: case ILOpCode.Ldelema: case ILOpCode.Ldelem:
                case ILOpCode.Stelem: case ILOpCode.Unbox_any: case ILOpCode.Refanyval: case ILOpCode.Mkrefany:
                case ILOpCode.Ldtoken: case ILOpCode.Leave: case ILOpCode.Ldftn: case ILOpCode.Ldvirtftn:
                case ILOpCode.Initobj: case ILOpCode.Constrained: case ILOpCode.Sizeof:
                    return 4;

                default:
                    return 0;
            }
        }

        private static bool IsTrivialBody(MethodBodyBlock body)
        {
            // An empty static constructor is just "ret"
            var il = body.GetILReader();
            return il.Length == 1 && il.ReadByte() == (byte) ILOpCode.Ret;
        }

        #endregion

        #region Metadata helpers

        private static bool IsAllowed(string fullName)
        {
            if (AllowedTypes.Contains(fullName) || IsFuncOrAction(fullName))
            {
                return true;
            }

            // Nested types are allowed when their outermost type is
            var outer = fullName;
            var nested = fullName.IndexOf('/');
            if (nested >= 0)
            {
                outer = fullName.Substring(0, nested);
                if (AllowedTypes.Contains(outer))
                {
                    return true;
                }
            }

            var dot = outer.LastIndexOf('.');
            if (dot < 0) return false;
            var ns = outer.Substring(0, dot);
            var simpleName = outer.Substring(dot + 1);

            if (ns == CompilerServicesNamespace)
            {
                return !DeniedCompilerServicesTypes.Contains(simpleName);
            }

            return AllowedNamespaces.Contains(ns);
        }

        // System.Func`1 to `17 and System.Action, Action`1 to `16, which lambdas use
        private static bool IsFuncOrAction(string fullName)
        {
            string arity;
            if (fullName.StartsWith("System.Func`", StringComparison.Ordinal))
            {
                arity = fullName.Substring("System.Func`".Length);
            }
            else if (fullName == "System.Action")
            {
                return true;
            }
            else if (fullName.StartsWith("System.Action`", StringComparison.Ordinal))
            {
                arity = fullName.Substring("System.Action`".Length);
            }
            else
            {
                return false;
            }

            int count;
            return int.TryParse(arity, out count) && count > 0 && count <= 17;
        }

        private static bool IsCoreLibrary(string assembly)
        {
            return assembly == "mscorlib" || assembly == "netstandard" || assembly == "System.Runtime" ||
                   assembly == "System.Private.CoreLib" || assembly.StartsWith("System.", StringComparison.Ordinal) ||
                   assembly == "System";
        }

        private static string FullName(MetadataReader reader, TypeReference reference)
        {
            var name = reader.GetString(reference.Name);
            if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                return FullName(reader, reader.GetTypeReference((TypeReferenceHandle) reference.ResolutionScope)) +
                       "/" + name;
            }

            var ns = reader.GetString(reference.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        private static string FullName(MetadataReader reader, TypeDefinition type)
        {
            var name = reader.GetString(type.Name);
            if (type.IsNested)
            {
                return FullName(reader, reader.GetTypeDefinition(type.GetDeclaringType())) + "/" + name;
            }

            var ns = reader.GetString(type.Namespace);
            return ns.Length == 0 ? name : ns + "." + name;
        }

        private static string ResolutionAssembly(MetadataReader reader, TypeReference reference)
        {
            while (reference.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                reference = reader.GetTypeReference((TypeReferenceHandle) reference.ResolutionScope);
            }

            if (reference.ResolutionScope.Kind == HandleKind.AssemblyReference)
            {
                var assembly = reader.GetAssemblyReference((AssemblyReferenceHandle) reference.ResolutionScope);
                return reader.GetString(assembly.Name);
            }

            return null;
        }

        private static string MethodName(MetadataReader reader, MethodDefinition method)
        {
            return FullName(reader, reader.GetTypeDefinition(method.GetDeclaringType())) + "." +
                   reader.GetString(method.Name);
        }

        private static bool IsCompilerGenerated(MetadataReader reader, TypeDefinition type)
        {
            // <>c, <PrivateImplementationDetails>, display classes and state machines
            while (true)
            {
                if (reader.GetString(type.Name).StartsWith("<", StringComparison.Ordinal))
                {
                    return true;
                }
                if (!type.IsNested)
                {
                    return false;
                }
                type = reader.GetTypeDefinition(type.GetDeclaringType());
            }
        }

        private static bool IsCompilerGeneratedField(MetadataReader reader, int token)
        {
            var handle = MetadataTokens.EntityHandle(token);
            if (handle.Kind != HandleKind.FieldDefinition)
            {
                // A static field of another assembly
                return false;
            }

            var field = reader.GetFieldDefinition((FieldDefinitionHandle) handle);
            return IsCompilerGenerated(reader, reader.GetTypeDefinition(field.GetDeclaringType()));
        }

        private static bool IsPublic(TypeDefinition type)
        {
            var visibility = type.Attributes & TypeAttributes.VisibilityMask;
            return visibility == TypeAttributes.Public || visibility == TypeAttributes.NestedPublic;
        }

        private static bool DerivesFromOrganism(MetadataReader reader, TypeDefinition type)
        {
            if (type.BaseType.Kind != HandleKind.TypeReference) return false;

            var baseType = reader.GetTypeReference((TypeReferenceHandle) type.BaseType);
            var name = FullName(reader, baseType);
            return ResolutionAssembly(reader, baseType) == OrganismBaseAssembly &&
                   (name == "OrganismBase.Animal" || name == "OrganismBase.Plant");
        }

        #endregion
    }
}
