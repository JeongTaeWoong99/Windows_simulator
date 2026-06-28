using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MikaSourceGen
{
    // 빠뜨린 핸들러 진단 (MIKA001)
    // 그 프로젝트의 [PacketHandler]들이 다루는 접두사(C_/S_)로 "수신 측"을 추론하고,
    // 같은 접두사인데 핸들러가 없는 [Packet]에 대해 경고를 낸다.
    public sealed partial class PacketHandlerGenerator
    {
        static readonly DiagnosticDescriptor MissingHandlerRule = new(
            id: "MIKA001",
            title: "패킷 핸들러 없음",
            messageFormat: "수신 패킷 '{0}'에 [PacketHandler]가 없습니다. 핸들러를 작성하세요.",
            category: "MikaNetwork",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // 참조 어셈블리(MikaProtocol 등)까지 포함한 전체 [Packet] 타입의 FQN.
        static ImmutableArray<string> GetAllPacketTypeNames(Compilation comp)
        {
            var results = ImmutableArray.CreateBuilder<string>();

            void Inspect(INamedTypeSymbol t)
            {
                foreach (var a in t.GetAttributes())
                {
                    if (a.AttributeClass?.ToDisplayString() == PacketAttr)
                    {
                        results.Add(t.ToDisplayString());
                        break;
                    }
                }

                foreach (var nested in t.GetTypeMembers())
                    Inspect(nested);
            }

            void Visit(INamespaceSymbol ns)
            {
                foreach (var t in ns.GetTypeMembers())
                    Inspect(t);
                foreach (var child in ns.GetNamespaceMembers())
                    Visit(child);
            }

            // System/MemoryPack 등 대형 어셈블리는 제외하고 Mika* 만 스캔한다.
            Visit(comp.Assembly.GlobalNamespace);
            foreach (var refAsm in comp.SourceModule.ReferencedAssemblySymbols)
            {
                if (refAsm.Name.StartsWith("Mika", System.StringComparison.Ordinal))
                    Visit(refAsm.GlobalNamespace);
            }

            return results.ToImmutable();
        }

        static void ReportMissingHandlers(
            SourceProductionContext spc,
            (ImmutableArray<HandlerInfo> Handlers, ImmutableArray<string> Packets) input)
        {
            // 핸들러가 하나도 없는 프로젝트(MikaProtocol 등)는 측 추론 불가 -> skip
            if (input.Handlers.IsDefaultOrEmpty) return;

            var handled = new HashSet<string>();
            var inboundPrefixes = new HashSet<string>();

            foreach (var h in input.Handlers)
            {
                handled.Add(h.PacketType);
                var pfx = Prefix(h.PacketType);
                if (pfx != null) inboundPrefixes.Add(pfx);
            }

            if (inboundPrefixes.Count == 0) return;

            foreach (var pkt in input.Packets)
            {
                var pfx = Prefix(pkt);
                if (pfx == null || !inboundPrefixes.Contains(pfx)) continue;
                if (handled.Contains(pkt)) continue;

                spc.ReportDiagnostic(
                    Diagnostic.Create(MissingHandlerRule, Location.None, SimpleName(pkt)));
            }
        }

        // "MikaProtocol.C_EchoRequest" -> "C_EchoRequest"
        static string SimpleName(string fqn)
        {
            var i = fqn.LastIndexOf('.');
            return i < 0 ? fqn : fqn.Substring(i + 1);
        }

        // "MikaProtocol.C_EchoRequest" -> "C_" (언더스코어 없으면 null)
        static string? Prefix(string fqn)
        {
            var name = SimpleName(fqn);
            var u = name.IndexOf('_');
            return u <= 0 ? null : name.Substring(0, u + 1);
        }
    }
}
