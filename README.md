# NullRowVersionIssue

A project demonstrating an issue with Microsoft.Data.SqlClient 3.0.0 when using rowversion and SqlServerRetryingExecutionStrategy.

# Instructions

Assumes a sql server instance available at `Data Source=localhost;Initial Catalog=bcd4a8a01d8749789bc9683bdfa1bbee;User ID=sa;Password=!aWfa19!;Authentication=SqlPassword`.

Build and run all the tests. 

The test called "Customer_without_default_address_uses_first_address" fails. 
The test can be made to pass if the version of Microsoft.Data.SqlClient is downgraded from 
version 3.0.0 to 2.1.3 or line 156, `builder.EnableRetryOnFailure();`, is commented out.

The test called "Customer_without_default_address_uses_first_address_using_datareader" fails.
The test can be made to pass if the version of Microsoft.Data.SqlClient is downgraded from
version 3.0.0 to 2.1.3

# Schema generated by EF

```sql
create table Addresses
(
	Id int identity
		constraint PK_Addresses
			primary key,
	CustomerId int not null,
	Address1 nvarchar(max),
	Address2 nvarchar(max),
	Address3 nvarchar(max),
	Version timestamp null
)
go

create index IX_Addresses_CustomerId
	on Addresses (CustomerId)
go

create table Customers
(
	Id int identity
		constraint PK_Customers
			primary key,
	Name nvarchar(max),
	DefaultAddressId int
		constraint FK_Customers_Addresses_DefaultAddressId
			references Addresses,
	Version timestamp null
)
go

alter table Addresses
	add constraint FK_Addresses_Customers_CustomerId
		foreign key (CustomerId) references Customers
			on delete cascade
go

create unique index IX_Customers_DefaultAddressId
	on Customers (DefaultAddressId)
	where [DefaultAddressId] IS NOT NULL
go
```