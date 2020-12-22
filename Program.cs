using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PerformanceTest
{
    public static class Program
    {
        const string _databaseName = "TestDatabase";

        public static async Task Main(string[] args)
        {
            var stopwatch = new System.Diagnostics.Stopwatch();

            using (var context = new MyContext())
            {

                context.Database.EnsureDeleted();
                context.Database.EnsureCreated();

                var LoadBatchesSQL = @"

                                    with N as
                                    (
                                       select top (100000) cast(row_number() over (order by (select null)) as int) i
                                       from sys.objects o, sys.columns c, sys.columns c2
                                    )
                                    insert into ReadingBatches(
                                                                     ReadingDateTime ,
                                                                     ReceivedDateTime)
                                    select '01-01-2017', '01-01-2017'
                                    from N;
                                    ";

                var LoadReadingsSQL = @"

                                        with N as
                                        (
                                           select top (100000) cast(row_number() over (order by (select null)) as int) i
                                           from sys.objects o, sys.columns c, sys.columns c2
                                        )
                                        insert into Readings (
                                                                Value ,
                                                                SomeBool ,
                                                                SomeId ,
                                                                ReadingBatchId)
                                        select 1, 1, 1, i
                                        from N

                                        ";

                Console.WriteLine("Generating Data ...");
                stopwatch.Start();
                await context.Database.ExecuteSqlRawAsync(LoadBatchesSQL);
                Console.WriteLine("Loaded Batches");

                for (var i = 0; i < 10; i++)
                {
                    var rows = await context.Database.ExecuteSqlRawAsync(LoadReadingsSQL);
                    Console.WriteLine($"Loaded Entity Batch {rows} rows");
                }

                stopwatch.Stop();
                Console.WriteLine($"Finished Generating Data - Took: {stopwatch.Elapsed}");
                stopwatch.Reset();

                var results = context.ReadingBatches.AsNoTracking()
                    .Include(x => x.Reading)
                    .AsEnumerable();

                stopwatch.Start();

                var batchSize = 10 * 1000;
                var ix = 0;
                foreach (var r in results)
                {
                    ix++;

                    if (ix % batchSize == 0)
                    {
                        Console.WriteLine($"Read Entity {ix} with name {r.Id}.  Current Memory: {GC.GetTotalMemory(false) / 1024}kb GC's Gen0:{GC.CollectionCount(0)} Gen1:{GC.CollectionCount(1)} Gen2:{GC.CollectionCount(2)}");
                    }

                }
            }

            stopwatch.Stop();
            Console.WriteLine($"Done.  Current Memory: {GC.GetTotalMemory(false) / 1024}kb - Took: {stopwatch.Elapsed}");

            GC.Collect();

            Console.ReadKey();
        }

        private class MyContext : DbContext
        {
            // Declare DBSets
            public DbSet<ReadingBatch> ReadingBatches { get; set; }
            public DbSet<Reading> Readings { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                // Select 1 provider
                optionsBuilder
                    .UseSqlServer($@"Server=(localdb)\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=5;ConnectRetryCount=0")
                    .EnableSensitiveDataLogging()
                    .UseLoggerFactory(new LoggerFactory());
            }
        }
    }

    public partial class Reading
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public decimal Value { get; set; }
        public bool SomeBool { get; set; }
        public int SomeId { get; set; }
        public long ReadingBatchId { get; set; }

        public virtual ReadingBatch ReadingBatch { get; set; }
    }

    public partial class ReadingBatch
    {
        public ReadingBatch()
        {
            Reading = new HashSet<Reading>();
        }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public DateTime ReadingDateTime { get; set; }
        public DateTime ReceivedDateTime { get; set; }

        public virtual ICollection<Reading> Reading { get; set; }
    }
}
