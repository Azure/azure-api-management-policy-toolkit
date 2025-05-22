// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Testing.Expressions.Extensions;

[TestClass]
public class JwtParserTests
{
    private readonly static DateTime TestDate = DateTime.UtcNow;

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Basic value")]
    [DataRow("Bearer ")]
    [DataRow("Bearer A.A")]
    [DataRow("Bearer A.A.A")]
    [DataRow("Bearer A.A.A.A")]
    [DataRow("Bearer A.A.A.A.A")]
    [DataRow("Bearer A.A.A.A.A.A")]
    [DataRow("Bearer #.A.A")]
    [DataRow("Bearer A.#.A")]
    [DataRow("Bearer A.A.#")]
    [DataRow("Bearer A.A.#.A.A")]
    [DataRow("Bearer A.A.A.#.A")]
    [DataRow("Bearer A.A.A.A.#")]
    public void JwtParser_ShouldReturnNull(string value)
    {
        // Act
        var result = new JwtParser().Parse(value);

        // Assert
        result.Should().BeNull();
    }

    [TestMethod]
    public void JwtParser_ShouldReturnJwtToken()
    {
        // Arrange
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        byte[] key = new byte[32];
        rng.GetBytes(key);

        var tokenHandler = new JsonWebTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Claims =
                new Dictionary<string, object>
                {
                    { "jti", "123" },
                    { "pid", "321" },
                    { "sub", "231" },
                    { "name", "John Doe" },
                    { "admin", true }
                },
            Audience = "users",
            Audiences = { "aud1", "aud2" },
            AdditionalHeaderClaims =
                new Dictionary<string, object>() { { "ahc1", "jd" }, { "ahc2", "jd.contoso.example" } },
            AdditionalInnerHeaderClaims =
                new Dictionary<string, object>() { { "aihc1", "jd" }, { "aihc2", "jd.contoso.example" } },
            Issuer = "contoso.exmaple",
            IssuedAt = TestDate,
            Expires = TestDate.AddMinutes(60),
            NotBefore = TestDate.AddMinutes(5),
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);

        // Act
        var result = new JwtParser().Parse($"Bearer {token}");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be("123");
        result.Algorithm.Should().Be(SecurityAlgorithms.HmacSha256Signature);
        result.Issuer.Should().Be("contoso.exmaple");
        result.Subject.Should().Be("231");
        result.Type.Should().Be("JWT");
        result.IssuedAt.Should().BeCloseTo(TestDate, TimeSpan.FromSeconds(1));
        result.ExpirationTime.Should().BeCloseTo(TestDate.AddMinutes(60), TimeSpan.FromSeconds(1));
        result.NotBefore.Should().BeCloseTo(TestDate.AddMinutes(5), TimeSpan.FromSeconds(1));
        result.Audiences.Should().BeEquivalentTo("aud1", "aud2", "users");
    }
}