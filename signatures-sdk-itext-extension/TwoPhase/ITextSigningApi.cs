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
using Signatures.SDK.IText.Model;
using Signatures.SDK.TwoPhase;
using Signatures.SDK.Common;
using Signatures.SDK.Model;
using Signatures.SDK.IText.Config;

namespace Signatures.SDK.IText.TwoPhase
{
    public class ITextSigningApi : AbstractConfigurer
    {
        private readonly ConfigProvider configProvider;

        public ITextSigningApi(ConfigProvider configProvider) : base()
        {
            this.configProvider = configProvider;
        }

        public ITextSignInputResponse DetermineSignInput(DetermineSignInput input)
        {
            // Assert input
            Contract.Requires(input != null, "input may not be null");

            // Initialze vars & objects
            var binding = input.Binding;
            Contract.Requires(binding != null, "input.Binding may not be null");
            SignatureConfig config = configProvider.GetConfiguration(binding);
            var formParameters = Mapper.GetFormParameters(config.SignatureFormParameters, input.SignatureFormParametersOverride);
            var signingDate = DateTimeOffset.UtcNow;

            PdfReader reader = new PdfReader(new MemoryStream(input.OrigData.Content));
            var outputStream = new MemoryStream();
            TwoPhaseSigner signer = new TwoPhaseSigner(reader, outputStream, new StampingProperties());
            ConfigureSigner(signer, signingDate, formParameters);
            List<X509Certificate> certificateChain = configProvider.GetCertificateChain(binding);

            DigestAlgorithm digestAlgorithm = config.DigestAlgorithm ?? DigestAlgorithm.SHA256;
            ICrlClient crlClient = new CrlClientOnline(certificateChain.ToArray());
            List<ICrlClient> lstCRL = new List<ICrlClient>() { crlClient };
            OCSPVerifier ocspVerifier = new OCSPVerifier(null, null);
            IOcspClient ocspClient = new OcspClientBouncyCastle(ocspVerifier);

            // Sign the document using the detached mode, CMS or CAdES equivalent.
            var state = signer.GetDataToSign(certificateChain.ToArray(), lstCRL, ocspClient, 0, Mapper.GetSubfilter(config), config.SignatureLevel, digestAlgorithm);

            // Construct result
            var signInput = new SignInput
            (
                name: input.OrigData.Name,
                input: state.DataToSign,
                signMode: input.SignMode,
                digestAlgorithm: config.DigestAlgorithm,
                signingDate: signingDate,
                binding: binding,
                signatureFormParameters: new SignatureFormParameters(formParameters),
                password: formParameters.PasswordProtection
            );
            return new ITextSignInputResponse(signInput, state);
        }


        public Digest Digest(Digest digest)
        {
            Contract.Requires(digest != null, "digest may not be null");

            SignInput signInput = digest.SignInput;
            using (var stream = new MemoryStream(signInput.Input))
            {
                byte[] result = DigestAlgorithms.Digest(stream, digest.SignInput.DigestAlgorithm.Value.ToString());
                return new Digest(new SignInput
                (
                    name: signInput.Name,
                    input: result,
                    signMode: SignMode.DIGEST,
                    digestAlgorithm: signInput.DigestAlgorithm,
                    binding: signInput.Binding,
                    password: signInput.Password,
                    signatureFormParameters: signInput.SignatureFormParameters,
                    signingDate: signInput.SigningDate
               ));
            }
        }

        public MergeSignatureResponse MergeSignature(MergeSignature mergeSignature, PdfSignerState state)
        {
            // Assert input
            Contract.Requires(mergeSignature != null, "mergeSignature may not be null");

            // Initialze vars & objects
            var binding = mergeSignature.Signature.Binding;
            Contract.Requires(binding != null, "input.Binding may not be null");
            SignatureConfig config = configProvider.GetConfiguration(binding);

            DigestAlgorithm digestAlgorithm = config.DigestAlgorithm ?? DigestAlgorithm.SHA256;
            var tsaClient = new TSAClientBouncyCastle(config.TimestampParameters.TsaUrl, null, null, TSAClientBouncyCastle.DEFAULTTOKENSIZE, digestAlgorithm.ToString());

            // Sign the document using the detached mode, CMS or CAdES equivalent.
            state.TwoPhaseSigner.MergeSignature(state, mergeSignature.Signature, tsaClient, Mapper.GetSubfilter(config));

            var signOutput = new SignOutput
            (
                value: ((MemoryStream)state.OutputStream).ToArray(), // I passed in a MemoryStream in DetermineSignInput
                name: mergeSignature.OrigData.Name,
                signature: mergeSignature.Signature
            );
            return new MergeSignatureResponse(signOutput);
        }
    }
}
