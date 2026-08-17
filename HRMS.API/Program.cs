using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using HRMS.API.Middleware;
using HRMS.API.Extensions;
using FluentValidation;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<HRMSDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddTransient<ExceptionHandlingMiddleware>();
builder.Services.AddApplicationServices();
builder.Services.AddInfrustructureServices();
builder.Services.AddAutoMapper(cfg => { }, typeof(HRMS.Application.Mappings.AttendanceMappingProfile).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(HRMS.Application.Validators.CreateAttendanceDtoValidator).Assembly);
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Frontend");
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
