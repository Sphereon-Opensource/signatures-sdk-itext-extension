using Signatures.SDK.Model;
using Sphereon.SDK.Signatures.Model;

namespace Signatures.SDK.IText.Model
{
    public class ITextSignInputResponse
    {
        public ITextSignInputResponse(SignInput signInput, PdfSignerState state)
        {
            SignInput = signInput;
            State = state;
        }

        public SignInput SignInput { get; }
        public PdfSignerState State { get; }
    }
}