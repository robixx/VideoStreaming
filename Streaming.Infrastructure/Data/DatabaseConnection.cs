using Microsoft.EntityFrameworkCore;
using Streaming.Model.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Streaming.Infrastructure.Data
{
    public class DatabaseConnection: DbContext
    {
        public DatabaseConnection(DbContextOptions<DatabaseConnection> options)
      : base(options)
        {
        }

        public DbSet<UploadedFile> UploadedFile { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<UploadedFile>().HasKey(i => i.Id);
           
            //modelBuilder.Entity<MenuSetUp>().HasNoKey();
            base.OnModelCreating(modelBuilder);
        }
    }
}
