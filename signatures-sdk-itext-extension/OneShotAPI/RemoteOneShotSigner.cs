using iText.Signatures;
using Sphereon.SDK.Signatures.Api;
using Sphereon.SDK.Signatures.Model;
using System;
using System.IO;

namespace Signatures.SDK.IText.OneShotAPI
{
    internal class RemoteOneShotSigner : IExternalSignature
    {
        private DigestAlgorithm digestAlgorithm;
        private readonly SigningApi signingApi;
        private readonly SignInput signInput;

        public Signature Signature { get; internal set; }

        public RemoteOneShotSigner(DigestAlgorithm digestAlgorithm, SigningApi signingApi, SignInput signInput)
        {
            this.digestAlgorithm = digestAlgorithm;
            this.signingApi = signingApi;
            this.signInput = signInput;
        }

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
                byte[] digest = DigestAlgorithms.Digest(stream, GetHashAlgorithm());
                signInput.Input = digest;
                signInput.SignMode = SignMode.DIGEST;
                var createSignature = new CreateSignature(signInput);
                var signatureResponse = signingApi.CreateSignature(createSignature);
                this.Signature = signatureResponse.Signature;
                return Signature.Value; ;
            }
        }
    }

}