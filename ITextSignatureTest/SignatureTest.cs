using IdentityModel.OidcClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Signatures.SDK;
using signatures_sdk_itext_extension;
using Sphereon.SDK.Signatures.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace test_signatures_sdk_itext
{

    [TestClass]
    public class SignatureTest
    {
        private const string CertificateAlias = "esignum";
        private const string TestPdfName = "test-unsigned.pdf";
        private const string SignedPdfName = "test-signed.pdf";


        private readonly LoginResult loginResult;
        private readonly ApiFactory apiFactory;
        private readonly ITextSigningApi iTextSigningApi;
        private string? signatureConfigId;
        private string? keyProviderId;


        public SignatureTest()
        {
            var signaturesSdkConfig = SignaturesSdkConfig.FromEnvironment();

            var authnApi = new AuthnApi(signaturesSdkConfig);
            this.loginResult = authnApi.LoginFromDesktop().GetAwaiter().GetResult();
            if (loginResult.IsError)
            {
                throw new Exception(loginResult.Error);
            }

            this.apiFactory = new ApiFactory(signaturesSdkConfig, loginResult.IdentityToken, signaturesSdkConfig.ServiceEndpoint);
            this.iTextSigningApi = new ITextSigningApi(apiFactory.KeysApi, apiFactory.SignatureConfigApi);
        }

        [TestMethod]
        public void TestPades()
        {
            TestCreateConfigs(SignatureLevel.PAdESBASELINELT);
            TestSign("pades");
        }

        [TestMethod]
        public void TestPKCS7()
        {
            TestCreateConfigs(SignatureLevel.PKCS7LT);
            TestSign("pkcs7");
        }

        public void TestCreateConfigs(SignatureLevel signatureLevel)
        {
            // Create a signature configuration
            SignatureFormParameters formParams = new(new PadesSignatureFormParameters(
                signerName: "Unit Test <test@sphereon.com>",
                reason: "E-signed by Unit Test <test@sphereon.com> , Testing!!",
                location: "My JVM",
                mode: PdfSignatureMode.CERTIFICATION
                ));


            var restrictions = new AccessRestrictions
            {
                RoleRestrictions = new List<RoleRestriction>
                {
                    new RoleRestriction("pdf-sign", RolePermission.USE)
                },
                AccessLevel = AccessLevel.PUBLIC
            };

            var configResponse = apiFactory.SignatureConfigApi.CreateConfig(new SignatureConfig(
                signatureLevel: signatureLevel,
                signatureFormParameters: formParams,
                accessRestrictions: restrictions,
                digestAlgorithm: DigestAlgorithm.SHA256,
                timestampParameters: new TimestampParameters(tsaUrl: "http://timestamp.sectigo.com/",
                    baselineLTAArchiveTimestampParameters: new TimestampParameterSettings(digestAlgorithm: DigestAlgorithm.SHA256,
                    timestampContainerForm: TimestampContainerForm.PDF))));
            Assert.IsNotNull(configResponse);
            this.signatureConfigId = configResponse.ConfigId;
            Console.WriteLine($"signatureConfigId: {signatureConfigId}");

            // Create a key/certificate provider configuration
            var createKeyProviderResponse = apiFactory.KeyProviderApi.CreateKeyProvider(new CreateKeyProvider(
                cacheEnabled: false,
                cacheTTLInSeconds: 24 * 60 * 60,
                type: KeyProviderType.AZUREKEYVAULT,
                azureKeyvaultSettings: new AzureKeyvaultSetting(
                    keyvaultUrl: "https://sphereon-certs.vault.azure.net/",
                    tenantId: "e2a42b2f-7460-4499-afc2-425315ef058a",
                    applicationId: "demo",
                    credentialOpts: GetAzureCredetials()
                    )
                ));
            Assert.IsNotNull(createKeyProviderResponse.ProviderId, "ProviderId was not returned");



            // Test GetKeyProvider
            var keyProviderResponse = apiFactory.KeyProviderApi.GetKeyProvider(providerId: createKeyProviderResponse.ProviderId);
            this.keyProviderId = keyProviderResponse.ProviderId;
            Console.WriteLine($"kid: {keyProviderId}");

            // Test GetKey, write certificate to %temp%
            var key = apiFactory.KeysApi.GetKey(providerId: createKeyProviderResponse.ProviderId, kid: CertificateAlias);
            Assert.IsNotNull(key);
            using (var certStream = File.OpenWrite($"{Path.GetTempPath()}\\{CertificateAlias}.cer"))
            {
                certStream.Write(key.KeyEntry.Certificate.Value);
            }
        }


        private void TestSign(string label)
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
            var signResponse = iTextSigningApi.MergeSignature(signData);

            // Write result PDF to %temp%
            using (var outputStream = File.Create(outputFilePath))
            {
                outputStream.Write(signResponse.SignOutput.Value);
            }
        }


        private static AzureCredentialOpts GetAzureCredetials()
        {
            return new AzureCredentialOpts(
                                    AzureCredentialOpts.CredentialModeEnum.SERVICECLIENTSECRET,
                                    new AzureSecretCredentialOpts(
                                        clientId: GetClientId(),
                                        clientSecret: GetClientSecret())
                                    );
        }

        private static string GetClientId()
        {
            string? clientId = Environment.GetEnvironmentVariable("SIGNATURES_SDK_AZURE_CLIENT_ID");
            if (clientId == null)
            {
                throw new Exception("Environment variable SIGNATURES_SDK_AZURE_CLIENT_ID is required to run this test");
            }

            return clientId;
        }

        private static string GetClientSecret()
        {
            string? clientSecret = Environment.GetEnvironmentVariable("SIGNATURES_SDK_AZURE_CLIENT_SECRET");
            if (clientSecret == null)
            {
                throw new Exception("Environment variable SIGNATURES_SDK_AZURE_CLIENT_SECRET is required to run this test");
            }

            return clientSecret;
        }
    }
}
