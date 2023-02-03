using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.X509;
using Sphereon.SDK.Signatures.Api;
using Sphereon.SDK.Signatures.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics.Contracts;
using Signatures.SDK.IText.Common;
using Signatures.SDK.Common;
using Signatures.SDK.IText.Config;

namespace Signatures.SDK.IText.OneShotAPI
{
    public class ITextSigningOneShotApi : AbstractConfigurer
    {
        protected readonly SigningApi signingApi;
        private readonly ConfigProvider configProvider;

        public ITextSigningOneShotApi(object configProvider1)
        {
        }

        public ITextSigningOneShotApi(SigningApi signingApi, ConfigProvider configProvider) : base()
        {
            this.signingApi = signingApi;
            this.configProvider = configProvider;
        }

        public SignOutput Sign(DetermineSignInput input)
        {
            // Assert input
            Contract.Requires(input != null, "input may not be null");
            Contract.Requires(input.OrigData != null, "input.OrigData may not be null");
            Contract.Requires(input.OrigData.Content != null || input.OrigData.Content.Length == 0, "input.OrigData.Content may not be null/empty");
            Contract.Requires(input.Binding != null, "input.Binding may not be null");

            // Initialze vars & objects
            var binding = input.Binding;
            Contract.Requires(binding != null, "input.Binding may not be null");
            SignatureConfig config = configProvider.GetConfiguration(binding);
            var formParameters = Mapper.GetFormParameters(config.SignatureFormParameters, input.SignatureFormParametersOverride);
            var signingDate = DateTimeOffset.UtcNow;

            PdfReader reader = new PdfReader(new MemoryStream(input.OrigData.Content));
            var outputStream = new MemoryStream();
            PdfSigner signer = new PdfSigner(reader, outputStream, new StampingProperties());
            ConfigureSigner(signer, signingDate, formParameters);
            List<X509Certificate> certificateChain = configProvider.GetCertificateChain(binding);

            var signInput = new SignInput
            (
                name: input.OrigData.Name,
                input: new byte[0], // RemoteSigner will load this
                signMode: input.SignMode,
                digestAlgorithm: config.DigestAlgorithm,
                signingDate: signingDate,
                binding: binding,
                signatureFormParameters: new SignatureFormParameters(formParameters),
                password: formParameters.PasswordProtection
            );

            DigestAlgorithm digestAlgorithm = config.DigestAlgorithm ?? DigestAlgorithm.SHA256;
            var remoteSigner = new RemoteOneShotSigner(digestAlgorithm, signingApi, signInput);
            var tsaClient = new TSAClientBouncyCastle(config.TimestampParameters.TsaUrl, null, null, TSAClientBouncyCastle.DEFAULTTOKENSIZE, digestAlgorithm.ToString());
            OCSPVerifier ocspVerifier = new OCSPVerifier(null, null);
            IOcspClient ocspClient = new OcspClientBouncyCastle(ocspVerifier);
            ICrlClient crlClient = new CrlClientOnline(certificateChain.ToArray());
            List<ICrlClient> lstCRL = new List<ICrlClient>() { crlClient };

            // Sign the document using the detached mode, CMS or CAdES equivalent.
            signer.SignDetached(remoteSigner, certificateChain.ToArray(), lstCRL, ocspClient, tsaClient, 0, Mapper.GetSubfilter(config));

            // Construct result
            var signOutput = new SignOutput
            (
                value: outputStream.ToArray(),
                name: input.OrigData.Name,
                signature: remoteSigner.Signature
            );
            return signOutput;
        }
    }
}
