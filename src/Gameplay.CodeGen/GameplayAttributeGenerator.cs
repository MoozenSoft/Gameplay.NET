using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Gameplay.CodeGen;

[Generator]
public class GameplayAttributeGenerator : IIncrementalGenerator
{
    private const string GameplayAttributeShortName = "GameplayAttribute";
    private const string GameplayAttributeFullName = "GameplayAttributeAttribute";
    private const string InterfaceName = "IAttributeSetComponent";
    private const string InterfaceNamespace = "Gameplay.Abilities";
    private const string FieldTypeName = "GameplayAttributeData";
    private const string FieldTypeNamespace = "Gameplay.Abilities";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var fields = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is FieldDeclarationSyntax field && HasGameplayAttribute(field),
            transform: static (ctx, _) => TransformField(ctx)
        ).Where(static info => info.StructName != null);

        context.RegisterSourceOutput(
            fields.Collect(),
            static (spc, fields) => GenerateCode(spc, fields)
        );
    }

    /// <summary>快速语法检查：FieldDeclaration 是否带有 [GameplayAttribute]。</summary>
    private static bool HasGameplayAttribute(FieldDeclarationSyntax field)
    {
        foreach (var list in field.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name == GameplayAttributeShortName || name == GameplayAttributeFullName)
                    return true;
            }
        }
        return false;
    }

    /// <summary>语义分析：验证字段及其包含类型是否满足生成条件。</summary>
    private static FieldInfo TransformField(GeneratorSyntaxContext ctx)
    {
        var fieldDecl = (FieldDeclarationSyntax)ctx.Node;

        // 检查包含类型
        if (fieldDecl.Parent is not TypeDeclarationSyntax typeDecl)
            return default;

        var typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol == null)
            return default;

        // 必须是 struct
        if (typeSymbol.TypeKind != TypeKind.Structure)
            return default;

        // 必须实现 IAttributeSetComponent
        if (!ImplementsIAttributeSetComponent(typeSymbol))
            return default;

        // 取字段名（仅处理单变量声明，多变量声明取第一个）
        var variable = fieldDecl.Declaration.Variables[0];
        var fieldName = variable.Identifier.Text;

        // 验证字段类型为 GameplayAttributeData
        var fieldSymbol = ctx.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
        if (fieldSymbol == null)
            return default;
        if (!IsGameplayAttributeData(fieldSymbol.Type))
            return default;

        return new FieldInfo
        {
            StructNamespace = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty,
            StructName = typeSymbol.Name,
            FieldName = fieldName
        };
    }

    private static bool ImplementsIAttributeSetComponent(INamedTypeSymbol typeSymbol)
    {
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.Name == InterfaceName &&
                iface.ContainingNamespace?.ToDisplayString() == InterfaceNamespace)
                return true;
        }
        return false;
    }

    private static bool IsGameplayAttributeData(ITypeSymbol typeSymbol)
    {
        return typeSymbol.Name == FieldTypeName &&
               typeSymbol.ContainingNamespace?.ToDisplayString() == FieldTypeNamespace;
    }

    /// <summary>按 struct 分组生成 partial 代码。</summary>
    private static void GenerateCode(SourceProductionContext spc, ImmutableArray<FieldInfo> fields)
    {
        if (fields.IsDefaultOrEmpty) return;

        // 按 (Namespace, StructName) 分组
        var groups = new Dictionary<(string, string), List<string>>();
        foreach (var field in fields)
        {
            var key = (field.StructNamespace, field.StructName);
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<string>();
                groups[key] = list;
            }
            list.Add(field.FieldName);
        }

        // 按 (Namespace, StructName) 排序，保证不同编译间 ID 分配确定
        int nextId = 1;

        foreach (var kvp in groups.OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2))
        {
            var ns = kvp.Key.Item1;
            var structName = kvp.Key.Item2;
            var fieldNames = kvp.Value;
            var source = GeneratePartialStruct(ns, structName, fieldNames);
            var hintName = string.IsNullOrEmpty(ns)
                ? $"{structName}.GameplayAttribute.g.cs"
                : $"{ns}.{structName}.GameplayAttribute.g.cs";
            spc.AddSource(hintName, source);

            var handlesSource = GenerateHandles(ns, structName, fieldNames, ref nextId);
            var handlesHint = string.IsNullOrEmpty(ns)
                ? $"{structName}.GameplayAttributeHandles.g.cs"
                : $"{ns}.{structName}.GameplayAttributeHandles.g.cs";
            spc.AddSource(handlesHint, handlesSource);
        }
    }

    /// <summary>生成 GameplayAttribute 句柄 + RegisterAll。</summary>
    private static string GenerateHandles(string ns, string structName,
        List<string> fieldNames, ref int nextId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Friflo.Engine.ECS;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"public static class {structName}Attributes");
        sb.AppendLine("{");

        foreach (var fieldName in fieldNames)
        {
            int id = nextId++;
            if (id > 63)
            {
                sb.AppendLine($"#error GameplayAttribute ID {id} exceeds 63-bit DirtyAttributeComponent limit. Reduce attribute count (current max: 64).");
                sb.AppendLine();
                continue;
            }
            sb.AppendLine($"    public static readonly Gameplay.Abilities.GameplayAttribute {fieldName}");
            sb.AppendLine($"        = new(id: {id},");
            sb.AppendLine($"              writeCurrentValue: (entity, value) =>");
            sb.AppendLine($"              {{");
            sb.AppendLine($"                  ref var data = ref entity.GetComponent<{structName}>().{fieldName};");
            sb.AppendLine($"                  data.CurrentValue = value;");
            sb.AppendLine($"              }});");
            sb.AppendLine();
        }

        sb.AppendLine($"    public static void RegisterAll(Gameplay.Abilities.AttributeSystem sys)");
        sb.AppendLine($"    {{");
        foreach (var fieldName in fieldNames)
            sb.AppendLine($"        sys.RegisterAttribute({fieldName});");
        sb.AppendLine($"    }}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>生成 partial struct 的源代码。</summary>
    private static string GeneratePartialStruct(string ns, string structName, List<string> fieldNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Friflo.Engine.ECS;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        sb.AppendLine($"public partial struct {structName}");
        sb.AppendLine("{");

        foreach (var fieldName in fieldNames)
        {
            sb.AppendLine($"    public static ref Gameplay.Abilities.GameplayAttributeData Get{fieldName}(Entity entity)");
            sb.AppendLine($"        => ref entity.GetComponent<{structName}>().{fieldName};");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>从 SyntaxProvider transform 传递的字段信息。</summary>
    private struct FieldInfo
    {
        public string StructNamespace;
        public string StructName;
        public string FieldName;
    }
}
