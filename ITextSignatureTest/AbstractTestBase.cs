using IdentityModel.OidcClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Signatures.SDK;
using Signatures.SDK.IText.Config;
using Sphereon.SDK.Signatures.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace test_signatures_sdk_itext
{
    public abstract class AbstractTestBase
    {
        protected const string CertificateAlias = "rdw-pdf-signatures";

        private readonly LoginResult loginResult;
        protected readonly ApiFactory apiFactory;


        protected string? signatureConfigId;
        protected string? keyProviderId;

        public AbstractTestBase(bool clientCredentialsOnly = false)
        {
            var signaturesSdkConfig = SignaturesSdkConfig.FromEnvironment();

            var authnApi = new AuthnApi(signaturesSdkConfig);

            string token;
            if (clientCredentialsOnly)
            {
                token = authnApi.LoginUsingClientCredentialsOnly().GetAwaiter().GetResult();
            }
            else
            {
                this.loginResult = authnApi.LoginFromDesktop().GetAwaiter().GetResult();
                if (loginResult.IsError)
                {
                    throw new Exception(loginResult.Error);
                }
                token = loginResult.IdentityToken;
            }
            this.apiFactory = new ApiFactory(signaturesSdkConfig, token, signaturesSdkConfig.ServiceEndpoint);
        }


        public ConfigProvider CreateTestConfigs(Mode mode, SignatureLevel signatureLevel, bool visual = false)
        {
            // Create a signature configuration
            SignatureFormParameters formParams = new(new PadesSignatureFormParameters(
                signerName: "RDW Signer Name",
                reason: "RDW",
                location: "Groningen, Nederland",
                mode: PdfSignatureMode.CERTIFICATION
                ));

            if (visual)
            {
                formParams.PadesSignatureFormParameters.VisualSignatureParameters = BuildVisualParameters();
            }

            var restrictions = new AccessRestrictions
            {
                RoleRestrictions = new List<RoleRestriction>
                {
                    new RoleRestriction("pdf-sign", RolePermission.USE)
                },
                AccessLevel = AccessLevel.PUBLIC
            };

            SignatureConfig signatureConfig = new SignatureConfig(
                            signatureLevel: signatureLevel,
                            signatureFormParameters: formParams,
                            accessRestrictions: restrictions,
                            digestAlgorithm: DigestAlgorithm.SHA256,
                            timestampParameters: new TimestampParameters(tsaUrl: "http://timestamping.ensuredca.com/",
                                baselineLTAArchiveTimestampParameters: new TimestampParameterSettings(digestAlgorithm: DigestAlgorithm.SHA256,
                                timestampContainerForm: TimestampContainerForm.PDF)));


            // Create a key/certificate provider configuration
            CreateKeyProvider createKeyProvider = new CreateKeyProvider(
                                    cacheEnabled: false,
                                    cacheTTLInSeconds: 24 * 60 * 60,
                                    type: KeyProviderType.AZUREKEYVAULT,
                                    azureKeyvaultSettings: new AzureKeyvaultSetting(
                                        keyvaultUrl: GetAzureKeyvaultUrl(),
                                        tenantId: GetAzureTenantId(),
                                        applicationId: GetAzureApplicationId(),
                                        credentialOpts: GetAzureCredetials()
                                        )
                                    );
            CreateKeyProviderOnline(createKeyProvider); // The remote createSignature call needs to know about the KeyProvider config


            // Build & return a ConfigProvider
            switch (mode)
            {
                case Mode.ONLINE:
                    {
                        CreateConfigOnline(signatureConfig, createKeyProvider);
                        return new ConfigProvider()
                            .WithOnlineCertificates(apiFactory.KeysApi)
                            .WithOnlineConfiguration(apiFactory.SignatureConfigApi);
                    }

                case Mode.OFFLINE:
                    return new ConfigProvider()
                        .WithOfflineCertificates(LoadCertificateChain())
                        .WithOfflineConfiguration(signatureConfig);
            }
            throw new InvalidOperationException("Mode not valid.");
        }

        private void CreateKeyProviderOnline(CreateKeyProvider createKeyProvider)
        {
            var createKeyProviderResponse = apiFactory.KeyProviderApi.CreateKeyProvider(createKeyProvider);
            Assert.IsNotNull(createKeyProviderResponse.ProviderId, "ProviderId was not returned");
            keyProviderId = createKeyProviderResponse.ProviderId;
            Console.WriteLine($"kid: {keyProviderId}");
        }

        private void CreateConfigOnline(SignatureConfig signatureConfig, CreateKeyProvider createKeyProvider)
        {
            var configResponse = apiFactory.SignatureConfigApi.CreateConfig(signatureConfig);
            Assert.IsNotNull(configResponse);
            this.signatureConfigId = configResponse.ConfigId;
            Console.WriteLine($"signatureConfigId: {signatureConfigId}");
        }

        private static IList<byte[]> LoadCertificateChain()
        {
            List<byte[]> certContents = new List<byte[]>();
            foreach (string file in Directory.GetFiles(@".\resources\cert", "*.cer"))
            {
                certContents.Add(File.ReadAllBytes(file));
            }
            return certContents;
        }

        private VisualSignatureParameters BuildVisualParameters()
        {
            var origData = ApiUtils.CreateOrigData(new FileInfo(@".\resources\sphereon.png"));
            return new VisualSignatureParameters(
                image: origData,
                fieldParameters: new VisualSignatureFieldParameters(originX: 50, originY: 400, width: 200, height: 30),
                textParameters: new VisualSignatureTextParameters(text: "Sphereon", textColor: "BLUE"),
                backgroundColor: "YELLOW"
                );
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

        private static string GetAzureKeyvaultUrl()
        {
            string? keyvaultUrl = Environment.GetEnvironmentVariable("SIGNATURES_SDK_AZURE_KEYVAULT_URL");
            if (keyvaultUrl == null)
            {
                throw new Exception("Environment variable SIGNATURES_SDK_AZURE_KEYVAULT_URL is required to run this test");
            }

            return keyvaultUrl;
        }

        private static string GetAzureTenantId()
        {
            string? tenantId = Environment.GetEnvironmentVariable("SIGNATURES_SDK_AZURE_TENANT_ID");
            if (tenantId == null)
            {
                throw new Exception("Environment variable SIGNATURES_SDK_AZURE_TENANT_ID is required to run this test");
            }

            return tenantId;
        }

        private static string GetAzureApplicationId()
        {
            string? tenantId = Environment.GetEnvironmentVariable("SIGNATURES_SDK_AZURE_TENANT_ID");
            if (tenantId == null)
            {
                throw new Exception("Environment variable SIGNATURES_SDK_AZURE_TENANT_ID is required to run this test");
            }

            return tenantId;
        }



        public enum Mode { ONLINE, OFFLINE }
    }
}