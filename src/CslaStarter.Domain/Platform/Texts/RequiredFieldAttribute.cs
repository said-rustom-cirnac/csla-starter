using System.ComponentModel.DataAnnotations;

namespace CslaStarter.Domain.Platform.Texts;

/// <summary>Marks a property as required; the message comes from <see cref="ErrorMessageType.Required"/>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class RequiredFieldAttribute : RequiredAttribute
{
    public override string FormatErrorMessage(string name) => Text.Error(ErrorMessageType.Required, name);
}
