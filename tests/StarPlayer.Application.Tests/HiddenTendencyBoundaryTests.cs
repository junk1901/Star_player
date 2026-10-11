using System.Reflection;
using StarPlayer.Domain.Baseball.PitchZone;
using StarPlayer.Infrastructure.Fixtures;

namespace StarPlayer.Application.Tests;

/// <summary>
/// Phase 3-B1: a batter's hidden zone tendency is resolver input only. WHAT THESE TESTS GUARANTEE (and no more): no field, property, method parameter,
/// return type or constructor parameter declared in the Application or Infrastructure assemblies names the <c>BatterZoneTendency</c> /
/// <c>ZoneReferenceDistribution</c> TYPES (directly, in an array or in a generic argument), and the type's own text form prints no values. They do NOT detect
/// the numbers being copied into some other DTO, string or JSON, and they say nothing about the Godot client (which cannot name Domain at all, see
/// ArchitectureTests). HANDOFF: when an Application query/DTO that could touch a batter first exists (Phase 3-D/5), add a test that serializes the real
/// output (JSON/text, including tooltip and Gemini payloads) and asserts the tendency values are absent.
/// </summary>
public class HiddenTendencyBoundaryTests
{
    private static readonly Assembly[] PublicLayers = [typeof(StarPlayer.Application.Queries.PlayerInfoService).Assembly, typeof(PlayerInfoComposition).Assembly];

    private static IEnumerable<Type> Mentions(Type type)
    {
        yield return type;
        if (type.HasElementType)
            foreach (var inner in Mentions(type.GetElementType()!))
                yield return inner;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                foreach (var inner in Mentions(argument))
                    yield return inner;
    }

    private static bool IsHidden(Type type) => Mentions(type).Any(t => t == typeof(BatterZoneTendency) || t == typeof(ZoneReferenceDistribution));

    [Fact]
    public void No_application_or_infrastructure_member_signature_names_the_tendency_or_reference_types()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var leaks = new List<string>();
        foreach (var type in PublicLayers.SelectMany(a => a.GetTypes()))
        {
            foreach (var field in type.GetFields(all).Where(f => IsHidden(f.FieldType)))
                leaks.Add($"{type.FullName}.{field.Name}");
            foreach (var property in type.GetProperties(all).Where(p => IsHidden(p.PropertyType)))
                leaks.Add($"{type.FullName}.{property.Name}");
            foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
                if (method.GetParameters().Any(p => IsHidden(p.ParameterType)) || (method is MethodInfo info && IsHidden(info.ReturnType)))
                    leaks.Add($"{type.FullName}.{method.Name}");
        }

        Assert.Empty(leaks);
    }

    [Fact]
    public void The_signature_guard_recognises_the_hidden_types_directly_in_arrays_and_in_generic_arguments()
    {
        Assert.True(IsHidden(typeof(BatterZoneTendency)));
        Assert.True(IsHidden(typeof(List<BatterZoneTendency>)));
        Assert.True(IsHidden(typeof(BatterZoneTendency[])));
        Assert.True(IsHidden(typeof(Dictionary<string, ZoneReferenceDistribution>)));
        Assert.False(IsHidden(typeof(PitchLocation)));
    }

    [Fact]
    public void The_tendency_text_form_does_not_print_its_values()
    {
        var tendency = BatterZoneTendency.Create([77, -77, 0, 0, 0, 0, 0, 0, 0], ZoneReferenceDistribution.UniformV1).Value;

        Assert.DoesNotContain("77", tendency.ToString());
    }
}
