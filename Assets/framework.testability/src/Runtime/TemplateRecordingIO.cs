using System;
using System.IO;

namespace Testability.Templates
{
    /// <summary>Bounded JSON artifact loading. Caller owns streams, paths and overwrite policy.</summary>
    public static class TemplateRecordingIO
    {
        public static TemplateRecording Read(Stream source, int maxBytes = 16777216)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (maxBytes < 1 || maxBytes > 67108864) throw new ArgumentOutOfRangeException(nameof(maxBytes));
            using (MemoryStream bounded = new MemoryStream())
            {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (bounded.Length + read > maxBytes) throw new ArgumentException("Recording file exceeds byte limit.");
                    bounded.Write(buffer, 0, read);
                }
                bounded.Position = 0;
                TemplateRecording recording = ArtifactJson.Read<TemplateRecording>(bounded);
                if (recording == null) throw new ArgumentException("Recording cannot be null.");
                recording.Validate(); return recording;
            }
        }
        public static void Write(Stream destination, TemplateRecording recording)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (recording == null) throw new ArgumentNullException(nameof(recording));
            recording.Validate(); ArtifactJson.Write(destination, recording);
        }
    }
}
