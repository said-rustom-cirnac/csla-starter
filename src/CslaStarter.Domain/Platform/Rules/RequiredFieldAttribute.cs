using System.ComponentModel.DataAnnotations;
using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Platform.Rules;

/// <summary>Marks a property as required; the message comes from <see cref="ErrorMessageType.Required"/>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class RequiredFieldAttribute : RequiredAttribute
{
    public override string FormatErrorMessage(string name) => Text.Error(ErrorMessageType.Required, name);
}
