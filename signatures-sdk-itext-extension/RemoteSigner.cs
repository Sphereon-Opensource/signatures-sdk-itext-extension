using iText.Signatures;

namespace signatures_sdk_itext_extension
{
    internal class RemoteSigner : IExternalSignature
    {
        public byte[] DataToSign { get; private set; }

        public string GetEncryptionAlgorithm()
        {
            throw new System.NotImplementedException();
        }

        public string GetHashAlgorithm()
        {
            throw new System.NotImplementedException();
        }

        public byte[] Sign(byte[] message)
        {
            this.DataToSign = message;
            return new byte[0];
        }
    }
}