using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Gameplay.CodeGen;

[Generator]
public class GameplayEventGenerator : IIncrementalGenerator
{
    private const string GameplayEventShortName = "GameplayEvent";
    private const string GameplayEventFullName = "GameplayEventAttribute";
    private const string EventSuffix = "Event";
    private const string OutputNamespace = "Gameplay.Abilities";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var events = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is TypeDeclarationSyntax tds && HasGameplayEventAttribute(tds),
            transform: static (ctx, _) => TransformEvent(ctx)
        ).Where(static info => info.StructName != null);

        context.RegisterSourceOutput(
            events.Collect(),
            static (spc, events) => GenerateCode(spc, events)
        );
    }

    /// <summary>快速语法检查：TypeDeclaration 是否带有 [GameplayEvent]。</summary>
    private static bool HasGameplayEventAttribute(TypeDeclarationSyntax typeDecl)
    {
        foreach (var list in typeDecl.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name == GameplayEventShortName || name == GameplayEventFullName)
                    return true;
            }
        }
        return false;
    }

    /// <summary>语义分析：验证 struct 是否满足生成条件并提取 Tag。</summary>
    private static EventInfo TransformEvent(GeneratorSyntaxContext ctx)
    {
        var typeDecl = (TypeDeclarationSyntax)ctx.Node;

        var typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol == null)
            return default;

        // 必须是 struct
        if (typeSymbol.TypeKind != TypeKind.Structure)
            return default;

        // 从 attribute 提取 Tag
        var tag = ExtractTag(typeDecl, ctx.SemanticModel);
        if (tag == null)
            return default;

        return new EventInfo
        {
            StructName = typeSymbol.Name,
            Tag = tag
        };
    }

    /// <summary>从 [GameplayEvent(Tag = "...")] 提取 Tag 值。</summary>
    private static string? ExtractTag(TypeDeclarationSyntax typeDecl, SemanticModel semanticModel)
    {
        foreach (var list in typeDecl.AttributeLists)
        {
            foreach (var attr in list.Attributes)
            {
                var name = attr.Name.ToString();
                if (name != GameplayEventShortName && name != GameplayEventFullName)
                    continue;

                // 优先用语义模型获取 NamedArguments
                var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (typeSymbol != null)
                {
                    foreach (var attrData in typeSymbol.GetAttributes())
                    {
                        if (attrData.AttributeClass?.Name != GameplayEventShortName &&
                            attrData.AttributeClass?.Name != GameplayEventFullName)
                            continue;

                        foreach (var namedArg in attrData.NamedArguments)
                        {
                            if (namedArg.Key == "Tag" && namedArg.Value.Value is string tagValue)
                                return tagValue;
                        }
                    }
                }

                // 回退：从语法树直接解析
                if (attr.ArgumentList != null)
                {
                    foreach (var arg in attr.ArgumentList.Arguments)
                    {
                        if (arg.NameEquals?.Name.Identifier.Text == "Tag" &&
                            arg.Expression is LiteralExpressionSyntax literal &&
                            literal.IsKind(SyntaxKind.StringLiteralExpression))
                        {
                            return literal.Token.ValueText;
                        }
                    }
                }
            }
        }
        return null;
    }

    /// <summary>生成 EGameplayEventKind enum + GameplayEventRegistry。</summary>
    private static void GenerateCode(SourceProductionContext spc, ImmutableArray<EventInfo> events)
    {
        if (events.IsDefaultOrEmpty) return;

        // 排序确保确定性输出
        var sorted = events.Sort(static (a, b) => string.CompareOrdinal(a.Tag, b.Tag));

        // 去重（按 Tag）
        var unique = new List<EventInfo>();
        var seenTags = new HashSet<string>();
        foreach (var ev in sorted)
        {
            if (seenTags.Add(ev.Tag))
                unique.Add(ev);
        }

        // 构建 enum member 名称（去 "Event" 后缀）
        var enumNameSet = new HashSet<string>();
        var members = new List<(string Name, string Tag)>();
        foreach (var ev in unique)
        {
            var memberName = ev.StructName.EndsWith(EventSuffix)
                ? ev.StructName.Substring(0, ev.StructName.Length - EventSuffix.Length)
                : ev.StructName;

            // 重名处理：加上完整 StructName
            if (!enumNameSet.Add(memberName))
                memberName = ev.StructName;

            members.Add((memberName, ev.Tag));
        }

        var source = BuildSource(members);
        spc.AddSource("EGameplayEventKind.g.cs", source);
    }

    /// <summary>构建最终的 C# 源码字符串。</summary>
    private static string BuildSource(List<(string Name, string Tag)> members)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {OutputNamespace};");
        sb.AppendLine();

        // Enum
        sb.AppendLine("public enum EGameplayEventKind : ushort");
        sb.AppendLine("{");
        sb.AppendLine("    Unknown = 0,");
        ushort id = 1;
        foreach (var (name, _) in members)
        {
            sb.AppendLine($"    {name} = {id},");
            id++;
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Registry
        sb.AppendLine("public static class GameplayEventRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly Dictionary<ushort, string> Tags = new()");
        sb.AppendLine("    {");
        id = 1;
        foreach (var (_, tag) in members)
        {
            sb.AppendLine($"        [{id}] = \"{tag}\",");
            id++;
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>从 SyntaxProvider transform 传递的事件信息。</summary>
    private struct EventInfo
    {
        public string StructName;
        public string Tag;
    }
}
