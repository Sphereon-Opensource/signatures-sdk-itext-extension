using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;
using iText.Kernel.Exceptions;
using iText.Signatures.Exceptions;
using Org.BouncyCastle.Crypto;
using Sphereon.SDK.Signatures.Model;
using System.Diagnostics.Contracts;
using Signatures.SDK.Model;
using Signatures.SDK.IText.API;
using Signatures.SDK.Common;

namespace Signatures.SDK.TwoPhase
{
    public class TwoPhaseSigner : PdfSigner
    {
        public TwoPhaseSigner(PdfReader reader, Stream outputStream, StampingProperties properties) : base(reader, outputStream, properties)
        {
            this.OutputStream = outputStream;
        }

        public TwoPhaseSigner(PdfReader reader, Stream outputStream, string path, StampingProperties properties) : base(reader, outputStream, path, properties)
        {
            this.OutputStream = outputStream;
        }

        public Stream OutputStream { get; }

        public PdfSignerState GetDataToSign(X509Certificate[] chain, ICollection<ICrlClient> crlList, IOcspClient ocspClient, int estimatedSize,
            CryptoStandard sigtype, SignatureLevel signatureLevel, DigestAlgorithm digestAlgorithm)
        {
            if (closed)
            {
                throw new PdfException(SignExceptionMessageConstant.THIS_INSTANCE_OF_PDF_SIGNER_ALREADY_CLOSED);
            }
            if (certificationLevel > 0 && IsDocumentPdf2())
            {
                if (DocumentContainsCertificationOrApprovalSignatures())
                {
                    throw new PdfException(SignExceptionMessageConstant.CERTIFICATION_SIGNATURE_CREATION_FAILED_DOC_SHALL_NOT_CONTAIN_SIGS);
                }
            }
            Contract.Requires(chain != null, "ocspClient may not be null");
            Contract.Requires(crlList != null, "ocspClient may not be null");
            Contract.Requires(ocspClient != null, "ocspClient may not be null");

            ICollection<byte[]> crlBytes = null;
            int i = 0;
            while (crlBytes == null && i < chain.Length)
            {
                crlBytes = ProcessCrl(chain[i++], crlList);
            }
            if (estimatedSize == 0)
            {
                estimatedSize = 8192 + 4192;
                if (crlBytes != null)
                {
                    foreach (byte[] element in crlBytes)
                    {
                        estimatedSize += element.Length + 10;
                    }
                }
                if (signatureLevel.ToString().Contains("LT"))
                {
                    estimatedSize += 4192;
                }
            }
            PdfSignatureAppearance appearance = GetSignatureAppearance();
            appearance.SetCertificate(chain[0]);
            if (sigtype == PdfSigner.CryptoStandard.CADES && !IsDocumentPdf2())
            {
                AddDeveloperExtension(PdfDeveloperExtension.ESIC_1_7_EXTENSIONLEVEL2);
            }
            
            PdfSignature dic = new PdfSignature(PdfName.Adobe_PPKLite, sigtype == PdfSigner.CryptoStandard.CADES 
                ? PdfName.ETSI_CAdES_DETACHED 
                : PdfName.Adbe_pkcs7_detached);
            dic.SetReason(appearance.GetReason());
            dic.SetLocation(appearance.GetLocation());
            dic.SetSignatureCreator(appearance.GetSignatureCreator());
            dic.SetContact(appearance.GetContact());
            dic.SetDate(new PdfDate(GetSignDate()));
            // time-stamp will over-rule this
            cryptoDictionary = dic;
            IDictionary<PdfName, int?> exc = new Dictionary<PdfName, int?>();
            exc.Add(PdfName.Contents, estimatedSize * 2 + 2);
            PreClose(exc);
            PdfPKCS7 sgn = new PdfPKCS7((ICipherParameters)null, chain, digestAlgorithm.ToString(), false);
            Stream data = GetRangeStream();
            byte[] hash = DigestAlgorithms.Digest(data, SignUtils.GetMessageDigest(digestAlgorithm.ToString()));

            IList<byte[]> ocspList = new List<byte[]>();
            if (chain.Length > 1 && ocspClient != null)
            {
                for (int j = 0; j < chain.Length - 1; ++j)
                {
                    byte[] ocsp = ocspClient.GetEncoded((X509Certificate)chain[j], (X509Certificate)chain[j + 1], null);
                    if (ocsp != null)
                    {
                        ocspList.Add(ocsp);
                    }
                }
            }
            var sh = sgn.GetAuthenticatedAttributeBytes(hash, sigtype, ocspList, crlBytes);
            return new PdfSignerState(this, sh, sgn, hash, ocspList, crlBytes, estimatedSize);
        }


        public void MergeSignature(PdfSignerState state, Signature extSignature, ITSAClient tsaClient, CryptoStandard sigtype)
        {
            PdfPKCS7 sgn = state.PKCS7Signer;
            sgn.SetExternalDigest(extSignature.Value, null, Mapper.GetEncryptionAlgorithm(extSignature.Algorithm));
            byte[] encodedSig = sgn.GetEncodedPKCS7(state.DocumentHash, sigtype, tsaClient, state.OcspList, state.CrlBytes);
            if (state.EstimatedSize < encodedSig.Length)
            {
                throw new IOException("Not enough space");
            }
            byte[] paddedSig = new byte[state.EstimatedSize];
            Array.Copy(encodedSig, 0, paddedSig, 0, encodedSig.Length);
            PdfDictionary dic2 = new PdfDictionary();
            dic2.Put(PdfName.Contents, new PdfString(paddedSig).SetHexWriting(true));
            Close(dic2);
            closed = true;
        }

        private bool IsDocumentPdf2()
        {
            return document.GetPdfVersion().CompareTo(PdfVersion.PDF_2_0) >= 0;
        }

    }
}
