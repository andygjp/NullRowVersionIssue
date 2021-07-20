namespace NullRowVersionIssue
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Xunit;
    using Xunit.Abstractions;

    public class Tests : IClassFixture<TestFixture>
    {
        private readonly TestFixture fixture;
        private readonly ITestOutputHelper outputHelper;

        public Tests(TestFixture fixture, ITestOutputHelper outputHelper)
        {
            this.fixture = fixture;
            this.outputHelper = outputHelper;
        }

        [Fact]
        public async Task Customer_with_default_address_uses_default_address()
        {
            var defaultAddress = new Address
            {
                Address1 = "312",
                Address2 = "Test Road"
            };
            var customer = new Customer
            {
                Name = "Jane Doe",
                Addresses =
                {
                    new Address
                    {
                        Address1 = "4834",
                        Address2 = "Test Street"
                    },
                    defaultAddress,
                }
            };
            AddCustomer(customer);
            await SaveChangesAsync();
            
            Assert.True(customer.Id > 0);
            Assert.True(defaultAddress.Id > 0);
            Assert.True(customer.DefaultAddressId is null);

            customer.DefaultAddress = defaultAddress;
            await SaveChangesAsync();

            Assert.True(customer.DefaultAddressId > 0);

            Assert.Same(defaultAddress, await GetDefaultOrFirstAddress(customer));
        }

        [Fact]
        public async Task Customer_without_default_address_uses_first_address()
        {
            var address = new Address
            {
                Address1 = "8992",
                Address2 = "Test Avenue"
            };
            var customer = new Customer
            {
                Name = "John Smith",
                Addresses =
                {
                    address,
                }
            };
            AddCustomer(customer);
            await SaveChangesAsync();
            
            Assert.True(customer.Id > 0);
            Assert.True(address.Id > 0);

            Assert.Same(address, await GetDefaultOrFirstAddress(customer));
        }

        [Fact]
        public async Task Customer_without_default_address_uses_first_address_using_datareader_GetFieldValue()
        {
            var address = new Address
            {
                Address1 = "8992",
                Address2 = "Test Avenue"
            };
            var customer = new Customer
            {
                Name = "John Smith",
                Addresses =
                {
                    address,
                }
            };
            AddCustomer(customer);
            await SaveChangesAsync();
            
            Assert.True(customer.Id > 0);
            Assert.True(address.Id > 0);

            var command = GetConnection().CreateCommand();
            command.CommandText = GetDefaultOrFirstAddressQuery(customer).ToQueryString();
            outputHelper.WriteLine(command.CommandText);
            var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            var ex = Record.Exception(() =>
            {
                // The select case looks like this:
                // SELECT [c].[Id],
                //   [a].[Id], [a].[Address1], [a].[Address2], [a].[Address3], [a].[CustomerId], [a].[Version],
                //   [t0].[Id], [t0].[Address1], [t0].[Address2], [t0].[Address3], [t0].[CustomerId], [t0].[Version]
                // Its the 6th index, [a].[Version], that is null
                var fieldValue = reader.GetFieldValue<byte[]>(6);
                return fieldValue;
            });

            await reader.CloseAsync();
            Assert.Null(ex);
        }

        [Fact]
        public async Task Customer_without_default_address_uses_first_address_using_datareader_GetValues()
        {
            var address = new Address
            {
                Address1 = "8992",
                Address2 = "Test Avenue"
            };
            var customer = new Customer
            {
                Name = "John Smith",
                Addresses =
                {
                    address,
                }
            };
            AddCustomer(customer);
            await SaveChangesAsync();
            
            Assert.True(customer.Id > 0);
            Assert.True(address.Id > 0);

            var command = GetConnection().CreateCommand();
            command.CommandText = GetDefaultOrFirstAddressQuery(customer).ToQueryString();
            outputHelper.WriteLine(command.CommandText);
            var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            var ex = Record.Exception(() =>
            {
                // The select case looks like this:
                // SELECT [c].[Id],
                //   [a].[Id], [a].[Address1], [a].[Address2], [a].[Address3], [a].[CustomerId], [a].[Version],
                //   [t0].[Id], [t0].[Address1], [t0].[Address2], [t0].[Address3], [t0].[CustomerId], [t0].[Version]
                // Its the 6th index, [a].[Version], that is null
                var data = new object[13];
                reader.GetValues(data);
                // Even with LegacyRowVersionNullBehavior enabled, the element at the 6th index is DbNull. In version
                // 2.1.3 this would've been an empty byte array, so this flag doesn't really maintain backwards compatibility.
                var bytes = (byte[]) data[6];
                return bytes;
            });

            await reader.CloseAsync();
            Assert.Null(ex);
        }

        [Fact]
        public void GetValues_null_rowversion_returns_empty_byte_array()
        {
            var con = GetConnection();
            using var command = con.CreateCommand();
            command.CommandText = "select cast(null as rowversion) rv";
            using var reader = command.ExecuteReader();
            reader.Read();
            var data = new object[1];
            reader.GetValues(data);
            Assert.True(data[0] is byte[] {Length: 0});
        }

        [Fact]
        public void GetValue_null_rowversion_returns_empty_byte_array()
        {
            var con = GetConnection();
            using var command = con.CreateCommand();
            command.CommandText = "select cast(null as rowversion) rv";
            using var reader = command.ExecuteReader();
            reader.Read();
            var value = reader.GetValue(0);
            Assert.True(value is byte[] {Length: 0});
        }

        [Fact]
        public void GetFieldValue_null_rowversion_does_not_throw_invalid_cast()
        {
            var con = GetConnection();
            using var command = con.CreateCommand();
            command.CommandText = "select cast(null as rowversion) rv";
            using var reader = command.ExecuteReader();
            reader.Read();
            var ex = Record.Exception(() => reader.GetFieldValue<byte[]>(0));
            Assert.Null(ex);
        }

        private void AddCustomer(Customer customer)
        {
            fixture.DataContext.Customers.Add(customer);
        }

        private Task<int> SaveChangesAsync()
        {
            return fixture.DataContext.SaveChangesAsync();
        }

        private DbConnection GetConnection()
        {
            return fixture.DataContext.Database.GetDbConnection();
        }

        private async Task<Address> GetDefaultOrFirstAddress(Customer customer)
        {
            var data = await GetDefaultOrFirstAddressQuery(customer).FirstOrDefaultAsync();
            
            return data?.DefaultAddress ?? data?.FallbackAddress;
        }

        private IQueryable<Data> GetDefaultOrFirstAddressQuery(Customer customer)
        {
            return fixture.DataContext.Customers.Select(x => new Data
                {
                    Id = x.Id,
                    DefaultAddress = x.DefaultAddress,
                    FallbackAddress = x.Addresses.OrderBy(y => y.Id).FirstOrDefault()
                })
                .Where(x => x.Id == customer.Id);
        }

        private record Data
        {
            public int Id { get; init; }
            public Address DefaultAddress { get; init; }
            public Address FallbackAddress { get; init; }
        }
    }

    public static class Init
    {
        public static void EnableLegacyRowVersionNullBehavior()
        {
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior", true);
        }
    }

    public class TestFixture : IAsyncLifetime
    {
        public DataContext DataContext { get; private set; }

        public async Task InitializeAsync()
        {
            Init.EnableLegacyRowVersionNullBehavior();
            DataContext = DataContextFactory.Create(ConnectionString());
            await DataContext.Database.EnsureCreatedAsync();
            await DataContext.Database.OpenConnectionAsync();
        }

        public async Task DisposeAsync()
        {
            await DataContext.Database.CloseConnectionAsync();
            var deleted = await DataContext.Database.EnsureDeletedAsync();
            if (deleted is false) throw new InvalidOperationException("Database has not been deleted");
        }

        private static string ConnectionString()
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = "localhost",
                InitialCatalog = Guid.NewGuid().ToString("N"),
                Authentication = SqlAuthenticationMethod.SqlPassword,
                UserID = "sa",
                Password = "!aWfa19!"
            }.ToString();
        }
    }

    public static class DataContextFactory
    {
        public static DataContext Create(string connectionString)
        {
            return new(new DbContextOptionsBuilder<DataContext>()
                .UseSqlServer(connectionString, builder =>
                {
                    // Commenting out the line below prevents InvalidOperationException
                    builder.EnableRetryOnFailure();
                })
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging()
                .Options);
        }
    }

    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Address> Addresses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Customer>().HasKey(x => x.Id);
            modelBuilder.Entity<Customer>().HasMany(x => x.Addresses).WithOne().HasForeignKey(x => x.CustomerId);
            modelBuilder.Entity<Customer>().HasOne(x => x.DefaultAddress).WithOne().HasForeignKey<Customer>(x => x.DefaultAddressId);
            modelBuilder.Entity<Customer>().Property(x => x.Version).IsRowVersion();
            modelBuilder.Entity<Address>().HasKey(x => x.Id);
            modelBuilder.Entity<Address>().Property(x => x.Version).IsRowVersion();
        }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual List<Address> Addresses { get; set; } = new();
        
        public int? DefaultAddressId { get; set; }
        public virtual Address DefaultAddress { get; set; }
        public byte[] Version { get; set; }
    }

    public class Address
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string Address3 { get; set; }
        public byte[] Version { get; set; }
    }
}