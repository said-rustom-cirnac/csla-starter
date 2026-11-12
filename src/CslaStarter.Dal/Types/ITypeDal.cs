namespace CslaStarter.Dal.Types;

/// <summary>Reads *Type lookup tables. <c>typeName</c> is the business type's class name, e.g. "UserRoleType".</summary>
public interface ITypeDal
{
    Task<IReadOnlyList<TypeDto>> FetchAsync(string typeName, CancellationToken cancellationToken);
}
