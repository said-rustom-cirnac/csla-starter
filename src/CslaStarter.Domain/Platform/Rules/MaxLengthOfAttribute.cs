using System.ComponentModel.DataAnnotations;
using CslaStarter.Domain.Platform.Texts;

namespace CslaStarter.Domain.Platform.Rules;

/// <summary>Limits a string's length; the message comes from <see cref="ErrorMessageType.MaxLength"/>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class MaxLengthOfAttribute(int maximum) : StringLengthAttribute(maximum)
{
    public override string FormatErrorMessage(string name) => Text.Error(ErrorMessageType.MaxLength, name, MaximumLength);
}
