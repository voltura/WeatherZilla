using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

var option = new RewriteOptions();

option.AddRedirect("^$", "swagger");

app.UseRewriter(option);
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
