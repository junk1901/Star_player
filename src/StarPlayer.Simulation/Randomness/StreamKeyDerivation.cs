using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using StarPlayer.Domain.Common;

namespace StarPlayer.Simulation.Randomness;

/// <summary>
/// Stable stream key derivation, version <see cref="RngVersions.KeyDerivation"/> (<c>kd1</c>). A stream's generator depends ONLY on
/// (master seed, context key, stream id), never on creation order, other streams, time, process, OS or culture.
/// <para>
/// Input bytes: ASCII <c>"SPRNG-kd1"</c> || master seed as 8-byte BIG-endian || 4-byte big-endian length + UTF-8 bytes of the context key ||
/// 4-byte big-endian length + UTF-8 bytes of the stream's canonical text (<c>name@version</c>). Digest: SHA-256 (32 bytes).
/// <c>initstate</c> = bytes 0..7 and <c>initseq</c> = bytes 8..15 of the digest, each read as an unsigned 64-bit BIG-endian integer; the
/// stream is then seeded with the PCG32 reference procedure (<see cref="Pcg32"/>).
/// </para>
/// </summary>
public static class StreamKeyDerivation
{
    private static readonly byte[] Tag = Encoding.ASCII.GetBytes("SPRNG-kd1");
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static (ulong InitState, ulong InitSeq) Derive(ulong masterSeed, string contextKey, RngStreamId stream)
    {
        byte[] context = Utf8.GetBytes(contextKey);
        byte[] id = Utf8.GetBytes(stream.Canonical);
        byte[] input = new byte[Tag.Length + 8 + 4 + context.Length + 4 + id.Length];
        var span = input.AsSpan();
        Tag.CopyTo(span);
        int at = Tag.Length;
        BinaryPrimitives.WriteUInt64BigEndian(span[at..], masterSeed);
        at += 8;
        BinaryPrimitives.WriteUInt32BigEndian(span[at..], (uint)context.Length);
        at += 4;
        context.CopyTo(span[at..]);
        at += context.Length;
        BinaryPrimitives.WriteUInt32BigEndian(span[at..], (uint)id.Length);
        at += 4;
        id.CopyTo(span[at..]);
        byte[] digest = SHA256.HashData(input);
        return (BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(0, 8)), BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(8, 8)));
    }

    /// <summary>A context key is an id-like text (letters, digits, <c>_ - . :</c>), 1 to 64 characters, so it needs no escaping in snapshot text.</summary>
    public static Error? ValidateContext(string? contextKey)
    {
        if (string.IsNullOrEmpty(contextKey) || contextKey.Length > 64
            || !contextKey.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.' or ':'))
            return new Error("Rng.context_invalid", "난수 맥락 키는 영문·숫자·'_'·'-'·'.'·':'로 된 1~64자여야 합니다.");
        return null;
    }
}
