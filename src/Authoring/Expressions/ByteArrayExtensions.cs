// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Security.Cryptography;

namespace Microsoft.Azure.ApiManagement.PolicyToolkit.Authoring.Expressions;

public static class ByteArrayExtensions
{
    /// <summary>
    /// Encrypts the input with the named symmetric algorithm, for example "Aes".
    /// </summary>
    public static byte[] Encrypt(this byte[] input, string alg, byte[] key, byte[] iv)
    {
        using var algorithm = Create(alg);
        return input.Encrypt(algorithm, key, iv);
    }

    public static byte[] Encrypt(this byte[] input, SymmetricAlgorithm alg)
    {
        using var encryptor = alg.CreateEncryptor();
        return encryptor.TransformFinalBlock(input, 0, input.Length);
    }

    public static byte[] Encrypt(this byte[] input, SymmetricAlgorithm alg, byte[] key, byte[] iv)
    {
        using var encryptor = alg.CreateEncryptor(key, iv);
        return encryptor.TransformFinalBlock(input, 0, input.Length);
    }

    /// <summary>
    /// Decrypts the input with the named symmetric algorithm, for example "Aes".
    /// </summary>
    public static byte[] Decrypt(this byte[] input, string alg, byte[] key, byte[] iv)
    {
        using var algorithm = Create(alg);
        return input.Decrypt(algorithm, key, iv);
    }

    public static byte[] Decrypt(this byte[] input, SymmetricAlgorithm alg)
    {
        using var decryptor = alg.CreateDecryptor();
        return decryptor.TransformFinalBlock(input, 0, input.Length);
    }

    public static byte[] Decrypt(this byte[] input, SymmetricAlgorithm alg, byte[] key, byte[] iv)
    {
        using var decryptor = alg.CreateDecryptor(key, iv);
        return decryptor.TransformFinalBlock(input, 0, input.Length);
    }

    private static SymmetricAlgorithm Create(string alg) => alg.ToUpperInvariant() switch
    {
        "AES" => Aes.Create(),
        "TRIPLEDES" or "3DES" => TripleDES.Create(),
        _ => throw new NotSupportedException($"Symmetric algorithm '{alg}' is not supported by the toolkit's emulation")
    };
}
