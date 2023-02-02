using iText.Signatures;
using Signatures.SDK.TwoPhase;
using System.Collections.Generic;
using System.IO;

namespace Signatures.SDK.Model
{
    public class PdfSignerState
    {
        private readonly TwoPhaseSigner signer;
        private readonly byte[] dataToSign;
        private readonly PdfPKCS7 pkcs7Signer;
        private readonly byte[] documentHash;
        private readonly IList<byte[]> ocspList;
        private readonly ICollection<byte[]> crlBytes;
        private readonly int estimatedSize;

        public PdfSignerState(TwoPhaseSigner signer, byte[] dataToSign, PdfPKCS7 sgn, byte[] documentHash, IList<byte[]> ocspList, ICollection<byte[]> crlBytes, int estimatedSize)
        {
            this.signer = signer;
            this.dataToSign = dataToSign;
            this.pkcs7Signer = sgn;
            this.documentHash = documentHash;
            this.ocspList = ocspList;
            this.crlBytes = crlBytes;
            this.estimatedSize = estimatedSize;
        }

        public byte[] DataToSign { get => dataToSign; }
        public TwoPhaseSigner TwoPhaseSigner { get => signer; }
        
        internal PdfPKCS7 PKCS7Signer { get => pkcs7Signer; }
        internal byte[] DocumentHash { get => documentHash; }
        internal ICollection<byte[]> OcspList { get => ocspList; }
        internal ICollection<byte[]> CrlBytes { get => crlBytes; }
        internal int EstimatedSize { get => estimatedSize; }
        public Stream OutputStream { get => signer.OutputStream;  }
    }
}