using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SmoothOperator.Application.DTOs;
using SmoothOperator.Application.Exceptions;

namespace SmoothOperator.Application.Features.Credentials.Queries
{
    public sealed record GenerateSshKeyQuery(string KeyType) : IRequest<GenerateSshKeyResponse>;

    public sealed class GenerateSshKeyQueryHandler : IRequestHandler<GenerateSshKeyQuery, GenerateSshKeyResponse>
    {
        public Task<GenerateSshKeyResponse> Handle(GenerateSshKeyQuery request, CancellationToken cancellationToken)
        {
            string privateKey;
            string publicKey;

            if (request.KeyType == "rsa")
            {
                using var rsa = RSA.Create(4096);
                privateKey = rsa.ExportRSAPrivateKeyPem();
                publicKey = EncodeRsaPublicKeyOpenSsh(rsa);
            }
            else if (request.KeyType == "ecdsa")
            {
                using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                privateKey = ecdsa.ExportECPrivateKeyPem();
                publicKey = EncodeEcdsaPublicKeyOpenSsh(ecdsa);
            }
            else
            {
                throw new BadRequestException($"Unsupported key type '{request.KeyType}'. Supported: rsa, ecdsa.");
            }

            return Task.FromResult(new GenerateSshKeyResponse
            {
                PrivateKey = privateKey,
                PublicKey = publicKey
            });
        }

        private static string EncodeRsaPublicKeyOpenSsh(RSA rsa)
        {
            var p = rsa.ExportParameters(false);
            using var ms = new MemoryStream();
            SshWriteString(ms, "ssh-rsa");
            SshWriteMpInt(ms, p.Exponent!);
            SshWriteMpInt(ms, p.Modulus!);
            return $"ssh-rsa {Convert.ToBase64String(ms.ToArray())}";
        }

        private static string EncodeEcdsaPublicKeyOpenSsh(ECDsa ecdsa)
        {
            var p = ecdsa.ExportParameters(false);
            var x = NormalizeCoordinate(p.Q.X!);
            var y = NormalizeCoordinate(p.Q.Y!);
            var point = new byte[1 + x.Length + y.Length];
            point[0] = 0x04;
            Buffer.BlockCopy(x, 0, point, 1, x.Length);
            Buffer.BlockCopy(y, 0, point, 1 + x.Length, y.Length);
            using var ms = new MemoryStream();
            SshWriteString(ms, "ecdsa-sha2-nistp256");
            SshWriteString(ms, "nistp256");
            SshWriteBytes(ms, point);
            return $"ecdsa-sha2-nistp256 {Convert.ToBase64String(ms.ToArray())}";
        }

        private static byte[] NormalizeCoordinate(byte[] b)
        {
            if (b.Length == 32) return b;
            if (b.Length > 32) return b[^32..];
            var padded = new byte[32];
            Buffer.BlockCopy(b, 0, padded, 32 - b.Length, b.Length);
            return padded;
        }

        private static void SshWriteLen(Stream s, int len)
        {
            s.WriteByte((byte)(len >> 24));
            s.WriteByte((byte)(len >> 16));
            s.WriteByte((byte)(len >> 8));
            s.WriteByte((byte)len);
        }

        private static void SshWriteBytes(Stream s, byte[] data)
        {
            SshWriteLen(s, data.Length);
            s.Write(data, 0, data.Length);
        }

        private static void SshWriteString(Stream s, string value)
        {
            SshWriteBytes(s, System.Text.Encoding.ASCII.GetBytes(value));
        }

        private static void SshWriteMpInt(Stream s, byte[] data)
        {
            int start = 0;
            while (start < data.Length - 1 && data[start] == 0) start++;
            bool needsPad = (data[start] & 0x80) != 0;
            int len = data.Length - start + (needsPad ? 1 : 0);
            SshWriteLen(s, len);
            if (needsPad) s.WriteByte(0x00);
            s.Write(data, start, data.Length - start);
        }
    }
}
