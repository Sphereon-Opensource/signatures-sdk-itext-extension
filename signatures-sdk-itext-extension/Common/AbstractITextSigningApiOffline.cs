using Org.BouncyCastle.X509;
using Sphereon.SDK.Signatures.Model;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Signatures.SDK.IText.Common
{
    public abstract class AbstractITextSigningApiOffline : AbstractConfigurer
    {
        private readonly SignatureConfig config;
        private readonly List<Certificate> certificateChain;

        public AbstractITextSigningApiOffline(SignatureConfig config, List<Certificate> certificateChain)
        {
            Contract.Requires(config != null, "config may not be null");
            Contract.Requires(certificateChain != null && certificateChain.Count > 0, "certificateChain may not be null/empty");

            this.config = config;
            this.certificateChain = certificateChain;
        }

        protected SignatureConfig GetConfiguration()
        {
            return config;
        }

        protected List<X509Certificate> GetCertificateChain()
        {
            var certificateWrappers = new List<X509Certificate>();
            certificateChain.ForEach(cert =>
            {
                var certificate = new X509Certificate(cert.Value);
                certificateWrappers.Add(certificate);
            });
            return certificateWrappers;
        }
    }
}