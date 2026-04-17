// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Compiling;

[TestClass]
public class SqlDataSourceTests
{
    [TestMethod]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.SqlDataSource(new SqlDataSourceConfig
                {
                    ConnectionInfo = new SqlConnectionInfoConfig
                    {
                        ConnectionString = "Server=tcp:myserver.database.windows.net;Database=mydb;",
                    },
                    Request = new SqlRequestConfig
                    {
                        SqlStatement = "SELECT * FROM Products",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <backend>
                <sql-data-source>
                    <connection-info>
                        <connection-string>Server=tcp:myserver.database.windows.net;Database=mydb;</connection-string>
                    </connection-info>
                    <request>
                        <sql-statement>SELECT * FROM Products</sql-statement>
                    </request>
                </sql-data-source>
            </backend>
        </policies>
        """,
        DisplayName = "Should compile sql-data-source with simple query"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.SqlDataSource(new SqlDataSourceConfig
                {
                    ConnectionInfo = new SqlConnectionInfoConfig
                    {
                        ConnectionString = "Server=tcp:myserver.database.windows.net;Database=mydb;",
                    },
                    Request = new SqlRequestConfig
                    {
                        SqlStatement = "SELECT * FROM Products WHERE Id = @Id",
                        Parameters = new SqlParameterConfig[]
                        {
                            new SqlParameterConfig
                            {
                                Name = "@Id",
                                SqlType = "Int",
                                Value = "123",
                            },
                        },
                    },
                });
            }
        }
        """,
        """
        <policies>
            <backend>
                <sql-data-source>
                    <connection-info>
                        <connection-string>Server=tcp:myserver.database.windows.net;Database=mydb;</connection-string>
                    </connection-info>
                    <request>
                        <sql-statement>SELECT * FROM Products WHERE Id = @Id</sql-statement>
                        <parameters>
                            <parameter name="@Id" sql-type="Int">123</parameter>
                        </parameters>
                    </request>
                </sql-data-source>
            </backend>
        </policies>
        """,
        DisplayName = "Should compile sql-data-source with parameters"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.SqlDataSource(new SqlDataSourceConfig
                {
                    SingleResult = "true",
                    ConnectionInfo = new SqlConnectionInfoConfig
                    {
                        ConnectionString = "Server=tcp:myserver.database.windows.net;Database=mydb;",
                    },
                    Request = new SqlRequestConfig
                    {
                        SqlStatement = "SELECT TOP 1 * FROM Products",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <backend>
                <sql-data-source single-result="true">
                    <connection-info>
                        <connection-string>Server=tcp:myserver.database.windows.net;Database=mydb;</connection-string>
                    </connection-info>
                    <request>
                        <sql-statement>SELECT TOP 1 * FROM Products</sql-statement>
                    </request>
                </sql-data-source>
            </backend>
        </policies>
        """,
        DisplayName = "Should compile sql-data-source with single-result"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.SqlDataSource(new SqlDataSourceConfig
                {
                    ConnectionInfo = new SqlConnectionInfoConfig
                    {
                        ConnectionString = "Server=tcp:myserver.database.windows.net;Database=mydb;",
                        UseManagedIdentity = "true",
                        ClientId = "my-client-id",
                    },
                    Request = new SqlRequestConfig
                    {
                        SqlStatement = "SELECT * FROM Products",
                    },
                });
            }
        }
        """,
        """
        <policies>
            <backend>
                <sql-data-source>
                    <connection-info>
                        <connection-string use-managed-identity="true" client-id="my-client-id">Server=tcp:myserver.database.windows.net;Database=mydb;</connection-string>
                    </connection-info>
                    <request>
                        <sql-statement>SELECT * FROM Products</sql-statement>
                    </request>
                </sql-data-source>
            </backend>
        </policies>
        """,
        DisplayName = "Should compile sql-data-source with managed identity"
    )]
    [DataRow(
        """
        [Document]
        public class PolicyDocument : IDocument
        {
            public void Backend(IBackendContext context) {
                context.SqlDataSource(new SqlDataSourceConfig
                {
                    Timeout = "30",
                    ConnectionInfo = new SqlConnectionInfoConfig
                    {
                        ConnectionString = "Server=tcp:myserver.database.windows.net;Database=mydb;",
                    },
                    Request = new SqlRequestConfig
                    {
                        SqlStatement = "SELECT * FROM Products WHERE Category = @Category AND Price = @MinPrice",
                        Parameters = new SqlParameterConfig[]
                        {
                            new SqlParameterConfig
                            {
                                Name = "@Category",
                                SqlType = "NVarChar",
                                Value = "Electronics",
                            },
                            new SqlParameterConfig
                            {
                                Name = "@MinPrice",
                                SqlType = "Decimal",
                                Value = "99.99",
                            },
                        },
                    },
                });
            }
        }
        """,
        """
        <policies>
            <backend>
                <sql-data-source timeout="30">
                    <connection-info>
                        <connection-string>Server=tcp:myserver.database.windows.net;Database=mydb;</connection-string>
                    </connection-info>
                    <request>
                        <sql-statement>SELECT * FROM Products WHERE Category = @Category AND Price = @MinPrice</sql-statement>
                        <parameters>
                            <parameter name="@Category" sql-type="NVarChar">Electronics</parameter>
                            <parameter name="@MinPrice" sql-type="Decimal">99.99</parameter>
                        </parameters>
                    </request>
                </sql-data-source>
            </backend>
        </policies>
        """,
        DisplayName = "Should compile sql-data-source with multiple parameters and timeout"
    )]
    public void SqlDataSource(string code, string expectedXml)
    {
        code.CompileDocument().Should().BeSuccessful().And.DocumentEquivalentTo(expectedXml);
    }
}