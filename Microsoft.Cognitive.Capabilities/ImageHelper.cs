using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace Microsoft.Cognitive.Capabilities
{

    public static class ImageHelper
    {
        public static byte[] CreateThumbnailJpgStream(Image loBMP, int lnWidth, int lnHeight, out int newWidth, out int newHeight)
        {

            System.Drawing.Bitmap bmpOut = null;

            ImageFormat loFormat = loBMP.RawFormat;

            decimal lnRatio;
            int lnNewWidth = 0;
            int lnNewHeight = 0;

            if (loBMP.Width > loBMP.Height)
            {
                lnRatio = (decimal)lnWidth / loBMP.Width;
                lnNewWidth = lnWidth;
                decimal lnTemp = loBMP.Height * lnRatio;
                lnNewHeight = (int)lnTemp;
            }
            else
            {
                lnRatio = (decimal)lnHeight / loBMP.Height;
                lnNewHeight = lnHeight;
                decimal lnTemp = loBMP.Width * lnRatio;
                lnNewWidth = (int)lnTemp;
            }

            // if we are going to end up with a larger image just return what we have
            if (lnHeight * lnWidth > loBMP.Width * loBMP.Height)
            {
                MemoryStream outStream = new MemoryStream(1024 * 1024 * 2);

                newHeight = loBMP.Height;
                newWidth = loBMP.Width;

                // write the image to disk
                loBMP.Save(outStream, ImageFormat.Jpeg);
                var data = new byte[outStream.Length];
                Array.Copy(outStream.GetBuffer(), data, outStream.Length);
                return data;
            }


            // *** This code creates cleaner (though bigger) thumbnails and properly
            // *** and handles GIF files better by generating a white background for
            // *** transparent images (as opposed to black)
            using (bmpOut = new Bitmap(lnNewWidth, lnNewHeight))
            {
                using (Graphics g = Graphics.FromImage(bmpOut))
                {
                    newHeight = lnNewHeight;
                    newWidth = lnNewWidth;
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

                    g.FillRectangle(Brushes.White, 0, 0, lnWidth, lnHeight);
                    g.DrawImage(loBMP, 0, 0, lnNewWidth, lnNewHeight);
                    MemoryStream outStream = new MemoryStream(1024 * 1024 * 2);

                    // write the image to disk
                    bmpOut.Save(outStream, ImageFormat.Jpeg);
                    bmpOut.Dispose();

                    outStream.Position = 0;
                    var data = new byte[outStream.Length];
                    Array.Copy(outStream.GetBuffer(), data, outStream.Length);
                    return data;

                }
            }

        }

        public static IEnumerable<byte[]> ConvertToJpegs(Stream stream, int maxWidth, int maxHeight)
        {
            int w, h;
            return ConvertTiffToBmps(stream).Select(img => ImageHelper.CreateThumbnailJpgStream(img, maxWidth, maxHeight, out w, out h));
        }


        public static IEnumerable<Bitmap> ConvertTiffToBmps(Stream stream)
        {
            using (Image imageFile = Image.FromStream(stream))
            {
                // rotate the image if needed
                CheckImageRotate(imageFile);

                FrameDimension frameDimensions = new FrameDimension(
                    imageFile.FrameDimensionsList[0]);

                // Gets the number of pages from the tiff image (if multipage) 
                int frameNum = imageFile.GetFrameCount(frameDimensions);

                for (int frame = 0; frame < frameNum; frame++)
                {
                    // yeild each frame as a bitmap. 
                    imageFile.SelectActiveFrame(frameDimensions, frame);
                    yield return new Bitmap(imageFile);
                }

            }
        }

        public static void CheckImageRotate(Image image)
        {

            if (image.PropertyIdList.Contains(0x0112))
            {
                int rotationValue = image.GetPropertyItem(0x0112).Value[0];
                switch (rotationValue)
                {
                    case 8: // rotated 90 right
                            // de-rotate:
                        image.RotateFlip(rotateFlipType: System.Drawing.RotateFlipType.Rotate270FlipNone);
                        break;

                    case 3: // bottoms up
                        image.RotateFlip(rotateFlipType: System.Drawing.RotateFlipType.Rotate180FlipNone);
                        break;

                    case 6: // rotated 90 left
                        image.RotateFlip(rotateFlipType: System.Drawing.RotateFlipType.Rotate90FlipNone);
                        break;
                    case 1: // landscape, do nothing
                    default:
                        break;
                }
                image.RemovePropertyItem(0x0112);
            }
        }
    }
}
