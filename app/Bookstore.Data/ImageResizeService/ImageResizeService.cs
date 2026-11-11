using Bookstore.Domain;
using ImageMagick;
using System.IO;
using System.Threading.Tasks;

namespace Bookstore.Data.ImageResizeService
{
    public class ImageResizeService : IImageResizeService
    {
        private const int BookCoverImageWidth = 400;
        private const int BookCoverImageHeight = 600;

        public async Task<Stream> ResizeImageAsync(Stream image)
        {
            if (image.CanSeek) image.Position = 0;

            using var magickImage = new MagickImage(image);

            var result = new MemoryStream();

            if (magickImage.BaseWidth == BookCoverImageWidth && magickImage.BaseHeight == BookCoverImageHeight)
            {
                // No resize required. Return the original bytes as an independent stream:
                // the caller's stream has already been consumed by MagickImage (and is read
                // again by the image validation service), so returning it directly would
                // hand downstream consumers a stream positioned at its end.
                if (image.CanSeek) image.Position = 0;

                await image.CopyToAsync(result);

                result.Position = 0;

                return result;
            }

            var size = new MagickGeometry(BookCoverImageWidth, BookCoverImageHeight) { IgnoreAspectRatio = false };

            magickImage.Resize(size);

            await magickImage.WriteAsync(result);

            result.Position = 0;

            return result;
        }
    }
}