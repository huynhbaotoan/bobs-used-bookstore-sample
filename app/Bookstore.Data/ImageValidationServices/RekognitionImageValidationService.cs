using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Bookstore.Domain;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bookstore.Data.ImageValidationServices
{
    public class RekognitionImageValidationService : IImageValidationService
    {
        private readonly IAmazonRekognition rekognitionClient;

        private readonly string[] BannedCategories = 
        {
            "Explicit Nudity", 
            "Suggestive", 
            "Violence", 
            "Visually Disturbing", 
            "Rude Gestures", 
            "Drugs", 
            "Tobacco", 
            "Alcohol", 
            "Gambling", 
            "Hate Symbols"
        };

        public RekognitionImageValidationService(IAmazonRekognition rekognitionClient)
        {
            this.rekognitionClient = rekognitionClient;
        }

        public async Task<bool> IsSafeAsync(Stream image)
        {
            if (image == null) return true;

            image.Position = 0;

            var memoryStream = new MemoryStream();

            image.CopyTo(memoryStream);

            // The AWS SDK reads the stream from its current position. Without rewinding,
            // zero bytes are sent and every image is reported as safe.
            memoryStream.Position = 0;

            var result = await rekognitionClient.DetectModerationLabelsAsync(new DetectModerationLabelsRequest()
            {
                Image = new Image { Bytes = memoryStream }
            });

            // AWS SDK v4 no longer auto-initializes empty response collections, so
            // ModerationLabels can be null when nothing was detected.
            var moderationLabels = result.ModerationLabels;

            if (moderationLabels == null) return true;

            return !moderationLabels.Any(x => BannedCategories.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
        }
    }
}
