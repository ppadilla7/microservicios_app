using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Students.Infrastructure;

namespace Students.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DebugController : ControllerBase
{
    private readonly StudentsDbContext _db;
    public DebugController(StudentsDbContext db) { _db = db; }

    [HttpGet("migrations")]
    public ActionResult<object> GetMigrations()
    {
        var applied = _db.Database.GetAppliedMigrations().ToArray();
        var pending = _db.Database.GetPendingMigrations().ToArray();
        return Ok(new { applied, pending });
    }

    [HttpGet("has-userid-column")]
    public async Task<ActionResult<object>> HasUserIdColumn()
    {
        try
        {
            await using var conn = _db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Students' AND COLUMN_NAME='UserId'";
            var result = await cmd.ExecuteScalarAsync();
            var has = Convert.ToInt32(result) > 0;
            return Ok(new { has });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}