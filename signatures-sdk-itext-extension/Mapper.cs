using iText.Signatures;
using Sphereon.SDK.Signatures.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Signatures.SDK
{
    internal class Mapper
    {

        internal static PadesSignatureFormParameters GetFormParameters(SignatureFormParameters signatureFormParameters, SignatureFormParameters signatureFormParametersOverride)
        {
            var config = signatureFormParameters.PadesSignatureFormParameters;
            var overrd = signatureFormParametersOverride?.PadesSignatureFormParameters;
            var result = new PadesSignatureFormParameters();
            result.ContactInfo = overrd?.ContactInfo ?? config.ContactInfo;
            result.CertificationPermission = overrd?.CertificationPermission ?? config.CertificationPermission;
            result.PasswordProtection = overrd?.PasswordProtection ?? config.PasswordProtection;
            result.Location = overrd?.Location ?? config.Location;
            result.Mode = overrd?.Mode ?? config.Mode;
            result.Reason = overrd?.Reason ?? config.Reason;
            result.SignerName = overrd?.SignerName ?? config.SignerName;
            result.SigningTimeZone = overrd?.SigningTimeZone ?? config.SigningTimeZone;
            result.VisualSignatureParameters = overrd?.VisualSignatureParameters ?? config.VisualSignatureParameters;
            return result;
        }

        internal static PdfSigner.CryptoStandard GetSubfilter(SignatureConfig config)
        {
            PdfSigner.CryptoStandard subfilter;
            switch (config.SignatureLevel)
            {
                case SignatureLevel.PKCS7B:
                case SignatureLevel.PKCS7T:
                case SignatureLevel.PKCS7LT:
                case SignatureLevel.PKCS7LTA:
                    subfilter = PdfSigner.CryptoStandard.CMS;
                    break;
                case SignatureLevel.PAdESBASELINEB:
                case SignatureLevel.PAdESBASELINET:
                case SignatureLevel.PAdESBASELINELT:
                case SignatureLevel.PAdESBASELINELTA:
                    subfilter = PdfSigner.CryptoStandard.CADES;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(config.SignatureLevel));
            }

            return subfilter;
        }
    }
}
