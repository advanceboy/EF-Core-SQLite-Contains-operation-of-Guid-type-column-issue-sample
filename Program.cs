using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace efcore_sqlite_guid_contains_issue_sample {
    public class Program {
        public static IConfigurationRoot Configuration { get; }

        static Program() {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddInMemoryCollection(new Dictionary<string, string> {
                    ["Logging:IncludeScopes"] = "false",
                    ["Logging:LogLevel:Default"] = "Debug",
                    ["Logging:LogLevel:Microsoft"] = "Information",
                })
                .Build();
        }

        public static void Configure(ILoggerFactory loggerFactory) {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
        }

        public static void Main(string[] args) {
            // Key Type: long
            Test<long>();

            // Key Type: Guid
            Test<Guid>();
        }

        public static void Test<TKey>() {
            // SqlServer
            RunTest<TKey>(options => options.UseSqlServer($@"Server=(localdb)\mssqllocaldb;Database={typeof(TKey).Name}IdContainsTest;Trusted_Connection=True;"));

            // InMemoryDatabase
            RunTest<TKey>(options => options.UseInMemoryDatabase());

            // SQLite
            RunTest<TKey>(options => options.UseSqlite($@"Filename={typeof(TKey).Name}IdContainsTest.db"));
        }


        public static void RunTest<TKey>(Action<DbContextOptionsBuilder> optionsAction) {
            var provider = new ServiceCollection()
                    .AddLogging()
                    .AddDbContext<MyContext<TKey>>(optionsAction)
                    .BuildServiceProvider();
            Configure(provider.GetService<ILoggerFactory>());

            using (var db = ActivatorUtilities.CreateInstance<MyContext<TKey>>(provider)) {
                db.Database.EnsureCreated();

                db.Entries.AddRange(
                    new Entry<TKey> { Value = "foo" },
                    new Entry<TKey> { Value = "bar" },
                    new Entry<TKey> { Value = "foobar" }
                );
                var count = db.SaveChanges();
                Console.WriteLine("{0} records saved to database", count);

                Console.WriteLine();
                Console.WriteLine("All records in database:");
                var allEntries = db.Entries.ToArrayAsync().Result;
                foreach (var entry in allEntries) {
                    Console.WriteLine(" - {0}: {1}", entry.Id, entry.Value);
                }

                var ids = allEntries.Select(e => e.Id).ToArray();
                Console.WriteLine();
                Console.WriteLine($"All ({count}) records in database:");
                int counter = 0;
                foreach (var entry in db.Entries.Where(e => ids.Contains(e.Id))) {
                    Console.WriteLine(" - {0}: {1}", entry.Id, entry.Value);
                    counter++;
                }

                // test
                System.Diagnostics.Debug.Assert(count == counter, "!!!ISSUE!!! Only in SQLite with Guid Id, no record is got.");
                

                Console.WriteLine();
                Console.WriteLine();

                db.Database.EnsureDeleted();
            }
        }
    }

    public class MyContext<TKey> : DbContext {
        public DbSet<Entry<TKey>> Entries { get; set; }
        public MyContext(DbContextOptions<MyContext<TKey>> options) : base(options) { }
    }

    public class Entry<TKey> {
        public TKey Id { get; set; }
        public string Value { get; set; }
    }
}
