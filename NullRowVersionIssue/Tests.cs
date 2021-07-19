namespace NullRowVersionIssue
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Microsoft.EntityFrameworkCore;
    using Xunit;

    public class Tests : IClassFixture<TestFixture>
    {
        private readonly TestFixture fixture;

        public Tests(TestFixture fixture)
        {
            this.fixture = fixture;
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

            Assert.Same(defaultAddress, GetDefaultOrFirstAddress(customer));
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

            Assert.Same(address, GetDefaultOrFirstAddress(customer));
        }

        private void AddCustomer(Customer customer)
        {
            fixture.DataContext.Customers.Add(customer);
        }

        private Task<int> SaveChangesAsync()
        {
            return fixture.DataContext.SaveChangesAsync();
        }

        private Address GetDefaultOrFirstAddress(Customer customer)
        {
            var data = fixture.DataContext.Customers.Select(x => new
                {
                    x.Id,
                    x.DefaultAddress,
                    FallbackAddress = x.Addresses.OrderBy(y => y.Id).FirstOrDefault()
                })
                .FirstOrDefault(x => x.Id == customer.Id);
            
            return data?.DefaultAddress ?? data?.FallbackAddress;
        }
    }

    public class TestFixture : IAsyncLifetime
    {
        public DataContext DataContext { get; private set; }

        public Task InitializeAsync()
        {
            DataContext = DataContextFactory.Create(ConnectionString());
            return DataContext.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
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
                .UseSqlServer(connectionString)
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
            modelBuilder.Entity<Address>().HasKey(x => x.Id);
        }
    }

    public class Customer
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public virtual List<Address> Addresses { get; set; } = new();
        
        public int? DefaultAddressId { get; set; }
        public virtual Address DefaultAddress { get; set; }
    }

    public class Address
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string Address3 { get; set; }
    }
}