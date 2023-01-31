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
    public class ITextSigningApi
    {
        private readonly KeysApi keysApi;
        private readonly SignatureConfigApi configApi;

        public ITextSigningApi(KeysApi keysApi, SignatureConfigApi configApi)
        {
            this.keysApi = keysApi;
            this.configApi = configApi;
        }

        public DetermineSignInputResponse DetermineSignInput(DetermineSignInput determineSignInput)
        {   // TODO asserts
            var binding = determineSignInput.Binding;
            var config = configApi.GetConfig(binding.SignatureConfigId).Config;
            var formParameters = Mapper.GetFormParameters(config.SignatureFormParameters, determineSignInput.SignatureFormParametersOverride);
            var fieldParameters = formParameters.VisualSignatureParameters?.FieldParameters;
            //var signingDate = DateTimeOffset.UtcNow;
            var signingDate = DateTimeOffset.Parse("31-1-2023 15:31:48 +00:00");

            PdfReader reader = new PdfReader(new MemoryStream(determineSignInput.OrigData.Content));
            var outputStream = new MemoryStream();
            PdfSigner signer = new PdfSigner(reader, outputStream, new StampingProperties());
            ConfigureSigner(signer, signingDate, formParameters, fieldParameters);

            List<X509Certificate> certificateWrappers = GetCertificateChain(binding);
            ICrlClient crlClient = new CrlClientOnline(certificateWrappers.ToArray());
            List<ICrlClient> lstCRL = new List<ICrlClient>() { crlClient };

            // Sign the document using the detached mode, CMS or CAdES equivalent.
            var remoteSigner = new RemoteSigner(config.DigestAlgorithm ?? DigestAlgorithm.SHA256);
            signer.SignDetached(remoteSigner, certificateWrappers.ToArray(), lstCRL, null, null, 0, Mapper.GetSubfilter(config));

            var signInput = new SignInput
            (
                name: determineSignInput.OrigData.Name,
                input: remoteSigner.DataToSign,             
                signMode: determineSignInput.SignMode,
                digestAlgorithm: config.DigestAlgorithm,
                signingDate: signingDate,
                binding: binding,
                signatureFormParameters: new SignatureFormParameters(formParameters),
                password: formParameters.PasswordProtection
            );
            return new DetermineSignInputResponse(signInput);
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

        public Digest Digest(Digest digest)
        {
            // TODO asserts

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

        public MergeSignatureResponse MergeSignature(MergeSignature mergeSignature)
        {   // TODO asserts
            var binding = mergeSignature.Signature.Binding;
            var config = configApi.GetConfig(binding.SignatureConfigId).Config;
            var formParameters = mergeSignature.Signature.SignatureFormParameters.PadesSignatureFormParameters;
            var fieldParameters = formParameters.VisualSignatureParameters?.FieldParameters;

            PdfReader reader = new PdfReader(new MemoryStream(mergeSignature.OrigData.Content));
            var outputStream = new MemoryStream();
            PdfSigner signer = new PdfSigner(reader, outputStream, new StampingProperties());
            ConfigureSigner(signer, mergeSignature.Signature.Date, formParameters, fieldParameters);

            List<X509Certificate> certificateWrappers = GetCertificateChain(binding);

            // Sign the document using the detached mode, CMS or CAdES equivalent.
            var remoteSigner = new RemoteSigner(config.DigestAlgorithm ?? DigestAlgorithm.SHA256, mergeSignature.Signature.Value);
            var tsaClient = new TSAClientBouncyCastle(config.TimestampParameters.TsaUrl);
            OCSPVerifier ocspVerifier = new OCSPVerifier(null, null);
            IOcspClient ocspClient = new OcspClientBouncyCastle(ocspVerifier);
            ICrlClient crlClient = new CrlClientOnline(certificateWrappers.ToArray());
            List<ICrlClient> lstCRL = new List<ICrlClient>() { crlClient };

            signer.SignDetached(remoteSigner, certificateWrappers.ToArray(), lstCRL, ocspClient, tsaClient, 0, Mapper.GetSubfilter(config));

            var signOutput = new SignOutput
            (
                value: outputStream.ToArray(),
                name: mergeSignature.OrigData.Name,
                signature: mergeSignature.Signature
            );
            return new MergeSignatureResponse(signOutput);
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
