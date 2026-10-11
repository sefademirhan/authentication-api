using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AuthenticationApi.Swagger;

/// <summary>Show an authentication requirement only on endpoints decorated with [Authorize].</summary>
public class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodRequiresAuth = context.MethodInfo.IsDefined(typeof(AuthorizeAttribute), true);
        var classRequiresAuth =
            context.MethodInfo.DeclaringType?.IsDefined(typeof(AuthorizeAttribute), true) == true;

        if (!methodRequiresAuth && !classRequiresAuth)
            return;

        operation.Security ??= new List<OpenApiSecurityRequirement>();
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
}
