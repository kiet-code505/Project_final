using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace TourismMvc.Localization;

public sealed class UserLocalizationFilter(UserText text, ITempDataDictionaryFactory tempDataFactory) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (UserText.IsUserRequest(context.HttpContext) && context.Result is ViewResult view)
        {
            context.HttpContext.Response.Headers.ContentLanguage = text.Language;
            foreach (var state in context.ModelState.Values)
            {
                var errors = state.Errors.ToArray();
                state.Errors.Clear();
                foreach (var error in errors)
                {
                    if (!string.IsNullOrEmpty(error.ErrorMessage)) state.Errors.Add(text[error.ErrorMessage]);
                    else if (error.Exception != null) state.Errors.Add(error.Exception);
                }
            }
            foreach (var key in new[] { "Title", "Description", "Message" })
                if (view.ViewData?[key] is string value) view.ViewData[key] = text[value];
            var tempData = tempDataFactory.GetTempData(context.HttpContext);
            foreach (var key in new[] { "AuthSuccess", "PaymentError", "PaymentNotice", "Success", "Error" })
                if (tempData.Peek(key) is string value) tempData[key] = text[value];
        }
        await next();
    }
}
