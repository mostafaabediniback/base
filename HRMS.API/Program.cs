using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "HRMS API", Version = "v1" }));
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

var db = new TestDb();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "HRMS.API", mode = "frontend-test" }));

// Users
app.MapGet("/api/Users", () => Results.Ok(db.Users.Select(db.UserResponse)));
app.MapGet("/api/Users/{id:int}", (int id) => db.Users.FirstOrDefault(x => x.Id == id) is { } u ? Results.Ok(db.UserResponse(u)) : Results.NotFound());
app.MapPost("/api/Users", (CreateUserDto dto) => {
    var u = new UserRow { Id = db.NextUser++, FirstName = dto.FirstName, LastName = dto.LastName, Email = dto.Email, SSN = dto.SSN, Phone = dto.Phone, CreatedAt = DateTime.UtcNow };
    db.Users.Add(u);
    if (dto.CompanyId is int companyId && db.Companies.Any(c => c.Id == companyId)) db.UserCompanies.Add(new UserCompanyRow(u.Id, companyId, dto.Role ?? CompanyRole.View_Only));
    return Results.Created($"/api/Users/{u.Id}", db.UserResponse(u));
});
app.MapPut("/api/Users/{id:int}", (int id, UpdateUserDto dto) => {
    var u = db.Users.FirstOrDefault(x => x.Id == id); if (u is null) return Results.NotFound();
    u.FirstName = dto.FirstName; u.LastName = dto.LastName; u.Phone = dto.Phone; return Results.NoContent();
});
app.MapDelete("/api/Users/{id:int}", (int id) => { db.Users.RemoveAll(x => x.Id == id); db.UserCompanies.RemoveAll(x => x.UserId == id); return Results.NoContent(); });

// Companies + UserCompany
app.MapGet("/api/Companies", () => Results.Ok(db.Companies.Select(db.CompanyResponse)));
app.MapGet("/api/Companies/{id:int}", (int id) => db.Companies.FirstOrDefault(x => x.Id == id) is { } c ? Results.Ok(db.CompanyResponse(c)) : Results.NotFound());
app.MapGet("/api/Companies/User/{userId:int}", (int userId) => Results.Ok(db.UserCompanies.Where(x => x.UserId == userId).Join(db.Companies, x => x.CompanyId, c => c.Id, (_, c) => db.CompanyResponse(c))));
app.MapGet("/api/Companies/RegNum/{regNum}", (string regNum) => db.Companies.FirstOrDefault(x => x.RegNum.Equals(regNum, StringComparison.OrdinalIgnoreCase)) is { } c ? Results.Ok(db.CompanyResponse(c)) : Results.NotFound());
app.MapPost("/api/Companies/{companyId:int}/User/{userId:int}", (int companyId, int userId, CompanyRole role) => {
    if (!db.Companies.Any(x => x.Id == companyId) || !db.Users.Any(x => x.Id == userId)) return Results.NotFound();
    db.UserCompanies.RemoveAll(x => x.UserId == userId && x.CompanyId == companyId); db.UserCompanies.Add(new(userId, companyId, role)); return Results.Ok(new { userId, companyId, role });
});
app.MapPost("/api/Companies", (CreateCompanyDto dto) => {
    if (!db.Users.Any(x => x.Id == dto.UserId)) return Results.BadRequest(new { message = "UserId not found" });
    var c = new CompanyRow { Id = db.NextCompany++, Name = dto.Name, RegNum = dto.RegNum, Address = dto.Address, CreatedAt = DateTime.UtcNow };
    db.Companies.Add(c); db.UserCompanies.Add(new(dto.UserId, c.Id, dto.Role)); return Results.Created($"/api/Companies/{c.Id}", db.CompanyResponse(c));
});
app.MapPut("/api/Companies/{id:int}", (int id, UpdateCompanyDto dto) => { var c = db.Companies.FirstOrDefault(x => x.Id == id); if (c is null) return Results.NotFound(); c.Address = dto.Address; return Results.NoContent(); });
app.MapDelete("/api/Companies/{id:int}", (int id) => { db.Companies.RemoveAll(x => x.Id == id); db.UserCompanies.RemoveAll(x => x.CompanyId == id); db.Departments.RemoveAll(x => x.CompanyId == id); db.Shifts.RemoveAll(x => x.CompanyId == id); return Results.NoContent(); });

// Departments
app.MapGet("/api/Departments", () => Results.Ok(db.Departments.Select(db.DepartmentResponse)));
app.MapGet("/api/Departments/{id:int}", (int id) => db.Departments.FirstOrDefault(x => x.Id == id) is { } d ? Results.Ok(db.DepartmentResponse(d)) : Results.NotFound());
app.MapGet("/api/Departments/company/{companyId:int}", (int companyId) => Results.Ok(db.Departments.Where(x => x.CompanyId == companyId).Select(db.DepartmentResponse)));
app.MapPost("/api/Departments", (CreateDepartmentDto dto) => { if (!db.Companies.Any(x => x.Id == dto.CompanyId)) return Results.BadRequest(new { message = "Company not found" }); var d = new DepartmentRow { Id = db.NextDepartment++, Name = dto.Name, Description = dto.Description, CompanyId = dto.CompanyId }; db.Departments.Add(d); return Results.Created($"/api/Departments/{d.Id}", db.DepartmentResponse(d)); });
app.MapPut("/api/Departments/{id:int}", (int id, UpdateDepartmentDto dto) => { var d = db.Departments.FirstOrDefault(x => x.Id == id); if (d is null) return Results.NotFound(); d.Name = dto.Name; d.Description = dto.Description; return Results.NoContent(); });
app.MapDelete("/api/Departments/{id:int}", (int id) => { db.Departments.RemoveAll(x => x.Id == id); db.EmployeeDepartments.RemoveAll(x => x.DepartmentId == id); return Results.NoContent(); });

// Employees + EmployeeDepartment
app.MapGet("/api/Employee", () => Results.Ok(db.Employees.Select(db.EmployeeResponse)));
app.MapGet("/api/Employee/{id:int}", (int id) => db.Employees.FirstOrDefault(x => x.Id == id) is { } e ? Results.Ok(db.EmployeeResponse(e)) : Results.NotFound());
app.MapPost("/api/Employee", (CreateEmployeeDto dto) => { var e = new EmployeeRow { Id = db.NextEmployee++, FirstName = dto.FirstName, LastName = dto.LastName, Email = dto.Email, Phone = dto.Phone, Address = dto.Address, SSN = dto.SSN, HireDate = dto.HireDate }; db.Employees.Add(e); return Results.Created($"/api/Employee/{e.Id}", db.EmployeeResponse(e)); });
app.MapPut("/api/Employee/{id:int}", (int id, UpdateEmployeeDto dto) => { var e = db.Employees.FirstOrDefault(x => x.Id == id); if (e is null) return Results.NotFound(); e.FirstName = dto.FirstName; e.LastName = dto.LastName; e.Address = dto.Address; e.Phone = dto.Phone; return Results.NoContent(); });
app.MapDelete("/api/Employee/{id:int}", (int id) => { db.Employees.RemoveAll(x => x.Id == id); db.EmployeeDepartments.RemoveAll(x => x.EmployeeId == id); return Results.NoContent(); });
app.MapPost("/api/Employee/{employeeId:int}/department/{departmentId:int}", (int employeeId, int departmentId) => { if (!db.Employees.Any(x => x.Id == employeeId) || !db.Departments.Any(x => x.Id == departmentId)) return Results.NotFound(); if (!db.EmployeeDepartments.Any(x => x.EmployeeId == employeeId && x.DepartmentId == departmentId)) db.EmployeeDepartments.Add(new(employeeId, departmentId, !db.EmployeeDepartments.Any(x => x.EmployeeId == employeeId))); return Results.Ok(); });
app.MapPost("/api/Employee/{employeeId:int}/departments/{departmentId:int}/primary", (int employeeId, int departmentId) => { if (!db.EmployeeDepartments.Any(x => x.EmployeeId == employeeId && x.DepartmentId == departmentId)) return Results.NotFound(); foreach (var x in db.EmployeeDepartments.Where(x => x.EmployeeId == employeeId)) x.IsPrimary = x.DepartmentId == departmentId; return Results.Ok(); });

// Shifts
app.MapGet("/api/Shifts", () => Results.Ok(db.Shifts.Select(db.ShiftResponse)));
app.MapGet("/api/Shifts/{id:int}", (int id) => db.Shifts.FirstOrDefault(x => x.Id == id) is { } s ? Results.Ok(db.ShiftResponse(s)) : Results.NotFound());
app.MapGet("/api/Shifts/Company/{companyId:int}", (int companyId) => Results.Ok(db.Shifts.Where(x => x.CompanyId == companyId).Select(db.ShiftResponse)));
app.MapPost("/api/Shifts", (CreateShiftDto dto) => { if (!db.Companies.Any(x => x.Id == dto.CompanyId)) return Results.BadRequest(new { message = "Company not found" }); var s = new ShiftRow { Id = db.NextShift++, ShiftName = dto.ShiftName, CompanyId = dto.CompanyId, StartTime = dto.StartTime, EndTime = dto.EndTime }; db.Shifts.Add(s); return Results.Created($"/api/Shifts/{s.Id}", db.ShiftResponse(s)); });
app.MapPut("/api/Shifts/{id:int}", (int id, UpdateShiftDto dto) => { var s = db.Shifts.FirstOrDefault(x => x.Id == id); if (s is null) return Results.NotFound(); s.ShiftName = dto.ShiftName; s.StartTime = dto.StartTime; s.EndTime = dto.EndTime; return Results.NoContent(); });
app.MapDelete("/api/Shifts/{id:int}", (int id) => { db.Shifts.RemoveAll(x => x.Id == id); return Results.NoContent(); });

// Attendances
app.MapGet("/api/Attendances", () => Results.Ok(db.Attendances.Select(db.AttendanceResponse)));
app.MapGet("/api/Attendances/{id:int}", (int id) => db.Attendances.FirstOrDefault(x => x.Id == id) is { } a ? Results.Ok(db.AttendanceResponse(a)) : Results.NotFound());
app.MapGet("/api/Attendances/Employee/{employeeId:int}", (int employeeId) => Results.Ok(db.Attendances.Where(x => x.EmployeeId == employeeId).Select(db.AttendanceResponse)));
app.MapGet("/api/Attendances/Status/{status}", (AttendanceStatus status) => Results.Ok(db.Attendances.Where(x => x.AttendanceStatus == status).Select(db.AttendanceResponse)));
app.MapGet("/api/Attendances/Employee/{employeeId:int}/Status/{status}", (int employeeId, AttendanceStatus status) => Results.Ok(db.Attendances.Where(x => x.EmployeeId == employeeId && x.AttendanceStatus == status).Select(db.AttendanceResponse)));
app.MapPost("/api/Attendances", (CreateAttendanceDto dto) => { if (!db.Employees.Any(x => x.Id == dto.EmployeeId) || !db.Departments.Any(x => x.Id == dto.DepartmentId) || !db.Shifts.Any(x => x.Id == dto.ShiftId)) return Results.BadRequest(new { message = "Employee, Department or Shift not found" }); var a = new AttendanceRow { Id = db.NextAttendance++, EmployeeId = dto.EmployeeId, DepartmentId = dto.DepartmentId, ShiftId = dto.ShiftId, Date = dto.Date, AttendanceStatus = dto.AttendanceStatus }; db.Attendances.Add(a); return Results.Created($"/api/Attendances/{a.Id}", db.AttendanceResponse(a)); });
app.MapPut("/api/Attendances/{id:int}", (int id, UpdateAttendanceDto dto) => { var a = db.Attendances.FirstOrDefault(x => x.Id == id); if (a is null) return Results.NotFound(); a.ClockedIn = dto.ClockedIn; a.ClockedOut = dto.ClockedOut; a.AttendanceStatus = dto.AttendanceStatus; return Results.NoContent(); });
app.MapDelete("/api/Attendances/{id:int}", (int id) => { db.Attendances.RemoveAll(x => x.Id == id); return Results.NoContent(); });

// Requests
app.MapGet("/api/Requests", () => Results.Ok(db.Requests.Select(db.RequestResponse)));
app.MapGet("/api/Requests/{id:int}", (int id) => db.Requests.FirstOrDefault(x => x.Id == id) is { } r ? Results.Ok(db.RequestResponse(r)) : Results.NotFound());
app.MapGet("/api/Requests/employee/{employeeId:int}", (int employeeId) => Results.Ok(db.Requests.Where(x => x.EmployeeId == employeeId).Select(db.RequestResponse)));
app.MapGet("/api/Requests/status/{status}", (RequestStatus status) => Results.Ok(db.Requests.Where(x => x.Status == status).Select(db.RequestResponse)));
app.MapGet("/api/Requests/type/{type}", (RequestType type) => Results.Ok(db.Requests.Where(x => x.RequestType == type).Select(db.RequestResponse)));
app.MapGet("/api/Requests/company/{companyId:int}", (int companyId) => { var depIds = db.Departments.Where(x => x.CompanyId == companyId).Select(x => x.Id).ToHashSet(); var empIds = db.EmployeeDepartments.Where(x => depIds.Contains(x.DepartmentId)).Select(x => x.EmployeeId).ToHashSet(); return Results.Ok(db.Requests.Where(x => empIds.Contains(x.EmployeeId)).Select(db.RequestResponse)); });
app.MapGet("/api/Requests/department/{departmentId:int}", (int departmentId) => { var empIds = db.EmployeeDepartments.Where(x => x.DepartmentId == departmentId).Select(x => x.EmployeeId).ToHashSet(); return Results.Ok(db.Requests.Where(x => empIds.Contains(x.EmployeeId)).Select(db.RequestResponse)); });
app.MapGet("/api/Requests/search", (int? employeeId, RequestStatus? status, RequestType? type) => { var q = db.Requests.AsEnumerable(); if (employeeId.HasValue) q = q.Where(x => x.EmployeeId == employeeId); if (status.HasValue) q = q.Where(x => x.Status == status); if (type.HasValue) q = q.Where(x => x.RequestType == type); return Results.Ok(q.Select(db.RequestResponse)); });
app.MapPost("/api/Requests", (CreateRequestDto dto) => { if (!db.Employees.Any(x => x.Id == dto.EmployeeId)) return Results.BadRequest(new { message = "Employee not found" }); var r = new RequestRow { Id = db.NextRequest++, EmployeeId = dto.EmployeeId, Description = dto.Description, StartDate = dto.StartDate, EndDate = dto.EndDate, CreatedAt = dto.CreatedAt, Status = RequestStatus.Pending, RequestType = dto.Type }; db.Requests.Add(r); return Results.Created($"/api/Requests/{r.Id}", db.RequestResponse(r)); });
app.MapPut("/api/Requests/{id:int}", (int id, UpdateRequestDto dto) => { var r = db.Requests.FirstOrDefault(x => x.Id == id); if (r is null) return Results.NotFound(); if (dto.ReviewedAt.HasValue) r.ReviewedAt = dto.ReviewedAt; if (dto.StartDate.HasValue) r.StartDate = dto.StartDate.Value; if (dto.EndDate.HasValue) r.EndDate = dto.EndDate.Value; if (dto.Description is not null) r.Description = dto.Description; if (dto.Status.HasValue) r.Status = dto.Status.Value; return Results.NoContent(); });
app.MapDelete("/api/Requests/{id:int}", (int id) => { db.Requests.RemoveAll(x => x.Id == id); return Results.NoContent(); });

app.Run();

enum CompanyRole { CEO, HRManager, HREmployee, View_Only }
enum AttendanceStatus { Pending, Present, Late, Absent, OnLeave, NoClockOut }
enum RequestStatus { Pending, in_Review, Rejected, Accepted, Canceled }
enum RequestType { Leave, Remote_Work, Equipment, Shift_Change, Other }

record CreateUserDto(string FirstName, string LastName, string Email, string SSN, string Phone, int? CompanyId, CompanyRole? Role);
record UpdateUserDto(string FirstName, string LastName, string Phone);
record CreateCompanyDto(int UserId, string Name, string RegNum, string Address, CompanyRole Role);
record UpdateCompanyDto(string Address);
record CreateDepartmentDto(string Name, string? Description, int CompanyId);
record UpdateDepartmentDto(string Name, string Description);
record CreateEmployeeDto(string FirstName, string LastName, string Email, string Phone, string Address, string SSN, DateTime HireDate);
record UpdateEmployeeDto(string FirstName, string LastName, string Address, string Phone);
record CreateShiftDto(string ShiftName, int CompanyId, TimeSpan StartTime, TimeSpan EndTime);
record UpdateShiftDto(TimeSpan StartTime, TimeSpan EndTime, string ShiftName);
record CreateAttendanceDto(int EmployeeId, int ShiftId, int DepartmentId, DateTime Date, AttendanceStatus AttendanceStatus = AttendanceStatus.Pending);
record UpdateAttendanceDto(DateTime? ClockedIn, DateTime? ClockedOut, AttendanceStatus AttendanceStatus);
record CreateRequestDto(int EmployeeId, RequestType Type, string Description, DateTime CreatedAt, DateTime StartDate, DateTime EndDate);
record UpdateRequestDto(DateTime? ReviewedAt, DateTime? StartDate, DateTime? EndDate, string? Description, RequestStatus? Status);

class UserRow { public int Id; public string FirstName=""; public string LastName=""; public string Email=""; public string SSN=""; public string Phone=""; public DateTime CreatedAt; }
class CompanyRow { public int Id; public string Name=""; public string RegNum=""; public string Address=""; public DateTime CreatedAt; }
record UserCompanyRow(int UserId, int CompanyId, CompanyRole Role);
class DepartmentRow { public int Id; public string Name=""; public string? Description; public int CompanyId; }
class EmployeeRow { public int Id; public string FirstName=""; public string LastName=""; public string Email=""; public string Phone=""; public string Address=""; public string SSN=""; public DateTime HireDate; }
class EmployeeDepartmentRow { public int EmployeeId; public int DepartmentId; public bool IsPrimary; public EmployeeDepartmentRow(int e,int d,bool p){EmployeeId=e;DepartmentId=d;IsPrimary=p;} }
class ShiftRow { public int Id; public string ShiftName=""; public int CompanyId; public TimeSpan StartTime; public TimeSpan EndTime; }
class AttendanceRow { public int Id; public int EmployeeId; public int ShiftId; public int DepartmentId; public DateTime Date; public DateTime? ClockedIn; public DateTime? ClockedOut; public AttendanceStatus AttendanceStatus; }
class RequestRow { public int Id; public int EmployeeId; public string? Description; public DateTime StartDate; public DateTime EndDate; public DateTime CreatedAt; public DateTime? ReviewedAt; public RequestStatus Status; public RequestType RequestType; }

class TestDb {
    public int NextUser=2, NextCompany=2, NextDepartment=2, NextEmployee=2, NextShift=2, NextAttendance=2, NextRequest=2;
    public List<UserRow> Users = new() { new() { Id=1, FirstName="Demo", LastName="Admin", Email="demo@hrms.local", SSN="0000000000", Phone="09000000000", CreatedAt=DateTime.UtcNow } };
    public List<CompanyRow> Companies = new() { new() { Id=1, Name="Demo Company", RegNum="REG-001", Address="Tehran", CreatedAt=DateTime.UtcNow } };
    public List<UserCompanyRow> UserCompanies = new() { new(1,1,CompanyRole.CEO) };
    public List<DepartmentRow> Departments = new() { new() { Id=1, Name="IT", Description="Information Technology", CompanyId=1 } };
    public List<EmployeeRow> Employees = new() { new() { Id=1, FirstName="Test", LastName="Employee", Email="employee@hrms.local", Phone="09120000000", Address="Tehran", SSN="1111111111", HireDate=DateTime.UtcNow.Date } };
    public List<EmployeeDepartmentRow> EmployeeDepartments = new() { new(1,1,true) };
    public List<ShiftRow> Shifts = new() { new() { Id=1, ShiftName="Morning", CompanyId=1, StartTime=new TimeSpan(8,0,0), EndTime=new TimeSpan(16,0,0) } };
    public List<AttendanceRow> Attendances = new() { new() { Id=1, EmployeeId=1, DepartmentId=1, ShiftId=1, Date=DateTime.UtcNow.Date, AttendanceStatus=AttendanceStatus.Present } };
    public List<RequestRow> Requests = new() { new() { Id=1, EmployeeId=1, Description="Demo leave", StartDate=DateTime.UtcNow.Date, EndDate=DateTime.UtcNow.Date.AddDays(1), CreatedAt=DateTime.UtcNow, Status=RequestStatus.Pending, RequestType=RequestType.Leave } };

    public object UserResponse(UserRow u) => new { u.Id, u.FirstName, u.LastName, u.Email, u.SSN, u.CreatedAt, Companies = UserCompanies.Where(x=>x.UserId==u.Id).Select(x=>new { x.CompanyId, CompanyName=Companies.FirstOrDefault(c=>c.Id==x.CompanyId)?.Name ?? "", Role=x.Role.ToString() }).ToList() };
    public object CompanyResponse(CompanyRow c) => new { c.Id, c.Name, c.Address, c.RegNum, c.CreatedAt, Users = UserCompanies.Where(x=>x.CompanyId==c.Id).Join(Users,x=>x.UserId,u=>u.Id,(_,u)=>new { u.Id,u.FirstName,u.LastName,u.Email }).ToList() };
    public object DepartmentResponse(DepartmentRow d) => new { d.Id, d.Name, d.Description, Employees = EmployeeDepartments.Where(x=>x.DepartmentId==d.Id).Join(Employees,x=>x.EmployeeId,e=>e.Id,(_,e)=>new { e.Id,e.FirstName,e.LastName }).ToList(), Company = Companies.Where(c=>c.Id==d.CompanyId).Select(c=>new { c.Id,c.Name,c.RegNum }).FirstOrDefault() };
    public object EmployeeResponse(EmployeeRow e) => new { e.Id,e.FirstName,e.LastName,e.Email,e.Phone,e.SSN,e.HireDate, DepartmentNames = EmployeeDepartments.Where(x=>x.EmployeeId==e.Id).Join(Departments,x=>x.DepartmentId,d=>d.Id,(_,d)=>new { d.Id,d.Name,Description=d.Description ?? "" }).ToList() };
    public object ShiftResponse(ShiftRow s) => new { s.Id,s.ShiftName,s.StartTime,s.EndTime, Company = Companies.Where(c=>c.Id==s.CompanyId).Select(c=>new { c.Id,c.Name }).FirstOrDefault() };
    public object AttendanceResponse(AttendanceRow a) { var e=Employees.First(x=>x.Id==a.EmployeeId); var d=Departments.First(x=>x.Id==a.DepartmentId); var s=Shifts.First(x=>x.Id==a.ShiftId); return new { a.Id, Employee=new { e.Id,e.FirstName,e.LastName,e.Email,e.Phone }, Department=new { d.Id,d.Name }, Shift=new { s.Id,s.ShiftName,s.StartTime,s.EndTime }, a.Date,a.ClockedIn,a.ClockedOut,a.AttendanceStatus }; }
    public object RequestResponse(RequestRow r) { var e=Employees.First(x=>x.Id==r.EmployeeId); return new { r.Id,r.EmployeeId,r.Description,r.StartDate,r.EndDate,r.ReviewedAt,r.Status,RequestType=r.RequestType, Employee=new { e.Id,e.FirstName,e.LastName } }; }
}