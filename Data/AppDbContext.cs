using Microsoft.EntityFrameworkCore;
using SmartAssistant.API.Entities;
using System.Collections.Generic;

namespace SmartAssistant.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();
}