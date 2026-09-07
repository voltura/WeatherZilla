using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    await next();
});

app.UseSwagger();
app.UseSwaggerUI();

var option = new RewriteOptions();

option.AddRedirect("^$", "swagger");

app.UseRewriter(option);
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
