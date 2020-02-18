using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DatingApp.API.Data.Abstraction;

namespace DatingApp.API.Helpers
{
    /// <summary>
    /// This class updates our users last active time using Action Filter
    /// </summary>
    public class LogUserActivity : IAsyncActionFilter
    {
        // context means do something while action is being executed, next means run some code after action executes
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var resultContext = await next();
            // Get user Id from our token
            var userId = int.Parse(resultContext.HttpContext.User
                .FindFirst(ClaimTypes.NameIdentifier).Value);
            // Inject our repository from services
            var repo = resultContext.HttpContext.RequestServices.GetService<IDatingRepository>();
            // Call the repo and get the user
            var user = await repo.GetUser(userId, true);
            // Update last active time
            user.LastActive = DateTime.Now;
            // Save user
            await repo.SaveAll();
            
        }
    }
}
