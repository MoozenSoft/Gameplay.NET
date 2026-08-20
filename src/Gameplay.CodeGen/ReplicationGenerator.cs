using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Gameplay.CodeGen;

/// <summary>扫描 [Replicated] 组件，生成 serializer + diff + RegisterAll 三件套。</summary>
/// <remarks>
/// 生成的 <c>ReplicatedComponentRegistration.RegisterAll()</c> 须由使用方在启动阶段、首次
/// World.Update 之前调用一次（World 即 Gameplay 的运行时入口），一次性注册全部 [Replicated] 组件的
/// serializer + diff；遗漏调用会导致复制集为空、静默无复制。
/// </remarks>
[Generator]
public class ReplicationGenerator : IIncrementalGenerator
{
    private const string ReplicatedShortName = "Replicated";
    private const string ReplicatedFullName = "ReplicatedAttribute";
    private const string RegistryFullName = "Gameplay.Replication.ReplicationRegistry";

    /// <summary>不支持字段类型的编译诊断（spec §4.1 fail-fast）。</summary>
    private static readonly DiagnosticDescriptor UnsupportedFieldTypeDescriptor = new(
        id: "GP_REPL001",
        title: "复制组件包含不支持的字段类型",
        messageFormat: "组件 {0} 的字段 {1} 类型不支持复制（仅支持 primitive / Vector3 / Quaternion / enum）",
        category: "Gameplay.Replication",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilation = context.CompilationProvider;
        var components = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is TypeDeclarationSyntax tds && HasReplicated(tds),
            transform: static (ctx, _) => TransformComponent(ctx)
        ).Where(static info => info.StructName != null);

        var combined = components.Collect().Combine(compilation);
        context.RegisterSourceOutput(combined, static (spc, pair) => GenerateCode(spc, pair.Left, pair.Right));
    }

    private static bool HasReplicated(TypeDeclarationSyntax typeDecl)
    {
        foreach (var list in typeDecl.AttributeLists)
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name == ReplicatedShortName || name == ReplicatedFullName)
                    return true;
            }
        return false;
    }

    private static ComponentInfo TransformComponent(GeneratorSyntaxContext ctx)
    {
        var typeDecl = (TypeDeclarationSyntax)ctx.Node;
        var typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol == null || typeSymbol.TypeKind != TypeKind.Structure)
            return default;

        var fields = new List<FieldInfo>();
        string? unsupportedField = null;
        Location? unsupportedLocation = null;
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IFieldSymbol field || field.IsStatic || field.IsImplicitlyDeclared)
                continue;
            var kind = Classify(field.Type);
            if (kind == FieldKind.Unsupported)
            {
                // 记录首个不支持字段，组件仍保留流转到 GenerateCode → 报编译诊断并跳过代码生成（spec §4.1）
                unsupportedField ??= field.Name;
                unsupportedLocation ??= field.Locations.Length > 0 ? field.Locations[0] : Location.None;
                continue;
            }
            fields.Add(new FieldInfo { Name = field.Name, Kind = kind, TypeName = field.Type.Name });
        }

        return new ComponentInfo
        {
            StructNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            StructName = typeSymbol.Name,
            Fields = fields,
            HasUnsupportedField = unsupportedField != null,
            UnsupportedFieldName = unsupportedField ?? string.Empty,
            Location = unsupportedLocation ?? Location.None,
        };
    }

    private static FieldKind Classify(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Int32:
            case SpecialType.System_Single:
            case SpecialType.System_Boolean:
                return FieldKind.Primitive;
        }
        if (type.TypeKind == TypeKind.Enum)
            return FieldKind.Enum;
        if (type.Name == "Vector3" && type.ContainingNamespace?.ToDisplayString() == "Gameplay.Core")
            return FieldKind.Vector3;
        if (type.Name == "Quaternion" && type.ContainingNamespace?.ToDisplayString() == "Gameplay.Core")
            return FieldKind.Quaternion;
        return FieldKind.Unsupported;
    }

    private static void GenerateCode(SourceProductionContext spc, ImmutableArray<ComponentInfo> components, Compilation compilation)
    {
        if (components.IsDefaultOrEmpty) return;
        var sorted = components.Sort(static (a, b) => string.CompareOrdinal(a.StructName, b.StructName));

        // 每个组件生成 XxxSerializer + XxxReplication（任何程序集，只要组件标了 [Replicated]）；
        // 含不支持字段的组件：报编译诊断并跳过代码生成（spec §4.1 fail-fast）
        foreach (var c in sorted)
        {
            if (c.HasUnsupportedField)
            {
                spc.ReportDiagnostic(Diagnostic.Create(UnsupportedFieldTypeDescriptor, c.Location, c.StructName, c.UnsupportedFieldName));
                continue;
            }
            spc.AddSource($"{c.StructNamespace}.{c.StructName}.Replication.g.cs", GeneratePerComponent(c));
        }

        // RegisterAll 只在定义 ReplicationRegistry 的程序集生成
        if (HasReplicationRegistry(compilation))
            spc.AddSource("ReplicatedComponentRegistration.g.cs", GenerateRegisterAll(sorted));
    }

    private static bool HasReplicationRegistry(Compilation compilation)
        => compilation.GetTypeByMetadataName(RegistryFullName) != null
           && SymbolEqualityComparer.Default.Equals(
               compilation.GetTypeByMetadataName(RegistryFullName)!.ContainingAssembly,
               compilation.Assembly);

    private static string GeneratePerComponent(ComponentInfo c)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Friflo.Engine.ECS;");
        sb.AppendLine("using Gameplay.Core;");
        sb.AppendLine("using Gameplay.Replication;");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(c.StructNamespace))
            sb.AppendLine($"namespace {c.StructNamespace};");
        sb.AppendLine();

        // Serializer
        sb.AppendLine($"public sealed class {c.StructName}Serializer : IComponentSerializer<{c.StructName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public void Write(in {c.StructName} c, ref ByteWriter w)");
        sb.AppendLine("    {");
        foreach (var f in c.Fields)
            sb.AppendLine($"        {WriteExpr(f, "c")}");
        sb.AppendLine("    }");
        sb.AppendLine($"    public void Read(ref {c.StructName} c, ref ByteReader r)");
        sb.AppendLine("    {");
        foreach (var f in c.Fields)
            sb.AppendLine($"        {ReadExpr(f, "c")}");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Diff
        sb.AppendLine($"public readonly struct {c.StructName}Replication : IReplicationDiff<{c.StructName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public bool Equals(in {c.StructName} a, in {c.StructName} b)");
        sb.AppendLine($"        => {EqualsExpr(c)};");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>生成 ReplicatedComponentRegistration.RegisterAll：一次性注册全部 [Replicated] 组件。
    /// 使用方须在启动阶段、首次 World.Update 之前调用一次 RegisterAll()，否则复制集为空、静默无复制。</summary>
    private static string GenerateRegisterAll(ImmutableArray<ComponentInfo> sorted)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Gameplay.Core;");
        sb.AppendLine("using Gameplay.Replication;");
        sb.AppendLine();
        sb.AppendLine("namespace Gameplay.Replication;");
        sb.AppendLine();
        sb.AppendLine("public static class ReplicatedComponentRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>一次性注册全部 [Replicated] 组件的 serializer + diff（须在首次 World.Update 之前调用一次）。</summary>");
        sb.AppendLine("    public static void RegisterAll()");
        sb.AppendLine("    {");
        foreach (var c in sorted)
        {
            if (c.HasUnsupportedField) continue;   // 未生成 serializer/diff，跳过注册
            sb.AppendLine($"        SerializerRegistry.Register(new {c.StructName}Serializer());");
            sb.AppendLine($"        ReplicationRegistry.Register<{c.StructName}>(new {c.StructName}Replication());");
        }
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string WriteExpr(FieldInfo f, string obj)
        => f.Kind switch
        {
            FieldKind.Primitive => $"w.Write({obj}.{f.Name});",
            FieldKind.Enum => $"w.Write((int){obj}.{f.Name});",
            FieldKind.Vector3 => $"w.Write(in {obj}.{f.Name});",
            FieldKind.Quaternion => $"w.Write(in {obj}.{f.Name});",
            _ => string.Empty,
        };

    private static string ReadExpr(FieldInfo f, string obj)
        => f.Kind switch
        {
            FieldKind.Primitive => $"{obj}.{f.Name} = r.Read{PrimitiveRead(f)}();",
            FieldKind.Enum => $"{obj}.{f.Name} = ({f.TypeName})r.ReadInt();",
            FieldKind.Vector3 => $"{obj}.{f.Name} = r.ReadVector3();",
            FieldKind.Quaternion => $"{obj}.{f.Name} = r.ReadQuaternion();",
            _ => string.Empty,
        };

    private static string PrimitiveRead(FieldInfo f)
        => f.TypeName switch
        {
            "Int32" => "Int",
            "Single" => "Float",
            "Boolean" => "Bool",
            _ => "Int",
        };

    private static string EqualsExpr(ComponentInfo c)
    {
        if (c.Fields.Count == 0) return "true";
        var parts = new List<string>();
        foreach (var f in c.Fields)
        {
            // Vector3/Quaternion 未定义 operator==（实现 IEquatable<T>），用 .Equals() 比较
            if (f.Kind == FieldKind.Vector3 || f.Kind == FieldKind.Quaternion)
                parts.Add($"a.{f.Name}.Equals(b.{f.Name})");
            else
                parts.Add($"a.{f.Name} == b.{f.Name}");
        }
        return string.Join(" && ", parts);
    }

    private enum FieldKind { Primitive, Enum, Vector3, Quaternion, Unsupported }

    private struct FieldInfo { public string Name; public FieldKind Kind; public string TypeName; }

    private struct ComponentInfo
    {
        public string StructNamespace;
        public string StructName;
        public List<FieldInfo> Fields;
        public bool HasUnsupportedField;
        public string UnsupportedFieldName;
        public Location Location;
    }
}
