using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Geom;
using iText.Signatures;
using Sphereon.SDK.Signatures.Model;
using System;
using System.Drawing;
using System.IO;

namespace Signatures.SDK.IText.Common
{
    public abstract class AbstractConfigurer
    {

        protected static void ConfigureSigner(PdfSigner signer, DateTimeOffset signingDate, PadesSignatureFormParameters formParameters)
        {
            PdfSignatureAppearance appearance = signer.GetSignatureAppearance()
                .SetReason(formParameters.Reason)
                .SetLocation(formParameters.Location);

            var visualp = formParameters.VisualSignatureParameters;
            if (visualp != null)
            {
                var fieldp = visualp.FieldParameters;
                if (fieldp != null)
                {
                    appearance.SetPageRect(new iText.Kernel.Geom.Rectangle(fieldp.OriginX, fieldp.OriginY, fieldp.Width, fieldp.Height));
                    appearance.SetPageNumber(fieldp.Page);
                }
                if (visualp.TextParameters != null)
                {
                    var textp = visualp.TextParameters;
                    appearance.SetLayer2Text(textp.Text);
                    if (textp.TextColor != null)
                    {
                        var sysColor = System.Drawing.Color.FromName(textp.TextColor);
                        appearance.SetLayer2FontColor(new DeviceRgb(sysColor));
                        appearance.SetLayer2FontSize(11);
                    }
                }
                else
                {
                    appearance.SetRenderingMode(PdfSignatureAppearance.RenderingMode.NAME_AND_DESCRIPTION);
                }
                if (visualp.Image != null)
                {
                    appearance.SetSignatureGraphic(ImageDataFactory.Create(visualp.Image.Content));
                    appearance.SetRenderingMode(visualp.TextParameters != null ?
                        PdfSignatureAppearance.RenderingMode.GRAPHIC_AND_DESCRIPTION
                        : PdfSignatureAppearance.RenderingMode.GRAPHIC);
                }
                if (visualp.Zoom != 100)
                {
                    appearance.SetImageScale(visualp.Zoom);
                }

                if (visualp.BackgroundColor != null && fieldp != null && fieldp.Width != 0 && fieldp.Height != 0)
                {
                    ApplyBackgroundFill(appearance, fieldp);
                }
            }

            signer.SetFieldName("sig");
            signer.SetSignDate(signingDate.DateTime);
        }

        private static void ApplyBackgroundFill(PdfSignatureAppearance appearance, VisualSignatureFieldParameters fieldp)
        {
            Bitmap bmp = new Bitmap((int)fieldp.Width, (int)fieldp.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.Yellow);
            }

            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                appearance.SetImage(ImageDataFactory.Create(ms.ToArray()));
            }
        }
    }
}