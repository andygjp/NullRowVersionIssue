# NullRowVersionIssue

A project demonstrating an issue with Microsoft.Data.SqlClient 3.0.0 when using rowversion and SqlServerRetryingExecutionStrategy.

# Instructions

Assumes a sql server instance available at `Data Source=localhost;Initial Catalog=bcd4a8a01d8749789bc9683bdfa1bbee;User ID=sa;Password=!aWfa19!;Authentication=SqlPassword`.

Build and run all the tests. The test called "Customer_without_default_address_uses_first_address" fails. 
The test can be made to pass if the version of Microsoft.Data.SqlClient is downgraded from version 3.0.0 to 2.1.3 or line 156, `builder.EnableRetryOnFailure();`,
is commented out.
