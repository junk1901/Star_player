using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace StarPlayer.Application.Tests;

/// <summary>
/// Compiled-code reference scanner (no Godot needed). It walks the IL of every method of an assembly and reports which
/// top-level types call, construct or load a member that belongs to another assembly. Unlike a namespace/text check this
/// sees through <c>var</c>, lambdas and async state machines (nested types count for their top-level owner). Known limit:
/// it does not decode generic type-argument blobs (<c>TypeSpec</c>), so a reference that appears only as a generic
/// argument is not seen; the public-surface tests and the type-reference list below cover that gap.
/// </summary>
internal static class IlScan
{
    private static readonly Dictionary<ushort, OperandType> Operands = BuildOperandTable();

    private static Dictionary<ushort, OperandType> BuildOperandTable()
    {
        var table = new Dictionary<ushort, OperandType>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetValue(null) is OpCode opcode)
                table[(ushort)opcode.Value] = opcode.OperandType;
        return table;
    }

    /// <summary>Full names of top-level types whose methods reference a member or type of <paramref name="targetAssembly"/>.</summary>
    public static HashSet<string> TypesReferencing(string assemblyPath, string targetAssembly)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();

        foreach (var typeHandle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(typeHandle);
            var owner = TopLevelName(md, type);
            if (result.Contains(owner))
                continue;
            foreach (var methodHandle in type.GetMethods())
            {
                var method = md.GetMethodDefinition(methodHandle);
                if (method.RelativeVirtualAddress == 0)
                    continue;
                var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
                if (il is not null && ReferencesAssembly(md, il, targetAssembly))
                {
                    result.Add(owner);
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>Names of the types of <paramref name="targetAssembly"/> that the assembly's metadata mentions at all.</summary>
    public static HashSet<string> NamedTypesFrom(string assemblyPath, string targetAssembly)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        return md.TypeReferences.Select(md.GetTypeReference)
            .Where(t => t.ResolutionScope.Kind == HandleKind.AssemblyReference
                        && md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)t.ResolutionScope).Name) == targetAssembly)
            .Select(t => $"{md.GetString(t.Namespace)}.{md.GetString(t.Name)}")
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string TopLevelName(MetadataReader md, TypeDefinition type)
    {
        while (!type.GetDeclaringType().IsNil)
            type = md.GetTypeDefinition(type.GetDeclaringType());
        var ns = md.GetString(type.Namespace);
        return ns.Length == 0 ? md.GetString(type.Name) : $"{ns}.{md.GetString(type.Name)}";
    }

    private static bool ReferencesAssembly(MetadataReader md, byte[] il, string target)
    {
        var i = 0;
        while (i < il.Length)
        {
            ushort code = il[i++];
            if (code == 0xFE && i < il.Length)
                code = (ushort)(0xFE00 | il[i++]);
            if (!Operands.TryGetValue(code, out var operand))
                return false; // unknown opcode: stop rather than misread the stream

            switch (operand)
            {
                case OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, i);
                    i += 4;
                    if (AssemblyOf(md, MetadataTokens.EntityHandle(token), 0) == target)
                        return true;
                    break;
                case OperandType.InlineSwitch:
                    i += 4 + 4 * BitConverter.ToInt32(il, i);
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    i += 2;
                    break;
                case OperandType.InlineI8 or OperandType.InlineR:
                    i += 8;
                    break;
                default: // InlineBrTarget, InlineI, InlineSig, InlineString, ShortInlineR
                    i += 4;
                    break;
            }
        }

        return false;
    }

    private static string? AssemblyOf(MetadataReader md, EntityHandle handle, int depth)
    {
        if (handle.IsNil || depth > 8)
            return null;
        switch (handle.Kind)
        {
            case HandleKind.MemberReference:
                return AssemblyOf(md, md.GetMemberReference((MemberReferenceHandle)handle).Parent, depth + 1);
            case HandleKind.MethodSpecification:
                return AssemblyOf(md, md.GetMethodSpecification((MethodSpecificationHandle)handle).Method, depth + 1);
            case HandleKind.TypeReference:
                var scope = md.GetTypeReference((TypeReferenceHandle)handle).ResolutionScope;
                return scope.Kind switch
                {
                    HandleKind.AssemblyReference => md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)scope).Name),
                    HandleKind.TypeReference => AssemblyOf(md, scope, depth + 1),
                    _ => null,
                };
            default:
                return null;
        }
    }
}
