using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;
using Signatures.SDK;
using Sphereon.SDK.Signatures.Api;
using Sphereon.SDK.Signatures.Model;
using System;
using System.Collections.Generic;
using System.IO;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Asn1.X9;

namespace signatures_sdk_itext_extension
{
    public class ITextSigningOneShotApi
    {
        private readonly KeysApi keysApi;
        private readonly SignatureConfigApi configApi;
        private readonly SigningApi signingApi;

        public ITextSigningOneShotApi(SigningApi signingApi, SignatureConfigApi configApi, KeysApi keysApi)
        {
            this.signingApi = signingApi;
            this.configApi = configApi;
            this.keysApi = keysApi;
        }

        public SignOutput Sign(DetermineSignInput determineSignInput)
        {   // TODO asserts
            var binding = determineSignInput.Binding;
            var config = configApi.GetConfig(binding.SignatureConfigId).Config;
            var formParameters = Mapper.GetFormParameters(config.SignatureFormParameters, determineSignInput.SignatureFormParametersOverride);
            var fieldParameters = formParameters.VisualSignatureParameters?.FieldParameters;
            var signingDate = DateTimeOffset.UtcNow;

            PdfReader reader = new PdfReader(new MemoryStream(determineSignInput.OrigData.Content));
            var outputStream = new MemoryStream();
            PdfSigner signer = new PdfSigner(reader, outputStream, new StampingProperties());
            ConfigureSigner(signer, signingDate, formParameters, fieldParameters);
            List<X509Certificate> certificateWrappers = GetCertificateChain(binding);

            var signInput = new SignInput
            (
                name: determineSignInput.OrigData.Name,
                input: new byte[0], // RemoteSigner will load this
                signMode: determineSignInput.SignMode,
                digestAlgorithm: config.DigestAlgorithm,
                signingDate: signingDate,
                binding: binding,
                signatureFormParameters: new SignatureFormParameters(formParameters),
                password: formParameters.PasswordProtection
            );

            // Sign the document using the detached mode, CMS or CAdES equivalent.
            DigestAlgorithm digestAlgorithm = config.DigestAlgorithm ?? DigestAlgorithm.SHA256;
            var remoteSigner = new RemoteSignerOneShot(digestAlgorithm, signingApi, signInput);
            var tsaClient = new TSAClientBouncyCastle(config.TimestampParameters.TsaUrl, null, null, TSAClientBouncyCastle.DEFAULTTOKENSIZE, digestAlgorithm.ToString());
            OCSPVerifier ocspVerifier = new OCSPVerifier(null, null);
            IOcspClient ocspClient = new OcspClientBouncyCastle(ocspVerifier);
            ICrlClient crlClient = new CrlClientOnline(certificateWrappers.ToArray());
            List<ICrlClient> lstCRL = new List<ICrlClient>() { crlClient };

            signer.SignDetached(remoteSigner, certificateWrappers.ToArray(), lstCRL, ocspClient, tsaClient, 0, Mapper.GetSubfilter(config));

            var signOutput = new SignOutput
            (
                value: outputStream.ToArray(),
                name: determineSignInput.OrigData.Name,
                signature: remoteSigner.Signature
            );
            return signOutput;
        }

        private static void ConfigureSigner(PdfSigner signer, DateTimeOffset signingDate, PadesSignatureFormParameters formParameters, VisualSignatureFieldParameters fieldParameters)
        {
            PdfSignatureAppearance appearance = signer.GetSignatureAppearance()
                .SetReason(formParameters.Reason)
                .SetLocation(formParameters.Location);
            if (fieldParameters != null)
            {
                appearance.SetPageRect(new Rectangle(fieldParameters.OriginX, fieldParameters.OriginY, fieldParameters.Width, fieldParameters.Height));
                appearance.SetPageNumber(fieldParameters.Page);
            }
            else
            {
                appearance.SetPageRect(new Rectangle(0, 0, 0, 0));
                appearance.SetPageNumber(1);
            }
            signer.SetFieldName("sig");
            signer.SetSignDate(signingDate.DateTime);
        }


        private List<X509Certificate> GetCertificateChain(ConfigKeyBinding binding)
        {
            var certificateWrappers = new List<X509Certificate>();
            var getKeyResponse = keysApi.GetKey(binding.KeyProviderId, binding.Kid);
            getKeyResponse.KeyEntry.CertificateChain.ForEach(cert =>
            {
                var certificate = new X509Certificate(cert.Value);
                certificateWrappers.Add(certificate);
            });
            return certificateWrappers;
        }
    }
}