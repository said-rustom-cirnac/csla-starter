using CslaStarter.Business.Security;
using CslaStarter.Core.Security;
using Microsoft.Extensions.DependencyInjection;

namespace CslaStarter.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>The current user and the data access context, both read from CSLA's ApplicationContext.</summary>
    public static IServiceCollection AddCslaStarterBusiness(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUser, CslaCurrentUser>();
        services.AddScoped<IDataAccessContext, CslaDataAccessContext>();
        return services;
    }
}
