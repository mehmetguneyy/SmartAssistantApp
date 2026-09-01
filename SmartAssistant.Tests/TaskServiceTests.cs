using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartAssistant.API.Data;
using SmartAssistant.API.Entities;
using Xunit;

namespace SmartAssistant.Tests
{
    public class TaskServiceTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var context = new AppDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public async Task AddTask_ShouldPersistTaskInDatabase()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var newTask = new TaskItem
            {
                Title = "Birim Test Görevi",
                Description = "xUnit ile EF Core in-memory testi",
                Category = "Test",
                Priority = "High",
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };

            // Act
            context.Tasks.Add(newTask);
            await context.SaveChangesAsync();

            // Assert
            var savedTask = await context.Tasks.FirstOrDefaultAsync(t => t.Title == "Birim Test Görevi");
            Assert.NotNull(savedTask);
            Assert.Equal("High", savedTask.Priority);
            Assert.False(savedTask.IsCompleted);
        }

        [Fact]
        public async Task MarkTaskAsCompleted_ShouldUpdateTaskStatus()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var task = new TaskItem
            {
                Title = "Tamamlanacak Görev",
                Category = "Test",
                Priority = "Medium",
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };
            context.Tasks.Add(task);
            await context.SaveChangesAsync();

            // Act
            task.IsCompleted = true;
            context.Tasks.Update(task);
            await context.SaveChangesAsync();

            // Assert
            var updatedTask = await context.Tasks.FindAsync(task.Id);
            Assert.NotNull(updatedTask);
            Assert.True(updatedTask.IsCompleted);
        }
    }
}