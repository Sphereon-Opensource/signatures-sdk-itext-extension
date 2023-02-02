using Microsoft.VisualStudio.TestTools.UnitTesting;
using Signatures.SDK;
using Signatures.SDK.IText.Config;
using Signatures.SDK.IText.OneShot;
using Sphereon.SDK.Signatures.Model;
using System;
using System.IO;

namespace test_signatures_sdk_itext
{

    [TestClass]
    public class ITextOneShotSignatureTest : AbstractTestBase
    {
        private const string TestPdfName = "test-unsigned.pdf";
        private const string SignedPdfName = "test-signed.pdf";


        [TestMethod]
        public void TestPades()
        {
            ConfigProvider configProvider = CreateTestConfigs(Mode.ONLINE, SignatureLevel.PAdESBASELINELT);
            var iTextSigningApi = new ITextSigningOneShotApi(apiFactory.SigningApi, configProvider);
            TestSign("pades", iTextSigningApi);
        }

        [TestMethod]
        public void TestPadesVisual()
        {
            ConfigProvider configProvider = CreateTestConfigs(Mode.ONLINE, SignatureLevel.PAdESBASELINELT, true);
            var iTextSigningApi = new ITextSigningOneShotApi(apiFactory.SigningApi, configProvider);
            TestSign("pades-visual", iTextSigningApi);
        }

        [TestMethod]
        public void TestPKCS7()
        {
            ConfigProvider configProvider = CreateTestConfigs(Mode.ONLINE, SignatureLevel.PKCS7LT);
            var iTextSigningApi = new ITextSigningOneShotApi(apiFactory.SigningApi, configProvider);
            TestSign("pkcs7", iTextSigningApi);
        }

        private void TestSign(string label, ITextSigningOneShotApi iTextSigningApi)
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
            var signOutput = iTextSigningApi.Sign(determineSignInput);

            // Write result PDF to %temp%
            using (var outputStream = File.Create(outputFilePath))
            {
                outputStream.Write(signOutput.Value);
            }
        }


    }
}
