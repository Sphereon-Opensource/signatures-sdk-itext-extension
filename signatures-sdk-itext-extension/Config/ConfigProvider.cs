using Org.BouncyCastle.X509;
using Sphereon.SDK.Signatures.Api;
using Sphereon.SDK.Signatures.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Signatures.SDK.IText.Config
{
    public class ConfigProvider
    {
        private KeysApi keysApi;
        private SignatureConfigApi signatureConfigApi;
        private IList<byte[]> certificateData;
        private SignatureConfig signatureConfig;

        public ConfigProvider WithOnlineCertificates(KeysApi keysApi)
        {
            this.keysApi = keysApi;
            return this;
        }

        public ConfigProvider WithOnlineConfiguration(SignatureConfigApi signatureConfigApi)
        {
            this.signatureConfigApi = signatureConfigApi;
            return this;
        }

        public ConfigProvider WithOfflineCertificates(IList<byte[]> certificateData)
        {
            this.certificateData = certificateData;
            return this;
        }

        public ConfigProvider WithOfflineConfiguration(SignatureConfig signatureConfig)
        {
            this.signatureConfig = signatureConfig;
            return this;
        }

        public SignatureConfig GetConfiguration(ConfigKeyBinding binding)
        {
            if (signatureConfig != null)
            {
                return signatureConfig;
            }
            else if (binding != null && !string.IsNullOrEmpty(binding.SignatureConfigId))
            {
                return signatureConfigApi.GetConfig(binding.SignatureConfigId).Config;
            }
            throw new InvalidOperationException("signatureConfig nor binding.SignatureConfigId is set.");
        }


        public List<X509Certificate> GetCertificateChain(ConfigKeyBinding binding)
        {
            var chain = new List<X509Certificate>();

            if (certificateData != null)
            {
                foreach (byte[] data in certificateData)
                {
                    chain.Add(new X509Certificate(data));
                }
            }
            else if (binding != null && !string.IsNullOrEmpty(binding.KeyProviderId) && !string.IsNullOrEmpty(binding.Kid))
            {
                var getKeyResponse = keysApi.GetKey(binding.KeyProviderId, binding.Kid);
                getKeyResponse.KeyEntry.CertificateChain.ForEach(cert =>
                {
                    var certificate = new X509Certificate(cert.Value);
                    chain.Add(certificate);
                });
            }
            else
            {
                throw new InvalidOperationException("certificateData nor binding.KeyProviderId/Kid is set.");
            }
            return chain;
        }
    }
}
