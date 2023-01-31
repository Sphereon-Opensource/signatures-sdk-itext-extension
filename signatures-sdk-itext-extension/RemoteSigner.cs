using iText.Signatures;
using Sphereon.SDK.Signatures.Model;
using System;
using System.IO;

namespace signatures_sdk_itext_extension
{
    internal class RemoteSigner : IExternalSignature
    {
        private DigestAlgorithm digestAlgorithm;
        private readonly byte[] signatureValue;

        public RemoteSigner(DigestAlgorithm digestAlgorithm, byte[] signatureValue = null)
        {
            this.digestAlgorithm = digestAlgorithm;
            this.signatureValue = signatureValue;
        }

        public byte[] DataToSign { get; private set; }

        public string GetEncryptionAlgorithm()
        {
            return "RSA";
        }

        public string GetHashAlgorithm()
        {
            return digestAlgorithm.ToString();
        }

        public byte[] Sign(byte[] message)
        {
            using (var stream = new MemoryStream(message))
            {
                Console.WriteLine("Message: " + BitConverter.ToString(message).Replace("-", string.Empty));
                byte[] digest = DigestAlgorithms.Digest(stream, GetHashAlgorithm());
                Console.WriteLine("Digest: " + BitConverter.ToString(digest).Replace("-", string.Empty));
                this.DataToSign = message;
                return signatureValue ?? new byte[0];
            }
        }
    }

}