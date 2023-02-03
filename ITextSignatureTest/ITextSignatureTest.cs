using Microsoft.VisualStudio.TestTools.UnitTesting;
using Signatures.SDK;
using Signatures.SDK.IText.Config;
using Signatures.SDK.IText.API;
using Sphereon.SDK.Signatures.Model;
using System;
using System.IO;

namespace test_signatures_sdk_itext
{

    [TestClass]
    public class ITextSignatureTest : AbstractTestBase
    {
        private const string TestPdfName = "test-unsigned.pdf";
        private const string SignedPdfName = "test-signed.pdf";


        [TestMethod]
        public void TestPades()
        {
            ConfigProvider configProvider = CreateTestConfigs(Mode.ONLINE, SignatureLevel.PAdESBASELINELT);
            var iTextSigningApi = new ITextSigningApi(configProvider);
            TestSign("pades", iTextSigningApi);
        }

        [TestMethod]
        public void TestPadesVisual()
        {
            ConfigProvider configProvider = CreateTestConfigs(Mode.ONLINE, SignatureLevel.PAdESBASELINELT, true);
            var iTextSigningApi = new ITextSigningApi(configProvider);
            TestSign("pades-visual", iTextSigningApi);
        }

        [TestMethod]
        public void TestPKCS7()
        {
            ConfigProvider configProvider = CreateTestConfigs(Mode.ONLINE, SignatureLevel.PKCS7LT);
            var iTextSigningApi = new ITextSigningApi(configProvider);
            TestSign("pkcs7", iTextSigningApi);
        }


        private void TestSign(string label, ITextSigningApi iTextSigningApi)
        {
            string inputFilePath = $"resources\\{TestPdfName}";
            string outputFilePath = $"{Path.GetTempPath()}\\{SignedPdfName}".Replace(".pdf", label + ".pdf", StringComparison.InvariantCultureIgnoreCase);


            // Create a signInput object
            var origData = ApiUtils.CreateOrigData(new FileInfo(inputFilePath));
            var determineSignInput = new DetermineSignInput(
                origData: origData,
                signMode: SignMode.DOCUMENT,
                new ConfigKeyBinding(CertificateAlias, signatureConfigId, keyProviderId)
                );
            var signInputResponse = iTextSigningApi.DetermineSignInput(determineSignInput);

            // Create a digest of the input (the signInput object is ammended)
            var createDigest = new Digest(signInputResponse.SignInput);
            var digestResponse = iTextSigningApi.Digest(createDigest);

            // Create a signature
            var createSignature = new CreateSignature(digestResponse.SignInput);
            var signatureResponse = apiFactory.SigningApi.CreateSignature(createSignature);

            // Merge the signature onto the PDF document
            var signData = new MergeSignature(
                origData: origData,
                signature: signatureResponse.Signature);
            var signResponse = iTextSigningApi.MergeSignature(signData, signInputResponse.State);

            // Write result PDF to %temp%
            using (var outputStream = File.Create(outputFilePath))
            {
                outputStream.Write(signResponse.SignOutput.Value);
            }
        }
    }
}
