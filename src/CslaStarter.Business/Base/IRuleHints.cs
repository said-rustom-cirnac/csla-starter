namespace CslaStarter.Business.Base;

/// <summary>Lets components ask a business object what its rules require, so markup never repeats the rules.</summary>
public interface IRuleHints
{
    bool PropertyHasRule(string propertyName, string ruleTypeName);

    int? MaxLengthOf(string propertyName);
}
