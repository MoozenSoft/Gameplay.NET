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
        var compilation = context.CompilationProvider;

        var events = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is TypeDeclarationSyntax tds && HasGameplayEventAttribute(tds),
            transform: static (ctx, _) => TransformEvent(ctx)
        ).Where(static info => info.StructName != null);

        var combined = events.Collect().Combine(compilation);

        context.RegisterSourceOutput(
            combined,
            static (spc, pair) => GenerateCode(spc, pair.Left, pair.Right)
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

        var fullTypeName = typeSymbol.ContainingNamespace?.ToDisplayString() is { Length: > 0 } ns
            ? $"{ns}.{typeSymbol.Name}"
            : typeSymbol.Name;

        return new EventInfo
        {
            StructName = typeSymbol.Name,
            FullTypeName = fullTypeName,
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

    /// <summary>生成 EGameplayEventKind enum + GameplayEventRegistry + Frame/Bus partial。</summary>
    private static void GenerateCode(SourceProductionContext spc, ImmutableArray<EventInfo> events, Compilation compilation)
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
        var members = new List<(string Name, string Tag, string FullTypeName)>();
        foreach (var ev in unique)
        {
            var memberName = ev.StructName.EndsWith(EventSuffix)
                ? ev.StructName.Substring(0, ev.StructName.Length - EventSuffix.Length)
                : ev.StructName;

            // 重名处理：加上完整 StructName
            if (!enumNameSet.Add(memberName))
                memberName = ev.StructName;

            members.Add((memberName, ev.Tag, ev.FullTypeName));
        }

        // 只在 GameplayEventFrame 所在的主程序集生成
        if (!HasGameplayEventFrame(compilation)) return;

        spc.AddSource("EGameplayEventKind.g.cs", BuildKindSource(members));
        spc.AddSource("GameplayEventFrame.Payloads.g.cs", BuildFrameSource(members));
        spc.AddSource("GameplayEventBus.Enqueue.g.cs", BuildBusSource(members));
    }

    /// <summary>构建 EGameplayEventKind enum + GameplayEventRegistry。</summary>
    private static string BuildKindSource(List<(string Name, string Tag, string FullTypeName)> members)
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
        foreach (var (name, _, _) in members)
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
        foreach (var (_, tag, _) in members)
        {
            sb.AppendLine($"        [{id}] = \"{tag}\",");
            id++;
        }
        sb.AppendLine("    };");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>检查 GameplayEventFrame 是否定义在**当前程序集**中（而非引用程序集）。</summary>
    private static bool HasGameplayEventFrame(Compilation compilation)
    {
        var frameType = compilation.GetTypeByMetadataName($"{OutputNamespace}.GameplayEventFrame");
        return frameType != null && frameType.ContainingAssembly.Equals(compilation.Assembly);
    }

    /// <summary>从 SyntaxProvider transform 传递的事件信息。</summary>
    private struct EventInfo
    {
        public string StructName;
        public string FullTypeName;
        public string Tag;
    }

    /// <summary>构建 GameplayEventFrame partial —— per-kind StructBuffer + ResetPayloads。</summary>
    private static string BuildFrameSource(List<(string Name, string Tag, string FullTypeName)> members)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine();
        sb.AppendLine($"namespace {OutputNamespace};");
        sb.AppendLine();
        sb.AppendLine("public sealed partial class GameplayEventFrame");
        sb.AppendLine("{");

        foreach (var (_, _, fullType) in members)
        {
            var fieldName = $"{fullType.Split('.')[^1]}s";
            sb.AppendLine($"    public readonly StructBuffer<{fullType}> {fieldName} = new();");
        }
        sb.AppendLine();

        sb.AppendLine("    partial void ResetPayloads()");
        sb.AppendLine("    {");
        foreach (var (_, _, fullType) in members)
        {
            var fieldName = $"{fullType.Split('.')[^1]}s";
            sb.AppendLine($"        {fieldName}.Reset();");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // Accessors
        foreach (var (_, _, fullType) in members)
        {
            var fieldName = $"{fullType.Split('.')[^1]}s";
            sb.AppendLine($"    public ref {fullType} Get{fullType.Split('.')[^1]}(int index)");
            sb.AppendLine($"        => ref {fieldName}.GetRef(index);");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>构建 GameplayEventBus partial —— per-kind Enqueue 方法。</summary>
    private static string BuildBusSource(List<(string Name, string Tag, string FullTypeName)> members)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using Friflo.Engine.ECS;");
        sb.AppendLine();
        sb.AppendLine($"namespace {OutputNamespace};");
        sb.AppendLine();
        sb.AppendLine("public sealed partial class GameplayEventBus");
        sb.AppendLine("{");

        for (int i = 0; i < members.Count; i++)
        {
            var (name, _, fullType) = members[i];
            int id = i + 1;
            var fieldName = $"{fullType.Split('.')[^1]}s";
            sb.AppendLine($"    public void Enqueue(in {fullType} payload, Entity source, Entity target)");
            sb.AppendLine($"    {{");
            sb.AppendLine($"        int index = pending.{fieldName}.Add(payload);");
            sb.AppendLine($"        pending.Records.Add(new GameplayEventRecord");
            sb.AppendLine($"        {{");
            sb.AppendLine($"            EventId      = {id},");
            sb.AppendLine($"            Source       = source,");
            sb.AppendLine($"            Target       = target,");
            sb.AppendLine($"            PayloadIndex = index,");
            sb.AppendLine($"        }});");
            sb.AppendLine($"    }}");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }
}
