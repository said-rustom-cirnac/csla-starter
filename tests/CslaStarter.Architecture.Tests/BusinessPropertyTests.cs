using System.Globalization;
using System.Reflection;
using Csla;
using Csla.Core;
using CslaStarter.Business.Base;
using CslaStarter.Business.Types;
using CslaStarter.Core.Localization;
using CslaStarter.Core.Resources;
using CslaStarter.Dal.Projects;

namespace CslaStarter.Architecture.Tests;

/// <summary>Every property a business object registers has a label in English and French.</summary>
public class BusinessPropertyTests
{
    private static readonly Assembly Business = typeof(AppBusinessBase<>).Assembly;

    [Fact]
    public void RegisteredProperties_HaveLocalizedDisplayName()
    {
        var missing = RegisteredProperties()
            .Where(p => p.Property?.GetCustomAttribute<LocalizedDisplayNameAttribute>() is null)
            .Select(p => $"{p.Owner.Name}.{p.Name}")
            .ToList();

        Assert.True(missing.Count == 0, "Add [LocalizedDisplayName] to: " + string.Join(", ", missing));
    }

    [Fact]
    public void LocalizedDisplayNameKeys_ExistInBothLanguages()
    {
        var missing = RegisteredProperties()
            .Select(p => p.Property?.GetCustomAttribute<LocalizedDisplayNameAttribute>()?.Key)
            .OfType<string>()
            .Distinct()
            .Where(key => Labels.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en-CA")) is null
                || Labels.ResourceManager.GetString(key, CultureInfo.GetCultureInfo("fr-CA")) == Labels.ResourceManager.GetString(key, CultureInfo.InvariantCulture)
                    && !Labels.ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo("fr"), true, false)!.Cast<System.Collections.DictionaryEntry>().Any(e => (string)e.Key == key))
            .ToList();

        Assert.True(missing.Count == 0, "Missing Labels keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void ProjectAccess_SeesAllRoles_MatchTheBusinessRoles() =>
        Assert.Equal(new HashSet<int> { UserRoleType.Admin, UserRoleType.Technical }, ProjectAccess.SeesAllRoles);

    // (owner type, registered name, CLR property) for every registered property of every CSLA type except commands.
    private static IEnumerable<(Type Owner, string Name, PropertyInfo? Property)> RegisteredProperties() =>
        Business.GetTypes()
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false } && typeof(ICslaObject).IsAssignableFrom(t) && !IsCommand(t))
            .SelectMany(t => FlattenStaticFields(t)
                .Where(f => typeof(IPropertyInfo).IsAssignableFrom(f.FieldType))
                .Select(f => (IPropertyInfo)f.GetValue(null)!)
                .Select(info => (t, info.Name, t.GetProperty(info.Name))));

    private static IEnumerable<FieldInfo> FlattenStaticFields(Type type)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                yield return field;
            }
        }
    }

    private static bool IsCommand(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(CommandBase<>))
            {
                return true;
            }
        }

        return false;
    }
}
